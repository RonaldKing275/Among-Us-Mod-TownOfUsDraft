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

        private static readonly HashSet<string> VanillaBannedRoles = new HashSet<string>
        {
            "Engineer", "Scientist", "Shapeshifter", "Guardian Angel", "GuardianAngel",
            "Noisemaker", "Tracker", "Phantom", "Impostor", "Crewmate", "Ghost"
        };

        public static void StartDraft()
        {
            DraftPlugin.Instance.Log.LogInfo("--- START DRAFTU (COMPILATION FIX) ---");
            LogAllDetectedOptions(); 

            // Włączamy blokadę standardowego rozdawania ról
            TownOfUsDraft.Patches.BlockTouGenerationPatch.BlockGeneration = true;

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

            List<RoleCategory> draftPool = BuildDraftPool(players.Count, rng);

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
                
                List<string> options = GenerateUniqueOptions(cat, rng);
                foreach(var op in options) _globalUsedRoles.Add(op);

                while (options.Count < 3) options.Add("Sheriff");

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

            string autoRole = "Sheriff"; 
            if (DraftHud.CurrentTurnOptions != null && DraftHud.CurrentTurnOptions.Count > 0)
            {
                var valid = DraftHud.CurrentTurnOptions.Where(r => r != "Crewmate" && r != "Sheriff").ToList();
                if (valid.Count > 0) autoRole = valid[0];
                else autoRole = DraftHud.CurrentTurnOptions[0];
            }
            OnPlayerSelectedRole(autoRole, pid);
        }

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
            DraftPlugin.Instance.Log.LogInfo("[Draft] Czekam na stabilizację gry...");
            
            Time.timeScale = 1f;
            if (PlayerControl.LocalPlayer != null) PlayerControl.LocalPlayer.moveable = true;

            TownOfUsDraft.Patches.BlockTouGenerationPatch.BlockGeneration = false;

            yield return new WaitForSeconds(1.5f);
            
            int timeout = 0;
            while ((HudManager.Instance == null || HudManager.Instance.KillButton == null) && timeout < 30)
            {
                yield return new WaitForSeconds(0.1f);
                timeout++;
            }

            DraftPlugin.Instance.Log.LogInfo($"[Draft] Aplikowanie {PendingRoles.Count} ról.");

            foreach (var kvp in PendingRoles)
            {
                var player = GetPlayerById(kvp.Key);
                if (player != null && !player.Data.Disconnected && !player.Data.IsDead)
                {
                    try { RoleManager.Instance.SetRole(player, kvp.Value); } 
                    catch (System.Exception e) { DraftPlugin.Instance.Log.LogError($"[Draft Apply Error] {e.Message}"); }
                }
            }
        }

        // --- POPRAWIONY SKANER MIRA API (Bez błędu CS0019) ---
        private static int GetMiraOption(string keyword)
        {
            try 
            {
                var touAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "TownOfUsMira");
                
                if (touAssembly == null) return 0;

                var roleOptsType = touAssembly.GetType("TownOfUs.Options.RoleOptions");
                if (roleOptsType == null) return 0;

                var fields = roleOptsType.GetFields(BindingFlags.Public | BindingFlags.Static);
                
                foreach (var f in fields)
                {
                    var val = f.GetValue(null);
                    if (val == null) continue;
                    var type = val.GetType();

                    // --- 1. POBIERANIE TYTUŁU (Naprawione) ---
                    string title = null;
                    var propTitle = type.GetProperty("Title");
                    if (propTitle != null) title = propTitle.GetValue(val) as string;
                    else 
                    {
                        var fieldTitle = type.GetField("Title");
                        if (fieldTitle != null) title = fieldTitle.GetValue(val) as string;
                    }

                    if (string.IsNullOrEmpty(title)) continue;

                    if (title.ToLower().Contains(keyword.ToLower()))
                    {
                        // --- 2. POBIERANIE WARTOŚCI (Naprawione) ---
                        object rawValue = null;
                        var propValue = type.GetProperty("Value");
                        if (propValue != null) rawValue = propValue.GetValue(val);
                        else
                        {
                            var fieldValue = type.GetField("Value");
                            if (fieldValue != null) rawValue = fieldValue.GetValue(val);
                        }

                        if (rawValue != null)
                        {
                            return (int)System.Convert.ToSingle(rawValue);
                        }
                    }
                }
            } 
            catch {}
            return 0;
        }

        private static List<RoleCategory> BuildDraftPool(int playerCount, System.Random rng)
        {
            List<RoleCategory> pool = new List<RoleCategory>();
            
            int imp = (GameOptionsManager.Instance?.CurrentGameOptions?.NumImpostors) ?? 1;
            for(int i=0; i<imp; i++) pool.Add(RoleCategory.RandomImp);

            int nk = GetMiraOption("Neutral Killing") + GetMiraOption("Neutral Killer");
            int ne = GetMiraOption("Neutral Evil");
            int nb = GetMiraOption("Neutral Benign");
            int no = GetMiraOption("Neutral Outlier") + GetMiraOption("Neutral Chaos");
            int rndN = GetMiraOption("Random Neutral");

            if (nk==0 && ne==0 && nb==0 && playerCount >= 4) {
                 DraftPlugin.Instance.Log.LogWarning("[Config] Nie znaleziono Neutrali. Ustawiam 1 NK (Fallback).");
                 nk = 1; 
            }

            int cInv = GetMiraOption("Investigative"); if (cInv == 0) cInv = 2;
            int cPro = GetMiraOption("Protective"); 
            int cSup = GetMiraOption("Support");       
            int cPow = GetMiraOption("Power");
            int cKil = GetMiraOption("Killing"); 

            DraftPlugin.Instance.Log.LogInfo($"[Pool] Imp={imp}, NK={nk}, NE={ne}, NB={nb}");

            for(int i=0; i<nk; i++) pool.Add(RoleCategory.NeutralKilling);
            for(int i=0; i<ne; i++) pool.Add(RoleCategory.NeutralEvil);
            for(int i=0; i<nb; i++) pool.Add(RoleCategory.NeutralBenign);
            for(int i=0; i<rndN; i++) pool.Add(GetRandomNeutralCategory(rng));

            for(int i=0; i<cInv; i++) pool.Add(RoleCategory.CrewInvestigative);
            for(int i=0; i<cPro; i++) pool.Add(RoleCategory.CrewProtective);
            for(int i=0; i<cKil; i++) pool.Add(RoleCategory.CrewKilling);
            for(int i=0; i<cSup; i++) pool.Add(RoleCategory.CrewSupport);
            for(int i=0; i<cPow; i++) pool.Add(RoleCategory.CrewPower);

            int remaining = playerCount - pool.Count;
            if (remaining < 0) 
            {
                var impTicket = pool[0]; pool.RemoveAt(0); 
                pool = pool.OrderBy(x => rng.Next()).Take(playerCount - 1).ToList();
                pool.Insert(0, impTicket);
            }
            else 
            {
                for(int i=0; i<remaining; i++) pool.Add(GetWeightedCrewCategory(rng));
            }

            return pool.OrderBy(x => rng.Next()).ToList();
        }

        // --- Helpery ---
        public static void OnPlayerSelectedRole(string roleName, byte forcedPlayerId = 255)
        {
            byte targetId = (forcedPlayerId == 255) ? PlayerControl.LocalPlayer.PlayerId : forcedPlayerId;
            var player = GetPlayerById(targetId);
            if (player != null)
            {
                RoleTypes type = RoleTypes.Crewmate;
                bool found = false;
                foreach (var r in RoleManager.Instance.AllRoles) {
                    var uObj = r as UnityEngine.Object;
                    if (uObj != null && uObj.name == roleName) { type = ((RoleBehaviour)r).Role; found = true; break; }
                }
                
                if (!found) {
                     foreach (var r in RoleManager.Instance.AllRoles) {
                        var uObj = r as UnityEngine.Object;
                        if (uObj != null && !VanillaBannedRoles.Contains(uObj.name)) { type = ((RoleBehaviour)r).Role; break; }
                     }
                }

                if (AmongUsClient.Instance.AmHost || targetId == PlayerControl.LocalPlayer.PlayerId)
                    SendRoleSelectedRpc(targetId, roleName);

                if (targetId == PlayerControl.LocalPlayer.PlayerId) DraftHud.ActiveTurnPlayerId = 255; 
                
                if (!PendingRoles.ContainsKey(targetId)) PendingRoles.Add(targetId, type);
                else PendingRoles[targetId] = type;
                
                if (AmongUsClient.Instance.AmHost) {
                    DraftHud.HostTimerActive = true; 
                    DraftHud.TurnWatchdogTimer = 0f;
                }
            }
        }
        
        public static void ApplyRoleFromRpc(PlayerControl player, RoleTypes type) {
            if (!PendingRoles.ContainsKey(player.PlayerId)) PendingRoles.Add(player.PlayerId, type);
            else PendingRoles[player.PlayerId] = type;
            if (AmongUsClient.Instance.AmHost) {
                DraftHud.HostTimerActive = true; 
                DraftHud.TurnWatchdogTimer = 0f;
            }
        }

        private static RoleCategory GetRandomNeutralCategory(System.Random r) {
            int roll = r.Next(0, 3);
            if (roll == 0) return RoleCategory.NeutralKilling;
            if (roll == 1) return RoleCategory.NeutralBenign;
            return RoleCategory.NeutralEvil;
        }
        private static RoleCategory GetWeightedCrewCategory(System.Random r) {
            int roll = r.Next(0, 100);
            if (roll < 20) return RoleCategory.CrewInvestigative;
            if (roll < 40) return RoleCategory.CrewKilling;
            if (roll < 60) return RoleCategory.CrewProtective;
            if (roll < 80) return RoleCategory.CrewSupport;
            return RoleCategory.CrewPower;
        } 
        private static List<string> GenerateUniqueOptions(RoleCategory category, System.Random rng) {
            List<string> allRoles = GetAllAvailableRoleNames();
            List<string> categoryRoles = RoleCategorizer.GetRolesInCategory(category, allRoles);
            var available = categoryRoles.Where(r => !_globalUsedRoles.Contains(r)).ToList();
            if (available.Count < 3) available = categoryRoles; 
            if (available.Count == 0) available = allRoles.Where(r => !r.Contains("Impostor")).ToList();
            return available.OrderBy(x => rng.Next()).Take(3).ToList();
        }
        private static List<string> GetAllAvailableRoleNames() {
            List<string> list = new List<string>();
            foreach (var r in RoleManager.Instance.AllRoles) { 
                var unityObj = r as UnityEngine.Object;
                if (unityObj == null) continue;
                if (!VanillaBannedRoles.Contains(unityObj.name) && !unityObj.name.Contains("Vanilla") && unityObj.name != "Unknown") 
                    list.Add(unityObj.name); 
            } return list;
        }
        private static PlayerControl GetPlayerById(byte id) { foreach (var p in PlayerControl.AllPlayerControls) if (p.PlayerId == id) return p; return null; }
        private static string FormatCategoryName(RoleCategory c) => c.ToString().Replace("Random","").Replace("Crew","Crewmate ");
        private static void SendStartTurnRpc(byte playerId, string cat, List<string> opts) {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)251, SendOption.Reliable, -1);
            writer.Write(playerId); writer.Write(cat); 
            writer.Write(opts.Count > 0 ? opts[0] : "Sheriff"); 
            writer.Write(opts.Count > 1 ? opts[1] : "Sheriff"); 
            writer.Write(opts.Count > 2 ? opts[2] : "Sheriff");
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
            DraftPlugin.Instance.Log.LogInfo("--- [DEBUG] SKANOWANIE OPCJI ---");
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try {
                    var type = asm.GetType("TownOfUs.CustomOption");
                    if (type != null)
                    {
                        DraftPlugin.Instance.Log.LogInfo($"Znaleziono CustomOption w: {asm.GetName().Name}");
                        var field = type.GetField("options", BindingFlags.Public | BindingFlags.Static);
                        var list = field?.GetValue(null) as IList;
                        if (list != null) {
                            foreach(var opt in list) {
                                var nameProp = opt.GetType().GetField("Name");
                                var valField = opt.GetType().GetField("value") ?? opt.GetType().GetField("Selection");
                                if (nameProp != null && valField != null) {
                                    string t = nameProp.GetValue(opt) as string;
                                    var v = valField.GetValue(opt);
                                    if (t.Contains("Neutral") || t.Contains("Count")) 
                                        DraftPlugin.Instance.Log.LogInfo($" -> {t}: {v}");
                                }
                            }
                        }
                    }
                } catch {}
            }
            DraftPlugin.Instance.Log.LogInfo("--------------------------------");
        }
    }
}