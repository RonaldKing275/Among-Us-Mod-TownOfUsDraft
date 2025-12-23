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

        // Ta metoda sprawdza się co klatkę
        private void Update()
        {
            // Reset przy wyjściu do lobby
            if (AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.NotJoined)
            {
                IsActive = false;
            }

            // Blokada ruchu, jeśli menu otwarte
            if (IsActive && PlayerControl.LocalPlayer != null)
            {
                PlayerControl.LocalPlayer.moveable = false;
            }
        }

        // Rysowanie interfejsu
        private void OnGUI()
        {
            if (!IsActive) return;

            GUI.depth = 0; 
            
            float w = 500, h = 450;
            float x = (Screen.width - w) / 2;
            float y = (Screen.height - h) / 2;

            GUI.backgroundColor = Color.black;
            GUI.Box(new Rect(x, y, w, h), "");

            GUIStyle headStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            headStyle.normal.textColor = Color.yellow;
            GUI.Label(new Rect(x, y + 20, w, 60), $"KATEGORIA:\n{CategoryTitle}", headStyle);

            GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 24 };
            btnStyle.normal.textColor = Color.white;
            btnStyle.hover.textColor = Color.cyan;

            if (MyOptions != null)
            {
                for (int i = 0; i < MyOptions.Count; i++)
                {
                    string role = MyOptions[i];
                    string display = role.Replace("Role", "");
                    
                    if (GUI.Button(new Rect(x + 50, y + 120 + (i * 90), w - 100, 70), display, btnStyle))
                    {
                        DraftPlugin.Instance.Log.LogInfo($"[UI] Kliknięto: {role}");
                        DraftManager.OnPlayerSelectedRole(role);
                        IsActive = false; 
                    }
                }
            }
        }
    }
}