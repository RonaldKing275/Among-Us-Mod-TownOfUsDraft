using HarmonyLib;
using AmongUs.GameOptions;
using UnityEngine;

namespace TownOfUsDraft.Patches
{
    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginCrewmate))]
    public static class ForceDraftPatch
    {
        [HarmonyPrefix]
        public static void Prefix(IntroCutscene __instance)
        {
            // Fix CS0120: Dodano .Instance
            if (DraftManager.Instance != null && DraftManager.Instance.IsDraftActive) return;

            if (AmongUsClient.Instance.AmHost)
            {
                Debug.Log("[FORCE DRAFT] Wykryto Intro Crewmate! Uruchamiam Draft...");
                // Fix CS0120: Dodano .Instance
                if (DraftManager.Instance != null) DraftManager.Instance.StartDraft();
            }
        }
    }

    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginImpostor))]
    public static class ForceDraftImpostorPatch
    {
        [HarmonyPrefix]
        public static void Prefix(IntroCutscene __instance)
        {
            // Fix CS0120: Dodano .Instance
            if (DraftManager.Instance != null && DraftManager.Instance.IsDraftActive) return;

            if (AmongUsClient.Instance.AmHost)
            {
                Debug.Log("[FORCE DRAFT] Wykryto Intro Impostora! Uruchamiam Draft...");
                // Fix CS0120: Dodano .Instance
                if (DraftManager.Instance != null) DraftManager.Instance.StartDraft();
            }
        }
    }
}