using HarmonyLib;
using AmongUs.GameOptions;
using System.Reflection;
using System.Linq;

namespace TownOfUsDraft.Patches
{
    [HarmonyPatch]
    public static class BlockTouGenerationPatch
    {
        public static bool BlockGeneration = true;

        // Metoda pomocnicza do bezpiecznego szukania celu patcha
        public static MethodBase TargetMethod()
        {
            var touAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "TownOfUsMira");

            if (touAssembly == null) return null; // Bezpieczne wyjście

            var type = touAssembly.GetType("TownOfUs.Patches.Roles.LogicRoleSelectionNormalPatch");
            if (type == null) return null;

            // Szukamy Postfixa
            return type.GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }

        // Przygotowanie patcha - sprawdza czy cel istnieje
        public static bool Prepare()
        {
            return TargetMethod() != null;
        }

        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (BlockGeneration)
            {
                DraftPlugin.Instance.Log.LogInfo("[BlockPatch] Zatrzymano generator TOU (Draft Mode).");
                return false; // Stop! Nie rozdawaj ról.
            }
            return true;
        }
    }
}