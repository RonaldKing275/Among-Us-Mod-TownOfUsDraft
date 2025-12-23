using BepInEx.Unity.IL2CPP;
using UnityEngine;
using System.Collections.Generic;

namespace TownOfUsDraft
{
    public class DraftHud : MonoBehaviour
    {
        public static bool IsActive = false;
        public static List<string> MyOptions = new List<string>();
        public static string CategoryTitle = "";

        private void Update()
        {
            // Reset przy wyjściu do lobby
            if (AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.NotJoined)
            {
                IsActive = false;
            }

            // Blokada ruchu
            if (IsActive && PlayerControl.LocalPlayer != null)
            {
                PlayerControl.LocalPlayer.moveable = false;
            }
        }

        private void OnGUI()
        {
            // Jeśli nieaktywne, nic nie rysuj
            if (!IsActive) return;

            try
            {
                // 1. Ustawienie głębokości na bardzo niską (Rysuj NA WIERZCHU)
                GUI.depth = -9999;

                float w = 500, h = 450;
                float x = (Screen.width - w) / 2;
                float y = (Screen.height - h) / 2;

                // 2. Czarne tło (Prosty Box)
                GUI.backgroundColor = Color.black;
                GUI.Box(new Rect(x, y, w, h), "");

                // 3. Nagłówek (Bez GUI.skin - surowy tekst)
                GUI.color = Color.yellow;
                GUI.Label(new Rect(x, y + 20, w, 40), $"WYBIERZ ROLĘ: {CategoryTitle}");
                
                // Przywracamy kolor dla przycisków
                GUI.color = Color.white;
                GUI.backgroundColor = Color.gray;

                if (MyOptions != null)
                {
                    for (int i = 0; i < MyOptions.Count; i++)
                    {
                        string role = MyOptions[i];
                        string display = role.Replace("Role", ""); // Wyświetl ładną nazwę
                        
                        // 4. Prosty przycisk (Bez stylów)
                        if (GUI.Button(new Rect(x + 50, y + 80 + (i * 90), w - 100, 70), display))
                        {
                            DraftPlugin.Instance.Log.LogInfo($"[UI] Kliknięto: {role}");
                            DraftManager.OnPlayerSelectedRole(role);
                            IsActive = false; 
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                // Jeśli coś wybuchnie w trakcie rysowania, zobaczymy to w logach
                DraftPlugin.Instance.Log.LogError($"[GUI ERROR] {e.Message}");
                IsActive = false; // Wyłączamy UI, żeby nie spamowało błędem
            }
        }
    }
}