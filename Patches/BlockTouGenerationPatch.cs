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
            DraftPlugin.Instance.Log.LogInfo($"[Prefix] EnableDraftMode: {TouConfigAdapter.EnableDraftMode}, PendingRoles.Count: {DraftManager.PendingRoles.Count}, _selectRolesBlocked: {_selectRolesBlocked}");
            
            if (!TouConfigAdapter.EnableDraftMode)
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
        
        public static void ResetPatchState()
        {
             _selectRolesBlocked = false;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix()
        {
            if (!TouConfigAdapter.EnableDraftMode) return;
            if (DraftManager.PendingRoles.Count == 0) return;
            if (DraftManager._rolesApplied) return;
            
            DraftPlugin.Instance.Log.LogError("╔═════════════════════════════════════════════════════════════════╗");
            DraftPlugin.Instance.Log.LogError("║       SelectRoles POSTFIX - APLIKUJĘ ROLE Z DRAFTU!            ║");
            DraftPlugin.Instance.Log.LogError("╚═════════════════════════════════════════════════════════════════╝");
            
            // Odblokuj TOU/MiraAPI żeby RpcSetRole działało dla custom ról
            SetTouReplaceRoleManagerFlag(false);
            DraftPlugin.Instance.Log.LogError("    → TOU odblokowane - MiraAPI patch będzie działać");
            
            // Aplikuj role z PendingRoles - TYLKO NA HOŚCIE
            if (AmongUsClient.Instance.AmHost)
            {
                int count = 0;
                foreach (var kvp in DraftManager.PendingRoles)
                {
                    var player = DraftManager.GetPlayerById(kvp.Key);
                    if (player != null && !player.Data.Disconnected)
                    {
                        var roleBehaviour = kvp.Value;
                        
                        DraftPlugin.Instance.Log.LogError($"[POSTFIX] → Gracz: {player.Data.PlayerName} (ID:{player.PlayerId})");
                        DraftPlugin.Instance.Log.LogError($"[POSTFIX]   ├─ Ustawiam rolę (RpcSetRole): {roleBehaviour.Role}");

                        // Używamy natywnego RpcSetRole, który TOU/MiraAPI patchuje
                        player.RpcSetRole(roleBehaviour.Role);
                        
                        count++;
                    }
                }
                
                DraftPlugin.Instance.Log.LogError($"    ✓ Przetworzono {count}/{DraftManager.PendingRoles.Count} ról");
                
                // KLUCZOWE: Wywołaj AssignTargets() PO aplikacji ról z draftu!
                // Wywołujemy tylko na hoście, TOU powinno zsynchronizować cele
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
            }
            
            // Wyczyść PendingRoles i ustaw flagę
            DraftManager._rolesApplied = true;
            DraftManager.PendingRoles.Clear();
            _selectRolesBlocked = false;
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
