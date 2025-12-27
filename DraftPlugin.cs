using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using TownOfUsDraft; // Upewnij się, że namespace się zgadza

namespace TownOfUsDraft
{
    [BepInPlugin("TownOfUsDraft", "Town Of Us Draft Mode", "1.0.0")]
    // Ważne: Informujemy BepInEx, że potrzebujemy TownOfUs
    [BepInDependency("au.avengers.townofusmira", BepInDependency.DependencyFlags.HardDependency)] 
    public class DraftPlugin : BasePlugin
    {
        public static DraftPlugin Instance;
        public Harmony Harmony { get; } = new Harmony("TownOfUsDraft");

        public override void Load()
        {
            Instance = this;
            
            // 1. Rejestracja klas w systemie IL2CPP (WYMAGANE dla MonoBehaviour)
            ClassInjector.RegisterTypeInIl2Cpp<DraftManager>();
            ClassInjector.RegisterTypeInIl2Cpp<DraftHud>();
            
            // 2. Aplikowanie patchy
            Harmony.PatchAll();
            
            // 3. Dodanie Managera do sceny (Kluczowy krok!)
            // Tworzymy pusty obiekt w grze, który będzie trzymał nasze skrypty
            var go = new GameObject("TownOfUsDraftManager");
            Object.DontDestroyOnLoad(go); // Żeby nie znikał przy zmianie sceny
            
            // Dodajemy komponenty - to uruchomi ich metody Awake() i ustawi Instance
            go.AddComponent<DraftManager>();
            go.AddComponent<DraftHud>();
            
            Log.LogInfo("Draft Mode Loaded, Registered & Patched.");
        }
    }
}