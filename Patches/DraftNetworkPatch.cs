using HarmonyLib;
using Hazel;
using InnerNet;
using MiraAPI.Roles; 
using UnityEngine;
using AmongUs.GameOptions;
using System.Linq; 

namespace TownOfUsDraft.Patches
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
    public static class DraftNetworkPatch
    {
        public const byte RPC_ROLE_SELECTED = 249; // Ktoś wybrał rolę
        public const byte RPC_START_TURN = 251;    // Nowa tura (Host -> All)
        public const byte RPC_SET_TEAMTYPE = 248;  // Synchronizacja TeamType (Host -> All)
        public const byte RPC_TIMER_SYNC = 254;    // Synchronizacja timera (Host -> All)
        public const byte RPC_SET_DRAFT_ROLE = 250; // Synchronizacja dokładnej roli po nazwie prefab (Host -> All)
        public const byte RPC_ASSIGN_TARGETS = 247; // NOWE: Wywołaj AssignTargets() na wszystkich klientach

        public static void Postfix(PlayerControl __instance, byte callId, MessageReader reader)
        {
            if (callId == RPC_ROLE_SELECTED)
            {
                byte playerId = reader.ReadByte();
                string roleName = reader.ReadString();
                PlayerControl target = Helpers.GetPlayerById(playerId);
                if (target != null) DraftManager.ApplyRoleFromRpc(target, roleName);
            }
            else if (callId == RPC_START_TURN)
            {
                // Odbieramy: Kto teraz wybiera? Jakie ma opcje? Jaka to kategoria?
                byte activePlayerId = reader.ReadByte();
                string catName = reader.ReadString();
                string op1 = reader.ReadString();
                string op2 = reader.ReadString();
                string op3 = reader.ReadString();

                DraftManager.OnTurnStarted(activePlayerId, catName, new System.Collections.Generic.List<string>{op1, op2, op3});
            }
            else if (callId == RPC_TIMER_SYNC)
            {
                // Synchronizacja timera od hosta
                float timerValue = reader.ReadSingle();
                DraftHud.TurnWatchdogTimer = timerValue;
            }
            else if (callId == RPC_SET_DRAFT_ROLE)
            {
                // Klient odbiera: playerId + nazwa prefab roli
                byte playerId = reader.ReadByte();
                string rolePrefabName = reader.ReadString();
                
                PlayerControl target = Helpers.GetPlayerById(playerId);
                if (target != null)
                {
                    DraftPlugin.Instance.Log.LogError($"[RPC_SET_DRAFT_ROLE] Odebrano: {target.Data.PlayerName} → {rolePrefabName}");
                    
                    // Znajdź dokładną rolę po nazwie prefab
                    RoleBehaviour exactRole = null;
                    foreach (var r in RoleManager.Instance.AllRoles)
                    {
                        var uObj = r as UnityEngine.Object;
                        if (uObj != null && uObj.name == rolePrefabName)
                        {
                            exactRole = r as RoleBehaviour;
                            DraftPlugin.Instance.Log.LogError($"[RPC_SET_DRAFT_ROLE] ✓ Znaleziono: {rolePrefabName}");
                            break;
                        }
                    }
                    
                    if (exactRole != null)
                    {
                        SetExactRoleLocal(target, exactRole);
                        
                        // Sprawdź rezultat
                        if (target.Data != null && target.Data.Role != null)
                        {
                            var finalRoleName = (target.Data.Role as UnityEngine.Object)?.name ?? target.Data.Role.GetType().Name;
                            DraftPlugin.Instance.Log.LogError($"[RPC_SET_DRAFT_ROLE] ✓ Rezultat: {target.Data.PlayerName} ma teraz {finalRoleName}");
                        }
                    }
                    else
                    {
                        DraftPlugin.Instance.Log.LogError($"[RPC_SET_DRAFT_ROLE] ✗ NIE znaleziono roli: {rolePrefabName}");
                    }
                }
            }
            else if (callId == RPC_ASSIGN_TARGETS)
            {
                // Host informuje wszystkich że należy wywołać AssignTargets()
                DraftPlugin.Instance.Log.LogError("[RPC_ASSIGN_TARGETS] Odebrano polecenie wywołania AssignTargets()");
                
                try
                {
                    var touAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "TownOfUsMira");
                        
                    if (touAssembly != null)
                    {
                        var patchType = touAssembly.GetTypes()
                            .FirstOrDefault(t => t.Name == "TouRoleManagerPatches");
                            
                        if (patchType != null)
                        {
                            var assignTargetsMethod = patchType.GetMethod("AssignTargets", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                            
                            if (assignTargetsMethod != null)
                            {
                                assignTargetsMethod.Invoke(null, null);
                                DraftPlugin.Instance.Log.LogError("[RPC_ASSIGN_TARGETS] ✓ AssignTargets() wywołane lokalnie!");
                            }
                            else
                            {
                                DraftPlugin.Instance.Log.LogWarning("[RPC_ASSIGN_TARGETS] ⚠ Nie znaleziono metody AssignTargets()");
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    DraftPlugin.Instance.Log.LogError($"[RPC_ASSIGN_TARGETS] ✗ Błąd: {ex.Message}");
                }
            }
            else if (callId == RPC_SET_TEAMTYPE)
            {
                // Odbieramy: playerId + teamType (byte: 0=Crewmate, 1=Impostor, 2=Neutral)
                byte playerId = reader.ReadByte();
                byte teamTypeByte = reader.ReadByte();
                PlayerControl target = Helpers.GetPlayerById(playerId);
                
                if (target != null && target.Data != null && target.Data.Role != null)
                {
                    var targetTeamType = (RoleTeamTypes)teamTypeByte;
                    
                    // Spróbuj ustawić TeamType przez reflection (jak w OnDraftCompleted)
                    var teamTypeProp = typeof(RoleBehaviour).GetProperty(
                        nameof(RoleBehaviour.TeamType), 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                    );
                    
                    if (teamTypeProp != null && teamTypeProp.CanWrite)
                    {
                        teamTypeProp.SetValue(target.Data.Role, targetTeamType);

                    }
                    else
                    {
                        // Fallback: backing field
                        var backingField = typeof(RoleBehaviour).GetField(
                            "<TeamType>k__BackingField", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                        );
                        
                        if (backingField != null)
                        {
                            backingField.SetValue(target.Data.Role, targetTeamType);

                        }
                        else
                        {
                            DraftPlugin.Instance.Log.LogWarning($"[RPC_SET_TEAMTYPE] ⚠ Nie można ustawić TeamType dla {target.Data.PlayerName}");
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Publiczny wrapper dla SetExactRoleLocal - pozwala innym klasom wywołać tę metodę.
        /// </summary>
        public static void CallSetExactRoleLocal(PlayerControl player, RoleBehaviour exactRole)
        {
            SetExactRoleLocal(player, exactRole);
        }
        
        /// <summary>
        /// Ustawia dokładną rolę graczowi, omijając problematyczny .First() w MiraAPI's SetRolePatch.
        /// Wywołuje wszystkie niezbędne eventy dla TOU-Mira (SetRoleEvent, ChangeRoleEvent).
        /// </summary>
        private static void SetExactRoleLocal(PlayerControl player, RoleBehaviour exactRole)
        {
            var data = player.Data;
            
            DraftPlugin.Instance.Log.LogError($"[SetExactRoleLocal] Ustawiam {player.Data.PlayerName} → {(exactRole as UnityEngine.Object)?.name}");
            
            // 1. Deinicjalizuj starą rolę
            if (data.Role)
            {
                DraftPlugin.Instance.Log.LogError($"[SetExactRoleLocal]   ├─ Deinicjalizuję starą rolę: {data.Role.GetType().Name}");
                data.Role.Deinitialize(player);
                UnityEngine.Object.Destroy(data.Role.gameObject);
            }
            
            // 2. Instantiate DOKŁADNEJ roli (omija problematyczny .First()!)
            var newRole = UnityEngine.Object.Instantiate(exactRole, data.gameObject.transform);
            DraftPlugin.Instance.Log.LogError($"[SetExactRoleLocal]   ├─ Instantiate: {(newRole as UnityEngine.Object)?.name}");
            
            // 3. Initialize nowej roli (KLUCZOWE dla ability buttons!)
            newRole.Initialize(player);
            data.Role = newRole;
            data.RoleType = exactRole.Role;
            DraftPlugin.Instance.Log.LogError($"[SetExactRoleLocal]   ├─ Initialize() wywołane, RoleType: {exactRole.Role}");
            
            if (!newRole.IsDead)
            {
                data.RoleWhenAlive = new Il2CppSystem.Nullable<RoleTypes>(exactRole.Role);
            }
            
            // 4. AdjustTasks (dla ról z custom taskami)
            newRole.AdjustTasks(player);
            DraftPlugin.Instance.Log.LogError($"[SetExactRoleLocal]   ├─ AdjustTasks() wywołane");
            
            // 4b. Obsługa ghost roles (Die/Revive) - zgodnie z MiraAPI's SetRolePatch
            switch (newRole.IsDead)
            {
                case true when !player.Data.IsDead:
                    DraftPlugin.Instance.Log.LogError($"[SetExactRoleLocal]   ├─ Ghost role + gracz żywy → Die()");
                    player.Die(DeathReason.Kill, false);
                    break;
                case false when player.Data.IsDead:
                    DraftPlugin.Instance.Log.LogError($"[SetExactRoleLocal]   ├─ Living role + gracz martwy → Revive()");
                    player.Revive();
                    break;
            }
            
            // 5. Wywołaj MiraAPI's SetRoleEvent - rejestruje rolę w GameHistory
            try
            {
                var setRoleEvent = new MiraAPI.Events.Vanilla.Gameplay.SetRoleEvent(player, exactRole.Role);
                MiraAPI.Events.MiraEventManager.InvokeEvent(setRoleEvent);
                DraftPlugin.Instance.Log.LogError($"[SetExactRoleLocal]   ├─ SetRoleEvent wywołany");
            }
            catch (System.Exception ex)
            {
                DraftPlugin.Instance.Log.LogError($"[SetExactRoleLocal]   ✗ Błąd SetRoleEvent: {ex.Message}");
            }
            
            // 6. Wywołaj TOU's ChangeRoleEvent - resetuje HUD i buttony
            try
            {
                // Musimy sprawdzić czy typ istnieje (może nie być załadowany)
                var touAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "TownOfUsMira");
                    
                if (touAssembly != null)
                {
                    var changeRoleEventType = touAssembly.GetTypes()
                        .FirstOrDefault(t => t.Name == "ChangeRoleEvent" && t.Namespace == "TownOfUs.Events.TouEvents");
                        
                    if (changeRoleEventType != null)
                    {
                        // Konstruktor: ChangeRoleEvent(PlayerControl player, RoleBehaviour oldRole, RoleBehaviour newRole, bool canOverride)
                        var changeRoleEvent = System.Activator.CreateInstance(changeRoleEventType, player, null, newRole, false);
                        MiraAPI.Events.MiraEventManager.InvokeEvent((MiraAPI.Events.MiraEvent)changeRoleEvent);
                        DraftPlugin.Instance.Log.LogError($"[SetExactRoleLocal]   ├─ ChangeRoleEvent wywołany");
                    }
                }
            }
            catch (System.Exception ex)
            {
                DraftPlugin.Instance.Log.LogError($"[SetExactRoleLocal]   ✗ Błąd ChangeRoleEvent: {ex.Message}");
            }
            
            DraftPlugin.Instance.Log.LogError($"[SetExactRoleLocal]   └─ ✓ GOTOWE! {player.Data.PlayerName} = {(data.Role as UnityEngine.Object)?.name}");
        }
    }

    public static class Helpers 
    {
        public static PlayerControl GetPlayerById(byte id)
        {
            foreach (var p in PlayerControl.AllPlayerControls) if (p.PlayerId == id) return p;
            return null;
        }
    }
}
