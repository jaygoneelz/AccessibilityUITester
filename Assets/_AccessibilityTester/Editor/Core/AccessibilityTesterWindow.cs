using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using AccessibilityTester.Editor.ShaderController;
using AccessibilityTester.Editor.ContrastMeter;

namespace AccessibilityTester.Editor.Core
{
    public class AccessibilityTesterWindow : EditorWindow
    {
        private const string RendererDataPrefKey = "AccessibilityTester.RendererDataPath";

        private CoreManager _coreManager;
        private ShaderControllerModule _shaderControllerModule;
        private ContrastMeterModule _contrastMeterModule;
        private UniversalRendererData _rendererData;

        [MenuItem("Window/Accessibility UI Tester")]
        public static void ShowWindow()
        {
            var window = GetWindow<AccessibilityTesterWindow>("Accessibility UI Tester");
            window.minSize = new Vector2(320, 300);
        }

        private void OnEnable()
        {
            _coreManager = new CoreManager();
            LoadRendererDataFromPrefs();
            _shaderControllerModule = new ShaderControllerModule(_coreManager, _rendererData);
            _contrastMeterModule = new ContrastMeterModule(_coreManager);
        }

        private void OnDisable()
        {
            _contrastMeterModule?.Dispose();
            _shaderControllerModule?.Dispose();
            _coreManager?.Dispose();
        }

        private void LoadRendererDataFromPrefs()
        {
            string path = EditorPrefs.GetString(RendererDataPrefKey, string.Empty);
            if (!string.IsNullOrEmpty(path))
            {
                _rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            }
        }

        private void SaveRendererDataToPrefs()
        {
            if (_rendererData != null)
            {
                string path = AssetDatabase.GetAssetPath(_rendererData);
                EditorPrefs.SetString(RendererDataPrefKey, path);
            }
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
                SaveRendererDataToPrefs();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button($"Toggle CVD Simulation ({_shaderControllerModule.CurrentStateLabel})"))
                _coreManager.RequestToggleCvdSimulation();

            if (GUILayout.Button($"Toggle Low Vision Blur ({(_shaderControllerModule.BlurEnabled ? "On" : "Off")})"))
                _coreManager.RequestToggleBlur();

            EditorGUILayout.Space();

            if (GUILayout.Button($"Toggle Contrast Meter ({(_contrastMeterModule.IsActive ? "On" : "Off")})"))
                _coreManager.RequestToggleContrastMeter();
            EditorGUILayout.HelpBox("Contrast Meter requires Play Mode.", MessageType.Info);

            EditorGUILayout.Space();

            if (GUILayout.Button("Run Contrast Scan"))
                _coreManager.RequestContrastScan();

            if (GUILayout.Button("Generate Report"))
                _coreManager.RequestGenerateReport();

            EditorGUILayout.Space();
            GUILayout.Label("Diagnostics", EditorStyles.boldLabel);
            FpsLogger.Enabled = EditorGUILayout.ToggleLeft("Log fps to Console/CSV during Play Mode", FpsLogger.Enabled);
        }
    }
}