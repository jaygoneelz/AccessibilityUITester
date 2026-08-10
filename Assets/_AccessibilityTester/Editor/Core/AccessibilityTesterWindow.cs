using UnityEditor;
using UnityEngine;

namespace AccessibilityTester.Editor.Core
{
    public class AccessibilityTesterWindow : EditorWindow
    {
        private CoreManager _coreManager;

        [MenuItem("Window/Accessibility UI Tester")]
        public static void ShowWindow()
        {
            var window = GetWindow<AccessibilityTesterWindow>("Accessibility UI Tester");
            window.minSize = new Vector2(320, 240);
        }

        private void OnEnable()
        {
            _coreManager = new CoreManager();
        }

        private void OnDisable()
        {
            _coreManager?.Dispose();
        }

        private void OnGUI()
        {
            GUILayout.Label("Accessibility UI Tester", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("Toggle CVD Simulation"))
                _coreManager.RequestToggleCvdSimulation();

            if (GUILayout.Button("Run Contrast Scan"))
                _coreManager.RequestContrastScan();

            if (GUILayout.Button("Generate Report"))
                _coreManager.RequestGenerateReport();
        }
    }
}