using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AccessibilityTester.Runtime.FontScaler
{
    /// <summary>
    /// Globally scales TextMeshProUGUI font sizes in the loaded scene,
    /// forces a layout rebuild, and detects RectTransform overlaps caused
    /// by the resize. Overlapping elements are highlighted by setting
    /// their text colour to magenta. Original font sizes and colours are
    /// cached so the scale can be reverted. Edit Mode safe.
    /// </summary>
    public static class FontScalerUtility
    {
        // Cached pre-scale state for a single text element, so ApplyScale can be reverted.
        private class OriginalState
        {
            public float fontSize;
            public Color color;
        }

        private static readonly Dictionary<TextMeshProUGUI, OriginalState> _originalStates =
            new Dictionary<TextMeshProUGUI, OriginalState>();

        public static bool IsScaled { get; private set; }

        public static int ApplyScale(float factor)
        {
            TextMeshProUGUI[] allText = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);

            foreach (var tmp in allText)
            {
                if (!_originalStates.ContainsKey(tmp))
                {
                    _originalStates[tmp] = new OriginalState { fontSize = tmp.fontSize, color = tmp.color };
                }

                OriginalState original = _originalStates[tmp];
                tmp.fontSize = original.fontSize * factor;
                tmp.color = original.color; // clear any previous overlap highlight before re-checking

                LayoutRebuilder.ForceRebuildLayoutImmediate(tmp.rectTransform);
            }

            IsScaled = true;
            return DetectAndHighlightOverlaps(allText);
        }

        public static void Revert()
        {
            foreach (var kvp in _originalStates)
            {
                if (kvp.Key == null) continue; // element may have been destroyed since caching
                kvp.Key.fontSize = kvp.Value.fontSize;
                kvp.Key.color = kvp.Value.color;
                LayoutRebuilder.ForceRebuildLayoutImmediate(kvp.Key.rectTransform);
            }

            _originalStates.Clear();
            IsScaled = false;
        }

        private static int DetectAndHighlightOverlaps(TextMeshProUGUI[] elements)
        {
            var bounds = new Rect[elements.Length];
            for (int i = 0; i < elements.Length; i++)
            {
                bounds[i] = GetScreenRect(elements[i].rectTransform);
            }

            var overlapping = new bool[elements.Length];
            for (int i = 0; i < elements.Length; i++)
            {
                for (int j = i + 1; j < elements.Length; j++)
                {
                    // Skip parent/child pairs — a label nested inside its
                    // own container overlapping that container is expected,
                    // not a resize-induced collision.
                    if (elements[i].transform.IsChildOf(elements[j].transform)) continue;
                    if (elements[j].transform.IsChildOf(elements[i].transform)) continue;

                    if (bounds[i].Overlaps(bounds[j]))
                    {
                        overlapping[i] = true;
                        overlapping[j] = true;
                    }
                }
            }

            int count = 0;
            for (int i = 0; i < elements.Length; i++)
            {
                if (overlapping[i])
                {
                    elements[i].color = Color.magenta;
                    count++;
                }
            }
            return count;
        }

        private static Rect GetScreenRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float xMin = corners[0].x, yMin = corners[0].y;
            float xMax = corners[2].x, yMax = corners[2].y;
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }
    }
}