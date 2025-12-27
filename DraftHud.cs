using UnityEngine;
using System.Collections.Generic;

namespace TownOfUsDraft
{
    public class DraftHud : MonoBehaviour
    {
        public static DraftHud Instance;
        public bool ShowHud = false;
        private List<string> _currentOptions = new List<string>();
        
        private GUIStyle _boxStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _randomButtonStyle;
        private bool _stylesInit = false;

        private void Awake() { Instance = this; }

        public void ShowSelection(List<string> options) { _currentOptions = options; ShowHud = true; }

        private void InitStyles()
        {
            if (_stylesInit) return;
            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = Texture2D.whiteTexture;
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20 };
            _buttonStyle.normal.textColor = Color.white;
            _randomButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
            _randomButtonStyle.normal.textColor = Color.cyan;
            _stylesInit = true;
        }

        private void OnGUI()
        {
            if (!ShowHud || !DraftManager.IsDraftActive) return;

            InitStyles();

            float width = 600; 
            float height = 500;
            float x = (Screen.width - width) / 2;
            float y = (Screen.height - height) / 2;

            GUI.color = new Color(0, 0, 0, 0.95f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(x, y, width, height));
            GUILayout.Label("DRAFT MODE", _buttonStyle); 
            
            if (_currentOptions.Count > 0)
            {
                foreach (var role in _currentOptions)
                {
                    if (role == "Random")
                    {
                        GUILayout.Space(15);
                        if (GUILayout.Button("? LOSOWA ROLA ?", _randomButtonStyle, GUILayout.Height(60))) {
                            DraftManager.OnPlayerSelectedRole("Random"); 
                            ShowHud = false;
                        }
                    }
                    else
                    {
                        if (GUILayout.Button(role, _buttonStyle, GUILayout.Height(50))) {
                            DraftManager.OnPlayerSelectedRole(role); 
                            ShowHud = false; 
                        }
                    }
                }
            }
            else { GUILayout.Label("Oczekiwanie...", _buttonStyle); }
            GUILayout.EndArea();
        }
    }
}