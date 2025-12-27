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
        private GUIStyle _randomButtonStyle; // Styl dla przycisku Random
        private bool _stylesInit = false;

        private void Awake()
        {
            Instance = this;
        }

        public void ShowSelection(List<string> options)
        {
            _currentOptions = options;
            ShowHud = true;
        }

        private void InitStyles()
        {
            if (_stylesInit) return;
            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = Texture2D.whiteTexture;

            _buttonStyle = new GUIStyle(GUI.skin.button);
            _buttonStyle.fontSize = 20;
            _buttonStyle.normal.textColor = Color.white;
            
            _randomButtonStyle = new GUIStyle(GUI.skin.button);
            _randomButtonStyle.fontSize = 20;
            _randomButtonStyle.normal.textColor = Color.cyan; // Wyróżniony kolor
            _randomButtonStyle.fontStyle = FontStyle.Bold;

            _stylesInit = true;
        }

        private void OnGUI()
        {
            if (!ShowHud) return;
            if (DraftManager.Instance == null || !DraftManager.Instance.IsDraftActive) return;

            InitStyles();

            float width = 600; 
            float height = 500; // Trochę wyższe bo 4 przyciski
            float x = (Screen.width - width) / 2;
            float y = (Screen.height - height) / 2;

            GUI.color = new Color(0, 0, 0, 0.9f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(x, y, width, height));
            GUILayout.Label("WYBIERZ SWOJĄ ROLĘ", _buttonStyle); 
            
            if (_currentOptions.Count > 0)
            {
                foreach (var role in _currentOptions)
                {
                    // Specjalna obsługa przycisku Random
                    if (role == "Random")
                    {
                        GUILayout.Space(10);
                        if (GUILayout.Button("??? LOSOWA ROLA Z KATEGORII ???", _randomButtonStyle, GUILayout.Height(60)))
                        {
                            DraftManager.Instance.OnPlayerSelectedRole("Random"); 
                            ShowHud = false;
                        }
                    }
                    else
                    {
                        if (GUILayout.Button(role, _buttonStyle, GUILayout.Height(50)))
                        {
                            DraftManager.Instance.OnPlayerSelectedRole(role); 
                            ShowHud = false; 
                        }
                    }
                }
            }
            else
            {
                GUILayout.Label("Oczekiwanie na turę...", _buttonStyle);
            }
            
            GUILayout.EndArea();
        }
    }
}