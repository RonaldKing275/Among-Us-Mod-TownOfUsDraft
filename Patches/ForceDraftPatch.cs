using HarmonyLib;
using UnityEngine;
using AmongUs.GameOptions;
using System.Collections;
using System;

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
            DraftPlugin.Instance.Log.LogInfo("[FORCE DRAFT] Draft zakończony!");
            
            // KLUCZOWE: Aplikuj role TERAZ (przed uruchomieniem Intro)
            if (DraftManager.PendingRoles.Count > 0 && !DraftManager._rolesApplied)
            {
                DraftPlugin.Instance.Log.LogInfo("[OnDraftCompleted] ═══════════════════════════════════");
                DraftPlugin.Instance.Log.LogInfo($"[OnDraftCompleted] APLIKUJĘ {DraftManager.PendingRoles.Count} RÓL Z DRAFTU!");
                
                int count = 0;
                foreach (var kvp in DraftManager.PendingRoles)
                {
                    var player = DraftManager.GetPlayerById(kvp.Key);
                    if (player != null && !player.Data.Disconnected)
                    {
                        var roleBehaviour = kvp.Value;
                        var roleName = roleBehaviour.GetType().Name.Replace("Role", "");
                        
                        DraftPlugin.Instance.Log.LogInfo($"[OnDraftCompleted] → {player.Data.PlayerName} = {roleName} (RoleID: {(int)roleBehaviour.Role})");
                        
                        // DEBUG: Log przed aplikacją roli
                        DraftPlugin.Instance.Log.LogInfo($"[OnDraftCompleted]    PRZED aplikacją: Role={player.Data.Role.Role}, TeamType={player.Data.Role.TeamType}");
                        DraftPlugin.Instance.Log.LogInfo($"[OnDraftCompleted]    Aplikuję rolę: {roleBehaviour.GetType().Name} (z PendingRoles)");
                        
                        // KLUCZOWE 1: Najpierw wyślij RPC dla bazowego typu (Impostor/Crewmate)
                        // To zapewni synchronizację między klientami dla vanilla części
                        bool shouldBeImpostor = IsImpostorRole(kvp.Key, roleBehaviour);
                        var baseRole = shouldBeImpostor ? RoleTypes.Impostor : RoleTypes.Crewmate;
                        
                        DraftPlugin.Instance.Log.LogInfo($"[OnDraftCompleted]    Wysyłam RpcSetRole({baseRole}) dla synchronizacji bazy");
                        player.RpcSetRole(baseRole);
                        
                        // KLUCZOWE 2: NADPISZ lokalnie player.Data.Role custom rolą
                        // To musi być AFTER RpcSetRole, żeby nie zostało nadpisane z powrotem
                        player.Data.Role = roleBehaviour;
                        
                        // DEBUG: Log po aplikacji
                        DraftPlugin.Instance.Log.LogInfo($"[OnDraftCompleted]    PO aplikacji: Role={player.Data.Role.Role}, TeamType={player.Data.Role.TeamType}, RoleName={player.Data.Role.GetType().Name}");
                        
                        // KLUCZOWE 3: Wywołaj Start() na roli żeby zainicjalizować komponenty
                        try
                        {
                            var startMethod = player.Data.Role.GetType().GetMethod("Start", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (startMethod != null)
                            {
                                startMethod.Invoke(player.Data.Role, null);
                                DraftPlugin.Instance.Log.LogInfo($"[OnDraftCompleted]    ✓ Wywołano Start() dla {player.Data.PlayerName}");
                            }
                            else
                            {
                                DraftPlugin.Instance.Log.LogWarning($"[OnDraftCompleted]    ⚠ Nie znaleziono Start() dla {roleName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            DraftPlugin.Instance.Log.LogError($"[OnDraftCompleted]    ✗ Błąd Start(): {ex.Message}");
                        }
                        
                        count++;
                    }
                }
                
                DraftPlugin.Instance.Log.LogInfo($"[OnDraftCompleted] ✓ Zaaplikowano {count}/{DraftManager.PendingRoles.Count} ról");
                DraftPlugin.Instance.Log.LogInfo("[OnDraftCompleted] ═══════════════════════════════════");
                
                DraftManager._rolesApplied = true;
                DraftManager.PendingRoles.Clear();
                
                // Odblokuj TOU
                DraftRoleOverridePatch.SetTouReplaceRoleManagerFlag(false);
                DraftPlugin.Instance.Log.LogInfo("[OnDraftCompleted] → TOU odblokowane");
            }
            
            // Krótkie czekanie żeby RpcSetRole się zsynchronizowało
            System.Threading.Thread.Sleep(100);
            
            DraftPlugin.Instance.Log.LogInfo("[OnDraftCompleted] → Ręcznie uruchamiam Intro...");
            
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
                    
                    // KLUCZOWE: Sprawdź player.Data.Role.TeamType (używane przez Intro)
                    if (localPlayer.Data.Role.TeamType == RoleTeamTypes.Impostor)
                    {
                        DraftPlugin.Instance.Log.LogInfo("[OnDraftCompleted] → Uruchamiam BeginImpostor (czerwony ekran)");
                        // Dla Impostorów podaj listę wszystkich Impostorów (włącznie z lokalnym!)
                        var impostors = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
                        foreach (var player in PlayerControl.AllPlayerControls)
                        {
                            if (player != null && player.Data.Role.TeamType == RoleTeamTypes.Impostor)
                            {
                                impostors.Add(player);
                            }
                        }
                        introCutscene.BeginImpostor(impostors);
                    }
                    else
                    {
                        DraftPlugin.Instance.Log.LogInfo("[OnDraftCompleted] → Uruchamiam BeginCrewmate (niebieski ekran)");
                        // Dla Crewmate podaj listę wszystkich graczy
                        introCutscene.BeginCrewmate(allPlayers);
                    }
                }
            }
        }
        
        // Sprawdź czy rola jest Impostor role
        private static bool IsImpostorRole(byte playerId, RoleBehaviour roleBehaviour)
        {
            // Sprawdź kategorię którą gracz wybrał w Drafcie
            if (DraftManager.HostDraftAssignments.TryGetValue(playerId, out var category))
            {
                // Kategorie Impostor z RoleCategorizer
                return category == RoleCategory.RandomImp ||
                       category == RoleCategory.CommonImp ||
                       category == RoleCategory.ImpConcealing ||
                       category == RoleCategory.ImpKilling ||
                       category == RoleCategory.ImpPower ||
                       category == RoleCategory.ImpSupport ||
                       category == RoleCategory.SpecialImp;
            }
            
            // Fallback: Sprawdź przez RoleMap
            var roleName = roleBehaviour.GetType().Name.Replace("Role", "");
            if (RoleCategorizer.RoleMap.TryGetValue(roleName, out var mappedCategory))
            {
                return mappedCategory == RoleCategory.RandomImp ||
                       mappedCategory == RoleCategory.CommonImp ||
                       mappedCategory == RoleCategory.ImpConcealing ||
                       mappedCategory == RoleCategory.ImpKilling ||
                       mappedCategory == RoleCategory.ImpPower ||
                       mappedCategory == RoleCategory.ImpSupport ||
                       mappedCategory == RoleCategory.SpecialImp;
            }
            
            // Default: nie jest Impostorem
            return false;
        }
        
        // Wyślij RPC do wszystkich klientów żeby zsynchronizować TeamType
        private static void SendTeamTypeRpc(byte playerId, RoleTeamTypes teamType)
        {
            if (!AmongUsClient.Instance.AmHost) return; // Tylko host wysyła RPC
            
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                DraftNetworkPatch.RPC_SET_TEAMTYPE,
                Hazel.SendOption.Reliable,
                -1
            );
            
            writer.Write(playerId);
            writer.Write((byte)teamType); // 0=Crewmate, 1=Impostor, 2=Neutral
            
            AmongUsClient.Instance.FinishRpcImmediately(writer);
            
            DraftPlugin.Instance.Log.LogInfo($"[SendTeamTypeRpc] Wysłano RPC: playerId={playerId}, teamType={teamType}");
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