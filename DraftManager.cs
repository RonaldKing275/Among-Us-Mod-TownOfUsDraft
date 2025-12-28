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
        public static Dictionary<byte, DraftCategory> HostDraftAssignments = new Dictionary<byte, DraftCategory>();
        private static HashSet<string> _globalUsedRoles = new HashSet<string>();
        public static Dictionary<byte, ICustomRole> PendingRoles = new Dictionary<byte, ICustomRole>();

        private static readonly HashSet<string> VanillaBannedRoles = new HashSet<string>
        {
            "Engineer", "Scientist", "Shapeshifter", "Guardian Angel", "GuardianAngel",
            "Noisemaker", "Tracker", "Phantom", "Impostor", "Crewmate", "Ghost"
        };

        public static void StartDraft()
        {
            DraftPlugin.Instance.Log.LogInfo("--- START DRAFTU (FINAL FIX) ---");
            LogAllDetectedOptions(); 

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

            if (players.Count == 0) return;

            List<DraftCategory> draftPool = BuildDraftPool(players.Count, rng);

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                DraftCategory cat = (i < draftPool.Count) ? draftPool[i] : DraftCategory.Support;
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
                
                DraftCategory cat = HostDraftAssignments.ContainsKey(nextPlayerId) ? HostDraftAssignments[nextPlayerId] : DraftCategory.Support;
                
                List<string> options = GenerateUniqueOptions(cat, rng);
                foreach(var op in options) _globalUsedRoles.Add(op);

                while (options.Count < 3) options.Add("Sheriff");

                // Ustawiamy zmienne w HUD (dla Hosta)
                DraftHud.TurnWatchdogTimer = 0f; 
                DraftHud.CurrentTurnPlayerId = nextPlayerId;
                DraftHud.CurrentTurnOptions = options; 

                string catName = FormatCategoryName(cat);
                OnTurnStarted(nextPlayerId, catName, options);
                SendStartTurnRpc(nextPlayerId, catName, options);
                return;
            }

            DraftPlugin.Instance.Log.LogInfo("[Host] Draft zakończony!");
            OnTurnStarted(255, "", new List<string>()); 
            SendStartTurnRpc(255, "", new List<string>{"","",""});
        }

        public static void ForceSkipTurn()
        {
            byte pid = DraftHud.CurrentTurnPlayerId;
            DraftPlugin.Instance.Log.LogWarning($"[Draft Watchdog] Timeout dla gracza {pid}.");

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
            DraftPlugin.Instance.Log.LogInfo("[Draft] Aplikowanie ról...");
            Time.timeScale = 1f;
            if (PlayerControl.LocalPlayer != null) PlayerControl.LocalPlayer.moveable = true;
            TownOfUsDraft.Patches.BlockTouGenerationPatch.BlockGeneration = false;

            yield return new WaitForSeconds(1.0f);

            foreach (var kvp in PendingRoles)
            {
                var player = GetPlayerById(kvp.Key);
                if (player != null && !player.Data.Disconnected)
                {
                    try { RoleManager.Instance.SetRole(player, kvp.Value); } 
                    catch {}
                }
            }
        }

        private static int GetMiraOption(string keyword)
        {
            try 
            {
                var touAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "TownOfUsMira");
                if (touAssembly == null) return 0;

                var allTypes = touAssembly.GetTypes().Where(t => t.Name.Contains("Option") || t.Name.Contains("Config"));

                foreach(var type in allTypes)
                {
                    var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
                    foreach (var f in fields)
                    {
                        try {
                            var val = f.GetValue(null);
                            if (val == null) continue;

                            string title = null;
                            var tProp = val.GetType().GetProperty("Title");
                            var tField = val.GetType().GetField("Title");
                            
                            if (tProp != null) title = tProp.GetValue(val) as string;
                            else if (tField != null) title = tField.GetValue(val) as string;

                            if (!string.IsNullOrEmpty(title) && 
                                title.Replace(" ", "").ToLower().Contains(keyword.Replace(" ", "").ToLower()))
                            {
                                object rawValue = null;
                                var vProp = val.GetType().GetProperty("Value");
                                var vField = val.GetType().GetField("Value");

                                if (vProp != null) rawValue = vProp.GetValue(val);
                                else if (vField != null) rawValue = vField.GetValue(val);

                                if (rawValue != null) return (int)System.Convert.ToSingle(rawValue);
                            }
                        } catch {}
                    }
                }
            } 
            catch {}
            return 0;
        }

        private static List<DraftCategory> BuildDraftPool(int playerCount, System.Random rng)
        {
            List<DraftCategory> pool = new List<DraftCategory>();
            
            int imp = (GameOptionsManager.Instance?.CurrentGameOptions?.NumImpostors) ?? 1;
            for(int i=0; i<imp; i++) pool.Add(DraftCategory.Impostor);

            int nk = GetMiraOption("Neutral Killing") + GetMiraOption("Neutral Killer");
            int ne = GetMiraOption("Neutral Evil");
            int nb = GetMiraOption("Neutral Benign");
            int rndN = GetMiraOption("Random Neutral");

            if (nk==0 && ne==0 && nb==0 && rndN==0 && playerCount >= 4) {
                 DraftPlugin.Instance.Log.LogWarning("[Config] 0 Neutrali. Ustawiam 1 NK.");
                 nk = 1; 
            } else {
                 DraftPlugin.Instance.Log.LogInfo($"[Config] Wykryto: NK={nk}, NE={ne}, NB={nb}, Rnd={rndN}");
            }

            int cInv = GetMiraOption("Investigative"); if (cInv == 0) cInv = 2;
            int cPro = GetMiraOption("Protective"); 
            int cSup = GetMiraOption("Support");       
            int cPow = GetMiraOption("Power");
            int cKil = GetMiraOption("Killing"); 

            for(int i=0; i<nk; i++) pool.Add(DraftCategory.NeutralKilling);
            for(int i=0; i<ne; i++) pool.Add(DraftCategory.NeutralEvil);
            for(int i=0; i<nb; i++) pool.Add(DraftCategory.NeutralBenign);
            for(int i=0; i<rndN; i++) pool.Add(GetRandomNeutralCategory(rng));

            for(int i=0; i<cInv; i++) pool.Add(DraftCategory.Investigative);
            for(int i=0; i<cPro; i++) pool.Add(DraftCategory.Protective);
            for(int i=0; i<cKil; i++) pool.Add(DraftCategory.Killing);
            for(int i=0; i<cSup; i++) pool.Add(DraftCategory.Support);
            for(int i=0; i<cPow; i++) pool.Add(DraftCategory.Power);

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

        private static List<string> GetAllAvailableRoleNames() {
            List<string> list = new List<string>();
            foreach (var r in RoleManager.Instance.AllRoles) { 
                var unityObj = r as UnityEngine.Object;
                if (unityObj == null) continue;
                
                if (VanillaBannedRoles.Contains(unityObj.name) || unityObj.name.Contains("Vanilla") || unityObj.name == "Unknown") 
                    continue;

                string rawName = r.ToString(); 
                if (r is ICustomRole) {
                    if (rawName.Contains(".")) rawName = rawName.Split('.').Last();
                } 
                else if (unityObj.name.Contains(".")) {
                    rawName = unityObj.name.Split('.').Last();
                }
                
                list.Add(rawName);
            } 
            return list;
        }

        private static DraftCategory GetRandomNeutralCategory(System.Random r) {
            int roll = r.Next(0, 3);
            if (roll == 0) return DraftCategory.NeutralKilling;
            if (roll == 1) return DraftCategory.NeutralBenign;
            return DraftCategory.NeutralEvil;
        }
        private static DraftCategory GetWeightedCrewCategory(System.Random r) {
            int roll = r.Next(0, 100);
            if (roll < 20) return DraftCategory.Investigative;
            if (roll < 40) return DraftCategory.Killing;
            if (roll < 60) return DraftCategory.Protective;
            if (roll < 80) return DraftCategory.Support;
            return DraftCategory.Power;
        } 
        
        private static List<string> GenerateUniqueOptions(DraftCategory category, System.Random rng) {
            List<string> allRoles = GetAllAvailableRoleNames();
            List<string> categoryRoles = RoleCategorizer.GetRandomRoles(category, 50); 
            
            var available = categoryRoles.Where(r => !_globalUsedRoles.Contains(r)).Distinct().ToList();
            if (available.Count < 3) available = categoryRoles.Distinct().ToList(); 
            if (available.Count == 0) available = new List<string>{"Crewmate", "Sheriff", "Engineer"};
            
            return available.OrderBy(x => rng.Next()).Take(3).ToList();
        }

        private static PlayerControl GetPlayerById(byte id) { foreach (var p in PlayerControl.AllPlayerControls) if (p.PlayerId == id) return p; return null; }
        
        private static string FormatCategoryName(DraftCategory c) 
        {
            string s = c.ToString();
            if (s == "Impostor") return "Impostor";
            if (s.StartsWith("Neutral")) return s.Replace("Neutral", "Neutral ");
            if (s == "Crewmate") return "Crewmate";
            return "Crewmate " + s;
        }

        private static void SendStartTurnRpc(byte playerId, string cat, List<string> opts) {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)251, SendOption.Reliable, -1);
            writer.Write(playerId); 
            writer.Write(cat); 
            
            // Ważne: Stary DraftNetworkPatch czyta 3 stringi, więc wysyłamy 3 stringi
            // (Jeśli Twój patch czyta listę z połączonego stringa, to tu trzeba by zmienić)
            // Ale patrząc na Twoje ostatnie przesłane pliki (DraftNetworkPatch.cs snippet), on czyta op1, op2, op3.
            
            writer.Write(opts.Count > 0 ? opts[0] : "Sheriff");
            writer.Write(opts.Count > 1 ? opts[1] : "Sheriff");
            writer.Write(opts.Count > 2 ? opts[2] : "Sheriff");
            
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void OnPlayerSelectedRole(ICustomRole role, byte forcedPlayerId = 255)
        {
            // Jeśli z jakiegoś powodu rola jest nullem (błąd), przerywamy
            if (role == null) 
            {
                DraftPlugin.Instance.Log.LogError("Błąd: Wybrano pustą rolę!");
                return;
            }

            byte targetId = (forcedPlayerId == 255) ? PlayerControl.LocalPlayer.PlayerId : forcedPlayerId;
            
            // Logika sieciowa (RPC)
            // Wysyłamy klucz (role.Name), żeby inni gracze wiedzieli co wybraliśmy
            if (AmongUsClient.Instance.AmHost || targetId == PlayerControl.LocalPlayer.PlayerId)
            {
                SendRoleSelectedRpc(targetId, role.Name); 
            }

            // Resetowanie stanu tury dla lokalnego gracza
            if (targetId == PlayerControl.LocalPlayer.PlayerId) 
                DraftHud.ActiveTurnPlayerId = 255; 
            
            // --- KLUCZOWA POPRAWKA ---
            // Nie szukamy w RoleTypes. Zapisujemy obiekt ICustomRole bezpośrednio.
            if (!PendingRoles.ContainsKey(targetId)) 
                PendingRoles.Add(targetId, role);
            else 
                PendingRoles[targetId] = role;
            
            DraftPlugin.Instance.Log.LogInfo($"Gracz {targetId} wybrał: {role.Name}");

            // Obsługa timera hosta
            if (AmongUsClient.Instance.AmHost) {
                DraftHud.HostTimerActive = true; 
                DraftHud.TurnWatchdogTimer = 0f;
            }
        }

        private static void SendRoleSelectedRpc(byte playerId, string roleName) {
            int roleId = 0; 
            foreach (var r in RoleManager.Instance.AllRoles) { 
                string rName = r.ToString();
                if (rName.Contains(".")) rName = rName.Split('.').Last();
                if (rName == roleName) { roleId = (int)((RoleBehaviour)r).Role; break; } 
            }
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)249, SendOption.Reliable, -1);
            writer.Write(playerId); writer.Write(roleId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void OnRandomRoleSelected() { 
            if (DraftHud.MyOptions.Count > 0) 
                OnPlayerSelectedRole(DraftHud.MyOptions[new System.Random().Next(DraftHud.MyOptions.Count)]); 
        }

        public static void ApplyRoleFromRpc(PlayerControl player, RoleTypes type) {
            if (!PendingRoles.ContainsKey(player.PlayerId)) PendingRoles.Add(player.PlayerId, type);
            else PendingRoles[player.PlayerId] = type;
            if (AmongUsClient.Instance.AmHost) {
                DraftHud.HostTimerActive = true; 
                DraftHud.TurnWatchdogTimer = 0f;
            }
        }
        
        private static void LogAllDetectedOptions()
        {
            DraftPlugin.Instance.Log.LogInfo("--- [DEBUG] SKANOWANIE OPCJI ---");
            try {
                var touAssembly = System.AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "TownOfUsMira");
                if (touAssembly != null) {
                    var allTypes = touAssembly.GetTypes().Where(t => t.Name.Contains("Option") || t.Name.Contains("Config"));
                    foreach(var type in allTypes) {
                        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
                        foreach (var f in fields) {
                            try {
                                var val = f.GetValue(null);
                                if (val != null) {
                                    var tProp = val.GetType().GetProperty("Title");
                                    var tField = val.GetType().GetField("Title");
                                    string t = null;
                                    if(tProp != null) t = tProp.GetValue(val) as string;
                                    else if(tField != null) t = tField.GetValue(val) as string;

                                    if (!string.IsNullOrEmpty(t) && (t.Contains("Neutral") || t.Contains("Count")))
                                        DraftPlugin.Instance.Log.LogInfo($" -> {t}");
                                }
                            } catch {}
                        }
                    }
                }
            } catch {}
            DraftPlugin.Instance.Log.LogInfo("--------------------------------");

            
        }

        public static void AssignDraftedRoles()
        {
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player.Data.Disconnected || player.Data.IsDead) continue;

                if (PendingRoles.ContainsKey(player.PlayerId))
                {
                    ICustomRole roleToAssign = PendingRoles[player.PlayerId];
                    
                    if (roleToAssign != null)
                    {
                        // TO JEST KLUCZOWE: Używamy metody .Assign() z MiraAPI
                        roleToAssign.Assign(player);
                        DraftPlugin.Instance.Log.LogInfo($"[Draft] Przypisano {player.Data.PlayerName} -> {roleToAssign.Name}");
                    }
                }
                else
                {
                    // Opcjonalnie: Upewnij się, że gracze bez roli są Crewmate
                    // CustomRoleManager.Instance.GetRole("Crewmate")?.Assign(player);
                }
            }
        }
    }
}