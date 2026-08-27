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
    /// (Runtime, pure logic). Runs in Edit Mode — no Play Mode required.
    /// Serializes the resulting SceneReport to JSON in the project's
    /// output folder.
    /// </summary>
    public class ReportWriterModule
    {
        private const string OutputFolder = "AccessibilityReports";

        private readonly CoreManager _coreManager;
        private AccessibilityThresholds _thresholds;
        private string _lastReportPath;

        public ReportWriterModule(CoreManager coreManager, AccessibilityThresholds thresholds)
        {
            _coreManager = coreManager;
            _thresholds = thresholds;
            _coreManager.OnGenerateReportRequested += HandleGenerateReport;
        }

        public void SetThresholds(AccessibilityThresholds thresholds) => _thresholds = thresholds;

        public string LastReportPath => _lastReportPath;

        private void HandleGenerateReport()
        {
            if (_thresholds == null)
            {
                Debug.LogWarning("[AccessibilityTester] No AccessibilityThresholds asset assigned. Drag one into the tool window first.");
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            SceneReport report = SceneReportScanner.Scan(_thresholds);
            stopwatch.Stop();

            string json = JsonUtility.ToJson(report, prettyPrint: true);

            string directory = Path.Combine(Application.dataPath, "..", OutputFolder);
            Directory.CreateDirectory(directory);

            string fileName = $"{report.sceneName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
            string fullPath = Path.Combine(directory, fileName);
            File.WriteAllText(fullPath, json);

            _lastReportPath = fullPath;

            Debug.Log($"[AccessibilityTester] Report generated: {report.totalElementsScanned} elements scanned, " +
                      $"{report.totalFailures} failure(s), scan took {stopwatch.ElapsedMilliseconds}ms. Saved to {fullPath}");
        }

        public void Dispose()
        {
            _coreManager.OnGenerateReportRequested -= HandleGenerateReport;
        }
    }
}