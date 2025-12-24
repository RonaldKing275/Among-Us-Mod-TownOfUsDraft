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
            DraftPlugin.Instance.Log.LogInfo("--- START DRAFTU (TIMER FIX) ---");
            _globalUsedRoles.Clear();
            
            if (!AmongUsClient.Instance.AmHost) return;

            int seed = AmongUsClient.Instance.GameId; 
            System.Random rng = new System.Random(seed);
            
            var players = PlayerControl.AllPlayerControls.ToArray().OrderBy(p => rng.Next()).ToList();
            List<RoleCategory> draftPool = BuildDraftPool(players.Count);

            TurnQueue.Clear();
            HostDraftAssignments.Clear();

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                RoleCategory cat = (i < draftPool.Count) ? draftPool[i] : RoleCategory.CrewSupport;
                HostDraftAssignments[p.PlayerId] = cat;
                TurnQueue.Enqueue(p.PlayerId);
            }

            ProcessNextTurn(rng);
        }

        // ZMIANA: Publiczne, żeby DraftHud mógł wywołać
        public static void ProcessNextTurn(System.Random rng = null)
        {
            if (rng == null) rng = new System.Random();

            if (TurnQueue.Count == 0)
            {
                DraftPlugin.Instance.Log.LogInfo("[Host] Draft zakończony!");
                OnTurnStarted(255, "", new List<string>()); 
                SendStartTurnRpc(255, "", new List<string>{"","",""});
                return;
            }

            byte nextPlayerId = TurnQueue.Dequeue();
            
            if (GetPlayerById(nextPlayerId) == null)
            {
                ProcessNextTurn(rng); 
                return;
            }

            RoleCategory cat = HostDraftAssignments[nextPlayerId];
            List<string> options = GenerateUniqueOptions(cat, rng);
            foreach(var op in options) _globalUsedRoles.Add(op);

            OnTurnStarted(nextPlayerId, FormatCategoryName(cat), options);
            SendStartTurnRpc(nextPlayerId, FormatCategoryName(cat), options);
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
                DraftHud.ActiveTurnPlayerId = 255; // Zablokuj UI
                ApplyRoleFromRpc(player, type);
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

        public static void ApplyRoleFromRpc(PlayerControl player, RoleTypes type)
        {
            try 
            {
                RoleManager.Instance.SetRole(player, type);
                DraftPlugin.Instance.Log.LogInfo($"[Draft Sync] Gracz {player.Data.PlayerName} wybrał {type}");

                // JEŚLI HOST: Włącz timer w HUDzie
                if (AmongUsClient.Instance.AmHost)
                {
                    DraftHud.HostTimerActive = true; // To bezpiecznie odpali ProcessNextTurn w Update()
                }
            } 
            catch {}
        }

        // --- Reszta metod bez zmian (Generowanie, BuildPool, RPC...) ---
        
        private static List<string> GenerateUniqueOptions(RoleCategory category, System.Random rng)
        {
            List<string> allRoles = GetAllAvailableRoleNames();
            List<string> categoryRoles = RoleCategorizer.GetRolesInCategory(category, allRoles);
            var available = categoryRoles.Where(r => !_globalUsedRoles.Contains(r)).ToList();
            if (available.Count < 3) available = categoryRoles; 
            if (available.Count == 0) available = allRoles.Where(r => !r.Contains("Impostor")).ToList();
            available = available.OrderBy(x => rng.Next()).ToList();
            return available.Take(3).ToList();
        }

        private static List<RoleCategory> BuildDraftPool(int playerCount)
        {
            List<RoleCategory> pool = new List<RoleCategory>();
            int imp = (GameOptionsManager.Instance?.CurrentGameOptions?.NumImpostors) ?? 1;
            for(int i=0; i<imp; i++) pool.Add(RoleCategory.RandomImp);

            int nk = GetSmartOption("neutral killer") + GetSmartOption("neutral killing") + GetSmartOption("neutral outliers");
            int ne = GetSmartOption("neutral evil");
            int nb = GetSmartOption("neutral benign");
            for(int i=0; i<nk; i++) pool.Add(RoleCategory.NeutralKilling);
            for(int i=0; i<ne; i++) pool.Add(RoleCategory.NeutralEvil);
            for(int i=0; i<nb; i++) pool.Add(RoleCategory.NeutralBenign);

            int cInv = GetSmartOption("investigative");
            int cPro = GetSmartOption("protective");
            int cSup = GetSmartOption("support");
            int cPow = GetSmartOption("power");
            int cKil = GetSmartOption("killing", "neutral");

            for(int i=0; i<cInv; i++) pool.Add(RoleCategory.CrewInvestigative);
            for(int i=0; i<cPro; i++) pool.Add(RoleCategory.CrewProtective);
            for(int i=0; i<cKil; i++) pool.Add(RoleCategory.CrewKilling);
            for(int i=0; i<cSup; i++) pool.Add(RoleCategory.CrewSupport);
            for(int i=0; i<cPow; i++) pool.Add(RoleCategory.CrewPower);

            int remaining = playerCount - pool.Count;
            if (remaining < 0) pool = pool.OrderBy(x => new System.Random().Next()).Take(playerCount).ToList();
            else for(int i=0; i<remaining; i++) pool.Add(GetWeightedCrewCategory(new System.Random()));

            return pool.OrderBy(x => new System.Random().Next()).ToList();
        }

        private static void SendStartTurnRpc(byte playerId, string cat, List<string> opts)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)251, SendOption.Reliable, -1);
            writer.Write(playerId); writer.Write(cat); writer.Write(opts[0]); writer.Write(opts[1]); writer.Write(opts[2]);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        private static void SendRoleSelectedRpc(byte playerId, string roleName)
        {
            int roleId = 0;
            foreach (var r in RoleManager.Instance.AllRoles) 
                if (GetRoleNameUnity(r) == roleName) { roleId = (int)((RoleBehaviour)r).Role; break; }
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)249, SendOption.Reliable, -1);
            writer.Write(playerId); writer.Write(roleId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        private static PlayerControl GetPlayerById(byte id) { foreach (var p in PlayerControl.AllPlayerControls) if (p.PlayerId == id) return p; return null; }
        private static int GetSmartOption(string k, string ex = null) {
            try {
                var field = typeof(ModdedOptionsManager).GetField("ModdedOptions", BindingFlags.Static | BindingFlags.NonPublic);
                if (field==null) return 0;
                var dict = field.GetValue(null) as IDictionary;
                foreach(var v in dict.Values) {
                    var n = v as ModdedNumberOption;
                    if(n!=null && n.Title.ToLower().Contains(k.ToLower()) && (ex==null || !n.Title.ToLower().Contains(ex.ToLower()))) return (int)n.Value;
                }
            } catch {} return 0;
        }
        private static List<string> GetAllAvailableRoleNames() {
            List<string> l = new List<string>();
            foreach(var r in RoleManager.Instance.AllRoles) {
                string n = GetRoleNameUnity(r);
                if(n!="Unknown" && !n.Contains("Vanilla") && !n.Contains("Ghost") && !n.Contains("Glitch")) l.Add(n);
            } return l;
        }
        private static string GetRoleNameUnity(object o) => (o as UnityEngine.Object)?.name ?? "null";
        private static string FormatCategoryName(RoleCategory c) => c.ToString().Replace("Random","").Replace("Crew","Crewmate ");
        private static RoleCategory GetWeightedCrewCategory(System.Random r) {
            int roll = r.Next(0, 100);
            if (roll < 20) return RoleCategory.CrewInvestigative;
            if (roll < 40) return RoleCategory.CrewKilling;
            if (roll < 60) return RoleCategory.CrewProtective;
            if (roll < 80) return RoleCategory.CrewSupport;
            return RoleCategory.CrewPower;
        }
    }
}