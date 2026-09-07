using System;

namespace AccessibilityTester.Runtime.ReportWriter
{
    /// <summary>
    /// One record per scanned text element. Plain data class, JSON-
    /// serializable via JsonUtility.
    /// </summary>
    [Serializable]
    public class ElementReport
    {
        public string elementName;
        public string hierarchyPath;
        public string sceneName;

        public float contrastRatio;
        public bool contrastPass;

        public float fontSizePoint;
        public bool fontSizePass;

        public string foregroundColorHex;
        public string backgroundColorHex;
        public string backgroundSourceLabel;

        // Honesty flag: describes how the background colour was obtained,
        // since Edit Mode static analysis cannot directly observe the
        // rendered pixel. "High" = read directly from a Graphic.color.
        // "Estimated" = sampled from a Sprite's texture pixels (ancestor
        // tint was white). "Medium"/"Low" = camera-background fallback,
        // depending on whether the camera uses a solid clear colour.
        // "Low (unreadable)" = white tint on a sprite whose texture is
        // not marked Read/Write Enabled, so sampling was not possible.
        public string backgroundConfidence;
    }

    /// <summary>
    /// Top-level JSON document produced by a full scene scan.
    /// </summary>
    [Serializable]
    public class SceneReport
    {
        public string sceneName;
        public string scanTimestampUtc;
        public float minContrastRatioUsed;
        public float minFontSizePointUsed;
        public int totalElementsScanned;
        public int totalFailures;
        public ElementReport[] elements;
    }
}