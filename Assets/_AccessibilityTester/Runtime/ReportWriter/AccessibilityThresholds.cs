using UnityEngine;

namespace AccessibilityTester.Runtime.ReportWriter
{
    /// <summary>
    /// Configurable pass/fail thresholds for the Report Writer scan.
    /// Stored as an asset so thresholds can be adjusted without
    /// recompiling, per proposal Phase 4 requirements.
    /// </summary>
    [CreateAssetMenu(fileName = "AccessibilityThresholds", menuName = "AccessibilityTester/Accessibility Thresholds")]
    public class AccessibilityThresholds : ScriptableObject
    {
        [Tooltip("Minimum WCAG contrast ratio for normal-size text (WCAG 2.1 SC 1.4.3 default: 4.5)")]
        public float minContrastRatio = 4.5f;

        [Tooltip("Minimum font size in points before an element is flagged (proposal default: 12pt)")]
        public float minFontSizePoint = 12f;
    }
}