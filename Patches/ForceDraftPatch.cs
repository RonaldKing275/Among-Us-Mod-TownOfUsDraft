using HarmonyLib;
using UnityEngine;
using AmongUs.GameOptions;
using System.Collections;
using System;
using Hazel;
using InnerNet;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using BepInEx.Unity.IL2CPP.Utils.Collections;

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
            DraftPlugin.Instance.Log.LogInfo("║  Wywołuję SelectRoles → (delay) → Begin() → Gra się rozpocznie! ║");
            DraftPlugin.Instance.Log.LogInfo("╚═════════════════════════════════════════════════════════════════╝");
            
            // Wyczyść FirstRoundPlayerNames żeby uniknąć duplikacji FirstRoundIndicator
            ClearFirstRoundPlayerNames();
            
            // WYWOŁAJ SelectRoles ponownie
            // Tym razem PendingRoles są pełne, więc:
            // → Prefix zablokuje vanilla/TOU
            // → Postfix zaaplikuje role
            var roleManager = RoleManager.Instance;
            if (roleManager != null)
            {
                DraftPlugin.Instance.Log.LogInfo("[OnDraftCompleted] Wywołuję RoleManager.SelectRoles()...");
                roleManager.SelectRoles();
                
                // PO aplikacji ról, POCZEKAJ 1 klatkę i POTEM wywołaj ShipStatus.Begin()
                // To pozwala Unity na Instantiate i Initialize wszystkich ról
                var shipStatus = ShipStatus.Instance;
                if (shipStatus != null)
                {
                    DraftPlugin.Instance.Log.LogInfo("[OnDraftCompleted] Role zaaplikowane! Czekam 1 klatkę przed Begin()...");
                    shipStatus.StartCoroutine(CoDelayedBegin(shipStatus).WrapToIl2Cpp());
                }
                else
                {
                    DraftPlugin.Instance.Log.LogError("[OnDraftCompleted] ShipStatus.Instance jest null!");
                }
            }
            else
            {
                DraftPlugin.Instance.Log.LogError("[OnDraftCompleted] RoleManager.Instance jest null!");
            }
        }
        
        // Coroutine: Czeka 1 klatkę i potem wywołuje ShipStatus.Begin()
        private static IEnumerator CoDelayedBegin(ShipStatus shipStatus)
        {
            // Czekaj 1 klatkę żeby Unity miał czas na Initialize ról
            yield return null;
            
            DraftPlugin.Instance.Log.LogInfo("[CoDelayedBegin] Unity gotowe! Wywołuję ShipStatus.Begin()...");
            shipStatus.Begin();
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
                            DraftPlugin.Instance.Log.LogInfo($"    → Czyszczę FirstRoundPlayerNames ({list.Count} graczy)");
                            list.Clear();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DraftPlugin.Instance.Log.LogWarning($"    ! Nie udało się wyczyścić FirstRoundPlayerNames: {ex.Message}");
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
                
                // Wyczyść DraftHud żeby nie interferował z UI
                if (DraftHud.Instance != null)
                {
                    UnityEngine.Object.Destroy(DraftHud.Instance.gameObject);
                    DraftPlugin.Instance.Log.LogInfo("[Game End] Usunięto DraftHud");
                }
                
                DraftPlugin.Instance.Log.LogInfo("[Game End] Zresetowano flagi Draftu");
            }
        }
    }
}
