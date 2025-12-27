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
            _buttonStyle.normal.textColor = Color.yellow;
            _stylesInit = true;
        }

        private void OnGUI()
        {
            if (!ShowHud) return;
            if (DraftManager.Instance == null || !DraftManager.Instance.IsDraftActive) return;

            InitStyles();

            float width = 600; 
            float height = 400;
            float x = (Screen.width - width) / 2;
            float y = (Screen.height - height) / 2;

            GUI.color = new Color(0, 0, 0, 0.9f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(x, y, width, height));
            GUILayout.Label("DRAFT MODE", _buttonStyle); 
            
            if (_currentOptions.Count > 0)
            {
                GUILayout.Label("Wybierz swoją rolę:", _buttonStyle);
                foreach (var role in _currentOptions)
                {
                    if (GUILayout.Button(role, GUILayout.Height(50)))
                    {
                        // Wywołanie metody z menedżera
                        DraftManager.Instance.OnPlayerSelectedRole(role); 
                        _currentOptions.Clear(); 
                        ShowHud = false; // Schowaj po wyborze
                    }
                }
            }
            else
            {
                GUILayout.Label("Oczekiwanie na innych...", _buttonStyle);
            }
            
            GUILayout.EndArea();
        }
    }
}