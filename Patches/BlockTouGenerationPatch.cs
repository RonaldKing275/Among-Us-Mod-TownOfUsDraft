using HarmonyLib;
using AmongUs.GameOptions;
using System.Reflection;
using System.Linq;

namespace TownOfUsDraft.Patches
{
    // NAJLEPSZE ROZWIĄZANIE: Używamy oficjalnej flagi TOU-Mira "ReplaceRoleManager"
    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
    public static class DraftRoleOverridePatch
    {
        public static bool BlockGeneration = true;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)] // Wykonaj przed TOU-Mira (Priority.Last)
        public static bool Prefix()
        {
            if (BlockGeneration)
            {
                DraftPlugin.Instance.Log.LogInfo("=== DRAFT MODE [SelectRoles]: BLOKUJĘ całkowicie i ustawiam wszystkich na Crewmate ===");
                
                // 1. ZABLOKUJ TOU
                SetTouReplaceRoleManagerFlag(true);
                DraftPlugin.Instance.Log.LogInfo("    → TouRoleManagerPatches.ReplaceRoleManager = true (TOU zablokowane)");
                
                // 2. RĘCZNIE USTAW WSZYSTKICH NA CREWMATE (żeby nikt nie dostał IsImpostor=true!)
                int count = 0;
                foreach (var player in PlayerControl.AllPlayerControls)
                {
                    if (player != null && !player.Data.Disconnected)
                    {
                        player.RpcSetRole(RoleTypes.Crewmate);
                        count++;
                        DraftPlugin.Instance.Log.LogInfo($"    → {player.Data.PlayerName} = Crewmate (czysty stan)");
                    }
                }
                DraftPlugin.Instance.Log.LogInfo($"    → Ustawiono {count} graczy na Crewmate");
                
                // 3. ZABLOKUJ Vanillę całkowicie! (return false)
                // To zapobiega losowaniu Impostorów przez Vanillę
                DraftPlugin.Instance.Log.LogInfo("    → Vanilla SelectRoles ZABLOKOWANE (return false)");
                return false;  // ← KLUCZOWA ZMIANA!
            }
            
            // Normalny tryb - pozwól TOU działać
            SetTouReplaceRoleManagerFlag(false);
            return true;
        }
        
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)] // Wykonaj po wszystkich innych
        public static void Postfix()
        {
            if (BlockGeneration)
            {
                DraftPlugin.Instance.Log.LogInfo("=== DRAFT MODE: SelectRoles zakończony, TOU pozostaje zablokowane do końca Draftu ===");
                // NIE przywracamy flagi tutaj - zrobi to ApplyDraftRolesAfterDraft() po zakończeniu Draftu!
            }
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