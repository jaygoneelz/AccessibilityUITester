using UnityEngine;

namespace AccessibilityTester.Runtime.ContrastMeter
{
    /// <summary>
    /// WCAG 2.1 contrast ratio calculations (Success Criterion 1.4.3).
    /// Uses 0.04045 instead of the literal spec value 0.03928, a known rounding
    /// error corrected in WCAG 2.2 to match the true sRGB crossover point.
    /// </summary>
    public static class WcagContrastUtility
    {
        public const float NormalTextMinRatio = 4.5f;
        public const float LargeTextMinRatio = 3.0f;
        public const float EnhancedNormalTextMinRatio = 7.0f;
        public const float EnhancedLargeTextMinRatio = 4.5f;
        public const float NonTextMinRatio = 3.0f;

        /// <summary>
        /// Converts a single sRGB-encoded channel (0-1) to linear light.
        /// </summary>
        public static float SrgbChannelToLinear(float channel)
        {
            return channel <= 0.04045f
                ? channel / 12.92f
                : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }

        /// <summary>
        /// Relative luminance: L = 0.2126R + 0.7152G + 0.0722B, computed on
        /// linearized channels.
        /// </summary>
        public static float RelativeLuminance(Color srgbColor)
        {
            float r = SrgbChannelToLinear(srgbColor.r);
            float g = SrgbChannelToLinear(srgbColor.g);
            float b = SrgbChannelToLinear(srgbColor.b);
            return 0.2126f * r + 0.7152f * g + 0.0722f * b;
        }

        /// <summary>
        /// WCAG contrast ratio between two colours. Argument order does not
        /// matter; the formula always divides the lighter luminance by the
        /// darker one.
        /// </summary>
        public static float ContrastRatio(Color colorA, Color colorB)
        {
            float lumA = RelativeLuminance(colorA);
            float lumB = RelativeLuminance(colorB);

            float lighter = Mathf.Max(lumA, lumB);
            float darker = Mathf.Min(lumA, lumB);

            return (lighter + 0.05f) / (darker + 0.05f);
        }

        public static bool PassesNormalText(float ratio) => ratio >= NormalTextMinRatio;
        public static bool PassesLargeText(float ratio) => ratio >= LargeTextMinRatio;
        public static bool PassesEnhancedNormalText(float ratio) => ratio >= EnhancedNormalTextMinRatio;
        public static bool PassesEnhancedLargeText(float ratio) => ratio >= EnhancedLargeTextMinRatio;
        public static bool PassesNonText(float ratio) => ratio >= NonTextMinRatio;
    }
}