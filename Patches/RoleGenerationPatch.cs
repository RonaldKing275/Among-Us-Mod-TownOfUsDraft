using HarmonyLib;
using TownOfUs.Roles; 

namespace TownOfUsDraft.Patches
{
    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
    public static class RoleGenerationPatch
    {
        [HarmonyPriority(Priority.First)]
        public static bool Prefix()
        {
            DraftPlugin.Instance.Log.LogInfo("!!! BLOKADA TOU: SelectRoles Zatrzymane !!!");
            return false; // Stop!
        }
    }
}