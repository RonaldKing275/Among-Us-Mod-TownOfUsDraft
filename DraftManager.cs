using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUs.GameOptions;
using MiraAPI.Roles; 
using MiraAPI.GameOptions; 
using MiraAPI.GameOptions.OptionTypes; 
using BepInEx.Unity.IL2CPP; 
using System.Reflection;
using System.Collections; 

namespace TownOfUsDraft
{
    public static class DraftManager
    {
        public static Dictionary<byte, List<string>> DraftOptions = new Dictionary<byte, List<string>>();
        public static Dictionary<byte, RoleCategory> PlayerCategories = new Dictionary<byte, RoleCategory>();
        
        private static bool _debugLogged = false;

        public static void StartDraft()
        {
            DraftPlugin.Instance.Log.LogInfo("--- START DRAFTU (IMPOSTOR FIX) ---");

            DebugLogAllOptions();

            int seed = AmongUsClient.Instance.GameId; 
            System.Random rng = new System.Random(seed);

            var players = PlayerControl.AllPlayerControls.ToArray()
                .OrderBy(p => rng.Next())
                .ToList();

            // 1. Budowanie Puli
            List<RoleCategory> draftPool = new List<RoleCategory>();

            // A. Impostorzy
            int impostorCount = 1;
            if (GameOptionsManager.Instance != null && GameOptionsManager.Instance.CurrentGameOptions != null)
                impostorCount = GameOptionsManager.Instance.CurrentGameOptions.NumImpostors;
            
            for (int i = 0; i < impostorCount; i++) draftPool.Add(RoleCategory.RandomImp);

            // B. Neutrale (Z configu TOU)
            int nkCount = GetCustomOptionInt("Neutral Killing");
            int neCount = GetCustomOptionInt("Neutral Evil");
            int nbCount = GetCustomOptionInt("Neutral Benign");
            
            // Logujemy dla pewności
            DraftPlugin.Instance.Log.LogInfo($"[Draft Config] Config: Imp={impostorCount}, NK={nkCount}, NE={neCount}, NB={nbCount}");

            for (int i = 0; i < nkCount; i++) draftPool.Add(RoleCategory.NeutralKilling);
            for (int i = 0; i < neCount; i++) draftPool.Add(RoleCategory.NeutralEvil);
            for (int i = 0; i < nbCount; i++) draftPool.Add(RoleCategory.NeutralBenign);

            // C. Crewmates (Reszta slotów)
            int filledSlots = draftPool.Count;
            int remainingSlots = players.Count - filledSlots;

            if (remainingSlots < 0)
            {
                // Jeśli przez przypadek ustawiono więcej ról specjalnych niż jest graczy
                draftPool = draftPool.OrderBy(x => rng.Next()).Take(players.Count).ToList();
            }
            else
            {
                for (int i = 0; i < remainingSlots; i++)
                {
                    draftPool.Add(GetWeightedCrewCategory(rng));
                }
            }

            // 2. Tasowanie puli i rozdawanie
            draftPool = draftPool.OrderBy(x => rng.Next()).ToList();
            DraftOptions.Clear();
            PlayerCategories.Clear();

            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                RoleCategory assignedCategory = (i < draftPool.Count) ? draftPool[i] : RoleCategory.CrewSupport;

                PlayerCategories[player.PlayerId] = assignedCategory;
                
                // Generowanie opcji (teraz naprawione dla Impostorów)
                List<string> myOptions = GenerateOptionsForCategory(assignedCategory, rng);
                DraftOptions[player.PlayerId] = myOptions;

                DraftPlugin.Instance.Log.LogInfo($"[Draft] {player.Data.PlayerName} -> {assignedCategory} [{string.Join(", ", myOptions)}]");

                if (player.PlayerId == PlayerControl.LocalPlayer.PlayerId)
                {
                    DraftHud.MyOptions = myOptions;
                    DraftHud.CategoryTitle = FormatCategoryName(assignedCategory);
                    DraftHud.IsActive = true;
                }
            }
        }

        // --- NAPRAWIONA METODA GENEROWANIA OPCJI ---
        private static List<string> GenerateOptionsForCategory(RoleCategory category, System.Random rng)
        {
            List<string> allRoles = GetAllAvailableRoleNames();
            
            // KLUCZOWA ZMIANA:
            // Używamy RoleCategorizer dla WSZYSTKICH kategorii, w tym RandomImp.
            // Dzięki temu Morphling, Janitor, Warlock itp. będą widoczni, bo są w Twoim słowniku.
            List<string> validRoles = RoleCategorizer.GetRolesInCategory(category, allRoles);

            // Zabezpieczenie: Jeśli z jakiegoś powodu lista jest pusta (np. brak załadowanych ról z tej kategorii)
            if (validRoles.Count < 3)
            {
                if (category == RoleCategory.RandomImp)
                {
                    // Fallback dla Impostorów - szukamy po nazwie tylko awaryjnie
                    var emergencyImps = allRoles.Where(r => r.Contains("Impostor") || r.Contains("Assassin") || r.Contains("Killer")).ToList();
                    validRoles.AddRange(emergencyImps);
                    // Usuwamy duplikaty
                    validRoles = validRoles.Distinct().ToList();
                }
                else
                {
                    // Fallback dla Crew - cokolwiek co nie jest Impostorem
                    validRoles = allRoles.Where(r => !r.Contains("Impostor") && !r.Contains("Assassin")).ToList();
                }
            }

            return PickRandomRoles(validRoles, 3, rng);
        }

        // --- Reszta metod (GetCustomOptionInt, DebugLog, itp.) ---
        
        private static void DebugLogAllOptions()
        {
            if (_debugLogged) return;
            _debugLogged = true;
            try 
            {
                var field = typeof(ModdedOptionsManager).GetField("ModdedOptions", BindingFlags.Static | BindingFlags.NonPublic);
                if (field == null) return;
                var optionsDict = field.GetValue(null) as IDictionary;
                if (optionsDict == null) return;
                DraftPlugin.Instance.Log.LogInfo("--- [DEBUG] Skanowanie opcji... ---");
                // Tu można odkomentować pętlę logującą, jeśli znów będą problemy z configiem
            }
            catch {}
        }

        private static int GetCustomOptionInt(string partialTitle)
        {
            try 
            {
                var field = typeof(ModdedOptionsManager).GetField("ModdedOptions", BindingFlags.Static | BindingFlags.NonPublic);
                if (field == null) return 0;
                var optionsDict = field.GetValue(null) as IDictionary;
                if (optionsDict == null) return 0;

                foreach (var val in optionsDict.Values)
                {
                    var numberOption = val as ModdedNumberOption;
                    if (numberOption != null && numberOption.Title != null)
                    {
                        if (numberOption.Title.ToLower().Contains(partialTitle.ToLower()))
                            return (int)numberOption.Value;
                    }
                }
            }
            catch {}
            return 0;
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
                if (GetRoleNameUnity(roleBase) == targetName) return TryInvokeAssign(roleBase, player);
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
                    RoleManager.Instance.SetRole(player, roleBehaviour.Role);
                    return true;
                }
                catch {}
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