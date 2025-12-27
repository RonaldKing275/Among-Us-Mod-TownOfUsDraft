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
    public class DraftManager : MonoBehaviour
    {
        public static DraftManager Instance;

        public Queue<byte> TurnQueue = new Queue<byte>();
        public Dictionary<byte, DraftCategory> HostDraftAssignments = new Dictionary<byte, DraftCategory>();
        public Dictionary<byte, string> PendingRoles = new Dictionary<byte, string>();
        public bool IsDraftActive = false;
        
        private MethodInfo _assignRoleMethod;
        private bool _assignMethodSearched = false;
        
        // Cache do opcji TOU (zeby nie szukac co chwile)
        private object _roleOptionsInstance;
        private System.Type _roleOptionsType;

        private void Awake()
        {
            if (Instance != null && Instance != this) Destroy(this);
            else Instance = this;
        }

        public void StartDraft()
        {
            if (!AmongUsClient.Instance.AmHost) return;

            Debug.Log("[Draft] --- START DRAFTU ---");
            
            FindAssignmentMethod();
            LoadTouOptionsRef(); // Załaduj config TOU do pamięci

            // Inicjalizacja puli ról (tylko włączone w configu!)
            RoleCategorizer.InitializeRoles();

            HostDraftAssignments.Clear();
            TurnQueue.Clear();
            PendingRoles.Clear();
            IsDraftActive = true;

            // --- 1. Gracze ---
            List<PlayerControl> players = PlayerControl.AllPlayerControls.ToArray().ToList();
            players.RemoveAll(p => p.Data.Disconnected || p.Data.IsDead);
            
            if (players.Count == 0) return;

            System.Random rng = new System.Random();
            players = players.OrderBy(x => rng.Next()).ToList();

            // --- 2. Odczyt Configu Gry ---
            // Pobieramy limity z ustawień TOU
            int impostors = GameOptionsManager.Instance.CurrentGameOptions.NumImpostors;
            
            int nkCount = GetConfigInt("NeutralKilling");
            int neCount = GetConfigInt("NeutralEvil");
            int nbCount = GetConfigInt("NeutralBenign");
            
            // Opcjonalne limity Crewmate (jesli TOU je ma)
            int maxSupport = GetConfigInt("MaxSupport"); 
            int maxInvest = GetConfigInt("MaxInvestigative");
            int maxPower = GetConfigInt("MaxPower");
            int maxKilling = GetConfigInt("MaxKilling"); // Np. Sheriff/Veteran

            Debug.Log($"[Draft Config] Imp: {impostors}, NK: {nkCount}, NE: {neCount}, NB: {nbCount}");

            int assignedCount = 0;

            // A. Przydziel Impostorów
            for (int i = 0; i < impostors && assignedCount < players.Count; i++)
            {
                HostDraftAssignments[players[assignedCount].PlayerId] = DraftCategory.Impostor;
                assignedCount++;
            }

            // B. Przydziel Neutrale (NK -> NE -> NB)
            for (int i = 0; i < nkCount && assignedCount < players.Count; i++) {
                HostDraftAssignments[players[assignedCount].PlayerId] = DraftCategory.NeutralKilling; assignedCount++;
            }
            for (int i = 0; i < neCount && assignedCount < players.Count; i++) {
                HostDraftAssignments[players[assignedCount].PlayerId] = DraftCategory.NeutralEvil; assignedCount++;
            }
            for (int i = 0; i < nbCount && assignedCount < players.Count; i++) {
                HostDraftAssignments[players[assignedCount].PlayerId] = DraftCategory.NeutralBenign; assignedCount++;
            }

            // C. Przydziel Crewmates (wg limitów lub losowo)
            // Używamy prostego licznika ile już przydzieliliśmy konkretnych podkategorii
            int currentSupport = 0, currentInvest = 0, currentPower = 0, currentKilling = 0;

            while (assignedCount < players.Count)
            {
                DraftCategory cat = DraftCategory.Crewmate;

                // Próba wpasowania w konkretne podkategorie jeśli są włączone
                if (currentSupport < maxSupport) { cat = DraftCategory.Support; currentSupport++; }
                else if (currentInvest < maxInvest) { cat = DraftCategory.Investigative; currentInvest++; }
                else if (currentPower < maxPower) { cat = DraftCategory.Power; currentPower++; }
                else if (currentKilling < maxKilling) { cat = DraftCategory.Killing; currentKilling++; }
                else 
                {
                    // Jak limity wyczerpane albo ich nie ma, daj losową podkategorię Crewmate
                    cat = RoleCategorizer.GetRandomCrewmateCategory();
                }

                HostDraftAssignments[players[assignedCount].PlayerId] = cat;
                assignedCount++;
            }

            // --- 3. Kolejka ---
            foreach(var p in players)
            {
                TurnQueue.Enqueue(p.PlayerId);
            }

            SendStartDraftRpc(players.Select(p => p.PlayerId).ToList());
            ProcessNextTurn();
        }

        // --- REFLEKSJA DO CONFIGU ---
        private void LoadTouOptionsRef()
        {
            try {
                // Próbujemy znaleźć Singleton opcji
                var type = System.Type.GetType("TownOfUs.Options.RoleOptions, TownOfUsMira");
                if (type != null) {
                    _roleOptionsType = type;
                    var singleton = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (singleton != null) _roleOptionsInstance = singleton.GetValue(null);
                }
            } catch (System.Exception e) { Debug.LogError("[Draft] Błąd ładowania opcji TOU: " + e.Message); }
        }

        private int GetConfigInt(string fieldName)
        {
            if (_roleOptionsInstance == null || _roleOptionsType == null) return 0;
            try {
                // Szukamy pola/właściwości o nazwie np. NeutralKilling
                var field = _roleOptionsType.GetField(fieldName);
                object optionObj = null;
                
                if (field != null) optionObj = field.GetValue(_roleOptionsInstance);
                else {
                    var prop = _roleOptionsType.GetProperty(fieldName);
                    if (prop != null) optionObj = prop.GetValue(_roleOptionsInstance);
                }

                if (optionObj != null) {
                    // To jest obiekt typu ModdedNumberOption lub podobny. Szukamy pola .Value
                    var valProp = optionObj.GetType().GetProperty("Value");
                    if (valProp != null) {
                        float fVal = (float)valProp.GetValue(optionObj);
                        return (int)fVal;
                    }
                }
            } catch {}
            return 0; // Domyślnie 0 jeśli nie znajdzie
        }

        public void ProcessNextTurn()
        {
            if (TurnQueue.Count == 0) { EndDraft(); return; }

            byte currentPlayerId = TurnQueue.Dequeue();
            PlayerControl player = GetPlayerById(currentPlayerId);

            if (player == null || player.Data.Disconnected) { ProcessNextTurn(); return; }

            DraftCategory cat = HostDraftAssignments.ContainsKey(currentPlayerId) ? HostDraftAssignments[currentPlayerId] : DraftCategory.Crewmate;

            // 1. Pobierz 3 role z kategorii (tylko te włączone w configu, bo RoleCategorizer już je przefiltrował)
            List<string> options = RoleCategorizer.GetRandomRoles(cat, 3);
            options.Add("Random"); // 4. Przycisk

            SendTurnRpc(currentPlayerId, options);
        }

        public void OnPlayerSelectedRole(string roleName)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, 249, Hazel.SendOption.Reliable);
            writer.Write(PlayerControl.LocalPlayer.PlayerId);
            writer.Write(roleName);
            AmongUsClient.Instance.FinishRpcImmediately(writer);

            if (AmongUsClient.Instance.AmHost) OnPlayerPickedRole(PlayerControl.LocalPlayer.PlayerId, roleName);
        }

        public void OnPlayerPickedRole(byte playerId, string selectedOption)
        {
            Debug.Log($"[Draft] Gracz {playerId} wybrał: {selectedOption}");
            string finalRoleName = selectedOption;

            if (selectedOption == "Random")
            {
                if (HostDraftAssignments.ContainsKey(playerId))
                {
                    // Losuj z przydzielonej kategorii
                    var oneRandom = RoleCategorizer.GetRandomRoles(HostDraftAssignments[playerId], 1);
                    if (oneRandom.Count > 0) finalRoleName = oneRandom[0];
                }
            }

            if (!PendingRoles.ContainsKey(playerId)) PendingRoles[playerId] = finalRoleName;
            if (AmongUsClient.Instance.AmHost) ProcessNextTurn();
        }

        private void EndDraft()
        {
            Debug.Log("[Draft] Koniec. Aplikowanie ról...");
            IsDraftActive = false;
            foreach(var kvp in PendingRoles)
            {
                PlayerControl p = GetPlayerById(kvp.Key);
                if (p != null) AssignRealRole(p, kvp.Value);
            }
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, 252, Hazel.SendOption.Reliable);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        private void FindAssignmentMethod()
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

        private void AssignRealRole(PlayerControl player, string roleName)
        {
            var allRoles = CustomRoleManager.AllRoles;
            ICustomRole roleToAssign = null;
            
            foreach(var roleObj in allRoles) 
            {
                if (roleObj is ICustomRole iRole) {
                    // NAPRAWA CS1061: Usunięto iRole.Name, używamy tylko ToString()
                    if (iRole.ToString() == roleName) {
                        roleToAssign = iRole;
                        break;
                    }
                }
            }

            if (roleToAssign != null && _assignRoleMethod != null)
            {
                try { _assignRoleMethod.Invoke(null, new object[] { player, roleToAssign }); }
                catch { SetVanillaFallback(player, roleName); }
            }
            else SetVanillaFallback(player, roleName);
        }

        private void SetVanillaFallback(PlayerControl player, string roleName)
        {
            if (roleName.Contains("Impostor")) player.RpcSetRole(RoleTypes.Impostor);
            else player.RpcSetRole(RoleTypes.Crewmate);
        }

        public static PlayerControl GetPlayerById(byte id)
        {
            foreach (var p in PlayerControl.AllPlayerControls) if (p.PlayerId == id) return p;
            return null;
        }

        public void ApplyRoleFromRpc(byte playerId, string roleName) { }
        public void OnTurnStarted(byte playerId, List<string> options)
        {
            if (playerId == PlayerControl.LocalPlayer.PlayerId && DraftHud.Instance != null)
                DraftHud.Instance.ShowSelection(options);
        }

        private void SendStartDraftRpc(List<byte> order)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, 251, Hazel.SendOption.Reliable);
            writer.Write(order.Count);
            foreach(var id in order) writer.Write(id);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        private void SendTurnRpc(byte playerId, List<string> options)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, 250, Hazel.SendOption.Reliable);
            writer.Write(playerId);
            writer.Write(options.Count);
            foreach(var role in options) writer.Write(role);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }
}