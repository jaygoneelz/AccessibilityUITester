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