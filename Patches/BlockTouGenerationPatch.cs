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
        [HarmonyPriority(Priority.First)]
        public static bool Prefix()
        {
            if (!TouConfigAdapter.EnableDraftMode.Value)
            {
                // Draft wyłączony - pozwól TOU działać normalnie
                SetTouReplaceRoleManagerFlag(false);
                return true;
            }
            
            // Sprawdź czy PendingRoles są puste I Draft się jeszcze nie zaczął
            if (DraftManager.PendingRoles.Count == 0 && !_selectRolesBlocked)
            {
                // PIERWSZE WYWOŁANIE SelectRoles - zablokuj i uruchom Draft!
                _selectRolesBlocked = true;
                DraftPlugin.Instance.Log.LogInfo("╔═════════════════════════════════════════════════════════════════╗");
                DraftPlugin.Instance.Log.LogInfo("║  SelectRoles - BLOKUJĘ! Uruchamiam Draft PRZED SelectRoles!    ║");
                DraftPlugin.Instance.Log.LogInfo("╚═════════════════════════════════════════════════════════════════╝");
                
                SetTouReplaceRoleManagerFlag(true); // Zablokuj TOU
                DraftManager.StartDraft();
                return false; // Zablokuj SelectRoles!
            }
            
            // Mamy role z draftu - ZABLOKUJ TOU i vanilla, aplikujemy w Postfix
            if (DraftManager.PendingRoles.Count > 0)
            {
                DraftPlugin.Instance.Log.LogInfo("[SelectRoles Prefix] Mamy role z Draftu - blokuję TOU/Vanilla, aplikuję w Postfix");
                SetTouReplaceRoleManagerFlag(true); // Zablokuj TOU
                return false; // Zablokuj vanilla SelectRoles
            }
            
            // Fallback - pozwól TOU działać
            DraftPlugin.Instance.Log.LogInfo("[SelectRoles Prefix] Fallback - pozwalam TOU działać");
            SetTouReplaceRoleManagerFlag(false);
            return true;
        }
        
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix()
        {
            if (!TouConfigAdapter.EnableDraftMode.Value) return;
            if (DraftManager.PendingRoles.Count == 0) return;
            if (DraftManager._rolesApplied) return;
            
            DraftPlugin.Instance.Log.LogInfo("╔═════════════════════════════════════════════════════════════════╗");
            DraftPlugin.Instance.Log.LogInfo("║       SelectRoles POSTFIX - APLIKUJĘ ROLE Z DRAFTU!            ║");
            DraftPlugin.Instance.Log.LogInfo("╚═════════════════════════════════════════════════════════════════╝");
            
            // 1. Odblokuj TOU/MiraAPI żeby RpcSetRole działało dla custom ról!
            SetTouReplaceRoleManagerFlag(false);
            DraftPlugin.Instance.Log.LogInfo("    → TOU odblokowane - MiraAPI patch będzie działać");
            
            // 2. Aplikuj role z PendingRoles
            int count = 0;
            foreach (var kvp in DraftManager.PendingRoles)
            {
                var player = DraftManager.GetPlayerById(kvp.Key);
                if (player != null && !player.Data.Disconnected)
                {
                    var roleBehaviour = kvp.Value;
                    var roleName = roleBehaviour.GetType().Name.Replace("Role", "");
                    
                    DraftPlugin.Instance.Log.LogInfo($"    → {player.Data.PlayerName} = {roleName} (RoleID: {(int)roleBehaviour.Role})");
                    
                    try
                    {
                        // KLUCZOWE: RpcSetRole z custom RoleTypes
                        // MiraAPI patch na RoleManager.SetRole zrobi Instantiate i Initialize!
                        player.RpcSetRole(roleBehaviour.Role);
                        
                        DraftPlugin.Instance.Log.LogInfo($"      ✓ RpcSetRole({roleBehaviour.Role}) wysłane");
                    }
                    catch (System.Exception ex)
                    {
                        DraftPlugin.Instance.Log.LogError($"      ✗ Błąd: {ex.Message}");
                    }
                    
                    count++;
                }
            }
            
            DraftPlugin.Instance.Log.LogInfo($"    ✓ Zaaplikowano {count}/{DraftManager.PendingRoles.Count} ról");
            
            // 3. Wyczyść PendingRoles i ustaw flagę
            DraftManager._rolesApplied = true;
            DraftManager.PendingRoles.Clear();
            
            // 4. Reset flagi blokady
            _selectRolesBlocked = false;
        }
        
        // Ustawia TouRoleManagerPatches.ReplaceRoleManager przez refleksję (publiczna żeby DraftManager mógł używać)
        public static void SetTouReplaceRoleManagerFlag(bool value)
        {
            try
            {
                // Znajdź TownOfUsMira assembly
                var touAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "TownOfUsMira");
                    
                if (touAssembly == null)
                {
                    DraftPlugin.Instance.Log.LogWarning("[SetTouFlag] TownOfUsMira assembly nie znalezione!");
                    return;
                }

                // Znajdź TouRoleManagerPatches
                var patchType = touAssembly.GetTypes()
                    .FirstOrDefault(t => t.Name == "TouRoleManagerPatches");
                    
                if (patchType == null)
                {
                    DraftPlugin.Instance.Log.LogWarning("[SetTouFlag] TouRoleManagerPatches nie znaleziony!");
                    return;
                }

                // Znajdź pole ReplaceRoleManager
                var field = patchType.GetField("ReplaceRoleManager", BindingFlags.Public | BindingFlags.Static);
                
                if (field == null)
                {
                    DraftPlugin.Instance.Log.LogWarning("[SetTouFlag] ReplaceRoleManager field nie znaleziony!");
                    return;
                }

                // Ustaw wartość
                field.SetValue(null, value);
                DraftPlugin.Instance.Log.LogInfo($"[SetTouFlag] ✓ TouRoleManagerPatches.ReplaceRoleManager = {value}");
            }
            catch (System.Exception ex)
            {
                DraftPlugin.Instance.Log.LogError($"[SetTouFlag] Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}