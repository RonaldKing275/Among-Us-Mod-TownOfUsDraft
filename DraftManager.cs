using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUs.GameOptions;
using MiraAPI.Roles; 
using MiraAPI.GameOptions; 
using TownOfUs.Options;    
using Hazel;               
using TownOfUs.Roles;
using System.Reflection;

namespace TownOfUsDraft
{
    public static class DraftManager
    {
        public static Queue<byte> TurnQueue = new Queue<byte>();
        public static Dictionary<byte, DraftCategory> HostDraftAssignments = new Dictionary<byte, DraftCategory>();
        public static Dictionary<byte, string> PendingRoles = new Dictionary<byte, string>();
        public static bool IsDraftActive = false;
        
        private static MethodInfo _assignRoleMethod;
        private static bool _assignMethodSearched = false;
        private static object _roleOptionsInstance;
        private static System.Type _roleOptionsType;

        public static void StartDraft()
        {
            if (!AmongUsClient.Instance.AmHost) return;

            Debug.Log("[Draft] --- START DRAFTU (STATIC FIX) ---");
            
            FindAssignmentMethod();
            LoadTouOptionsRef(); 

            // 1. Inicjalizacja puli (tylko włączone role > 0%)
            RoleCategorizer.InitializeRoles();

            HostDraftAssignments.Clear();
            TurnQueue.Clear();
            PendingRoles.Clear();
            IsDraftActive = true;

            // 2. Gracze
            List<PlayerControl> players = PlayerControl.AllPlayerControls.ToArray().ToList();
            players.RemoveAll(p => p.Data.Disconnected || p.Data.IsDead);
            
            if (players.Count == 0) return;

            System.Random rng = new System.Random();
            players = players.OrderBy(x => rng.Next()).ToList();

            // 3. Pobierz Config Gry
            int impostors = GameOptionsManager.Instance.CurrentGameOptions.NumImpostors;
            int nkCount = GetConfigInt("NeutralKilling", "MaxNeutralKilling");
            int neCount = GetConfigInt("NeutralEvil", "MaxNeutralEvil");
            int nbCount = GetConfigInt("NeutralBenign", "MaxNeutralBenign");

            // Limity Crewmate (próba pobrania z TOU, jeśli istnieją)
            int maxSupport = GetConfigInt("MaxSupport", "CrewmateSupport", "SupportCount"); 
            int maxInvest = GetConfigInt("MaxInvestigative", "CrewmateInvestigative", "InvestigativeCount");
            int maxPower = GetConfigInt("MaxPower", "CrewmatePower", "PowerCount");
            int maxKilling = GetConfigInt("MaxKilling", "CrewmateKilling", "KillingCount");
            int maxProtective = GetConfigInt("MaxProtective", "CrewmateProtective", "ProtectiveCount");

            Debug.Log($"[Draft Config] Imp: {impostors}, NK: {nkCount}, NE: {neCount}, NB: {nbCount}");

            int assignedCount = 0;

            // A. Impostorzy
            for (int i = 0; i < impostors && assignedCount < players.Count; i++) {
                HostDraftAssignments[players[assignedCount].PlayerId] = DraftCategory.Impostor; assignedCount++;
            }

            // B. Neutrale
            for (int i = 0; i < nkCount && assignedCount < players.Count; i++) {
                HostDraftAssignments[players[assignedCount].PlayerId] = DraftCategory.NeutralKilling; assignedCount++;
            }
            for (int i = 0; i < neCount && assignedCount < players.Count; i++) {
                HostDraftAssignments[players[assignedCount].PlayerId] = DraftCategory.NeutralEvil; assignedCount++;
            }
            for (int i = 0; i < nbCount && assignedCount < players.Count; i++) {
                HostDraftAssignments[players[assignedCount].PlayerId] = DraftCategory.NeutralBenign; assignedCount++;
            }

            // C. Crewmates - jeśli brak limitów w configu, balansujemy sami
            int currentSupport = 0, currentInvest = 0, currentPower = 0, currentKilling = 0, currentProtective = 0;

            while (assignedCount < players.Count)
            {
                DraftCategory cat = DraftCategory.Crewmate;

                // Próba wpasowania w limity configu
                if (currentSupport < maxSupport) { cat = DraftCategory.Support; currentSupport++; }
                else if (currentInvest < maxInvest) { cat = DraftCategory.Investigative; currentInvest++; }
                else if (currentPower < maxPower) { cat = DraftCategory.Power; currentPower++; }
                else if (currentKilling < maxKilling) { cat = DraftCategory.Killing; currentKilling++; }
                else if (currentProtective < maxProtective) { cat = DraftCategory.Protective; currentProtective++; }
                else 
                {
                    // Fallback: Jeśli config nie określa, losujemy podkategorię
                    cat = RoleCategorizer.GetRandomCrewmateCategory();
                }

                HostDraftAssignments[players[assignedCount].PlayerId] = cat;
                assignedCount++;
            }

            // 4. Kolejka
            foreach(var p in players) TurnQueue.Enqueue(p.PlayerId);

            SendStartDraftRpc(players.Select(p => p.PlayerId).ToList());
            ProcessNextTurn();
        }

        public static void ProcessNextTurn()
        {
            if (TurnQueue.Count == 0) { EndDraft(); return; }

            byte currentPlayerId = TurnQueue.Dequeue();
            PlayerControl player = GetPlayerById(currentPlayerId);

            if (player == null || player.Data.Disconnected) { ProcessNextTurn(); return; }

            // Pobieramy kategorię przypisaną graczowi
            DraftCategory cat = HostDraftAssignments.ContainsKey(currentPlayerId) ? HostDraftAssignments[currentPlayerId] : DraftCategory.Crewmate;

            // Pobieramy role (tylko włączone) + Random
            List<string> options = RoleCategorizer.GetRandomRoles(cat, 3);
            options.Add("Random");

            SendTurnRpc(currentPlayerId, options);
        }

        public static void OnPlayerPickedRole(byte playerId, string selectedOption)
        {
            Debug.Log($"[Draft] Gracz {playerId} wybrał: {selectedOption}");
            string finalRoleName = selectedOption;

            // LOGIKA RANDOM: Losujemy 1 rolę z PRZYPISANEJ kategorii
            if (selectedOption == "Random")
            {
                if (HostDraftAssignments.ContainsKey(playerId))
                {
                    DraftCategory cat = HostDraftAssignments[playerId];
                    var oneRandom = RoleCategorizer.GetRandomRoles(cat, 1);
                    if (oneRandom.Count > 0) finalRoleName = oneRandom[0];
                    else finalRoleName = (cat == DraftCategory.Impostor) ? "Impostor" : "Crewmate";
                }
            }

            if (!PendingRoles.ContainsKey(playerId)) PendingRoles[playerId] = finalRoleName;
            
            if (AmongUsClient.Instance.AmHost) ProcessNextTurn();
        }
        
        // Metoda wywoływana przez lokalne UI (DraftHud)
        public static void OnPlayerSelectedRole(string roleName)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, 249, Hazel.SendOption.Reliable);
            writer.Write(PlayerControl.LocalPlayer.PlayerId);
            writer.Write(roleName);
            AmongUsClient.Instance.FinishRpcImmediately(writer);

            if (AmongUsClient.Instance.AmHost) OnPlayerPickedRole(PlayerControl.LocalPlayer.PlayerId, roleName);
        }

        private static void EndDraft()
        {
            Debug.Log("[Draft] Koniec.");
            IsDraftActive = false;
            
            // Zamknij HUD u wszystkich
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, 252, Hazel.SendOption.Reliable);
            AmongUsClient.Instance.FinishRpcImmediately(writer);

            foreach(var kvp in PendingRoles)
            {
                PlayerControl p = GetPlayerById(kvp.Key);
                if (p != null) AssignRealRole(p, kvp.Value);
            }
        }

        // --- BRAKUJĄCE METODY RPC (Fix CS0103) ---
        private static void SendStartDraftRpc(List<byte> order)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, 251, Hazel.SendOption.Reliable);
            writer.Write(order.Count);
            foreach(var id in order) writer.Write(id);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        private static void SendTurnRpc(byte playerId, List<string> options)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, 250, Hazel.SendOption.Reliable);
            writer.Write(playerId);
            writer.Write(options.Count);
            foreach(var role in options) writer.Write(role);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        // --- HELPERS (Config, Reflection, Assignment) ---

        private static void FindAssignmentMethod()
        {
            if (_assignMethodSearched) return;
            _assignMethodSearched = true;
            var utilsType = typeof(CustomRoleUtils);
            if (utilsType != null) _assignRoleMethod = utilsType.GetMethod("SetRole", BindingFlags.Public | BindingFlags.Static);
            if (_assignRoleMethod == null) {
                var mgrType = typeof(CustomRoleManager);
                if (mgrType != null) _assignRoleMethod = mgrType.GetMethod("SetRole", BindingFlags.Public | BindingFlags.Static);
            }
        }

        private static void LoadTouOptionsRef()
        {
            try {
                var type = System.Type.GetType("TownOfUs.Options.RoleOptions, TownOfUsMira");
                if (type != null) {
                    _roleOptionsType = type;
                    var singleton = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (singleton != null) _roleOptionsInstance = singleton.GetValue(null);
                }
            } catch {}
        }

        private static int GetConfigInt(params string[] fieldNames)
        {
            if (_roleOptionsInstance == null || _roleOptionsType == null) return 0;
            foreach (var name in fieldNames) {
                try {
                    var prop = _roleOptionsType.GetProperty(name);
                    object optionObj = (prop != null) ? prop.GetValue(_roleOptionsInstance) : _roleOptionsType.GetField(name)?.GetValue(_roleOptionsInstance);
                    if (optionObj != null) {
                        var valProp = optionObj.GetType().GetProperty("Value");
                        if (valProp != null) return (int)((float)valProp.GetValue(optionObj));
                    }
                } catch {}
            }
            return 0; 
        }

        private static void AssignRealRole(PlayerControl player, string roleName)
        {
            var allRoles = CustomRoleManager.AllRoles;
            ICustomRole roleToAssign = null;
            foreach(var roleObj in allRoles) {
                if (roleObj is ICustomRole iRole && iRole.ToString() == roleName) {
                    roleToAssign = iRole; break;
                }
            }

            if (roleToAssign != null && _assignRoleMethod != null)
            {
                try { _assignRoleMethod.Invoke(null, new object[] { player, roleToAssign }); }
                catch { SetVanillaFallback(player, roleName); }
            }
            else SetVanillaFallback(player, roleName);
        }

        private static void SetVanillaFallback(PlayerControl player, string roleName) {
            if (roleName.Contains("Impostor")) player.RpcSetRole(RoleTypes.Impostor);
            else player.RpcSetRole(RoleTypes.Crewmate);
        }
        
        public static PlayerControl GetPlayerById(byte id) {
            foreach (var p in PlayerControl.AllPlayerControls) if (p.PlayerId == id) return p;
            return null;
        }
        
        public static void OnTurnStarted(byte playerId, List<string> options) {
            if (playerId == PlayerControl.LocalPlayer.PlayerId && DraftHud.Instance != null)
                DraftHud.Instance.ShowSelection(options);
        }
        public static void OnDraftEnded() { if (DraftHud.Instance != null) DraftHud.Instance.ShowHud = false; }
        public static void ApplyRoleFromRpc(byte playerId, string roleName) { }
    }
}