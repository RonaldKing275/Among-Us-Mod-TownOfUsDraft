using HarmonyLib;
using UnityEngine;
using AmongUs.GameOptions;
using System.Collections;

namespace TownOfUsDraft.Patches
{
    public static class ForceDraftPatch
    {
        private static bool _draftStarted = false;
        private static bool _draftCompleted = false;

        // START DRAFTU podczas Intro (ale BLOKUJEMY Intro do zakończenia Draftu!)
        [HarmonyPatch(typeof(IntroCutscene), "BeginCrewmate")]
        public static class ForceDraftCrewmatePatch
        {
            public static bool Prefix()
            {
                if (!TouConfigAdapter.EnableDraftMode.Value) return true;
                
                // Jeśli Draft już zakończony, pozwól Intro działać normalnie
                if (_draftCompleted) return true;
                
                // Jeśli Draft jeszcze się nie rozpoczął, rozpocznij go TERAZ
                if (!_draftStarted)
                {
                    _draftStarted = true;
                    DraftPlugin.Instance.Log.LogInfo("[FORCE DRAFT] Blokuję BeginCrewmate! Uruchamiam Draft...");
                    DraftManager.StartDraft();
                }
                
                // BLOKUJ Intro do zakończenia Draftu!
                DraftPlugin.Instance.Log.LogInfo("[FORCE DRAFT] BeginCrewmate zablokowany - czekam na zakończenie Draftu...");
                return false; // Nie pozwalaj Intro się uruchomić!
            }
        }

        [HarmonyPatch(typeof(IntroCutscene), "BeginImpostor")]
        public static class ForceDraftImpostorPatch
        {
            public static bool Prefix()
            {
                if (!TouConfigAdapter.EnableDraftMode.Value) return true;
                
                // Jeśli Draft już zakończony, pozwól Intro działać normalnie
                if (_draftCompleted) return true;
                
                // Jeśli Draft jeszcze się nie rozpoczął, rozpocznij go TERAZ
                if (!_draftStarted)
                {
                    _draftStarted = true;
                    DraftPlugin.Instance.Log.LogInfo("[FORCE DRAFT] Blokuję BeginImpostor! Uruchamiam Draft...");
                    DraftManager.StartDraft();
                }
                
                // BLOKUJ Intro do zakończenia Draftu!
                DraftPlugin.Instance.Log.LogInfo("[FORCE DRAFT] BeginImpostor zablokowany - czekam na zakończenie Draftu...");
                return false; // Nie pozwalaj Intro się uruchomić!
            }
        }


        // KLUCZOWY PATCH: Aplikuj role w ShowRole Prefix (przed pokazaniem Intro)
        [HarmonyPatch(typeof(IntroCutscene), "ShowRole")]
        public static class ApplyDraftRolesInShowRole
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)] // MUSI być pierwszy
            public static void Prefix()
            {
                if (!TouConfigAdapter.EnableDraftMode.Value) return;
                if (DraftManager._rolesApplied) return; // Już zaaplikowane
                
                DraftPlugin.Instance.Log.LogInfo("[ShowRole Prefix] ═══════════════════════════════════");
                DraftPlugin.Instance.Log.LogInfo("[ShowRole Prefix] APLIKUJĘ ROLE Z DRAFTU!");
                
                int count = 0;
                foreach (var kvp in DraftManager.PendingRoles)
                {
                    var player = DraftManager.GetPlayerById(kvp.Key);
                    if (player != null && !player.Data.Disconnected)
                    {
                        var roleBehaviour = kvp.Value;
                        var roleName = roleBehaviour.GetType().Name.Replace("Role", "");
                        
                        DraftPlugin.Instance.Log.LogInfo($"[ShowRole Prefix] → {player.Data.PlayerName} = {roleName} (RoleID: {(int)roleBehaviour.Role})");
                        
                        // KLUCZOWE: RpcSetRole w ShowRole Prefix
                        // MiraAPI automatycznie doda komponenty roli
                        player.RpcSetRole(roleBehaviour.Role);
                        
                        count++;
                    }
                }
                
                DraftPlugin.Instance.Log.LogInfo($"[ShowRole Prefix] ✓ Zaaplikowano {count}/{DraftManager.PendingRoles.Count} ról");
                DraftPlugin.Instance.Log.LogInfo("[ShowRole Prefix] ═══════════════════════════════════");
                
                DraftManager._rolesApplied = true;
                DraftManager.PendingRoles.Clear();
                
                // Odblokuj TOU dla ShowRole
                DraftRoleOverridePatch.SetTouReplaceRoleManagerFlag(false);
            }
        }

        // Metoda wywoływana po zakończeniu Draftu
        public static void OnDraftCompleted()
        {
            _draftCompleted = true;
            DraftPlugin.Instance.Log.LogInfo("[FORCE DRAFT] Draft zakończony! Ręcznie uruchamiam Intro...");
            
            // Ręcznie uruchom Intro dla lokalnego gracza
            var introCutscene = UnityEngine.Object.FindObjectOfType<IntroCutscene>();
            if (introCutscene != null)
            {
                // Wywołaj odpowiednią metodę w zależności od roli
                var localPlayer = PlayerControl.LocalPlayer;
                if (localPlayer != null)
                {
                    // Przygotuj listę graczy
                    var allPlayers = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
                    foreach (var player in PlayerControl.AllPlayerControls)
                    {
                        if (player != null) allPlayers.Add(player);
                    }
                    
                    if (localPlayer.Data.Role.IsImpostor)
                    {
                        DraftPlugin.Instance.Log.LogInfo("[FORCE DRAFT] → Uruchamiam BeginImpostor");
                        // Dla Impostorów podaj listę innych Impostorów
                        var impostors = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
                        foreach (var player in PlayerControl.AllPlayerControls)
                        {
                            if (player != null && player.Data.Role.IsImpostor && player.PlayerId != localPlayer.PlayerId)
                            {
                                impostors.Add(player);
                            }
                        }
                        introCutscene.BeginImpostor(impostors);
                    }
                    else
                    {
                        DraftPlugin.Instance.Log.LogInfo("[FORCE DRAFT] → Uruchamiam BeginCrewmate");
                        // Dla Crewmate podaj listę wszystkich graczy
                        introCutscene.BeginCrewmate(allPlayers);
                    }
                }
            }
        }

        // Reset flagi na koniec gry
        [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
        public static class ResetDraftFlag
        {
            public static void Postfix()
            {
                _draftStarted = false;
                _draftCompleted = false;
            }
        }
    }
}