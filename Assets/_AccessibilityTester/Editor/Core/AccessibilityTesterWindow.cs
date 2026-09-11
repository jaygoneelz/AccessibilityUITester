using UnityEditor;
using UnityEngine;
using AccessibilityTester.Editor.ContrastMeter;
using AccessibilityTester.Editor.ReportWriter;
using AccessibilityTester.Editor.FontScaler;
using AccessibilityTester.Runtime.ReportWriter;
#if ACCESSIBILITY_TESTER_URP
using UnityEngine.Rendering.Universal;
using AccessibilityTester.Editor.ShaderController;
#endif

namespace AccessibilityTester.Editor.Core
{
    public class AccessibilityTesterWindow : EditorWindow
    {
#if ACCESSIBILITY_TESTER_URP
        private const string RendererDataPrefKey = "AccessibilityTester.RendererDataPath";
#endif
        private const string ThresholdsPrefKey = "AccessibilityTester.ThresholdsPath";

        private CoreManager _coreManager;
#if ACCESSIBILITY_TESTER_URP
        private ShaderControllerModule _shaderControllerModule;
        private UniversalRendererData _rendererData;
#endif
        private ContrastMeterModule _contrastMeterModule;
        private ReportWriterModule _reportWriterModule;
        private FontScalerModule _fontScalerModule;
        private AccessibilityThresholds _thresholds;
        private float _fontScaleFactor = 2.0f;

        [MenuItem("Window/Accessibility UI Tester")]
        public static void ShowWindow()
        {
            var window = GetWindow<AccessibilityTesterWindow>("Accessibility UI Tester");
            window.minSize = new Vector2(320, 420);
        }

        private void OnEnable()
        {
            _coreManager = new CoreManager();
            LoadThresholdsFromPrefs();

#if ACCESSIBILITY_TESTER_URP
            LoadRendererDataFromPrefs();
            _shaderControllerModule = new ShaderControllerModule(_coreManager, _rendererData);
#endif
            _contrastMeterModule = new ContrastMeterModule(_coreManager);
            _reportWriterModule = new ReportWriterModule(_coreManager, _thresholds);
            _fontScalerModule = new FontScalerModule(_coreManager);
        }

        private void OnDisable()
        {
            _fontScalerModule?.Dispose();
            _reportWriterModule?.Dispose();
            _contrastMeterModule?.Dispose();
#if ACCESSIBILITY_TESTER_URP
            _shaderControllerModule?.Dispose();
#endif
            _coreManager?.Dispose();
        }

#if ACCESSIBILITY_TESTER_URP
        private void LoadRendererDataFromPrefs()
        {
            string path = EditorPrefs.GetString(RendererDataPrefKey, string.Empty);
            if (!string.IsNullOrEmpty(path))
                _rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
        }

        private void SaveRendererDataToPrefs()
        {
            if (_rendererData != null)
                EditorPrefs.SetString(RendererDataPrefKey, AssetDatabase.GetAssetPath(_rendererData));
        }
#endif

        private void LoadThresholdsFromPrefs()
        {
            string path = EditorPrefs.GetString(ThresholdsPrefKey, string.Empty);
            if (!string.IsNullOrEmpty(path))
                _thresholds = AssetDatabase.LoadAssetAtPath<AccessibilityThresholds>(path);
        }

        private void SaveThresholdsToPrefs()
        {
            if (_thresholds != null)
                EditorPrefs.SetString(ThresholdsPrefKey, AssetDatabase.GetAssetPath(_thresholds));
        }

        private void OnGUI()
        {
            GUILayout.Label("Accessibility UI Tester", EditorStyles.boldLabel);
            EditorGUILayout.Space();

#if ACCESSIBILITY_TESTER_URP
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
#else
            EditorGUILayout.HelpBox("Shader Controller (CVD simulation, low vision blur) requires URP and is unavailable in this project.", MessageType.Info);
            EditorGUILayout.Space();
#endif

            if (GUILayout.Button($"Toggle Contrast Meter ({(_contrastMeterModule.IsActive ? "On" : "Off")})"))
                _coreManager.RequestToggleContrastMeter();
            EditorGUILayout.HelpBox("Contrast Meter requires Play Mode.", MessageType.Info);

            EditorGUILayout.Space();
            GUILayout.Label("Report Writer", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _thresholds = (AccessibilityThresholds)EditorGUILayout.ObjectField(
                "Thresholds", _thresholds, typeof(AccessibilityThresholds), false);
            if (EditorGUI.EndChangeCheck())
            {
                _reportWriterModule.SetThresholds(_thresholds);
                SaveThresholdsToPrefs();
            }

            _reportWriterModule.ScanInPlayMode = EditorGUILayout.ToggleLeft(
                "Scan in Play Mode (accurate for runtime camera-follow scenes; auto enters/exits Play Mode)",
                _reportWriterModule.ScanInPlayMode);

            if (GUILayout.Button("Generate Report"))
                _coreManager.RequestGenerateReport();

            if (!string.IsNullOrEmpty(_reportWriterModule.LastReportPath))
                EditorGUILayout.HelpBox($"Last report: {_reportWriterModule.LastReportPath}", MessageType.None);

            EditorGUILayout.Space();
            GUILayout.Label("Font Scaler", EditorStyles.boldLabel);

            _fontScaleFactor = EditorGUILayout.Slider("Scale Factor", _fontScaleFactor, 0.5f, 3f);

            if (GUILayout.Button($"Apply Font Scale ({(_fontScalerModule.IsScaled ? "Currently Scaled" : "Original")})"))
                _coreManager.RequestFontScale(_fontScaleFactor);

            if (GUILayout.Button("Revert Font Scale"))
                _fontScalerModule.Revert();

            EditorGUILayout.Space();
            GUILayout.Label("Diagnostics", EditorStyles.boldLabel);
            FpsLogger.Enabled = EditorGUILayout.ToggleLeft("Log fps to Console/CSV during Play Mode", FpsLogger.Enabled);
        }
    }
}