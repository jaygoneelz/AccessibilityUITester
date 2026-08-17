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
            // Derives the linear gray luminance required for exactly 4.5:1
            // against white from the contrast formula itself, then inverts
            // the gamma curve to get the sRGB channel value. This checks
            // internal consistency of ContrastRatio() without depending on
            // any external reference colour value.
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
    }
}