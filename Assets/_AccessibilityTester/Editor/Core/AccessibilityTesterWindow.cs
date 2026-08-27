using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using AccessibilityTester.Editor.ShaderController;
using AccessibilityTester.Editor.ContrastMeter;
using AccessibilityTester.Editor.ReportWriter;
using AccessibilityTester.Editor.FontScaler;
using AccessibilityTester.Runtime.ReportWriter;

namespace AccessibilityTester.Editor.Core
{
    /// <summary>
    /// Top-level EditorWindow for the Accessibility UI Tester. Owns the
    /// CoreManager event bus and composes all four module bridges (Shader
    /// Controller, Contrast Meter, Report Writer, Font Scaler) into a
    /// single UI. Persists the assigned URP Renderer Data and Accessibility
    /// Thresholds assets across editor sessions via EditorPrefs.
    /// </summary>
    public class AccessibilityTesterWindow : EditorWindow
    {
        private const string RendererDataPrefKey = "AccessibilityTester.RendererDataPath";
        private const string ThresholdsPrefKey = "AccessibilityTester.ThresholdsPath";

        private CoreManager _coreManager;
        private ShaderControllerModule _shaderControllerModule;
        private ContrastMeterModule _contrastMeterModule;
        private ReportWriterModule _reportWriterModule;
        private FontScalerModule _fontScalerModule;
        private UniversalRendererData _rendererData;
        private AccessibilityThresholds _thresholds;
        private float _fontScaleFactor = 2.0f;

        [MenuItem("Window/Accessibility UI Tester")]
        public static void ShowWindow()
        {
            var window = GetWindow<AccessibilityTesterWindow>("Accessibility UI Tester");
            window.minSize = new Vector2(320, 460);
        }

        private void OnEnable()
        {
            _coreManager = new CoreManager();
            LoadRendererDataFromPrefs();
            LoadThresholdsFromPrefs();
            _shaderControllerModule = new ShaderControllerModule(_coreManager, _rendererData);
            _contrastMeterModule = new ContrastMeterModule(_coreManager);
            _reportWriterModule = new ReportWriterModule(_coreManager, _thresholds);
            _fontScalerModule = new FontScalerModule(_coreManager);
        }

        private void OnDisable()
        {
            _fontScalerModule?.Dispose();
            _reportWriterModule?.Dispose();
            _contrastMeterModule?.Dispose();
            _shaderControllerModule?.Dispose();
            _coreManager?.Dispose();
        }

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
            GUILayout.Label("Report Writer", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _thresholds = (AccessibilityThresholds)EditorGUILayout.ObjectField(
                "Thresholds", _thresholds, typeof(AccessibilityThresholds), false);
            if (EditorGUI.EndChangeCheck())
            {
                _reportWriterModule.SetThresholds(_thresholds);
                SaveThresholdsToPrefs();
            }

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