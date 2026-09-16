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
    /// - Play Mode: two ways to trigger this, depending on whether the
    ///   Editor is already playing.
    ///     - Already in Play Mode: scans immediately, against whatever
    ///       state the developer has navigated the game to themselves.
    ///       This is the reliable path for any game with a menu, loading
    ///       screen, or other flow that must be navigated through before
    ///       reaching the state worth evaluating — this tool has no
    ///       generic way to know how to click through an arbitrary
    ///       project's own menu system.
    ///     - Not yet in Play Mode: can optionally auto-enter Play Mode,
    ///       wait (by wall-clock time, not frame count, since fps varies)
    ///       for gameplay scripts to settle, then scan and automatically
    ///       return to Edit Mode. This only reaches genuine gameplay
    ///       state for scenes with no menu gating (e.g. a single test
    ///       scene) — the tool asks for confirmation before doing this,
    ///       since for a menu-gated game it will scan the menu, not
    ///       gameplay. Uses EditorApplication.playModeStateChanged rather
    ///       than manually tracked instance state, since entering Play
    ///       Mode triggers a domain reload by default, which destroys and
    ///       recreates this module — playModeStateChanged is re-subscribed
    ///       fresh in OnEnable() after any such reload. The pending-scan
    ///       flag itself is stored in SessionState (not EditorPrefs), since
    ///       EditorPrefs is machine-global rather than project-scoped and
    ///       would otherwise leak a stale "pending scan" flag across
    ///       different Unity projects; it also self-clears if Play Mode is
    ///       exited before the settle wait completes, and expires if left
    ///       unconsumed for too long (e.g. a cancelled Play Mode entry),
    ///       so it can never silently hijack an unrelated future session.
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
                "to the state you want evaluated, then click Generate Report again — " +
                "it will scan immediately with no wait.",
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
                    // Play Mode entry was cancelled last time) — don't act on it.
                    SessionState.SetBool(PendingScanSessionKey, false);
                    return;
                }

                _settleStartTime = EditorApplication.timeSinceStartup;
                _waitingForSettle = true;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode && _waitingForSettle)
            {
                // Play Mode was stopped (manually, or otherwise) before the
                // settle wait completed — cancel the pending scan instead of
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
            report.playModeSettleFrames = Application.isPlaying ? Mathf.RoundToInt(PlayModeSettleSeconds * 60f) : 0;

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