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