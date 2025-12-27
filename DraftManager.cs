using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUs.GameOptions;
using MiraAPI.Roles; 
using MiraAPI.GameOptions; 
using MiraAPI.GameOptions.OptionTypes; 
using BepInEx.Unity.IL2CPP; 
using BepInEx.Unity.IL2CPP.Utils.Collections; 
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
        public static Dictionary<byte, RoleTypes> PendingRoles = new Dictionary<byte, RoleTypes>();

        // POPRAWIONY FILTR: Blokujemy nazwy obiektów Unity, a nie nazwy klas
        private static readonly HashSet<string> VanillaBannedRoles = new HashSet<string>
        {
            "Engineer", "Scientist", "Shapeshifter", "Guardian Angel", "GuardianAngel",
            "Noisemaker", "Tracker", "Phantom", "Impostor", "Crewmate", "Ghost"
        };

        public static void StartDraft()
        {
            DraftPlugin.Instance.Log.LogInfo("--- START DRAFTU (FINAL FIX) ---");
            LogAllDetectedOptions(); // Debug opcji

            PendingRoles.Clear();
            _globalUsedRoles.Clear();
            TurnQueue.Clear();
            HostDraftAssignments.Clear();

            if (!AmongUsClient.Instance.AmHost) return;

            int seed = AmongUsClient.Instance.GameId; 
            System.Random rng = new System.Random(seed);
            
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

            while (TurnQueue.Count > 0)
            {
                byte nextPlayerId = TurnQueue.Peek();
                var player = GetPlayerById(nextPlayerId);

                if (player == null || player.Data == null || player.Data.Disconnected)
                {
                    TurnQueue.Dequeue(); 
                    continue; 
                }

                TurnQueue.Dequeue(); 
                RoleCategory cat = HostDraftAssignments.ContainsKey(nextPlayerId) ? HostDraftAssignments[nextPlayerId] : RoleCategory.CrewSupport;
                
                // Generujemy opcje
                List<string> options = GenerateUniqueOptions(cat, rng);
                foreach(var op in options) _globalUsedRoles.Add(op);

                // BEZPIECZNIK: Upewnij się, że mamy 3 opcje przed wysłaniem!
                // Jeśli jest mniej, wypełniamy "pustymi", żeby RPC nie wywaliło błędu
                while (options.Count < 3) options.Add("Crewmate"); // Fallback do zwykłego

                DraftHud.TurnWatchdogTimer = 0f; 
                DraftHud.CurrentTurnPlayerId = nextPlayerId;
                DraftHud.CurrentTurnOptions = options; 

                OnTurnStarted(nextPlayerId, FormatCategoryName(cat), options);
                SendStartTurnRpc(nextPlayerId, FormatCategoryName(cat), options);
                return;
            }

            DraftPlugin.Instance.Log.LogInfo("[Host] Draft zakończony!");
            OnTurnStarted(255, "", new List<string>()); 
            SendStartTurnRpc(255, "", new List<string>{"","",""});
        }

        public static void ForceSkipTurn()
        {
            byte pid = DraftHud.CurrentTurnPlayerId;
            DraftPlugin.Instance.Log.LogWarning($"[Draft Watchdog] Timeout dla gracza {pid}. Auto-pick.");

            string autoRole = "Sheriff"; // Ostateczny fallback
            
            // Próbujemy wylosować z opcji przygotowanych dla tego gracza
            if (DraftHud.CurrentTurnOptions != null && DraftHud.CurrentTurnOptions.Count > 0)
            {
                // Wybieramy pierwszą validną rolę (nie "Crewmate" jeśli to możliwe)
                var valid = DraftHud.CurrentTurnOptions.Where(r => r != "Crewmate").ToList();
                if (valid.Count > 0) autoRole = valid[0];
                else autoRole = DraftHud.CurrentTurnOptions[0];
            }
            
            OnPlayerSelectedRole(autoRole, pid);
        }

        // --- RPC HANDLERS ---

        public static void OnTurnStarted(byte activePlayerId, string catTitle, List<string> options)
        {
            if (activePlayerId == 255)
            {
                DraftHud.IsDraftActive = false;
                if (DraftHud.Instance != null)
                    DraftHud.Instance.StartCoroutine(FinalizeDraftRoutine().WrapToIl2Cpp());
                return;
            }

            DraftHud.ActiveTurnPlayerId = activePlayerId;
            DraftHud.CategoryTitle = catTitle;
            DraftHud.MyOptions = options;
            DraftHud.IsDraftActive = true;
        }

        private static IEnumerator FinalizeDraftRoutine()
        {
            Time.timeScale = 1f;
            if (PlayerControl.LocalPlayer != null) PlayerControl.LocalPlayer.moveable = true;
            yield return new WaitForSeconds(0.5f);

            foreach (var kvp in PendingRoles)
            {
                var player = GetPlayerById(kvp.Key);
                if (player != null)
                {
                    try { RoleManager.Instance.SetRole(player, kvp.Value); } catch {}
                }
            }
        }

        public static void OnPlayerSelectedRole(string roleName, byte forcedPlayerId = 255)
        {
            byte targetId = (forcedPlayerId == 255) ? PlayerControl.LocalPlayer.PlayerId : forcedPlayerId;
            
            var player = GetPlayerById(targetId);
            if (player != null)
            {
                RoleTypes type = RoleTypes.Crewmate;
                bool found = false;
                foreach (var r in RoleManager.Instance.AllRoles) 
                {
                    var uObj = r as UnityEngine.Object;
                    if (uObj != null && uObj.name == roleName) { 
                        type = ((RoleBehaviour)r).Role; 
                        found = true;
                        break; 
                    }
                }
                
                // Jeśli nie znaleziono roli po nazwie, szukaj czegokolwiek z TOU (awaryjnie)
                if (!found)
                {
                     foreach (var r in RoleManager.Instance.AllRoles) {
                        var uObj = r as UnityEngine.Object;
                        if (uObj != null && !VanillaBannedRoles.Contains(uObj.name)) {
                             type = ((RoleBehaviour)r).Role; break;
                        }
                     }
                }

                if (AmongUsClient.Instance.AmHost || targetId == PlayerControl.LocalPlayer.PlayerId)
                    SendRoleSelectedRpc(targetId, roleName);

                if (targetId == PlayerControl.LocalPlayer.PlayerId) DraftHud.ActiveTurnPlayerId = 255; 
                
                if (!PendingRoles.ContainsKey(targetId)) PendingRoles.Add(targetId, type);
                else PendingRoles[targetId] = type;
                
                if (AmongUsClient.Instance.AmHost)
                {
                    DraftHud.HostTimerActive = true; 
                    DraftHud.TurnWatchdogTimer = 0f;
                }
            }
        }

        public static void ApplyRoleFromRpc(PlayerControl player, RoleTypes type)
        {
            if (!PendingRoles.ContainsKey(player.PlayerId)) PendingRoles.Add(player.PlayerId, type);
            else PendingRoles[player.PlayerId] = type;

            if (AmongUsClient.Instance.AmHost)
            {
                DraftHud.HostTimerActive = true; 
                DraftHud.TurnWatchdogTimer = 0f;
            }
        }

        // --- CONFIG & GENERATION ---

        private static List<RoleCategory> BuildDraftPool(int playerCount)
        {
            List<RoleCategory> pool = new List<RoleCategory>();
            
            // Impostor (Default 1)
            int imp = (GameOptionsManager.Instance?.CurrentGameOptions?.NumImpostors) ?? 1;
            for(int i=0; i<imp; i++) pool.Add(RoleCategory.RandomImp);

            // Pobieranie Configu (z Fallbackiem!)
            // Jeśli GetSmartOption zwróci 0, używamy wartości domyślnych (np. 1) żebyś mógł grać
            int nk = GetSmartOption("Neutral Killing") + GetSmartOption("Neutral Killer");
            if (nk == 0 && playerCount > 4) nk = 1; // Fallback: Zawsze 1 NK przy >4 graczach

            int ne = GetSmartOption("Neutral Evil");
            if (ne == 0 && playerCount > 6) ne = 1; // Fallback: 1 NE przy >6 graczach

            int nb = GetSmartOption("Neutral Benign");
            int no = GetSmartOption("Neutral Outlier") + GetSmartOption("Neutral Chaos");

            int cInv = GetSmartOption("Investigative"); if (cInv == 0) cInv = 2; // Default 2
            int cPro = GetSmartOption("Protective");    if (cPro == 0) cPro = 1; // Default 1
            int cSup = GetSmartOption("Support");       if (cSup == 0) cSup = 1; // Default 1
            int cPow = GetSmartOption("Power");
            int cKil = GetSmartOption("Killing", "Neutral");

            DraftPlugin.Instance.Log.LogInfo($"[Pool] Imp={imp}, NK={nk}, NE={ne}, Inv={cInv}, Pro={cPro}");

            for(int i=0; i<nk+no; i++) pool.Add(RoleCategory.NeutralKilling);
            for(int i=0; i<ne; i++) pool.Add(RoleCategory.NeutralEvil);
            for(int i=0; i<nb; i++) pool.Add(RoleCategory.NeutralBenign);

            for(int i=0; i<cInv; i++) pool.Add(RoleCategory.CrewInvestigative);
            for(int i=0; i<cPro; i++) pool.Add(RoleCategory.CrewProtective);
            for(int i=0; i<cKil; i++) pool.Add(RoleCategory.CrewKilling);
            for(int i=0; i<cSup; i++) pool.Add(RoleCategory.CrewSupport);
            for(int i=0; i<cPow; i++) pool.Add(RoleCategory.CrewPower);

            // Wypełnianie / Docinanie
            int remaining = playerCount - pool.Count;
            if (remaining < 0) 
            {
                var impTicket = pool[0]; pool.RemoveAt(0);
                pool = pool.OrderBy(x => new System.Random().Next()).Take(playerCount - 1).ToList();
                pool.Insert(0, impTicket);
            }
            else 
            {
                for(int i=0; i<remaining; i++) pool.Add(GetWeightedCrewCategory(new System.Random()));
            }

            return pool.OrderBy(x => new System.Random().Next()).ToList();
        }

        private static int GetSmartOption(string keyword, string exclude = null)
        {
            try 
            {
                var touAssembly = System.AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "TownOfUsMira");
                if (touAssembly == null) return 0;
                var customOptType = touAssembly.GetType("TownOfUs.CustomOption");
                var optionsField = customOptType?.GetField("options", BindingFlags.Public | BindingFlags.Static);
                var list = optionsField?.GetValue(null) as IList;
                if (list == null) return 0;

                foreach (var opt in list)
                {
                    var nameProp = opt.GetType().GetField("Name");
                    if (nameProp == null) continue;
                    string title = nameProp.GetValue(opt) as string;
                    if (string.IsNullOrEmpty(title)) continue;

                    if (title.ToLower().Contains(keyword.ToLower()) && (exclude == null || !title.ToLower().Contains(exclude.ToLower())))
                    {
                        var valField = opt.GetType().GetField("value") ?? opt.GetType().GetField("Selection");
                        if (valField != null) return (int)System.Convert.ToSingle(valField.GetValue(opt));
                    }
                }
            }
            catch {}
            return 0;
        }

        // --- HELPERY ---
        private static List<string> GenerateUniqueOptions(RoleCategory category, System.Random rng)
        {
            List<string> allRoles = GetAllAvailableRoleNames();
            List<string> categoryRoles = RoleCategorizer.GetRolesInCategory(category, allRoles);
            var available = categoryRoles.Where(r => !_globalUsedRoles.Contains(r)).ToList();

            if (available.Count < 3) available = categoryRoles; // Zezwól na powtórki jeśli mało
            if (available.Count == 0) available = allRoles.Where(r => !r.Contains("Impostor")).ToList(); // Awaryjnie

            return available.OrderBy(x => rng.Next()).Take(3).ToList();
        }

        private static List<string> GetAllAvailableRoleNames()
        {
            List<string> list = new List<string>();
            foreach (var r in RoleManager.Instance.AllRoles) 
            { 
                var unityObj = r as UnityEngine.Object;
                if (unityObj == null) continue;
                string name = unityObj.name;
                
                if (name == "Unknown" || name.Contains("Vanilla") || name.Contains("Ghost")) continue;
                if (VanillaBannedRoles.Contains(name)) continue; 
                list.Add(name); 
            }
            return list;
        }

        private static PlayerControl GetPlayerById(byte id) { foreach (var p in PlayerControl.AllPlayerControls) if (p.PlayerId == id) return p; return null; }
        private static string FormatCategoryName(RoleCategory c) => c.ToString().Replace("Random","").Replace("Crew","Crewmate ");
        private static RoleCategory GetWeightedCrewCategory(System.Random r) { return RoleCategory.CrewSupport; } 
        private static void SendStartTurnRpc(byte playerId, string cat, List<string> opts) {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)251, SendOption.Reliable, -1);
            writer.Write(playerId); writer.Write(cat); 
            // BEZPIECZNY ZAPIS - zapobiega crashowi przy pustej liście
            writer.Write(opts.Count > 0 ? opts[0] : "Crewmate"); 
            writer.Write(opts.Count > 1 ? opts[1] : "Crewmate"); 
            writer.Write(opts.Count > 2 ? opts[2] : "Crewmate");
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        private static void SendRoleSelectedRpc(byte playerId, string roleName) {
            int roleId = 0; foreach (var r in RoleManager.Instance.AllRoles) { var uObj = r as UnityEngine.Object; if (uObj != null && uObj.name == roleName) { roleId = (int)((RoleBehaviour)r).Role; break; } }
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)249, SendOption.Reliable, -1);
            writer.Write(playerId); writer.Write(roleId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void OnRandomRoleSelected() { if (DraftHud.MyOptions.Count > 0) OnPlayerSelectedRole(DraftHud.MyOptions[new System.Random().Next(DraftHud.MyOptions.Count)]); }
        
        private static void LogAllDetectedOptions()
        {
            DraftPlugin.Instance.Log.LogInfo("--- [DEBUG] PEŁNA LISTA OPCJI TOU ---");
            try 
            {
                var touAssembly = System.AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "TownOfUsMira");
                if (touAssembly != null)
                {
                    var customOptType = touAssembly.GetType("TownOfUs.CustomOption");
                    if (customOptType != null)
                    {
                        var optionsField = customOptType.GetField("options", BindingFlags.Public | BindingFlags.Static);
                        if (optionsField != null)
                        {
                            var list = optionsField.GetValue(null) as IList;
                            if (list != null)
                            {
                                foreach (var opt in list)
                                {
                                    var nameProp = opt.GetType().GetField("Name");
                                    var valField = opt.GetType().GetField("value") ?? opt.GetType().GetField("Selection");
                                    
                                    if (nameProp != null && valField != null)
                                    {
                                        string title = nameProp.GetValue(opt) as string;
                                        var val = valField.GetValue(opt);
                                        // Wypisz wszystko co ma wartość > 0 lub zawiera "Neutral"
                                        if (title.Contains("Neutral") || title.Contains("Max") || title.Contains("Count"))
                                        {
                                            DraftPlugin.Instance.Log.LogInfo($" -> OPCJA: '{title}' = {val}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception e) { DraftPlugin.Instance.Log.LogError($"Błąd skanowania: {e}"); }
            DraftPlugin.Instance.Log.LogInfo("---------------------------------------");
        }
    }
}