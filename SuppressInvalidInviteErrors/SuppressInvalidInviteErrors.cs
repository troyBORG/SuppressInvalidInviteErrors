using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using SkyFrost.Base;
using System.Reflection;

namespace SuppressInvalidInviteErrors;

public class SuppressInvalidInviteErrorsMod : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.0";
	public override string Name => "SuppressInvalidInviteErrors";
	public override string Author => "troyBORG";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/troyBORG/SuppressInvalidInviteErrors";

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> SuppressForwardToAdminsErrors = new ModConfigurationKey<bool>(
		"SuppressForwardToAdminsErrors",
		"Suppress 'Couldn't find hosted world for invite request' errors when invite requests reference non-existent sessions",
		() => true
	);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> SuppressProcessGrantedInviteWarnings = new ModConfigurationKey<bool>(
		"SuppressProcessGrantedInviteWarnings",
		"Suppress 'Received granted invite request that has not been forwarded in this session' warnings for old invite requests",
		() => true
	);

	private static ModConfiguration? Config;

	public override void OnEngineInit() {
		Config = GetConfiguration();
		Config.Save(true);

		Harmony harmony = new("com.example.SuppressInvalidInviteErrors");
		harmony.PatchAll();
	}

	/// <summary>
	/// Patches ForwardToAdmins to silently ignore invite requests for worlds that no longer exist.
	/// This prevents error spam on Headless startup when old invite requests are received for expired sessions.
	/// </summary>
	[HarmonyPatch(typeof(InviteRequestManager), "ForwardToAdmins")]
	class InviteRequestManager_ForwardToAdmins_Patch {
		static bool Prefix(InviteRequestManager __instance, Message incomingMessage, InviteRequest request) {
			// Check if suppression is enabled
			if (Config != null && !Config.GetValue(SuppressForwardToAdminsErrors)) {
				return true; // Let original method run if suppression is disabled
			}

			// Check if the world exists before proceeding
			World world = __instance.GetCorrespondingHostedWorld(request);
			if (world == null) {
				// Silently ignore - this is an old invite request for a session that no longer exists
				// This prevents error spam on Headless startup
				return false; // Skip original method
			}
			
			// World exists, let the original method handle it
			return true; // Continue to original method
		}
	}

	/// <summary>
	/// Patches ProcessGrantedInviteRequest to silently ignore old invite requests that weren't forwarded in this session.
	/// This prevents warning spam on Headless startup when old granted invite requests are received.
	/// </summary>
	[HarmonyPatch(typeof(InviteRequestManager), "ProcessGrantedInviteRequest")]
	class InviteRequestManager_ProcessGrantedInviteRequest_Patch {
		private static FieldInfo? _forwardedInviteRequestsField;

		static bool Prefix(InviteRequestManager __instance, Message incomingMessage, InviteRequest request) {
			// Check if suppression is enabled
			if (Config != null && !Config.GetValue(SuppressProcessGrantedInviteWarnings)) {
				return true; // Let original method run if suppression is disabled
			}

			// Lazy initialization of the field info
			if (_forwardedInviteRequestsField == null) {
				_forwardedInviteRequestsField = typeof(InviteRequestManager).GetField("_forwardedInviteRequests", BindingFlags.NonPublic | BindingFlags.Instance);
				if (_forwardedInviteRequestsField == null) {
					// If we can't find the field, let the original method run
					return true;
				}
			}

			// Check if the request was forwarded in this session
			var forwardedInviteRequests = _forwardedInviteRequestsField.GetValue(__instance);
			if (forwardedInviteRequests != null) {
				// Use reflection to check if the dictionary contains the key
				var containsKeyMethod = forwardedInviteRequests.GetType().GetMethod("ContainsKey", new[] { typeof(string) });
				if (containsKeyMethod != null) {
					bool containsKey = (bool)containsKeyMethod.Invoke(forwardedInviteRequests, new object[] { request.InviteRequestId })!;
					if (!containsKey) {
						// This invite request wasn't forwarded in this session (old/stale request)
						// Silently ignore to prevent warning spam on Headless startup
						return false; // Skip original method
					}
				}
			}

			// Request was found in forwarded list, let original method handle it
			return true; // Continue to original method
		}
	}
}
