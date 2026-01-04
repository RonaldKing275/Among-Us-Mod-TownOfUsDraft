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
        // Callback: Po zakończeniu Draftu wznów grę
        public static void OnDraftCompleted()
        {
            // ⚠️ KRYTYCZNE: SelectRoles() TYLKO NA HOŚCIE!
            // Klienty otrzymają role przez RPC od hosta
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            {
                // Wyczyść FirstRoundPlayerNames żeby uniknąć duplikacji FirstRoundIndicator
                ClearFirstRoundPlayerNames();
                
                // WYWOŁAJ SelectRoles ponownie - Postfix zaaplikuje role
                var roleManager = RoleManager.Instance;
                if (roleManager != null)
                {
                    roleManager.SelectRoles();
                    
                    // PO aplikacji ról, POCZEKAJ aż Unity zainicjalizuje wszystko
                    var shipStatus = ShipStatus.Instance;
                    if (shipStatus != null)
                    {
                        shipStatus.StartCoroutine(CoDelayedBegin(shipStatus).WrapToIl2Cpp());
                    }
                }
            }
        }
        
        // Coroutine: Czeka 3s aż Unity zainicjalizuje role, potem wywołuje ShipStatus.Begin()
        private static IEnumerator CoDelayedBegin(ShipStatus shipStatus)
        {
            yield return new WaitForSeconds(3f);
            
            // Begin() musi być wywołane TYLKO na hoście!
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            {
                shipStatus.Begin();
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
                // Wyczyść DraftHud żeby nie interferował z UI
                if (DraftHud.Instance != null)
                {
                    UnityEngine.Object.Destroy(DraftHud.Instance.gameObject);
                }
            }
        }
    }
}
