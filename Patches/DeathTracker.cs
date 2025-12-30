using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using AmongUs.GameOptions;
using System.Linq;
using Hazel;
using BepInEx.Unity.IL2CPP; 
using System.Reflection; // Niezbędne do naprawy błędu

namespace TownOfUsDraft.Patches
{
    [HarmonyPatch]
    public static class DeathTracker
    {
        // Pula zgonów z bieżącej rundy
        public static List<byte> CurrentGameRound1Deaths = new List<byte>();
        
        // Pula chronionych w TEJ grze (na bazie poprzedniej)
        public static HashSet<byte> ShieldedPlayers = new HashSet<byte>();

        private static bool _isRoundOne = true;

        // --- 1. START GRY: Przenosimy pechowców do listy chronionych ---
        [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
        [HarmonyPostfix]
        public static void OnGameStart()
        {
            ShieldedPlayers.Clear();
            
            // Bierzemy max 2 pierwsze osoby z poprzedniej gry
            var luckyOnes = CurrentGameRound1Deaths.Take(2).ToList();
            foreach(var id in luckyOnes) 
            {
                ShieldedPlayers.Add(id);
                var player = GetPlayerById(id);
                if (player != null)
                {
                    DraftPlugin.Instance.Log.LogInfo($"[PityShield] Tarcza aktywna dla: {player.Data.PlayerName} (ID: {id})");
                }
            }

            if (ShieldedPlayers.Count == 0)
            {
                DraftPlugin.Instance.Log.LogInfo("[PityShield] Brak graczy z tarczą w tej rundzie.");
            }

            CurrentGameRound1Deaths.Clear();
            _isRoundOne = true;
        }
        
        // Helper do znajdowania gracza
        private static PlayerControl GetPlayerById(byte id)
        {
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (p.PlayerId == id) return p;
            }
            return null;
        }

        // --- 2. MEETING: Koniec tarczy ---
        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
        [HarmonyPostfix]
        public static void OnMeetingStart()
        {
            _isRoundOne = false; 
        }

        // --- 3. REJESTRACJA ŚMIERCI (Dla przyszłej gry) ---
        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
        [HarmonyPostfix]
        public static void OnPlayerDied(PlayerControl __instance)
        {
            if (_isRoundOne)
            {
                if (!CurrentGameRound1Deaths.Contains(__instance.PlayerId))
                {
                    CurrentGameRound1Deaths.Add(__instance.PlayerId);
                    DraftPlugin.Instance.Log.LogInfo($"[DeathTracker] Zgon R1: {__instance.Data.PlayerName}");
                }
            }
        }

        // --- 4. EFEKT TARCZY (Blokada Kill) ---
        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
        [HarmonyPrefix]
        public static bool PrefixMurderPlayer(PlayerControl __instance, PlayerControl target)
        {
            // Jeśli brak celu lub to nie runda 1 -> działaj normalnie
            if (target == null || !_isRoundOne) return true;

            // Sprawdź czy cel ma Twoją tarczę
            if (ShieldedPlayers.Contains(target.PlayerId))
            {
                DraftPlugin.Instance.Log.LogWarning($"[PityShield] ZABLOKOWANO atak na {target.Data.PlayerName}!");
                
                // --- FIX: BEZPIECZNE POBIERANIE COOLDOWNU ---
                // Zamiast szukać klasy NormalGameOptions, wyciągamy wartość bezpośrednio
                if (GameOptionsManager.Instance.CurrentGameOptions != null)
                {
                    var opts = GameOptionsManager.Instance.CurrentGameOptions;
                    // Magia: Znajdź "KillCooldown" niezależnie od tego, gdzie jest
                    var prop = opts.GetType().GetProperty("KillCooldown");
                    
                    float cooldown = 10f; // Domyślna wartość w razie błędu
                    if (prop != null)
                    {
                        try { cooldown = (float)prop.GetValue(opts); } catch {}
                    }
                    
                    __instance.SetKillTimer(cooldown);
                }

                return false; // ANULUJ ZABÓJSTWO (Ofiara przeżywa)
            }

            return true; // Wykonaj zabójstwo
        }
    }
}