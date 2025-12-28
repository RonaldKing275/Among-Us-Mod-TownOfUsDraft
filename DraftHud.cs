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

        // Timer Hosta
        public static bool HostTimerActive = false;
        private float _hostTimer = 0f;

        // WATCHDOG
        public static float TurnWatchdogTimer = 0f;
        public static byte CurrentTurnPlayerId = 255;
        // NOWE POLE: Opcje aktualnego gracza (dla auto-picka)
        public static List<string> CurrentTurnOptions = new List<string>(); 
        private const float MAX_TURN_TIME = 20.0f; 

        private bool _wasPaused = false;

        private void Awake() { Instance = this; }

        private void Update()
        {
            if (HostTimerActive && AmongUsClient.Instance.AmHost)
            {
                _hostTimer += Time.unscaledDeltaTime;
                if (_hostTimer >= 0.5f)
                {
                    HostTimerActive = false;
                    _hostTimer = 0f;
                    DraftManager.ProcessNextTurn();
                }
            }

            if (IsDraftActive && AmongUsClient.Instance.AmHost && !HostTimerActive)
            {
                if (CurrentTurnPlayerId != 255)
                {
                    TurnWatchdogTimer += Time.unscaledDeltaTime;
                    if (TurnWatchdogTimer >= MAX_TURN_TIME)
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
                HostTimerActive = false;
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

        private void OnGUI()
        {
            if (!IsDraftActive) return;

            GUI.depth = -9999;
            GUI.backgroundColor = new Color(0.05f, 0.05f, 0.1f, 0.98f); 
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

            if (ActiveTurnPlayerId == 255)
            {
                GUIStyle processingStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 32, fontStyle = FontStyle.Bold };
                processingStyle.normal.textColor = Color.gray;
                GUI.Label(new Rect(0, Screen.height/2 - 50, Screen.width, 100), "FINALIZACJA DRAFTU...", processingStyle);
                return;
            }

            bool isMyTurn = (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.PlayerId == ActiveTurnPlayerId);
            
            string activeName = "Unknown";
            foreach(var p in PlayerControl.AllPlayerControls) 
                if(p.PlayerId == ActiveTurnPlayerId) activeName = p.Data.PlayerName;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 36, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = Color.white;

            string timeLeft = "";
            if (AmongUsClient.Instance.AmHost) 
                timeLeft = $" ({Mathf.Ceil(MAX_TURN_TIME - TurnWatchdogTimer)}s)";

            if (isMyTurn)
            {
                GUI.Label(new Rect(0, 50, Screen.width, 50), $"TWOJA TURA: {CategoryTitle}{timeLeft}", titleStyle);
                
                float w = 600;
                float x = (Screen.width - w) / 2;
                GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 24 };

                if (MyOptions != null)
                {
                    for(int i=0; i<MyOptions.Count; i++)
                    {
                        string display = MyOptions[i].Replace("Role", "");
                        if (GUI.Button(new Rect(x, 150 + (i * 100), w, 80), display, btnStyle))
                        {
                            DraftManager.OnPlayerSelectedRole(MyOptions[i]);
                        }
                    }
                    GUI.backgroundColor = new Color(0.7f, 0.2f, 0.2f);
                    if (GUI.Button(new Rect(x, 500, w, 80), "LOSUJ (RANDOM)", btnStyle))
                    {
                        DraftManager.OnRandomRoleSelected();
                    }
                }
            }
            else
            {
                GUIStyle waitStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 48, fontStyle = FontStyle.Bold };
                waitStyle.normal.textColor = Color.yellow;
                
                string dots = ""; int t = (int)(Time.unscaledTime * 2) % 4; for(int i=0; i<t; i++) dots += ".";
                GUI.Label(new Rect(0, Screen.height/2 - 100, Screen.width, 200), $"WYBIERA GRACZ: {activeName}{dots}", waitStyle);
            }
        }
    }
}