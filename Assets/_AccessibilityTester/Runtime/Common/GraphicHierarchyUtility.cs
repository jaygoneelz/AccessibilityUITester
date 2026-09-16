using UnityEngine;
using UnityEngine.UI;

namespace AccessibilityTester.Runtime.Common
{
    /// <summary>
    /// Shared UI-hierarchy helpers used by both the Contrast Meter (runtime
    /// click-sampling) and the Report Writer (static scan) — kept in one
    /// place so the two don't drift against each other.
    /// </summary>
    public static class GraphicHierarchyUtility
    {
        /// <summary>
        /// Walks up the transform hierarchy from <paramref name="start"/>
        /// (exclusive) and returns the nearest ancestor with a Graphic
        /// component (Image, RawImage, Text, TextMeshProUGUI, etc.), or
        /// null if none exists up to the root.
        /// </summary>
        public static Graphic FindAncestorGraphic(Transform start)
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
    }
}
