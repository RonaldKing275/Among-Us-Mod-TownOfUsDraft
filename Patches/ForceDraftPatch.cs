using HarmonyLib;
using AmongUs.GameOptions;
using System;
using System.Linq;

namespace TownOfUsDraft.Patches
{
    public static class ForceDraftPatch
    {
        // Callback: Po zakończeniu Draftu wznów grę
        public static void OnDraftCompleted()
        {
            DraftPlugin.Instance.Log.LogError("╔═══════════════════════════════════════════════════════╗");
            DraftPlugin.Instance.Log.LogError("║           OnDraftCompleted() WYWOŁANE!                ║");
            DraftPlugin.Instance.Log.LogError("╚═══════════════════════════════════════════════════════╝");
            
            // ⚠️ KRYTYCZNE: SelectRoles() TYLKO NA HOŚCIE!
            // Klienty otrzymają role przez RPC od hosta
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            {
                DraftPlugin.Instance.Log.LogError("[OnDraftCompleted] HOST: Rozpoczynam sekwencję rozpoczęcia gry...");
                
                // Wyczyść FirstRoundPlayerNames żeby uniknąć duplikacji FirstRoundIndicator
                ClearFirstRoundPlayerNames();
                
                // WYWOŁAJ SelectRoles ponownie - Postfix zaaplikuje role
                var roleManager = RoleManager.Instance;
                if (roleManager != null)
                {
                    DraftPlugin.Instance.Log.LogError("[OnDraftCompleted] Wywołuję roleManager.SelectRoles()...");
                    roleManager.SelectRoles();
                    
                    // PO aplikacji ról, RĘCZNIE uruchom intro bo zablokujemy vanilla SelectRoles
                    var shipStatus = ShipStatus.Instance;
                    if (shipStatus != null)
                    {
                        DraftPlugin.Instance.Log.LogError("[OnDraftCompleted] Wywołuję shipStatus.Begin() ręcznie...");
                        shipStatus.Begin();
                        DraftPlugin.Instance.Log.LogError("[OnDraftCompleted] ✓ shipStatus.Begin() wywołane!");
                    }
                    else
                    {
                        DraftPlugin.Instance.Log.LogError("[OnDraftCompleted] ✗ ShipStatus.Instance jest NULL!");
                    }
                }
                else
                {
                    DraftPlugin.Instance.Log.LogError("[OnDraftCompleted] ✗ RoleManager.Instance jest NULL!");
                }
            }
        }
        
        // Wyczyść TOU's FirstRoundPlayerNames żeby uniknąć duplikacji modifierów
        private static void ClearFirstRoundPlayerNames()
        {
            try
            {
                var firstDeadPatchType = Type.GetType("TownOfUs.Patches.FirstDeadPatch, TownOfUs");
                if (firstDeadPatchType != null)
                {
                    var firstRoundField = firstDeadPatchType.GetField("FirstRoundPlayerNames", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    
                    if (firstRoundField != null)
                    {
                        var list = firstRoundField.GetValue(null) as System.Collections.Generic.List<string>;
                        if (list != null)
                        {
                            list.Clear();
                        }
                    }
                }
            }
            catch { }
        }
        
        // Reset flagi na koniec gry
        [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
        public static class ResetDraftFlag
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                DraftPlugin.Instance.Log.LogInfo("[OnGameEnd] Resetuję stan Draftu...");
                
                // Resetuj menadżera
                DraftManager.ResetState();
                
                // Resetuj patch blokujący
                DraftRoleOverridePatch.ResetPatchState();

                // Wyczyść DraftHud
                if (DraftHud.Instance != null)
                {
                    UnityEngine.Object.Destroy(DraftHud.Instance.gameObject);
                }
            }
        }

        // Dodatkowy reset przy starcie gry (bezpiecznik)
        [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.StartGame))]
        public static class ResetDraftOnStart
        {
            [HarmonyPrefix]
            public static void Prefix()
            {
                DraftPlugin.Instance.Log.LogInfo("[OnGameStart] Upewniam się, że stan Draftu jest czysty...");
                DraftManager.ResetState();
                DraftRoleOverridePatch.ResetPatchState();
            }
        }
    }
}
