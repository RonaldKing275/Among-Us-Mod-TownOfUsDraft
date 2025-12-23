using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUs.GameOptions;
using MiraAPI.Roles; 
using BepInEx.Unity.IL2CPP; 
using System.Reflection;

namespace TownOfUsDraft
{
    public static class DraftManager
    {
        public static Dictionary<byte, List<string>> DraftOptions = new Dictionary<byte, List<string>>();
        public static Dictionary<byte, RoleCategory> PlayerCategories = new Dictionary<byte, RoleCategory>();

        public static void StartDraft()
        {
            DraftPlugin.Instance.Log.LogInfo("--- START DRAFTU (SAFE MODE) ---");

            var players = PlayerControl.AllPlayerControls.ToArray().ToList();
            
            // Pobieranie liczby impostorów
            int impostorCount = 1;
            if (GameOptionsManager.Instance != null && GameOptionsManager.Instance.CurrentGameOptions != null)
                impostorCount = GameOptionsManager.Instance.CurrentGameOptions.NumImpostors;
            
            int seed = AmongUsClient.Instance.GameId; 
            System.Random rng = new System.Random(seed);
            
            players = players.OrderBy(a => rng.Next()).ToList();
            List<byte> impostorIds = new List<byte>();
            for (int i = 0; i < impostorCount; i++) impostorIds.Add(players[i].PlayerId);

            List<string> allRoles = GetAllRoleNames();
            List<RoleCategory> nonImpostorSlots = GenerateNonImpostorSlots(players.Count - impostorCount, rng);
            List<RoleCategory> impSlots = GenerateImpSlots(impostorCount);

            DraftOptions.Clear();
            PlayerCategories.Clear();

            foreach (var player in PlayerControl.AllPlayerControls) 
            {
                bool isImpostor = impostorIds.Contains(player.PlayerId);

                // [USUNIĘTO] Nie wymuszamy roli Vanilla tutaj.
                // Powodowało to crash, bo niszczyło rolę TOU w trakcie Intro.
                // ForceVanillaRole(player, isImpostor); 

                List<string> myOptions = new List<string>();
                RoleCategory assignedCategory = RoleCategory.Unknown;

                if (isImpostor)
                {
                    if (impSlots.Count > 0) { assignedCategory = impSlots[0]; impSlots.RemoveAt(0); }
                    else assignedCategory = RoleCategory.RandomImp; 
                }
                else
                {
                    if (nonImpostorSlots.Count > 0) { assignedCategory = nonImpostorSlots[0]; nonImpostorSlots.RemoveAt(0); }
                    else assignedCategory = RoleCategory.CrewSupport; 
                }

                List<string> potentialRoles = RoleCategorizer.GetRolesInCategory(assignedCategory, allRoles);
                if (potentialRoles.Count < 3)
                {
                    if (isImpostor) potentialRoles = allRoles.Where(r => IsImpostorRole(r)).ToList();
                    else potentialRoles = allRoles.Where(r => !IsImpostorRole(r)).ToList();
                }

                myOptions = PickRandomRoles(potentialRoles, 3, rng);
                DraftOptions[player.PlayerId] = myOptions;
                PlayerCategories[player.PlayerId] = assignedCategory;

                // UI DLA LOKALNEGO GRACZA
                if (player.PlayerId == PlayerControl.LocalPlayer.PlayerId)
                {
                    DraftHud.MyOptions = myOptions;
                    DraftHud.CategoryTitle = assignedCategory.ToString();
                    DraftHud.IsActive = true;
                    DraftPlugin.Instance.Log.LogInfo($"[UI] Otwieram dla: {player.Data.PlayerName}");
                }
            }
        }

        public static void OnPlayerSelectedRole(string roleName)
        {
            var player = PlayerControl.LocalPlayer;
            if (player != null) FinalizeRole(player, roleName);
        }

        public static void FinalizeRole(PlayerControl player, string roleName)
        {
            // Teraz, gdy gracz sam klika, jest bezpiecznie zamienić rolę
            if (TryAssignRoleByName(player, roleName))
                DraftPlugin.Instance.Log.LogInfo($"SUKCES! Rola {roleName} nadana.");
            else
                DraftPlugin.Instance.Log.LogError($"BŁĄD! Nie udało się nadać roli {roleName}");
        }

        // --- Helpery ---
        
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
            // Używamy bezpiecznej metody SetRole z MiraAPI
            var roleBehaviour = roleObject as RoleBehaviour;
            if (roleBehaviour != null)
            {
                try
                {
                    RoleTypes type = roleBehaviour.Role;
                    RoleManager.Instance.SetRole(player, type);
                    return true;
                }
                catch (System.Exception ex)
                {
                    DraftPlugin.Instance.Log.LogError($"SetRole Error: {ex.Message}");
                }
            }
            return false;
        }

        private static bool IsImpostorRole(string roleName)
        {
            var cat = RoleCategorizer.GetCategory(roleName);
            if (cat == RoleCategory.RandomImp) return true;
            return roleName.Contains("Impostor") || roleName.Contains("Assassin");
        }
        private static string GetRoleNameUnity(object roleObject)
        {
            if (roleObject == null) return "null";
            var unityObject = roleObject as UnityEngine.Object;
            return unityObject != null ? unityObject.name : "Unknown";
        }
        private static List<string> GetAllRoleNames()
        {
            List<string> list = new List<string>();
            foreach (var r in RoleManager.Instance.AllRoles)
            {
                string name = GetRoleNameUnity(r);
                if (name != "Unknown" && !name.Contains("Vanilla") && !name.Contains("Ghost") && !name.Contains("Glitch")) list.Add(name);
            }
            return list;
        }
        private static List<RoleCategory> GenerateNonImpostorSlots(int count, System.Random rng)
        {
            List<RoleCategory> slots = new List<RoleCategory>();
            slots.Add(RoleCategory.CrewInvestigative); slots.Add(RoleCategory.CrewKilling);
            slots.Add(RoleCategory.CrewProtective); slots.Add(RoleCategory.CrewSupport);
            slots.Add(RoleCategory.CrewPower); slots.Add(RoleCategory.NeutralEvil);
            slots.Add(RoleCategory.NeutralKilling); slots.Add(RoleCategory.NeutralBenign);
            while (slots.Count < count) slots.Add(RoleCategory.CrewSupport);
            return slots.OrderBy(a => rng.Next()).ToList();
        }
        private static List<RoleCategory> GenerateImpSlots(int count)
        {
            List<RoleCategory> s = new List<RoleCategory>();
            for(int i=0; i < count + 5; i++) s.Add(RoleCategory.RandomImp); 
            return s;
        }
        private static List<string> PickRandomRoles(List<string> source, int count, System.Random rng)
        {
            List<string> res = new List<string>();
            List<string> pool = new List<string>(source);
            for (int i = 0; i < count; i++) {
                if (pool.Count == 0) break;
                int idx = rng.Next(0, pool.Count);
                res.Add(pool[idx]);
                pool.RemoveAt(idx);
            }
            return res;
        }
    }
}