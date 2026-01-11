using HarmonyLib;
using UnityEngine;

namespace TownOfUsDraft.Patches
{
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Start))]
    public static class HudPatch
    {
        public static void Postfix(HudManager __instance)
        {
            // Sprawdzamy, czy nasz DraftHud już tam jest, żeby nie dodawać go dwa razy
            if (__instance.gameObject.GetComponent<DraftHud>() == null)
            {
                __instance.gameObject.AddComponent<DraftHud>();
            }
        }
    }
}