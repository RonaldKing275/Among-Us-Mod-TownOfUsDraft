using HarmonyLib;
using UnityEngine;

namespace TownOfUsDraft.Patches
{
    // Patchujemy moment, gdy gra ustawia ekran "Crewmate"
    [HarmonyPatch(typeof(IntroCutscene), "BeginCrewmate")]
    public static class ForceDraftCrewmatePatch
    {
        public static void Postfix()
        {
            DraftPlugin.Instance.Log.LogInfo("[FORCE DRAFT] Wykryto BeginCrewmate! Uruchamiam Draft...");
            DraftManager.StartDraft();
        }
    }

    // Patchujemy moment, gdy gra ustawia ekran "Impostor" (dla pewności)
    [HarmonyPatch(typeof(IntroCutscene), "BeginImpostor")]
    public static class ForceDraftImpostorPatch
    {
        public static void Postfix()
        {
            DraftPlugin.Instance.Log.LogInfo("[FORCE DRAFT] Wykryto BeginImpostor! Uruchamiam Draft...");
            DraftManager.StartDraft();
        }
    }
}