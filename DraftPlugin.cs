using BepInEx;
using BepInEx.Configuration; // Dodano brakujący using
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;

namespace TownOfUsDraft
{
    [BepInPlugin("TownOfUsDraft", "Town Of Us Draft Mode", "1.3.0")]
    [BepInDependency("mira.api")] // Poprawne ID MiraAPI
    [BepInDependency("auavengers.tou.mira")]
    public class DraftPlugin : BasePlugin, MiraAPI.PluginLoading.IMiraPlugin
    {
        public static DraftPlugin Instance;
        public Harmony Harmony { get; } = new Harmony("TownOfUsDraft");

        // Implementacja IMiraPlugin
        public string OptionsTitleText => "Draft Mode";
        public ConfigFile GetConfigFile() => Config;

        public override void Load()
        {
            Instance = this;
            
            // Inicjalizacja konfiguracji
            TouConfigAdapter.InitializeConfig(Config);
            
            // USUNIĘTO: Ręczna inicjalizacja może zakłócać automatyczny skan MiraAPI.
            // Ponieważ plugin implementuje IMiraPlugin, MiraAPI samo zainicjalizuje opcje.
            
            // Rejestracja klasy HUD (wymagane dla IL2CPP)
            ClassInjector.RegisterTypeInIl2Cpp<DraftHud>();
            
            // Aplikowanie patchy (w tym nowego HudPatch)
            Harmony.PatchAll();
            
            Log.LogInfo("====================================");
            Log.LogInfo("Town Of Us - Draft Mode v1.3.0");
            // Log.LogInfo($"Draft Mode: {(TouConfigAdapter.EnableDraftMode ? "ENABLED" : "DISABLED")}");
            // Log.LogInfo($"Draft Timeout: {TouConfigAdapter.DraftTimeout}s");
            Log.LogInfo("Options managed via Lobby Settings (MiraAPI)");
            Log.LogInfo("====================================");
        }
    }
}