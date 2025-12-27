using HarmonyLib;
using Hazel;
using System.Collections.Generic;

namespace TownOfUsDraft.Patches
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
    public static class DraftNetworkPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
        {
            if (callId == 251) // Start Draft
            {
                int count = reader.ReadInt32();
                // Opcjonalnie: pobierz listę, ale głównie chodzi o to, by nie crashowało
                return;
            }

            if (callId == 250) // Turn Info
            {
                byte playerId = reader.ReadByte();
                int optionCount = reader.ReadInt32();
                List<string> options = new List<string>();
                for(int i=0; i<optionCount; i++) options.Add(reader.ReadString());

                DraftManager.OnTurnStarted(playerId, options);
                return;
            }

            if (callId == 249) // Select Role
            {
                byte playerId = reader.ReadByte();
                string roleName = reader.ReadString();
                DraftManager.OnPlayerPickedRole(playerId, roleName);
            }
            
            if (callId == 252) // End Draft
            {
                DraftManager.OnDraftEnded();
            }
        }
    }
}