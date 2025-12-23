using BepInEx.Unity.IL2CPP;
using UnityEngine;
using System.Collections.Generic;

namespace TownOfUsDraft
{
    public class DraftHud : MonoBehaviour
    {
        // Pola statyczne przetrwają nawet jeśli obiekt HUD zostanie zniszczony i stworzony na nowo
        public static bool IsActive = false;
        public static List<string> MyOptions = new List<string>();
        public static string CategoryTitle = "";

        private bool _hasLogged = false;

        private void Update()
        {
            // Reset po wyjściu do menu
            if (AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.NotJoined)
            {
                IsActive = false;
                _hasLogged = false;
            }

            // Blokada ruchu gracza
            if (IsActive && PlayerControl.LocalPlayer != null)
            {
                PlayerControl.LocalPlayer.moveable = false;
            }
        }

        private void OnGUI()
        {
            if (!IsActive) return;

            // Debug: Potwierdzenie, że OnGUI ruszyło
            if (!_hasLogged)
            {
                DraftPlugin.Instance.Log.LogInfo("[DraftHud] OnGUI rysuje pierwszą klatkę!");
                _hasLogged = true;
            }

            try
            {
                // Rysuj NA WIERZCHU wszystkiego
                GUI.depth = -9999;

                // Tło i Okno
                float w = 600, h = 500;
                float x = (Screen.width - w) / 2;
                float y = (Screen.height - h) / 2;

                // Używamy GUI.Window dla pewności (automatycznie obsługuje focus)
                GUI.backgroundColor = Color.black;
                GUI.Box(new Rect(x, y, w, h), ""); // Czarne tło

                // Nagłówek
                GUI.color = Color.yellow;
                GUI.Label(new Rect(x, y + 20, w, 50), $"WYBIERZ ROLĘ: {CategoryTitle}");
                GUI.color = Color.white;

                if (MyOptions != null)
                {
                    for (int i = 0; i < MyOptions.Count; i++)
                    {
                        string role = MyOptions[i];
                        string display = role.Replace("Role", "");

                        if (GUI.Button(new Rect(x + 50, y + 80 + (i * 100), w - 100, 80), display))
                        {
                            DraftPlugin.Instance.Log.LogInfo($"[UI] Kliknięto przycisk: {role}");
                            DraftManager.OnPlayerSelectedRole(role);
                            IsActive = false;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                DraftPlugin.Instance.Log.LogError($"[GUI ERROR] {e.Message}");
            }
        }
    }
}