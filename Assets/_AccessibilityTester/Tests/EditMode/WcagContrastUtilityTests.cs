using NUnit.Framework;
using UnityEngine;
using AccessibilityTester.Runtime.ContrastMeter;

namespace AccessibilityTester.Tests.EditMode
{
    public class WcagContrastUtilityTests
    {
        private const float Tolerance = 0.01f;

        [Test]
        public void BlackOnWhite_Equals21To1()
        {
            float ratio = WcagContrastUtility.ContrastRatio(Color.black, Color.white);
            Assert.AreEqual(21f, ratio, Tolerance);
        }

        [Test]
        public void WhiteOnWhite_Equals1To1()
        {
            float ratio = WcagContrastUtility.ContrastRatio(Color.white, Color.white);
            Assert.AreEqual(1f, ratio, Tolerance);
        }

        [Test]
        public void BlackOnBlack_Equals1To1()
        {
            float ratio = WcagContrastUtility.ContrastRatio(Color.black, Color.black);
            Assert.AreEqual(1f, ratio, Tolerance);
        }

        [Test]
        public void ArgumentOrder_DoesNotAffectResult()
        {
            float ratioAB = WcagContrastUtility.ContrastRatio(Color.black, Color.white);
            float ratioBA = WcagContrastUtility.ContrastRatio(Color.white, Color.black);
            Assert.AreEqual(ratioAB, ratioBA, Tolerance);
        }

        [Test]
        public void ContrastRatio_MatchesClosedFormSolution_ForGrayOnWhite()
        {
            const float targetRatio = 4.5f;
            const float whiteLuminance = 1f;

            float targetGrayLuminance = (whiteLuminance + 0.05f) / targetRatio - 0.05f;

            float grayChannel = 1.055f * Mathf.Pow(targetGrayLuminance, 1f / 2.4f) - 0.055f;
            Color derivedGray = new Color(grayChannel, grayChannel, grayChannel);

            float actualRatio = WcagContrastUtility.ContrastRatio(derivedGray, Color.white);

            Assert.AreEqual(targetRatio, actualRatio, Tolerance,
                $"Closed-form derived gray should produce exactly {targetRatio}:1, got {actualRatio:F4}");
        }

        [Test]
        public void PassFailThresholds_AreCorrectlyOrdered()
        {
            Assert.Less(WcagContrastUtility.LargeTextMinRatio, WcagContrastUtility.NormalTextMinRatio);
            Assert.Less(WcagContrastUtility.NormalTextMinRatio, WcagContrastUtility.EnhancedNormalTextMinRatio);
            Assert.AreEqual(WcagContrastUtility.EnhancedLargeTextMinRatio, WcagContrastUtility.NormalTextMinRatio);
        }

        /// <summary>
        /// Logs computed ratios for a fixed set of hex pairs, for manual
        /// cross-checking against WebAIM's contrast checker
        /// (webaim.org/resources/contrastchecker). Does not assert against
        /// hardcoded "expected" values, since those would need the same
        /// external verification this test exists to provide.
        /// </summary>
        [Test]
        public void LogRatios_ForManualWebAimCrossCheck()
        {
            LogPair("#000000", "#FFFFFF");
            LogPair("#767676", "#FFFFFF");
            LogPair("#0000FF", "#FFFFFF");
            LogPair("#FF0000", "#000000");
            LogPair("#595959", "#FFFFFF");
            LogPair("#1A1A1A", "#FFFFFF");
            LogPair("#3B3B3B", "#ADADAD");
            LogPair("#4A90D9", "#FFFFFF");
        }

        private static void LogPair(string fgHex, string bgHex)
        {
            Color fg = HexToColor(fgHex);
            Color bg = HexToColor(bgHex);
            float ratio = WcagContrastUtility.ContrastRatio(fg, bg);
            bool normalPass = WcagContrastUtility.PassesNormalText(ratio);
            bool largePass = WcagContrastUtility.PassesLargeText(ratio);

            Debug.Log($"[G18CrossCheck] {fgHex} on {bgHex} -> {ratio:F2}:1 " +
                      $"(Normal AA: {(normalPass ? "PASS" : "FAIL")}, Large AA: {(largePass ? "PASS" : "FAIL")})");
        }

        private static Color HexToColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }
    }
}