using HarmonyLib;
using UnityEngine;
using AmongUs.GameOptions;
using System.Collections;
using System;
using Hazel;
using InnerNet;

namespace TownOfUsDraft.Patches
{
    public static class ForceDraftPatch
    {
        private static bool _draftStarted = false;
        private static bool _draftCompleted = false;

        // Callback: Po zakończeniu Draftu wznów grę
        public static void OnDraftCompleted()
        {
            _draftCompleted = true;
            DraftPlugin.Instance.Log.LogInfo("╔═════════════════════════════════════════════════════════════════╗");
            DraftPlugin.Instance.Log.LogInfo($"║  DRAFT ZAKOŃCZONY! {DraftManager.PendingRoles.Count} ról w PendingRoles               ║");
            DraftPlugin.Instance.Log.LogInfo("║  Wywołuję SelectRoles ponownie - Postfix zaaplikuje role!      ║");
            DraftPlugin.Instance.Log.LogInfo("╚═════════════════════════════════════════════════════════════════╝");
            
            // WYWOŁAJ SelectRoles ponownie
            // Tym razem PendingRoles są pełne, więc:
            // → Prefix zablokuje vanilla/TOU
            // → Postfix zaaplikuje role
            var roleManager = RoleManager.Instance;
            if (roleManager != null)
            {
                DraftPlugin.Instance.Log.LogInfo("[OnDraftCompleted] Wywołuję RoleManager.SelectRoles()...");
                roleManager.SelectRoles();
            }
            else
            {
                DraftPlugin.Instance.Log.LogError("[OnDraftCompleted] RoleManager.Instance jest null!");
            }
        }

        // Reset flagi na koniec gry
        public static void ResetFlags()
        {
            _draftStarted = false;
            _draftCompleted = false;
        }

        // Reset flagi na koniec gry
        [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
        public static class ResetDraftFlag
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                ResetFlags();
                DraftPlugin.Instance.Log.LogInfo("[Game End] Zresetowano flagi Draftu");
            }
        }
    }
}
