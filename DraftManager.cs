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
using Hazel; 

namespace TownOfUsDraft
{
    public static class DraftManager
    {
        public static Queue<byte> TurnQueue = new Queue<byte>();
        public static Dictionary<byte, RoleCategory> HostDraftAssignments = new Dictionary<byte, RoleCategory>();
        private static HashSet<string> _globalUsedRoles = new HashSet<string>();

        public static void StartDraft()
        {
            DraftPlugin.Instance.Log.LogInfo("--- START DRAFTU (WATCHDOG EDITION) ---");
            _globalUsedRoles.Clear();
            
            if (!AmongUsClient.Instance.AmHost) return;

            // Zabezpieczenie przed starymi danymi
            TurnQueue.Clear();
            HostDraftAssignments.Clear();

            int seed = AmongUsClient.Instance.GameId; 
            System.Random rng = new System.Random(seed);
            
            // Pobieramy tylko aktywnych graczy
            var players = PlayerControl.AllPlayerControls.ToArray()
                .Where(p => p != null && !p.Data.Disconnected)
                .OrderBy(p => rng.Next())
                .ToList();

            List<RoleCategory> draftPool = BuildDraftPool(players.Count);

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                RoleCategory cat = (i < draftPool.Count) ? draftPool[i] : RoleCategory.CrewSupport;
                HostDraftAssignments[p.PlayerId] = cat;
                TurnQueue.Enqueue(p.PlayerId);
            }

            ProcessNextTurn(rng);
        }

        public static void ProcessNextTurn(System.Random rng = null)
        {
            if (rng == null) rng = new System.Random();

            // Pętla sprawdzająca, czy następny gracz w ogóle istnieje
            while (TurnQueue.Count > 0)
            {
                byte nextPlayerId = TurnQueue.Peek(); // Podglądamy, ale jeszcze nie usuwamy
                var player = GetPlayerById(nextPlayerId);

                // Jeśli gracza nie ma lub wyszedł -> usuń z kolejki i sprawdź następnego
                if (player == null || player.Data == null || player.Data.Disconnected)
                {
                    DraftPlugin.Instance.Log.LogWarning($"[Draft] Pomijanie gracza ID {nextPlayerId} (Disconnected/Null)");
                    TurnQueue.Dequeue(); 
                    continue; 
                }

                // Znaleziono poprawnego gracza -> Rozpoczynamy turę
                TurnQueue.Dequeue(); // Teraz usuwamy
                RoleCategory cat = HostDraftAssignments.ContainsKey(nextPlayerId) ? HostDraftAssignments[nextPlayerId] : RoleCategory.CrewSupport;
                
                List<string> options = GenerateUniqueOptions(cat, rng);
                foreach(var op in options) _globalUsedRoles.Add(op);

                // Resetujemy Watchdog Timer w HUDzie
                DraftHud.TurnWatchdogTimer = 0f; 
                DraftHud.CurrentTurnPlayerId = nextPlayerId; // Informacja dla Watchdoga

                OnTurnStarted(nextPlayerId, FormatCategoryName(cat), options);
                SendStartTurnRpc(nextPlayerId, FormatCategoryName(cat), options);
                return;
            }

            // Jeśli pętla się skończyła, znaczy że kolejka pusta -> Koniec
            DraftPlugin.Instance.Log.LogInfo("[Host] Draft zakończony!");
            OnTurnStarted(255, "", new List<string>()); 
            SendStartTurnRpc(255, "", new List<string>{"","",""});
        }

        // Metoda awaryjna wywoływana przez Watchdoga
        public static void ForceSkipTurn()
        {
            DraftPlugin.Instance.Log.LogWarning("[Draft Watchdog] WYMUSZONO POMINIĘCIE TURY (TIMEOUT)!");
            // Jeśli gracz zlagował, po prostu idziemy do następnego. 
            // W idealnym świecie losowalibyśmy mu rolę, ale tutaj priorytetem jest odblokowanie gry.
            // Gracz dostanie rolę domyślną (Crewmate) albo losową jeśli gra sama nada.
            
            // Po prostu odpal następną turę
            ProcessNextTurn();
        }

        public static void OnTurnStarted(byte activePlayerId, string catTitle, List<string> options)
        {
            if (activePlayerId == 255)
            {
                DraftHud.IsDraftActive = false;
                return;
            }

            DraftHud.ActiveTurnPlayerId = activePlayerId;
            DraftHud.CategoryTitle = catTitle;
            DraftHud.MyOptions = options;
            DraftHud.IsDraftActive = true;
        }

        public static void OnPlayerSelectedRole(string roleName)
        {
            var player = PlayerControl.LocalPlayer;
            if (player != null)
            {
                RoleTypes type = RoleTypes.Crewmate;
                foreach (var r in RoleManager.Instance.AllRoles) 
                    if (GetRoleNameUnity(r) == roleName) { type = ((RoleBehaviour)r).Role; break; }

                SendRoleSelectedRpc(player.PlayerId, roleName);
                DraftHud.ActiveTurnPlayerId = 255; 
                
                // Lokalnie też aplikujemy (z try-catch!)
                ApplyRoleSafe(player, type);
            }
        }
        
        public static void OnRandomRoleSelected()
        {
            if (DraftHud.MyOptions.Count > 0)
            {
                string randomPick = DraftHud.MyOptions[new System.Random().Next(DraftHud.MyOptions.Count)];
                OnPlayerSelectedRole(randomPick);
            }
        }

        // Wrapper do bezpiecznego aplikowania roli
        public static void ApplyRoleFromRpc(PlayerControl player, RoleTypes type)
        {
            ApplyRoleSafe(player, type);
        }

        private static void ApplyRoleSafe(PlayerControl player, RoleTypes type)
        {
            try 
            {
                RoleManager.Instance.SetRole(player, type);
                DraftPlugin.Instance.Log.LogInfo($"[Draft Sync] Gracz {player.Data.PlayerName} wybrał {type}");
            } 
            catch (System.Exception e)
            {
                // Nawet jak wywali błąd HUDa, logujemy to i IDZIEMY DALEJ
                DraftPlugin.Instance.Log.LogError($"[Draft ERROR] Błąd przy SetRole dla {player.Data.PlayerName}: {e.Message}");
            }
            finally
            {
                // Zawsze, nawet po błędzie, planujemy następną turę (jeśli jesteśmy hostem)
                if (AmongUsClient.Instance.AmHost)
                {
                    DraftHud.HostTimerActive = true; 
                    DraftHud.TurnWatchdogTimer = 0f; // Resetujemy strażnika, bo ruch został wykonany
                }
            }
        }

        // --- Helpery ---
        // (Reszta funkcji bez zmian - GenerateUniqueOptions, BuildDraftPool, SendRpc itp.)
        // Skopiuj je z poprzedniej wersji, bo są dobre. Poniżej tylko skróty dla kompilacji:

        private static List<string> GenerateUniqueOptions(RoleCategory category, System.Random rng) {
             List<string> allRoles = GetAllAvailableRoleNames();
             List<string> categoryRoles = RoleCategorizer.GetRolesInCategory(category, allRoles);
             var available = categoryRoles.Where(r => !_globalUsedRoles.Contains(r)).ToList();
             if (available.Count < 3) available = categoryRoles; 
             if (available.Count == 0) available = allRoles.Where(r => !r.Contains("Impostor")).ToList();
             return available.OrderBy(x => rng.Next()).Take(3).ToList();
        }

        private static List<RoleCategory> BuildDraftPool(int playerCount) {
            List<RoleCategory> pool = new List<RoleCategory>();
            int imp = (GameOptionsManager.Instance?.CurrentGameOptions?.NumImpostors) ?? 1;
            for(int i=0; i<imp; i++) pool.Add(RoleCategory.RandomImp);
            // ... (reszta logiki poola z poprzedniego pliku) ...
            // Dla uproszczenia tutaj wklejam skrót, ale użyj swojej pełnej wersji z GetSmartOption!
            int remaining = playerCount - pool.Count;
            if (remaining < 0) pool = pool.OrderBy(x => new System.Random().Next()).Take(playerCount).ToList();
            else for(int i=0; i<remaining; i++) pool.Add(GetWeightedCrewCategory(new System.Random()));
            return pool.OrderBy(x => new System.Random().Next()).ToList();
        }

        // Pamiętaj o wklejeniu pełnej metody BuildDraftPool i GetSmartOption z poprzedniego pliku!
        private static int GetSmartOption(string k, string ex = null) { try { var f = typeof(ModdedOptionsManager).GetField("ModdedOptions", BindingFlags.Static | BindingFlags.NonPublic); if (f==null) return 0; var d = f.GetValue(null) as IDictionary; foreach(var v in d.Values) { var n = v as ModdedNumberOption; if(n!=null && n.Title.ToLower().Contains(k.ToLower()) && (ex==null || !n.Title.ToLower().Contains(ex.ToLower()))) return (int)n.Value; } } catch {} return 0; }
        private static List<string> GetAllAvailableRoleNames() { List<string> l = new List<string>(); foreach(var r in RoleManager.Instance.AllRoles) { string n = (r as UnityEngine.Object)?.name ?? "null"; if(n!="Unknown" && !n.Contains("Vanilla") && !n.Contains("Ghost") && !n.Contains("Glitch")) l.Add(n); } return l; }
        private static string GetRoleNameUnity(object o) => (o as UnityEngine.Object)?.name ?? "null";
        private static string FormatCategoryName(RoleCategory c) => c.ToString().Replace("Random","").Replace("Crew","Crewmate ");
        private static RoleCategory GetWeightedCrewCategory(System.Random r) { return RoleCategory.CrewSupport; } // Skrót
        private static PlayerControl GetPlayerById(byte id) { foreach (var p in PlayerControl.AllPlayerControls) if (p.PlayerId == id) return p; return null; }
        
        private static void SendStartTurnRpc(byte playerId, string cat, List<string> opts) {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)251, SendOption.Reliable, -1);
            writer.Write(playerId); writer.Write(cat); writer.Write(opts[0]); writer.Write(opts[1]); writer.Write(opts[2]);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        private static void SendRoleSelectedRpc(byte playerId, string roleName) {
            int roleId = 0; foreach (var r in RoleManager.Instance.AllRoles) if (GetRoleNameUnity(r) == roleName) { roleId = (int)((RoleBehaviour)r).Role; break; }
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)249, SendOption.Reliable, -1);
            writer.Write(playerId); writer.Write(roleId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }
}