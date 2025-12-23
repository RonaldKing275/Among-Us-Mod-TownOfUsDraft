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

        private bool _hasLogged = false;
        private bool _wasPaused = false; // Flaga, czy my zatrzymaliśmy czas

        private void Update()
        {
            // Jeśli nie jesteśmy w grze/lobby, resetuj wszystko
            if (AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.NotJoined)
            {
                if (IsActive) CloseDraft();
                _hasLogged = false;
            }

            // --- LOGIKA ZATRZYMYWANIA CZASU ---
            if (IsActive)
            {
                // Zamrażamy czas, żeby Intro się nie skończyło
                if (Time.timeScale != 0f)
                {
                    Time.timeScale = 0f;
                    _wasPaused = true;
                }

                // Blokada ruchu (na wszelki wypadek)
                if (PlayerControl.LocalPlayer != null)
                    PlayerControl.LocalPlayer.moveable = false;
            }
            else if (_wasPaused)
            {
                // Jeśli Draft się skończył, a my pauzowaliśmy -> ODMRÓŹ
                Time.timeScale = 1f;
                _wasPaused = false;
                
                if (PlayerControl.LocalPlayer != null)
                    PlayerControl.LocalPlayer.moveable = true;
            }
        }

        private void CloseDraft()
        {
            IsActive = false;
            Time.timeScale = 1f; // Zawsze przywracaj czas przy zamknięciu
            _wasPaused = false;
        }

        // Zabezpieczenie: Przy niszczeniu obiektu (wyjście z gry) odmrażamy czas
        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        private void OnGUI()
        {
            if (!IsActive) return;

            if (!_hasLogged)
            {
                DraftPlugin.Instance.Log.LogInfo("[DraftHud] GUI Otwarte - Czas Zatrzymany!");
                _hasLogged = true;
            }

            try
            {
                GUI.depth = -9999; // Zawsze na wierzchu

                float w = 600, h = 500;
                float x = (Screen.width - w) / 2;
                float y = (Screen.height - h) / 2;

                // Tło
                GUI.backgroundColor = Color.black;
                GUI.Box(new Rect(x, y, w, h), ""); 

                // Nagłówek
                GUI.color = Color.yellow;
                
                // POPRAWKA: GUIStyle z dużej litery
                GUIStyle centeredStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20, fontStyle = FontStyle.Bold };
                centeredStyle.normal.textColor = Color.yellow;
                
                GUI.Label(new Rect(x, y + 20, w, 50), $"WYBIERZ ROLĘ: {CategoryTitle}", centeredStyle);
                
                GUI.color = Color.white;

                if (MyOptions != null)
                {
                    for (int i = 0; i < MyOptions.Count; i++)
                    {
                        string role = MyOptions[i];
                        string display = role.Replace("Role", ""); // Wyświetl ładniejszą nazwę

                        // Przycisk wyboru
                        if (GUI.Button(new Rect(x + 50, y + 80 + (i * 100), w - 100, 80), display))
                        {
                            DraftPlugin.Instance.Log.LogInfo($"[UI] Kliknięto: {role}");
                            
                            // 1. Nadaj rolę
                            DraftManager.OnPlayerSelectedRole(role);
                            
                            // 2. Zamknij Draft (to automatycznie wznowi czas w Update)
                            CloseDraft();
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                DraftPlugin.Instance.Log.LogError($"[GUI ERROR] {e.Message}");
                // W razie błędu awaryjnie zamknij, żeby nie zaciąć gry
                CloseDraft(); 
            }
        }
    }
}