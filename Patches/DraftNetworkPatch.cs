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
            // RPC 251: Start Draft
            if (callId == 251)
            {
                int count = reader.ReadInt32();
                // Logika startu u klienta...
                return;
            }

            // RPC 250: Turn Info
            if (callId == 250)
            {
                byte playerId = reader.ReadByte();
                int optionCount = reader.ReadInt32();
                List<string> options = new List<string>();
                for(int i=0; i<optionCount; i++) options.Add(reader.ReadString());

                if (DraftManager.Instance != null)
                {
                    DraftManager.Instance.OnTurnStarted(playerId, options);
                }
                return;
            }

            // RPC 249: Select Role (Klient wysyła wybór)
            if (callId == 249)
            {
                byte playerId = reader.ReadByte();
                string roleName = reader.ReadString();

                if (DraftManager.Instance != null)
                {
                    DraftManager.Instance.OnPlayerPickedRole(playerId, roleName);
                }
            }
        }
    }
}