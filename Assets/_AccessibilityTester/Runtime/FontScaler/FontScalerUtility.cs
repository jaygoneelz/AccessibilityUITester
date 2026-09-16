using System;
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
            TextMeshProUGUI[] allText = UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);

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

        /// <summary>
        /// Sort-and-sweep overlap detection: process elements left-to-right
        /// by their xMin, keeping an "active" set of elements whose x-range
        /// could still overlap one not yet processed. Once an active
        /// element's xMax falls behind the current element's xMin it can
        /// never overlap anything later in the sweep (since xMin only
        /// increases from here on), so it's dropped from the active set —
        /// this keeps the number of pairwise checks close to the number of
        /// elements that are actually near each other on the x-axis,
        /// instead of every possible pair (O(n log n) for the sort, plus
        /// roughly linear for the sweep on typical, spatially spread-out
        /// UIs; still O(n²) in the worst case where everything overlaps in
        /// x, same as the all-pairs approach it replaces).
        /// </summary>
        private static int DetectAndHighlightOverlaps(TextMeshProUGUI[] elements)
        {
            int n = elements.Length;
            var bounds = new Rect[n];
            for (int i = 0; i < n; i++)
            {
                bounds[i] = GetScreenRect(elements[i].rectTransform);
            }

            var overlapping = new bool[n];

            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            Array.Sort(order, (a, b) => bounds[a].xMin.CompareTo(bounds[b].xMin));

            var active = new List<int>();
            foreach (int i in order)
            {
                float xMin = bounds[i].xMin;

                for (int a = active.Count - 1; a >= 0; a--)
                {
                    if (bounds[active[a]].xMax < xMin) active.RemoveAt(a);
                }

                foreach (int j in active)
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

                active.Add(i);
            }

            int count = 0;
            for (int i = 0; i < n; i++)
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