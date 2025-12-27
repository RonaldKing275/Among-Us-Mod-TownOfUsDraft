using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using TownOfUsDraft;

namespace TownOfUsDraft
{
    [BepInPlugin("TownOfUsDraft", "Town Of Us Draft Mode", "1.0.0")]
    public class DraftPlugin : BasePlugin
    {
        public static DraftPlugin Instance;
        public Harmony Harmony { get; } = new Harmony("TownOfUsDraft");

        public override void Load()
        {
            Instance = this;
            
            // Rejestracja HUD (Wymagane)
            ClassInjector.RegisterTypeInIl2Cpp<DraftHud>();
            
            Harmony.PatchAll();
            
            var go = new GameObject("TownOfUsDraftHUD");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<DraftHud>();
            
            Log.LogInfo("Draft Mode (Static) Loaded.");
        }
    }
}