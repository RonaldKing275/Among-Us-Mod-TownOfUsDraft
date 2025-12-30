using HarmonyLib;

namespace TownOfUsDraft.Patches
{
    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
    public static class RoleGenerationPatch
    {
        [HarmonyPriority(Priority.First)]
        public static bool Prefix()
        {
            if (BlockTouGenerationPatch.BlockGeneration)
            {
                DraftPlugin.Instance.Log.LogInfo("!!! BLOKADA: SelectRoles Zatrzymane (Draft Mode) !!!");
                return false;
            }
            return true;
        }
    }
}