using HarmonyLib;
using UnityEngine;
using AmongUs.GameOptions;

namespace TownOfUsDraft.Patches
{
    public static class ForceDraftPatch
    {
        // 1. NEUTRALIZACJA (Naprawiona)
        // Wpinamy się w "ShowRole" (zanim gra zdecyduje, jaki ekran pokazać).
        // Ustawiamy gracza na Crewmate, więc gra sama wybierze "BeginCrewmate" (niebieski ekran).
        [HarmonyPatch(typeof(IntroCutscene), "ShowRole")]
        public static class NeutralizeShowRolePatch
        {
            public static void Prefix()
            {
                if (PlayerControl.LocalPlayer != null)
                {
                    DraftPlugin.Instance.Log.LogInfo("[Intro Fix] ShowRole startuje -> Wymuszam Vanilla Crewmate (Blue Screen).");
                    RoleManager.Instance.SetRole(PlayerControl.LocalPlayer, RoleTypes.Crewmate);
                }
            }
        }

        // 2. START DRAFTU
        // Skoro wyżej wymusiliśmy Crewmate'a, gra na 100% wywoła BeginCrewmate.
        // Tutaj odpalamy nasze UI.
        [HarmonyPatch(typeof(IntroCutscene), "BeginCrewmate")]
        public static class ForceDraftCrewmatePatch
        {
            public static void Postfix()
            {
                DraftPlugin.Instance.Log.LogInfo("[FORCE DRAFT] Wykryto Intro! Uruchamiam Draft...");
                DraftManager.StartDraft();
            }
        }

        // Zostawiamy to tylko awaryjnie, gdyby coś poszło nie tak z neutralizacją.
        [HarmonyPatch(typeof(IntroCutscene), "BeginImpostor")]
        public static class ForceDraftImpostorPatch
        {
            public static void Postfix()
            {
                DraftPlugin.Instance.Log.LogInfo("[FORCE DRAFT] Wykryto Intro (Imp)! Uruchamiam Draft...");
                DraftManager.StartDraft();
            }
        }
    }
}