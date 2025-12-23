using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUs.GameOptions;
using MiraAPI.Roles; 
using MiraAPI.GameOptions; 
using MiraAPI.GameOptions.OptionTypes; // Ważne dla ModdedNumberOption
using BepInEx.Unity.IL2CPP; 
using System.Reflection;
using System.Collections; // Potrzebne do obsługi IDictionary

namespace TownOfUsDraft
{
    public static class DraftManager
    {
        public static Dictionary<byte, List<string>> DraftOptions = new Dictionary<byte, List<string>>();
        public static Dictionary<byte, RoleCategory> PlayerCategories = new Dictionary<byte, RoleCategory>();

        public static void StartDraft()
        {
            DraftPlugin.Instance.Log.LogInfo("--- START DRAFTU (CONFIG SYNC FIX) ---");

            int seed = AmongUsClient.Instance.GameId; 
            System.Random rng = new System.Random(seed);

            // 1. Pobierz i przetasuj graczy
            var players = PlayerControl.AllPlayerControls.ToArray()
                .OrderBy(p => rng.Next())
                .ToList();

            DraftPlugin.Instance.Log.LogInfo($"[Draft] Graczy: {players.Count}. Budowanie puli z Configu...");

            // 2. Budowanie Puli Kategorii
            List<RoleCategory> draftPool = new List<RoleCategory>();

            // A. Impostorzy (Vanilla Settings)
            int impostorCount = 1;
            if (GameOptionsManager.Instance != null && GameOptionsManager.Instance.CurrentGameOptions != null)
                impostorCount = GameOptionsManager.Instance.CurrentGameOptions.NumImpostors;
            
            for (int i = 0; i < impostorCount; i++) draftPool.Add(RoleCategory.RandomImp);

            // B. Neutrale (TOU Custom Settings przez Reflection)
            int nkCount = GetCustomOptionInt("Neutral Killing");
            int neCount = GetCustomOptionInt("Neutral Evil");
            int nbCount = GetCustomOptionInt("Neutral Benign");
            
            // Możesz dodać logowanie, żeby sprawdzić czy dobrze czyta
            DraftPlugin.Instance.Log.LogInfo($"[Draft Config] NK: {nkCount}, NE: {neCount}, NB: {nbCount}");

            for (int i = 0; i < nkCount; i++) draftPool.Add(RoleCategory.NeutralKilling);
            for (int i = 0; i < neCount; i++) draftPool.Add(RoleCategory.NeutralEvil);
            for (int i = 0; i < nbCount; i++) draftPool.Add(RoleCategory.NeutralBenign);

            // C. Crewmates (Reszta slotów)
            int filledSlots = draftPool.Count;
            int remainingSlots = players.Count - filledSlots;

            if (remainingSlots < 0)
            {
                DraftPlugin.Instance.Log.LogWarning("[Draft] UWAGA: Więcej ról specjalnych niż graczy! Ucinam nadmiar.");
                draftPool = draftPool.OrderBy(x => rng.Next()).Take(players.Count).ToList();
            }
            else
            {
                for (int i = 0; i < remainingSlots; i++)
                {
                    draftPool.Add(GetWeightedCrewCategory(rng));
                }
            }

            // 3. Tasowanie puli
            draftPool = draftPool.OrderBy(x => rng.Next()).ToList();

            DraftOptions.Clear();
            PlayerCategories.Clear();

            // 4. Rozdawanie
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                RoleCategory assignedCategory = RoleCategory.CrewSupport; 

                if (i < draftPool.Count)
                {
                    assignedCategory = draftPool[i];
                }

                PlayerCategories[player.PlayerId] = assignedCategory;

                List<string> myOptions = GenerateOptionsForCategory(assignedCategory, rng);
                DraftOptions[player.PlayerId] = myOptions;

                DraftPlugin.Instance.Log.LogInfo($"[Draft] {player.Data.PlayerName} -> {assignedCategory}");

                if (player.PlayerId == PlayerControl.LocalPlayer.PlayerId)
                {
                    DraftHud.MyOptions = myOptions;
                    DraftHud.CategoryTitle = FormatCategoryName(assignedCategory);
                    DraftHud.IsActive = true;
                }
            }
        }

        // --- NAPRAWIONA METODA POBIERANIA OPCJI (REFLECTION) ---
        private static int GetCustomOptionInt(string partialTitle)
        {
            try 
            {
                // 1. Dobieramy się do ukrytego pola 'ModdedOptions' w ModdedOptionsManager
                var field = typeof(ModdedOptionsManager).GetField("ModdedOptions", BindingFlags.Static | BindingFlags.NonPublic);
                if (field == null) return 0;

                // 2. Pobieramy wartość pola (Dictionary<uint, IModdedOption>)
                var optionsDict = field.GetValue(null) as IDictionary;
                if (optionsDict == null) return 0;

                // 3. Przeszukujemy słownik
                foreach (var val in optionsDict.Values)
                {
                    // Rzutujemy na ModdedNumberOption (to są suwaki liczbowe)
                    var numberOption = val as ModdedNumberOption;
                    
                    // Sprawdzamy czy tytuł zawiera np. "Neutral Killing"
                    if (numberOption != null && numberOption.Title != null && numberOption.Title.Contains(partialTitle))
                    {
                        return (int)numberOption.Value;
                    }
                }
            }
            catch (System.Exception e)
            {
                DraftPlugin.Instance.Log.LogWarning($"Błąd odczytu opcji '{partialTitle}': {e.Message}");
            }

            return 0; // Jeśli nie znaleziono lub błąd
        }

        // --- Reszta metod bez zmian ---

        private static List<string> GenerateOptionsForCategory(RoleCategory category, System.Random rng)
        {
            List<string> allRoles = GetAllAvailableRoleNames();
            List<string> validRoles = new List<string>();

            if (category == RoleCategory.RandomImp)
            {
                validRoles = allRoles.Where(r => r.Contains("Impostor") || r.Contains("Assassin") || r.Contains("Killer")).ToList();
            }
            else
            {
                validRoles = RoleCategorizer.GetRolesInCategory(category, allRoles);
                if (validRoles.Count < 3)
                {
                     validRoles = allRoles.Where(r => !r.Contains("Impostor")).ToList();
                }
            }

            return PickRandomRoles(validRoles, 3, rng);
        }

        private static RoleCategory GetWeightedCrewCategory(System.Random rng)
        {
            int roll = rng.Next(0, 100);
            if (roll < 20) return RoleCategory.CrewInvestigative;
            if (roll < 40) return RoleCategory.CrewKilling;
            if (roll < 60) return RoleCategory.CrewProtective;
            if (roll < 80) return RoleCategory.CrewSupport;
            return RoleCategory.CrewPower;
        }

        public static void OnPlayerSelectedRole(string roleName)
        {
            var player = PlayerControl.LocalPlayer;
            if (player != null) FinalizeRole(player, roleName);
        }

        public static void FinalizeRole(PlayerControl player, string roleName)
        {
            if (TryAssignRoleByName(player, roleName))
                DraftPlugin.Instance.Log.LogInfo($"SUKCES! Rola {roleName} nadana.");
            else
                DraftPlugin.Instance.Log.LogError($"BŁĄD! Nie udało się nadać roli {roleName}");
        }

        private static bool TryAssignRoleByName(PlayerControl player, string targetName)
        {
            foreach (var roleBase in RoleManager.Instance.AllRoles)
            {
                if (GetRoleNameUnity(roleBase) == targetName)
                {
                    return TryInvokeAssign(roleBase, player);
                }
            }
            return false;
        }

        private static bool TryInvokeAssign(object roleObject, PlayerControl player)
        {
            var roleBehaviour = roleObject as RoleBehaviour;
            if (roleBehaviour != null)
            {
                try
                {
                    RoleTypes type = roleBehaviour.Role;
                    RoleManager.Instance.SetRole(player, type);
                    return true;
                }
                catch (System.Exception ex) { DraftPlugin.Instance.Log.LogError($"SetRole Error: {ex.Message}"); }
            }
            return false;
        }

        private static string GetRoleNameUnity(object roleObject)
        {
            if (roleObject == null) return "null";
            var unityObject = roleObject as UnityEngine.Object;
            return unityObject != null ? unityObject.name : "Unknown";
        }

        private static List<string> GetAllAvailableRoleNames()
        {
            List<string> list = new List<string>();
            foreach (var r in RoleManager.Instance.AllRoles)
            {
                string name = GetRoleNameUnity(r);
                if (name != "Unknown" && !name.Contains("Vanilla") && !name.Contains("Ghost") && !name.Contains("Glitch")) 
                    list.Add(name);
            }
            return list;
        }

        private static List<string> PickRandomRoles(List<string> source, int count, System.Random rng)
        {
            List<string> res = new List<string>();
            List<string> pool = new List<string>(source).OrderBy(x => rng.Next()).ToList();
            for (int i = 0; i < count; i++) {
                if (pool.Count == 0) break;
                res.Add(pool[0]);
                pool.RemoveAt(0);
            }
            return res;
        }
        
        private static string FormatCategoryName(RoleCategory cat)
        {
            return cat.ToString().Replace("Random", "").Replace("Crew", "Crewmate ");
        }
    }
}