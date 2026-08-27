using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AccessibilityTester.Editor.ReportWriter
{
    /// <summary>
    /// Editor utility: populates the active scene's Canvas with a
    /// specified number of procedurally generated TextMeshProUGUI
    /// elements, for O3 scan-time stress testing per proposal Phase 4.
    /// Not part of the shipped tool — development/testing utility only.
    /// Kept under a separate top-level menu path ("Tools") rather than
    /// nested under "Window/Accessibility UI Tester" to avoid colliding
    /// with that menu item, which must remain a direct window-opening
    /// command rather than becoming a submenu container.
    /// </summary>
    public static class StressTestSceneGenerator
    {
        [MenuItem("Tools/Accessibility Tester/Generate 1000-Element Stress Test")]
        public static void GenerateStressTestElements()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[AccessibilityTester] No Canvas found in the active scene. Add one before generating stress-test elements.");
                return;
            }

            GameObject container = new GameObject("StressTestElements");
            container.transform.SetParent(canvas.transform, false);

            const int count = 1000;
            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject($"StressText_{i}");
                go.transform.SetParent(container.transform, false);

                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.text = $"Item {i}";
                tmp.fontSize = 12 + (i % 20);
                tmp.color = new Color((i % 10) / 10f, (i % 7) / 7f, (i % 13) / 13f);

                var rect = go.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(100, 20);
                rect.anchoredPosition = new Vector2((i % 50) * 20, (i / 50) * 20);
            }

            Debug.Log($"[AccessibilityTester] Generated {count} stress-test elements under '{container.name}'.");
        }

        [MenuItem("Tools/Accessibility Tester/Remove Stress Test Elements")]
        public static void RemoveStressTestElements()
        {
            GameObject container = GameObject.Find("StressTestElements");
            if (container != null)
            {
                Object.DestroyImmediate(container);
                Debug.Log("[AccessibilityTester] Stress-test elements removed.");
            }
            else
            {
                Debug.Log("[AccessibilityTester] No stress-test container found.");
            }
        }
    }
}