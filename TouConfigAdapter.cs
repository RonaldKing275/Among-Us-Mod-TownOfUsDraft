using AmongUs.GameOptions;
using System.Reflection;
using UnityEngine;
using MiraAPI.GameOptions;
using BepInEx.Configuration;
// using TownOfUsDraft.Options; - Usunięto, bo DraftOptions jest teraz w głównym namespace

namespace TownOfUsDraft
{
    public static class TouConfigAdapter
    {
        // Config entries - inicjalizowane przez DraftPlugin
        public static ConfigEntry<int> CrewSupport;
        public static ConfigEntry<int> CrewProtective;
        public static ConfigEntry<int> CrewInvestigative;
        public static ConfigEntry<int> CrewKilling;
        public static ConfigEntry<int> CrewPower;
        public static ConfigEntry<int> NeutralKilling;
        public static ConfigEntry<int> NeutralEvil;
        public static ConfigEntry<int> NeutralBenign;
        public static ConfigEntry<int> RandomNeutral;

        // Przekierowanie do MiraAPI Options
        public static bool EnableDraftMode => OptionGroupSingleton<DraftOptions>.Instance.EnableDraftMode.Value;
        public static float DraftTimeout => OptionGroupSingleton<DraftOptions>.Instance.DraftTimeout.Value;

        public static void InitializeConfig(ConfigFile config)
        {
            // Opcje DraftMode i DraftTimeout są teraz w GameSettings (MiraAPI)

            CrewSupport = config.Bind("Roles", "CrewSupport", 2, 
                "Liczba ról Crew Support w drafcie");

            CrewProtective = config.Bind("Roles", "CrewProtective", 1, 
                "Liczba ról Crew Protective w drafcie");

            CrewInvestigative = config.Bind("Roles", "CrewInvestigative", 2, 
                "Liczba ról Crew Investigative w drafcie");

            CrewKilling = config.Bind("Roles", "CrewKilling", 1, 
                "Liczba ról Crew Killing w drafcie");

            CrewPower = config.Bind("Roles", "CrewPower", 0, 
                "Liczba ról Crew Power w drafcie");

            NeutralKilling = config.Bind("Roles", "NeutralKilling", 1, 
                "Liczba ról Neutral Killing w drafcie");

            NeutralEvil = config.Bind("Roles", "NeutralEvil", 1, 
                "Liczba ról Neutral Evil w drafcie");

            NeutralBenign = config.Bind("Roles", "NeutralBenign", 0, 
                "Liczba ról Neutral Benign w drafcie");

            RandomNeutral = config.Bind("Roles", "RandomNeutral", 0, 
                "Liczba losowych ról Neutral w drafcie");

            DraftPlugin.Instance.Log.LogInfo("[Config] Konfiguracja Draft Mode załadowana.");
        }

        // Ta metoda zwraca wartość z configa
        public static int GetRoleCount(string optionName, int defaultValue = 0)
        {
            switch (optionName.ToLower())
            {
                case "support": return CrewSupport?.Value ?? defaultValue;
                case "protective": return CrewProtective?.Value ?? defaultValue;
                case "investigative": return CrewInvestigative?.Value ?? defaultValue;
                case "killing": return CrewKilling?.Value ?? defaultValue;
                case "power": return CrewPower?.Value ?? defaultValue;
                case "neutralkilling":
                case "neutral killing": return NeutralKilling?.Value ?? defaultValue;
                case "neutralevil":
                case "neutral evil": return NeutralEvil?.Value ?? defaultValue;
                case "neutralbenign":
                case "neutral benign": return NeutralBenign?.Value ?? defaultValue;
                case "randomneutral":
                case "random neutral": return RandomNeutral?.Value ?? defaultValue;
                default: return defaultValue;
            }
        }
    }
}
