using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AccessibilityTester.Runtime.ContrastMeter;

namespace AccessibilityTester.Runtime.ReportWriter
{
    /// <summary>
    /// Static-analysis scan of every Canvas in the currently loaded scene.
    /// Finds Text and TextMeshProUGUI elements, computes contrast against
    /// the nearest ancestor Graphic (falling back to white if none exists,
    /// since there is no camera-background concept outside Play Mode),
    /// checks font size, and produces a SceneReport. Runs in Edit Mode —
    /// no EventSystem/raycast dependency, unlike Contrast Meter.
    /// Scans inactive GameObjects too (e.g. UI screens toggled on/off by
    /// game logic, such as pause/start/end screens sharing one Canvas),
    /// so a single scan covers all UI states rather than only whichever
    /// screen happens to be active when the scan runs.
    /// </summary>
    public static class SceneReportScanner
    {
        public static SceneReport Scan(AccessibilityThresholds thresholds)
        {
            var report = new SceneReport
            {
                sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                scanTimestampUtc = DateTime.UtcNow.ToString("o"),
                minContrastRatioUsed = thresholds.minContrastRatio,
                minFontSizePointUsed = thresholds.minFontSizePoint
            };

            var elements = new List<ElementReport>();

            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                ScanCanvas(canvas, thresholds, elements);
            }

            report.elements = elements.ToArray();
            report.totalElementsScanned = elements.Count;
            report.totalFailures = elements.FindAll(e => !e.contrastPass || !e.fontSizePass).Count;

            return report;
        }

        private static void ScanCanvas(Canvas canvas, AccessibilityThresholds thresholds, List<ElementReport> results)
        {
            Text[] legacyTexts = canvas.GetComponentsInChildren<Text>(includeInactive: true);
            foreach (var text in legacyTexts)
            {
                results.Add(BuildReport(text.gameObject, text.color, text.fontSize, thresholds));
            }

            TextMeshProUGUI[] tmpTexts = canvas.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
            foreach (var tmp in tmpTexts)
            {
                results.Add(BuildReport(tmp.gameObject, tmp.color, tmp.fontSize, thresholds));
            }
        }

        private static ElementReport BuildReport(GameObject go, Color fgColor, float fontSize, AccessibilityThresholds thresholds)
        {
            Graphic backgroundGraphic = FindAncestorGraphic(go.transform);
            Color bgColor = backgroundGraphic != null ? backgroundGraphic.color : Color.white;
            string bgLabel = backgroundGraphic != null ? backgroundGraphic.gameObject.name : "None (default white assumed)";

            float ratio = WcagContrastUtility.ContrastRatio(fgColor, bgColor);

            bool contrastPass = ratio >= thresholds.minContrastRatio;
            bool fontPass = fontSize >= thresholds.minFontSizePoint;

            return new ElementReport
            {
                elementName = go.name,
                hierarchyPath = GetHierarchyPath(go.transform),
                sceneName = go.scene.name,
                contrastRatio = ratio,
                contrastPass = contrastPass,
                fontSizePoint = fontSize,
                fontSizePass = fontPass,
                foregroundColorHex = ColorToHex(fgColor),
                backgroundColorHex = ColorToHex(bgColor),
                backgroundSourceLabel = bgLabel
            };
        }

        private static Graphic FindAncestorGraphic(Transform start)
        {
            Transform current = start.parent;
            while (current != null)
            {
                var graphic = current.GetComponent<Graphic>();
                if (graphic != null) return graphic;
                current = current.parent;
            }
            return null;
        }

        private static string GetHierarchyPath(Transform t)
        {
            string path = t.name;
            Transform current = t.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        private static string ColorToHex(Color c)
        {
            return $"#{(int)(c.r * 255):X2}{(int)(c.g * 255):X2}{(int)(c.b * 255):X2}";
        }
    }
}