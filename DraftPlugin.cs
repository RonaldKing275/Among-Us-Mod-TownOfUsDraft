using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;

namespace TownOfUsDraft
{
    [BepInPlugin("TownOfUsDraft", "Town Of Us Draft Mode", "1.1.0")]
    public class DraftPlugin : BasePlugin
    {
        public static DraftPlugin Instance;
        public Harmony Harmony { get; } = new Harmony("TownOfUsDraft");

        public override void Load()
        {
            Instance = this;
            
            // Inicjalizacja konfiguracji
            TouConfigAdapter.InitializeConfig(Config);
            
            // Rejestracja klasy HUD (wymagane dla IL2CPP)
            ClassInjector.RegisterTypeInIl2Cpp<DraftHud>();
            
            // Aplikowanie patchy (w tym nowego HudPatch)
            Harmony.PatchAll();
            
            Log.LogInfo("====================================");
            Log.LogInfo("Town Of Us - Draft Mode v1.0.0");
            Log.LogInfo($"Draft Mode: {(TouConfigAdapter.EnableDraftMode.Value ? "ENABLED" : "DISABLED")}");
            Log.LogInfo($"Draft Timeout: {TouConfigAdapter.DraftTimeout.Value}s");
            Log.LogInfo("====================================");
        }
    }
}