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
                SetTouReplaceRoleManagerFlag(true); // Zablokuj TOU
                DraftManager.StartDraft();
                return false; // Zablokuj SelectRoles!
            }
            
            // Mamy role z draftu - ZABLOKUJ TOU i vanilla, aplikujemy w Postfix
            if (DraftManager.PendingRoles.Count > 0)
            {
                SetTouReplaceRoleManagerFlag(true); // Zablokuj TOU
                return false; // Zablokuj vanilla SelectRoles
            }
            
            // Fallback - pozwól TOU działać
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
            
            // Odblokuj TOU/MiraAPI żeby RpcSetRole działało dla custom ról
            SetTouReplaceRoleManagerFlag(false);
            
            // Aplikuj role z PendingRoles
            foreach (var kvp in DraftManager.PendingRoles)
            {
                var player = DraftManager.GetPlayerById(kvp.Key);
                if (player != null && !player.Data.Disconnected)
                {
                    player.RpcSetRole(kvp.Value.Role);
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
