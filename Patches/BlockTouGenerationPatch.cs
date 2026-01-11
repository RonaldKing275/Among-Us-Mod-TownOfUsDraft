using HarmonyLib;
using AmongUs.GameOptions;
using System.Reflection;
using System.Linq;

namespace TownOfUsDraft.Patches
{
    // Patch na RoleManager.SelectRoles - aplikuj role z Draftu SYNCHRONICZNIE!
    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
    public static class DraftRoleOverridePatch
    {
        private static bool _selectRolesBlocked = false;
        
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First + 200)] // NAJWYŻSZY priorytet
        [HarmonyBefore("auavengers.tou.mira")] // KRYTYCZNE: Wykonaj się PRZED TOU-Mira!
        public static bool Prefix()
        {
            DraftPlugin.Instance.Log.LogInfo($"[Prefix] EnableDraftMode: {TouConfigAdapter.EnableDraftMode.Value}, PendingRoles.Count: {DraftManager.PendingRoles.Count}, _selectRolesBlocked: {_selectRolesBlocked}");
            
            if (!TouConfigAdapter.EnableDraftMode.Value)
            {
                // Draft wyłączony - pozwól TOU działać normalnie
                SetTouReplaceRoleManagerFlag(false);
                DraftPlugin.Instance.Log.LogInfo("[Prefix] Draft wyłączony - TOU może działać normalnie");
                return true;
            }
            
            // Sprawdź czy PendingRoles są puste I Draft się jeszcze nie zaczął
            if (DraftManager.PendingRoles.Count == 0 && !_selectRolesBlocked)
            {
                // PIERWSZE WYWOŁANIE SelectRoles - zablokuj i uruchom Draft!
                _selectRolesBlocked = true;
                SetTouReplaceRoleManagerFlag(true); // Zablokuj TOU
                DraftPlugin.Instance.Log.LogInfo("[Prefix] BLOKUJĘ SelectRoles! Uruchamiam Draft...");
                DraftManager.StartDraft();
                return false; // Zablokuj SelectRoles!
            }
            
            // Mamy role z draftu - ZABLOKUJ vanilla, aplikujemy w Postfix
            if (DraftManager.PendingRoles.Count > 0)
            {
                SetTouReplaceRoleManagerFlag(true); // Zablokuj TOU (vanilla sprawdzi tę flagę i nic nie zrobi)
                DraftPlugin.Instance.Log.LogInfo("[Prefix] Mamy role z Draftu - BLOKUJĘ vanilla, aplikuję w Postfix!");
                return false; // BLOKUJ vanilla SelectRoles - nie chcemy losowych ról!
            }
            
            // Fallback - pozwól TOU działać
            SetTouReplaceRoleManagerFlag(false);
            DraftPlugin.Instance.Log.LogInfo("[Prefix] Fallback - TOU może działać");
            return true;
        }
        
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix()
        {
            if (!TouConfigAdapter.EnableDraftMode.Value) return;
            if (DraftManager.PendingRoles.Count == 0) return;
            if (DraftManager._rolesApplied) return;
            
            DraftPlugin.Instance.Log.LogError("╔═════════════════════════════════════════════════════════════════╗");
            DraftPlugin.Instance.Log.LogError("║       SelectRoles POSTFIX - APLIKUJĘ ROLE Z DRAFTU!            ║");
            DraftPlugin.Instance.Log.LogError("╚═════════════════════════════════════════════════════════════════╝");
            
            // Odblokuj TOU/MiraAPI żeby RpcSetRole działało dla custom ról
            SetTouReplaceRoleManagerFlag(false);
            DraftPlugin.Instance.Log.LogError("    → TOU odblokowane - MiraAPI patch będzie działać");
            
            // Aplikuj role z PendingRoles
            int count = 0;
            foreach (var kvp in DraftManager.PendingRoles)
            {
                var player = DraftManager.GetPlayerById(kvp.Key);
                if (player != null && !player.Data.Disconnected)
                {
                    var roleBehaviour = kvp.Value;
                    var rolePrefabName = (roleBehaviour as UnityEngine.Object)?.name ?? "Unknown";
                    
                    DraftPlugin.Instance.Log.LogError($"[POSTFIX] → Gracz: {player.Data.PlayerName} (ID:{player.PlayerId})");
                    DraftPlugin.Instance.Log.LogError($"[POSTFIX]   ├─ Wybrana rola (prefab): {rolePrefabName}");
                    DraftPlugin.Instance.Log.LogError($"[POSTFIX]   ├─ RoleBehaviour Type: {roleBehaviour.GetType().FullName}");
                    DraftPlugin.Instance.Log.LogError($"[POSTFIX]   ├─ RoleBehaviour.Role (RoleTypes): {(int)roleBehaviour.Role} = {roleBehaviour.Role}");
                    
                    if (AmongUsClient.Instance.AmHost)
                    {
                        DraftPlugin.Instance.Log.LogError($"[POSTFIX]   ├─ HOST: Ustawiam lokalnie + wysyłam RPC_SET_DRAFT_ROLE");
                        
                        // Host: Ustaw lokalnie używając DraftNetworkPatch.SetExactRoleLocal
                        DraftNetworkPatch.CallSetExactRoleLocal(player, roleBehaviour);
                        
                        // Host: Wyślij custom RPC do klientów
                        SendSetDraftRoleRpc(player.PlayerId, rolePrefabName);
                        
                        DraftPlugin.Instance.Log.LogError($"[POSTFIX]   └─ ✓ Custom RPC wysłany");
                    }
                    else
                    {
                        DraftPlugin.Instance.Log.LogError($"[POSTFIX]   └─ KLIENT: Czekam na RPC_SET_DRAFT_ROLE od hosta");
                    }
                    
                    // Sprawdź przypisaną rolę
                    if (player.Data != null && player.Data.Role != null)
                    {
                        var assignedRoleName = (player.Data.Role as UnityEngine.Object)?.name ?? player.Data.Role.GetType().Name;
                        var assignedRoleType = player.Data.Role.Role;
                        DraftPlugin.Instance.Log.LogError($"[POSTFIX]      ✓ player.Data.Role: {assignedRoleName} (RoleTypes:{(int)assignedRoleType})");
                    }
                    else
                    {
                        DraftPlugin.Instance.Log.LogError($"[POSTFIX]      ✗ player.Data.Role jest NULL.");
                    }
                    
                    count++;
                }
            }
            
            DraftPlugin.Instance.Log.LogError($"    ✓ Przetworzono {count}/{DraftManager.PendingRoles.Count} ról");
            
            // KLUCZOWE: Wywołaj AssignTargets() PO aplikacji ról z draftu!
            // TOU wywołało AssignTargets() w swoim Postfix, ale dla STARYCH ról.
            // Teraz musimy wywołać ponownie dla NOWYCH ról - ZARÓWNO na hoście JAK I na klientach!
            DraftPlugin.Instance.Log.LogError("    → Wywołuję TOU's AssignTargets() dla nowych ról...");
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
                            // Wywołaj lokalnie na hoście
                            assignTargetsMethod.Invoke(null, null);
                            DraftPlugin.Instance.Log.LogError("    ✓ AssignTargets() wywołane na hoście!");
                            
                            // Wyślij RPC do wszystkich klientów
                            if (AmongUsClient.Instance.AmHost)
                            {
                                SendAssignTargetsRpc();
                                DraftPlugin.Instance.Log.LogError("    ✓ Wysłano RPC_ASSIGN_TARGETS do klientów!");
                            }
                        }
                        else
                        {
                            DraftPlugin.Instance.Log.LogWarning("    ⚠ Nie znaleziono metody AssignTargets()");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DraftPlugin.Instance.Log.LogError($"    ✗ Błąd podczas wywołania AssignTargets(): {ex.Message}");
            }
            
            // Wyczyść PendingRoles i ustaw flagę
            DraftManager._rolesApplied = true;
            DraftManager.PendingRoles.Clear();
            _selectRolesBlocked = false;
        }
        
        // Wysyła RPC informujący wszystkich że należy wywołać AssignTargets()
        private static void SendAssignTargetsRpc()
        {
            if (!AmongUsClient.Instance.AmHost)
            {
                DraftPlugin.Instance.Log.LogWarning("[SendAssignTargetsRpc] Tylko host może wysyłać ten RPC!");
                return;
            }
            
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                DraftNetworkPatch.RPC_ASSIGN_TARGETS,
                Hazel.SendOption.Reliable,
                -1   // Wyślij do wszystkich
            );
            
            // Brak danych do wysłania - to tylko sygnał
            
            AmongUsClient.Instance.FinishRpcImmediately(writer);
            
            DraftPlugin.Instance.Log.LogError($"[SendAssignTargetsRpc] ✓ Wysłano RPC_ASSIGN_TARGETS");
        }
        
        // Wysyła custom RPC z nazwą prefab roli do wszystkich klientów
        private static void SendSetDraftRoleRpc(byte playerId, string rolePrefabName)
        {
            if (!AmongUsClient.Instance.AmHost)
            {
                DraftPlugin.Instance.Log.LogWarning("[SendSetDraftRoleRpc] Tylko host może wysyłać ten RPC!");
                return;
            }
            
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                250, // RPC_SET_DRAFT_ROLE
                Hazel.SendOption.Reliable,
                -1   // Wyślij do wszystkich
            );
            
            writer.Write(playerId);
            writer.Write(rolePrefabName);
            
            AmongUsClient.Instance.FinishRpcImmediately(writer);
            
            DraftPlugin.Instance.Log.LogError($"[SendSetDraftRoleRpc] ✓ Wysłano RPC: PlayerId={playerId}, Role={rolePrefabName}");
        }
        
        // Ustawia TouRoleManagerPatches.ReplaceRoleManager przez refleksję
        public static void SetTouReplaceRoleManagerFlag(bool value)
        {
            try
            {
                var touAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "TownOfUsMira");
                    
                if (touAssembly == null) return;

                var patchType = touAssembly.GetTypes()
                    .FirstOrDefault(t => t.Name == "TouRoleManagerPatches");
                    
                if (patchType == null) return;

                var field = patchType.GetField("ReplaceRoleManager", BindingFlags.Public | BindingFlags.Static);
                
                if (field == null) return;

                field.SetValue(null, value);
            }
            catch { }
        }
    }
}
