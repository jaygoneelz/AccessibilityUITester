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
    /// Bridges the CoreManager event bus to SceneReportScanner and writes the
    /// resulting SceneReport as JSON in the project's output folder.
    ///
    /// Edit Mode scans run immediately. If the Editor is already in Play Mode,
    /// the scan runs against whatever state you have navigated to, which is the
    /// reliable path for games with menus or loading screens. Otherwise the tool
    /// can enter Play Mode, wait PlayModeSettleSeconds, scan, and exit. That only
    /// reaches gameplay in scenes with no menu gating, so it asks for
    /// confirmation first.
    ///
    /// The pending-scan flag lives in SessionState so it survives the domain
    /// reload that entering Play Mode triggers. It clears if Play Mode exits
    /// early and expires if unused, so it can't affect a later session.
    /// </summary>
    public class ReportWriterModule
    {
        private const string OutputFolder = "AccessibilityReports";
        private const float PlayModeSettleSeconds = 10f;
        private const string PendingScanSessionKey = "AccessibilityTester.PendingPlayModeScan";
        private const string PendingScanRequestedAtSessionKey = "AccessibilityTester.PendingPlayModeScan.RequestedAt";
        private const double PendingScanAbandonAfterSeconds = 120; // guards against a cancelled/never-completed Play Mode entry leaving this set forever

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

            if (Application.isPlaying)
            {
                // Already playing: scan immediately against whatever state
                // the developer has navigated to. No settle wait needed,
                // since a human already got the game to the right state.
                RunScanAndSave();
                return;
            }

            if (!ScanInPlayMode)
            {
                RunScanAndSave();
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Scan in Play Mode",
                "This will enter Play Mode, wait " + PlayModeSettleSeconds.ToString("F0") +
                " seconds, then scan automatically and return to Edit Mode.\n\n" +
                "This only reaches genuine gameplay state for scenes with no menu or " +
                "loading screen to get through first. For a game with a main menu or " +
                "start screen, automatic entry will scan whatever the menu screen shows, " +
                "not gameplay.\n\n" +
                "For menu-gated games: click Cancel, enter Play Mode yourself, navigate " +
                "to the state you want evaluated, then click Generate Report again. " +
                "It will scan immediately with no wait.",
                "Proceed Automatically",
                "Cancel");

            if (!proceed) return;

            SessionState.SetBool(PendingScanSessionKey, true);
            SessionState.SetFloat(PendingScanRequestedAtSessionKey, (float)EditorApplication.timeSinceStartup);
            Debug.Log($"[AccessibilityTester] Entering Play Mode to scan with correct runtime camera state (will wait {PlayModeSettleSeconds:F0}s for gameplay/loading to settle)...");
            EditorApplication.EnterPlaymode();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (!SessionState.GetBool(PendingScanSessionKey, false)) return;

                double requestedAt = SessionState.GetFloat(PendingScanRequestedAtSessionKey, 0f);
                double age = EditorApplication.timeSinceStartup - requestedAt;
                if (age > PendingScanAbandonAfterSeconds)
                {
                    // Stale request from an earlier, abandoned attempt (e.g.
                    // Play Mode entry was cancelled last time). Don't act on it.
                    SessionState.SetBool(PendingScanSessionKey, false);
                    return;
                }

                _settleStartTime = EditorApplication.timeSinceStartup;
                _waitingForSettle = true;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode && _waitingForSettle)
            {
                // Play Mode was stopped (manually, or otherwise) before the
                // settle wait completed. Cancel the pending scan instead of
                // leaving it set for a future, unrelated Play Mode session to
                // trip over.
                _waitingForSettle = false;
                SessionState.SetBool(PendingScanSessionKey, false);
            }
        }

        private void OnEditorUpdate()
        {
            if (!_waitingForSettle || !Application.isPlaying) return;

            double elapsed = EditorApplication.timeSinceStartup - _settleStartTime;
            if (elapsed < PlayModeSettleSeconds) return;

            _waitingForSettle = false;
            SessionState.SetBool(PendingScanSessionKey, false);

            RunScanAndSave();
            EditorApplication.ExitPlaymode();
        }

        private void RunScanAndSave()
        {
            var stopwatch = Stopwatch.StartNew();
            SceneReport report = SceneReportScanner.Scan(_thresholds);
            stopwatch.Stop();

            report.scannedInPlayMode = Application.isPlaying;
            report.playModeSettleSecondsUsed = Application.isPlaying ? PlayModeSettleSeconds : 0f;

            string json = JsonUtility.ToJson(report, prettyPrint: true);

            string directory = Path.Combine(Application.dataPath, "..", OutputFolder);
            Directory.CreateDirectory(directory);

            string fileName = $"{report.sceneName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
            string fullPath = Path.Combine(directory, fileName);
            File.WriteAllText(fullPath, json);

            _lastReportPath = fullPath;

            string modeNote = report.scannedInPlayMode
                ? "Play Mode scan"
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