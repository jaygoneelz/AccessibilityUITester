using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using AccessibilityTester.Editor.ShaderController;

namespace AccessibilityTester.Editor.Core
{
    public class AccessibilityTesterWindow : EditorWindow
    {
        private CoreManager _coreManager;
        private ShaderControllerModule _shaderControllerModule;
        private UniversalRendererData _rendererData;

        [MenuItem("Window/Accessibility UI Tester")]
        public static void ShowWindow()
        {
            var window = GetWindow<AccessibilityTesterWindow>("Accessibility UI Tester");
            window.minSize = new Vector2(320, 240);
        }

        private void OnEnable()
        {
            _coreManager = new CoreManager();
            _shaderControllerModule = new ShaderControllerModule(_coreManager, _rendererData);
        }

        private void OnDisable()
        {
            _shaderControllerModule?.Dispose();
            _coreManager?.Dispose();
        }

        private void OnGUI()
        {
            GUILayout.Label("Accessibility UI Tester", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            _rendererData = (UniversalRendererData)EditorGUILayout.ObjectField(
                "URP Renderer Data", _rendererData, typeof(UniversalRendererData), false);
            if (EditorGUI.EndChangeCheck())
            {
                _shaderControllerModule.SetRendererData(_rendererData);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button($"Toggle CVD Simulation ({_shaderControllerModule.CurrentStateLabel})"))
                _coreManager.RequestToggleCvdSimulation();

            if (GUILayout.Button("Run Contrast Scan"))
                _coreManager.RequestContrastScan();

            if (GUILayout.Button("Generate Report"))
                _coreManager.RequestGenerateReport();
        }
    }
}