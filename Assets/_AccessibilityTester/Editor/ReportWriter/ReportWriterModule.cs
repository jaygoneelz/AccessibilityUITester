using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using AccessibilityTester.Runtime.ReportWriter;
using AccessibilityTester.Editor.Core;
using Debug = UnityEngine.Debug;

namespace AccessibilityTester.Editor.ReportWriter
{
    /// <summary>
    /// Bridges the CoreManager event bus (Editor) to SceneReportScanner
    /// (Runtime, pure logic). Serializes the resulting SceneReport to
    /// JSON in the project's output folder.
    ///
    /// Supports two scan modes:
    /// - Edit Mode (default): fast, but scenes using runtime camera-follow
    ///   logic can produce misleading background estimates.
    /// - Play Mode: enters Play Mode, waits (by wall-clock time, not
    ///   frame count, since fps varies) for gameplay scripts and any
    ///   loading/transition screens to settle, then scans against the
    ///   actual runtime camera state, and automatically returns to Edit
    ///   Mode afterward. Uses EditorApplication.playModeStateChanged
    ///   rather than manually tracked instance state, since entering Play
    ///   Mode triggers a domain reload by default, which destroys and
    ///   recreates this module — playModeStateChanged is re-subscribed
    ///   fresh in OnEnable() after any such reload.
    /// </summary>
    public class ReportWriterModule
    {
        private const string OutputFolder = "AccessibilityReports";
        private const float PlayModeSettleSeconds = 10f;
        private const string PendingScanPrefKey = "AccessibilityTester.PendingPlayModeScan";

        private readonly CoreManager _coreManager;
        private AccessibilityThresholds _thresholds;
        private string _lastReportPath;

        private double _settleStartTime;
        private bool _waitingForSettle;

        public ReportWriterModule(CoreManager coreManager, AccessibilityThresholds thresholds)
        {
            _coreManager = coreManager;
            _thresholds = thresholds;
            _coreManager.OnGenerateReportRequested += HandleGenerateReport;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
        }

        public void SetThresholds(AccessibilityThresholds thresholds) => _thresholds = thresholds;

        public string LastReportPath => _lastReportPath;

        public bool ScanInPlayMode { get; set; } = false;

        private void HandleGenerateReport()
        {
            if (_thresholds == null)
            {
                Debug.LogWarning("[AccessibilityTester] No AccessibilityThresholds asset assigned. Drag one into the tool window first.");
                return;
            }

            if (!ScanInPlayMode || Application.isPlaying)
            {
                RunScanAndSave();
                return;
            }

            EditorPrefs.SetBool(PendingScanPrefKey, true);
            Debug.Log($"[AccessibilityTester] Entering Play Mode to scan with correct runtime camera state (will wait {PlayModeSettleSeconds:F0}s for gameplay/loading to settle)...");
            EditorApplication.EnterPlaymode();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(PendingScanPrefKey, false))
            {
                _settleStartTime = EditorApplication.timeSinceStartup;
                _waitingForSettle = true;
            }
        }

        private void OnEditorUpdate()
        {
            if (!_waitingForSettle || !Application.isPlaying) return;

            double elapsed = EditorApplication.timeSinceStartup - _settleStartTime;
            if (elapsed < PlayModeSettleSeconds) return;

            _waitingForSettle = false;
            EditorPrefs.SetBool(PendingScanPrefKey, false);

            RunScanAndSave();
            EditorApplication.ExitPlaymode();
        }

        private void RunScanAndSave()
        {
            var stopwatch = Stopwatch.StartNew();
            SceneReport report = SceneReportScanner.Scan(_thresholds);
            stopwatch.Stop();

            report.scannedInPlayMode = Application.isPlaying;
            report.playModeSettleFrames = Application.isPlaying ? Mathf.RoundToInt(PlayModeSettleSeconds * 60f) : 0;

            string json = JsonUtility.ToJson(report, prettyPrint: true);

            string directory = Path.Combine(Application.dataPath, "..", OutputFolder);
            Directory.CreateDirectory(directory);

            string fileName = $"{report.sceneName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
            string fullPath = Path.Combine(directory, fileName);
            File.WriteAllText(fullPath, json);

            _lastReportPath = fullPath;

            string modeNote = report.scannedInPlayMode
                ? $"Play Mode scan (settled {PlayModeSettleSeconds:F0}s)"
                : "Edit Mode scan";

            Debug.Log($"[AccessibilityTester] Report generated ({modeNote}): {report.totalElementsScanned} elements scanned, " +
                      $"{report.totalFailures} failure(s), scan took {stopwatch.ElapsedMilliseconds}ms. Saved to {fullPath}");
        }

        public void Dispose()
        {
            _coreManager.OnGenerateReportRequested -= HandleGenerateReport;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
        }
    }
}