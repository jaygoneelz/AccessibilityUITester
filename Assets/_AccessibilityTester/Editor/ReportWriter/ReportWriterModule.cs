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
    /// - Play Mode: enters Play Mode, waits for gameplay scripts to
    ///   settle, then scans against the actual runtime camera state, and
    ///   automatically returns to Edit Mode afterward. Uses
    ///   EditorApplication.playModeStateChanged rather than manually
    ///   tracked instance state, since entering Play Mode triggers a
    ///   domain reload by default, which destroys and recreates this
    ///   module (and any state/subscriptions held only on the instance) —
    ///   playModeStateChanged is re-subscribed fresh in OnEnable() after
    ///   any such reload, avoiding a lost-subscription race.
    /// </summary>
    public class ReportWriterModule
    {
        private const string OutputFolder = "AccessibilityReports";
        private const int PlayModeSettleFrames = 30;
        private const string PendingScanPrefKey = "AccessibilityTester.PendingPlayModeScan";

        private readonly CoreManager _coreManager;
        private AccessibilityThresholds _thresholds;
        private string _lastReportPath;

        private int _framesWaited;
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

            // Persist the "a scan is pending" intent via EditorPrefs, since
            // entering Play Mode triggers a domain reload that destroys
            // this object's in-memory state. EditorPrefs survives reloads.
            EditorPrefs.SetBool(PendingScanPrefKey, true);
            Debug.Log($"[AccessibilityTester] Entering Play Mode to scan with correct runtime camera state (will wait {PlayModeSettleFrames} frames to settle)...");
            EditorApplication.EnterPlaymode();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(PendingScanPrefKey, false))
            {
                _framesWaited = 0;
                _waitingForSettle = true;
            }
        }

        private void OnEditorUpdate()
        {
            if (!_waitingForSettle || !Application.isPlaying) return;

            _framesWaited++;
            if (_framesWaited < PlayModeSettleFrames) return;

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
            report.playModeSettleFrames = Application.isPlaying ? PlayModeSettleFrames : 0;

            string json = JsonUtility.ToJson(report, prettyPrint: true);

            string directory = Path.Combine(Application.dataPath, "..", OutputFolder);
            Directory.CreateDirectory(directory);

            string fileName = $"{report.sceneName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
            string fullPath = Path.Combine(directory, fileName);
            File.WriteAllText(fullPath, json);

            _lastReportPath = fullPath;

            string modeNote = report.scannedInPlayMode
                ? $"Play Mode scan (settled {PlayModeSettleFrames} frames)"
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