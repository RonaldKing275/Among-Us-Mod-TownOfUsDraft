using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using Il2CppInterop.Runtime.Injection;

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
            
            // Rejestracja klasy w IL2CPP
            ClassInjector.RegisterTypeInIl2Cpp<DraftHud>();
            
            // Patchowanie (jeśli jakieś zostało, na razie puste jest ok)
            Harmony.PatchAll();
            
            // Tworzymy NIEZNISZCZALNY obiekt kontrolny
            // To gwarantuje, że Update() w DraftHud będzie działać zawsze
            var go = new GameObject("DraftModeController");
            GameObject.DontDestroyOnLoad(go);
            go.AddComponent<DraftHud>();
            
            Log.LogInfo("Draft Mode: Controller created and registered.");
        }
    }
}