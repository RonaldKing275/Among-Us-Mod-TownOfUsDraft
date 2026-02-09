using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using System.Collections.Generic;
using InnerNet; 

namespace TownOfUsDraft
{
    public class DraftHud : MonoBehaviour
    {
        public static DraftHud Instance;
        public static bool IsDraftActive = false;
        
        public static byte ActiveTurnPlayerId = 255; 
        public static string CategoryTitle = "";
        public static List<string> MyOptions = new List<string>();

        // USUNIĘTO: Timer Hosta - nie jest już używany
        // public static bool HostTimerActive = false;
        // private float _hostTimer = 0f;

        // WATCHDOG
        public static float TurnWatchdogTimer = 0f;
        public static byte CurrentTurnPlayerId = 255;
        // NOWE POLE: Opcje aktualnego gracza (dla auto-picka)
        public static List<string> CurrentTurnOptions = new List<string>(); 
        
        // Timeout z configa
        private float MaxTurnTime => TouConfigAdapter.DraftTimeout; 

        private bool _wasPaused = false;

        private void Awake() { Instance = this; }

        private void Update()
        {
            // USUNIĘTO: HostTimerActive logic - to powodowało automatyczne przeskakiwanie tur!
            // Draft teraz działa w pełni synchronicznie - ProcessNextTurn() jest wywoływane TYLKO po wyborze roli.

            if (IsDraftActive && AmongUsClient.Instance.AmHost)
            {
                if (CurrentTurnPlayerId != 255)
                {
                    TurnWatchdogTimer += Time.unscaledDeltaTime;
                    
                    // Synchronizuj timer co 0.5s dla płynności
                    if (Mathf.FloorToInt(TurnWatchdogTimer * 2) != Mathf.FloorToInt((TurnWatchdogTimer - Time.unscaledDeltaTime) * 2))
                    {
                        DraftManager.SendTimerSyncRpc(TurnWatchdogTimer);
                    }
                    
                    if (TurnWatchdogTimer >= MaxTurnTime)
                    {
                        DraftPlugin.Instance.Log.LogWarning($"[Watchdog] Timeout gracza {CurrentTurnPlayerId}. Auto-pick.");
                        TurnWatchdogTimer = 0f;
                        DraftManager.ForceSkipTurn(); // To teraz wylosuje rolę!
                    }
                }
            }

            var state = AmongUsClient.Instance.GameState;
            if (state == InnerNetClient.GameStates.NotJoined || state == InnerNetClient.GameStates.Ended)
            {
                if (IsDraftActive || _wasPaused) ForceUnfreeze();
                IsDraftActive = false;
                // USUNIĘTO: HostTimerActive = false; - pole już nie istnieje
                return;
            }

            if (IsDraftActive)
            {
                if (Time.timeScale != 0f) { Time.timeScale = 0f; _wasPaused = true; }
                if (PlayerControl.LocalPlayer != null) PlayerControl.LocalPlayer.moveable = false;
            }
            else if (_wasPaused)
            {
                ForceUnfreeze();
            }
        }

        private void ForceUnfreeze()
        {
            Time.timeScale = 1f;
            _wasPaused = false;
            if (PlayerControl.LocalPlayer != null) PlayerControl.LocalPlayer.moveable = true;
        }

        // Style cache - tworzenie raz zamiast w każdej klatce
        private GUIStyle _titleStyle;
        private GUIStyle _btnStyle;
        private GUIStyle _waitStyle;
        private GUIStyle _processingStyle;

        private void InitializeStyles()
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label) 
                { 
                    alignment = TextAnchor.MiddleCenter, 
                    fontSize = 36, 
                    fontStyle = FontStyle.Bold 
                };
                _titleStyle.normal.textColor = Color.white;

                _btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 24 };

                _waitStyle = new GUIStyle(GUI.skin.label) 
                { 
                    alignment = TextAnchor.MiddleCenter, 
                    fontSize = 48, 
                    fontStyle = FontStyle.Bold 
                };
                _waitStyle.normal.textColor = Color.yellow;

                _processingStyle = new GUIStyle(GUI.skin.label) 
                { 
                    alignment = TextAnchor.MiddleCenter, 
                    fontSize = 32, 
                    fontStyle = FontStyle.Bold 
                };
                _processingStyle.normal.textColor = Color.gray;
            }
        }

        private void OnGUI()
        {
            if (!IsDraftActive) return;

            InitializeStyles();

            GUI.depth = -9999;
            GUI.backgroundColor = new Color(0.05f, 0.05f, 0.1f, 0.98f); 
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

            if (ActiveTurnPlayerId == 255)
            {
                GUI.Label(new Rect(0, Screen.height/2 - 50, Screen.width, 100), "FINALIZACJA DRAFTU...", _processingStyle);
                return;
            }

            bool isMyTurn = (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.PlayerId == ActiveTurnPlayerId);
            
            string activeName = "Unknown";
            foreach(var p in PlayerControl.AllPlayerControls) 
                if(p.PlayerId == ActiveTurnPlayerId) activeName = p.Data.PlayerName;

            // Timer widoczny dla wszystkich graczy (synchronizowany przez RPC)
            string timeLeft = $" ({Mathf.Ceil(MaxTurnTime - TurnWatchdogTimer)}s)";

            if (isMyTurn)
            {
                GUI.Label(new Rect(0, 50, Screen.width, 50), $"TWOJA TURA: {CategoryTitle}{timeLeft}", _titleStyle);
                
                float w = 600;
                float x = (Screen.width - w) / 2;

                if (MyOptions != null)
                {
                    for(int i=0; i<MyOptions.Count; i++)
                    {
                        string option = MyOptions[i];
                        bool isEmpty = option == "NO_OPTION";
                        
                        string display = isEmpty ? "BRAK" : option.Replace("Role", "");
                        
                        // Zapisz obecny stan GUI.enabled
                        bool prevEnabled = GUI.enabled;
                        
                        // Jeśli pusta opcja -> zablokuj przycisk
                        if (isEmpty) GUI.enabled = false;

                        if (GUI.Button(new Rect(x, 150 + (i * 100), w, 80), display, _btnStyle))
                        {
                            if (!isEmpty) 
                            {
                                DraftManager.OnPlayerSelectedRole(option);
                            }
                        }
                        
                        // Przywróć stan GUI.enabled
                        GUI.enabled = prevEnabled;
                    }
                    GUI.backgroundColor = new Color(0.7f, 0.2f, 0.2f);
                    if (GUI.Button(new Rect(x, 500, w, 80), "LOSUJ (RANDOM)", _btnStyle))
                    {
                        DraftManager.OnRandomRoleSelected();
                    }
                }
            }
            else
            {
                string dots = ""; int t = (int)(Time.unscaledTime * 2) % 4; for(int i=0; i<t; i++) dots += ".";
                GUI.Label(new Rect(0, Screen.height/2 - 50, Screen.width, 100), $"WYBIERA: {activeName}{dots}{timeLeft}", _waitStyle);
            }
        }
    }
}