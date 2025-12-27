using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUs.GameOptions;
using MiraAPI.Roles; 
using MiraAPI.GameOptions; 
using TownOfUs.Options;    
using Hazel;               
using TownOfUs.Roles;
using System.Reflection; // Niezbędne do Refleksji

namespace TownOfUsDraft
{
    public class DraftManager : MonoBehaviour
    {
        public static DraftManager Instance;

        public Queue<byte> TurnQueue = new Queue<byte>();
        public Dictionary<byte, DraftCategory> HostDraftAssignments = new Dictionary<byte, DraftCategory>();
        public Dictionary<byte, string> PendingRoles = new Dictionary<byte, string>();
        public bool IsDraftActive = false;
        public List<string> CurrentPool = new List<string>();

        // Cache dla metody przypisującej rolę
        private MethodInfo _assignRoleMethod;
        private bool _assignMethodSearched = false;

        private void Awake()
        {
            if (Instance != null && Instance != this) Destroy(this);
            else Instance = this;
        }

        public void StartDraft()
        {
            if (!AmongUsClient.Instance.AmHost) return;

            Debug.Log("[Draft] --- START DRAFTU ---");
            
            // Szukamy metody przypisywania ról (tylko raz)
            FindAssignmentMethod();

            HostDraftAssignments.Clear();
            TurnQueue.Clear();
            PendingRoles.Clear();
            IsDraftActive = true;

            int impostors = 0;
            if (GameOptionsManager.Instance != null && GameOptionsManager.Instance.CurrentGameOptions != null)
                impostors = GameOptionsManager.Instance.CurrentGameOptions.NumImpostors;
            
            Debug.Log($"[Draft] Config: Imp={impostors}");

            List<PlayerControl> players = PlayerControl.AllPlayerControls.ToArray().ToList();
            players.RemoveAll(p => p.Data.Disconnected || p.Data.IsDead);
            
            if (players.Count == 0) return;

            System.Random rng = new System.Random();
            players = players.OrderBy(x => rng.Next()).ToList();

            int assignedCount = 0;

            for (int i = 0; i < impostors && assignedCount < players.Count; i++)
            {
                HostDraftAssignments[players[assignedCount].PlayerId] = DraftCategory.Impostor;
                assignedCount++;
            }
            
            while (assignedCount < players.Count)
            {
                HostDraftAssignments[players[assignedCount].PlayerId] = DraftCategory.Crewmate;
                assignedCount++;
            }

            foreach(var p in players)
            {
                TurnQueue.Enqueue(p.PlayerId);
            }

            SendStartDraftRpc(players.Select(p => p.PlayerId).ToList());
            ProcessNextTurn();
        }

        private void FindAssignmentMethod()
        {
            if (_assignMethodSearched) return;
            _assignMethodSearched = true;

            Debug.Log("[Draft] Szukanie metody przypisywania ról w MiraAPI/TOU...");

            // 1. Sprawdź CustomRoleUtils (najbardziej prawdopodobne)
            var utilsType = typeof(CustomRoleUtils);
            if (utilsType != null)
            {
                // Szukamy metod: SetRole, AssignRole, SetCustomRole
                _assignRoleMethod = utilsType.GetMethod("SetRole", BindingFlags.Public | BindingFlags.Static);
                if (_assignRoleMethod == null) _assignRoleMethod = utilsType.GetMethod("AssignRole", BindingFlags.Public | BindingFlags.Static);
                if (_assignRoleMethod == null) _assignRoleMethod = utilsType.GetMethod("SetCustomRole", BindingFlags.Public | BindingFlags.Static);
            }

            // 2. Jeśli nie, sprawdź CustomRoleManager
            if (_assignRoleMethod == null)
            {
                var mgrType = typeof(CustomRoleManager);
                if (mgrType != null)
                {
                    _assignRoleMethod = mgrType.GetMethod("SetRole", BindingFlags.Public | BindingFlags.Static);
                    if (_assignRoleMethod == null) _assignRoleMethod = mgrType.GetMethod("AssignRole", BindingFlags.Public | BindingFlags.Static);
                }
            }
            
            // 3. Sprawdź TouRoleUtils (z TownOfUs)
            if (_assignRoleMethod == null)
            {
                 // Próbujemy znaleźć typ po nazwie, bo może nie być zaimportowany
                 var touUtils = System.Type.GetType("TownOfUs.Utilities.TouRoleUtils, TownOfUsMira");
                 if (touUtils != null)
                 {
                     _assignRoleMethod = touUtils.GetMethod("SetRole", BindingFlags.Public | BindingFlags.Static);
                 }
            }

            if (_assignRoleMethod != null)
                Debug.Log($"[Draft] ZNALEZIONO metodę: {_assignRoleMethod.Name} w {_assignRoleMethod.DeclaringType.Name}");
            else
                Debug.LogError("[Draft] KRYTYCZNE: Nie znaleziono metody przypisywania ról! Będę używał Vanilla RPC.");
        }

        public void ProcessNextTurn()
        {
            if (TurnQueue.Count == 0)
            {
                EndDraft();
                return;
            }

            byte currentPlayerId = TurnQueue.Dequeue();
            PlayerControl player = GetPlayerById(currentPlayerId);

            if (player == null || player.Data.Disconnected)
            {
                ProcessNextTurn();
                return;
            }

            DraftCategory cat = HostDraftAssignments.ContainsKey(currentPlayerId) ? HostDraftAssignments[currentPlayerId] : DraftCategory.Crewmate;
            
            if (cat == DraftCategory.Crewmate)
            {
                DraftCategory[] crewSubs = { DraftCategory.Support, DraftCategory.Investigative, DraftCategory.Protective, DraftCategory.Power, DraftCategory.Killing };
                cat = crewSubs[Random.Range(0, crewSubs.Length)];
            }

            CurrentPool = RoleCategorizer.GetRandomRoles(cat, 3);
            SendTurnRpc(currentPlayerId, CurrentPool);
        }

        public void OnPlayerPickedRole(byte playerId, string roleName)
        {
            Debug.Log($"[Draft] Gracz {playerId} wybrał: {roleName}");
            if (!PendingRoles.ContainsKey(playerId)) PendingRoles[playerId] = roleName;

            if (AmongUsClient.Instance.AmHost) ProcessNextTurn();
        }

        public void OnPlayerSelectedRole(string roleName)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, 249, Hazel.SendOption.Reliable);
            writer.Write(PlayerControl.LocalPlayer.PlayerId);
            writer.Write(roleName);
            AmongUsClient.Instance.FinishRpcImmediately(writer);

            if (AmongUsClient.Instance.AmHost)
            {
                OnPlayerPickedRole(PlayerControl.LocalPlayer.PlayerId, roleName);
            }
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

        private void AssignRealRole(PlayerControl player, string roleName)
        {
            // Znajdź rolę
            var allRoles = CustomRoleManager.AllRoles;
            ICustomRole roleToAssign = null;
            
            foreach(var roleObj in allRoles) 
            {
                if (roleObj is ICustomRole iRole)
                {
                    if (iRole.ToString() == roleName) // Używamy ToString() dla bezpieczeństwa
                    {
                        roleToAssign = iRole;
                        break;
                    }
                }
            }

            if (roleToAssign != null)
            {
                // UŻYWAMY ZNALEZIONEJ METODY (REFLEKSJA)
                if (_assignRoleMethod != null)
                {
                    try 
                    {
                        // Wywołanie: Metoda(player, role)
                        _assignRoleMethod.Invoke(null, new object[] { player, roleToAssign });
                        Debug.Log($"[Draft] Przypisano {roleName} (Refleksja) dla {player.Data.PlayerName}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[Draft] Błąd przy wywołaniu metody przypisania: {e.Message}");
                        // Fallback
                        SetVanillaFallback(player, roleName);
                    }
                }
                else
                {
                    Debug.LogWarning("[Draft] Brak metody przypisania. Używam Vanilla.");
                    SetVanillaFallback(player, roleName);
                }
            }
            else
            {
                SetVanillaFallback(player, roleName);
            }
        }

        private void SetVanillaFallback(PlayerControl player, string roleName)
        {
            if (roleName == "Impostor") player.RpcSetRole(RoleTypes.Impostor);
            else player.RpcSetRole(RoleTypes.Crewmate);
        }

        public static PlayerControl GetPlayerById(byte id)
        {
            foreach (var p in PlayerControl.AllPlayerControls)
                if (p.PlayerId == id) return p;
            return null;
        }

        public void ApplyRoleFromRpc(byte playerId, string roleName) { }
        
        public void OnTurnStarted(byte playerId, List<string> options)
        {
            if (playerId == PlayerControl.LocalPlayer.PlayerId)
            {
                if (DraftHud.Instance != null)
                    DraftHud.Instance.ShowSelection(options);
            }
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