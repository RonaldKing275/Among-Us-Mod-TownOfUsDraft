using HarmonyLib;
using UnityEngine;

namespace TownOfUsDraft.Patches
{
    // Używamy stringa "ShowRole", żeby oszukać kompilator
    // Harmony znajdzie tę metodę w trakcie gry
    [HarmonyPatch(typeof(IntroCutscene), "ShowRole")]
    public static class ForceDraftPatch
    {
        // Używamy Prefix, żeby odpalić Draft ZANIM gra pokaże rolę
        public static void Prefix()
        {
            DraftPlugin.Instance.Log.LogInfo("[FORCE DRAFT] Wykryto ShowRole! Uruchamiam Draft...");
            
            // Uruchomienie logiki
            DraftManager.StartDraft();
        }
    }
}