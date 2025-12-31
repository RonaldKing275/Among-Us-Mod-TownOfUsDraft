using HarmonyLib;
using Hazel;
using InnerNet;
using MiraAPI.Roles; 
using UnityEngine;
using AmongUs.GameOptions; 

namespace TownOfUsDraft.Patches
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
    public static class DraftNetworkPatch
    {
        public const byte RPC_ROLE_SELECTED = 249; // Ktoś wybrał rolę
        public const byte RPC_START_TURN = 251;    // Nowa tura (Host -> All)
        public const byte RPC_SET_TEAMTYPE = 248;  // Synchronizacja TeamType (Host -> All)

        public static void Postfix(PlayerControl __instance, byte callId, MessageReader reader)
        {
            if (callId == RPC_ROLE_SELECTED)
            {
                byte playerId = reader.ReadByte();
                string roleName = reader.ReadString();
                PlayerControl target = Helpers.GetPlayerById(playerId);
                if (target != null) DraftManager.ApplyRoleFromRpc(target, roleName);
            }
            else if (callId == RPC_START_TURN)
            {
                // Odbieramy: Kto teraz wybiera? Jakie ma opcje? Jaka to kategoria?
                byte activePlayerId = reader.ReadByte();
                string catName = reader.ReadString();
                string op1 = reader.ReadString();
                string op2 = reader.ReadString();
                string op3 = reader.ReadString();

                DraftManager.OnTurnStarted(activePlayerId, catName, new System.Collections.Generic.List<string>{op1, op2, op3});
            }
            else if (callId == RPC_SET_TEAMTYPE)
            {
                // Odbieramy: playerId + teamType (byte: 0=Crewmate, 1=Impostor, 2=Neutral)
                byte playerId = reader.ReadByte();
                byte teamTypeByte = reader.ReadByte();
                PlayerControl target = Helpers.GetPlayerById(playerId);
                
                if (target != null && target.Data != null && target.Data.Role != null)
                {
                    var targetTeamType = (RoleTeamTypes)teamTypeByte;
                    
                    // Spróbuj ustawić TeamType przez reflection (jak w OnDraftCompleted)
                    var teamTypeProp = typeof(RoleBehaviour).GetProperty(
                        nameof(RoleBehaviour.TeamType), 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                    );
                    
                    if (teamTypeProp != null && teamTypeProp.CanWrite)
                    {
                        teamTypeProp.SetValue(target.Data.Role, targetTeamType);
                        DraftPlugin.Instance.Log.LogInfo($"[RPC_SET_TEAMTYPE] ✓ Ustawiono TeamType dla {target.Data.PlayerName} na {targetTeamType}");
                    }
                    else
                    {
                        // Fallback: backing field
                        var backingField = typeof(RoleBehaviour).GetField(
                            "<TeamType>k__BackingField", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                        );
                        
                        if (backingField != null)
                        {
                            backingField.SetValue(target.Data.Role, targetTeamType);
                            DraftPlugin.Instance.Log.LogInfo($"[RPC_SET_TEAMTYPE] ✓ Ustawiono TeamType (backing field) dla {target.Data.PlayerName} na {targetTeamType}");
                        }
                        else
                        {
                            DraftPlugin.Instance.Log.LogWarning($"[RPC_SET_TEAMTYPE] ⚠ Nie można ustawić TeamType dla {target.Data.PlayerName}");
                        }
                    }
                }
            }
        }
    }

    public static class Helpers 
    {
        public static PlayerControl GetPlayerById(byte id)
        {
            foreach (var p in PlayerControl.AllPlayerControls) if (p.PlayerId == id) return p;
            return null;
        }
    }
}