using System;

namespace AccessibilityTester.Runtime.ReportWriter
{
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
        public string backgroundConfidence;
    }

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

        // Records whether this scan ran with the game actually running
        // (Play Mode, correct runtime camera state) or statically (Edit
        // Mode, camera may be at an unrepresentative default position for
        // scenes using runtime camera-follow logic). See dissertation
        // §4.14 for the specific limitation this addresses.
        public bool scannedInPlayMode;

        // Derived as PlayModeSettleSeconds * 60, assuming 60fps — NOT a
        // measured frame count. The settle wait itself is wall-clock-timed
        // (fps varies), so this is only an estimate of how many frames
        // that wait was roughly equivalent to; 0 when scannedInPlayMode is
        // false.
        public int estimatedPlayModeSettleFrames;
    }
}