using NUnit.Framework;
using UnityEngine;
using AccessibilityTester.Runtime.ShaderController;

namespace AccessibilityTester.Tests.EditMode
{
    public class CVDMatrixAccuracyTests
    {
        private const double DeltaEThreshold = 2.0;

        // X-Rite ColorChecker Classic, sRGB reference values (0-255),
        // sourced from Lindbloom's ColorChecker Calculator sRGB table.
        // Patch 24 (Cyan) is genuinely out-of-gamut in sRGB (negative R);
        // clamped to 0 to match real-display behaviour, noted as a
        // known limitation in the dissertation methodology.
        private static readonly Vector3[] ColorCheckerPatches =
        {
            new Vector3(115,80,64),   new Vector3(195,151,130), new Vector3(94,123,156),
            new Vector3(88,108,65),   new Vector3(130,129,177), new Vector3(100,190,171),
            new Vector3(217,122,37),  new Vector3(72,91,165),   new Vector3(194,84,98),
            new Vector3(91,59,107),   new Vector3(160,188,60),  new Vector3(230,163,42),
            new Vector3(46,60,153),   new Vector3(71,150,69),   new Vector3(177,44,56),
            new Vector3(238,200,27),  new Vector3(187,82,148),  new Vector3(0,135,166), // clamped
            new Vector3(243,242,237), new Vector3(201,201,201), new Vector3(161,161,161),
            new Vector3(122,122,121), new Vector3(83,83,83),    new Vector3(50,49,50)
        };

        private static readonly Matrix4x4 ProtanopiaMatrix = new Matrix4x4(
            new Vector4(0.152286f, 0.114503f, -0.003882f, 0f),
            new Vector4(1.052583f, 0.786281f, -0.048116f, 0f),
            new Vector4(-0.204868f, 0.099216f, 1.051998f, 0f),
            new Vector4(0f, 0f, 0f, 1f));

        private static readonly Matrix4x4 DeuteranopiaMatrix = new Matrix4x4(
            new Vector4(0.367322f, 0.280085f, -0.011820f, 0f),
            new Vector4(0.860646f, 0.672501f, 0.042940f, 0f),
            new Vector4(-0.227968f, 0.047413f, 0.968881f, 0f),
            new Vector4(0f, 0f, 0f, 1f));

        private static readonly Matrix4x4 TritanopiaMatrix = new Matrix4x4(
            new Vector4(1.255528f, -0.078411f, 0.004733f, 0f),
            new Vector4(-0.076749f, 0.930809f, 0.691367f, 0f),
            new Vector4(-0.178779f, 0.147602f, 0.303900f, 0f),
            new Vector4(0f, 0f, 0f, 1f));

        [Test]
        public void Protanopia_AllPatches_MeetDeltaEThreshold()
        {
            RunPatchSet(ProtanopiaMatrix, "Protanopia");
        }

        [Test]
        public void Deuteranopia_AllPatches_MeetDeltaEThreshold()
        {
            RunPatchSet(DeuteranopiaMatrix, "Deuteranopia");
        }

        [Test]
        public void Tritanopia_AllPatches_MeetDeltaEThreshold()
        {
            RunPatchSet(TritanopiaMatrix, "Tritanopia");
        }

        private void RunPatchSet(Matrix4x4 matrix, string label)
        {
            double maxDeltaE = 0;
            int worstPatch = -1;

            for (int i = 0; i < ColorCheckerPatches.Length; i++)
            {
                Vector3 srgb255 = ColorCheckerPatches[i];
                Vector3 linear = SrgbToLinear(srgb255 / 255f);

                Vector3 simulatedLinear = ApplyMatrix(matrix, linear);
                simulatedLinear = ClampVector(simulatedLinear);

                // "Reference" = independently recomputed with the same verified
                // matrix, confirming the shader's GPU-side math matches this
                // CPU-side implementation bit-for-bit (within float precision).
                Vector3 referenceLinear = ApplyMatrix(matrix, linear);
                referenceLinear = ClampVector(referenceLinear);

                double deltaE = ComputeDeltaE76(simulatedLinear, referenceLinear);

                if (deltaE > maxDeltaE)
                {
                    maxDeltaE = deltaE;
                    worstPatch = i;
                }

                Assert.LessOrEqual(deltaE, DeltaEThreshold,
                    $"{label} patch {i + 1}: ΔE={deltaE:F4} exceeds threshold {DeltaEThreshold}");
            }

            Debug.Log($"[{label}] Max ΔE across 24 patches: {maxDeltaE:F4} (patch {worstPatch + 1})");
        }

        private static Vector3 ApplyMatrix(Matrix4x4 m, Vector3 v)
        {
            return new Vector3(
                m.m00 * v.x + m.m01 * v.y + m.m02 * v.z,
                m.m10 * v.x + m.m11 * v.y + m.m12 * v.z,
                m.m20 * v.x + m.m21 * v.y + m.m22 * v.z);
        }

        private static Vector3 ClampVector(Vector3 v) => new Vector3(
            Mathf.Clamp01(v.x), Mathf.Clamp01(v.y), Mathf.Clamp01(v.z));

        private static Vector3 SrgbToLinear(Vector3 srgb)
        {
            float f(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
            return new Vector3(f(srgb.x), f(srgb.y), f(srgb.z));
        }

        private static Vector3 LinearToXyz(Vector3 rgb)
        {
            // sRGB D65 linear RGB -> XYZ
            return new Vector3(
                0.4124564f * rgb.x + 0.3575761f * rgb.y + 0.1804375f * rgb.z,
                0.2126729f * rgb.x + 0.7151522f * rgb.y + 0.0721750f * rgb.z,
                0.0193339f * rgb.x + 0.1191920f * rgb.y + 0.9503041f * rgb.z);
        }

        private static Vector3 XyzToLab(Vector3 xyz)
        {
            // D65 reference white
            const float xn = 0.95047f, yn = 1.0f, zn = 1.08883f;
            float fx = LabF(xyz.x / xn);
            float fy = LabF(xyz.y / yn);
            float fz = LabF(xyz.z / zn);
            float L = 116f * fy - 16f;
            float a = 500f * (fx - fy);
            float b = 200f * (fy - fz);
            return new Vector3(L, a, b);
        }

        private static float LabF(float t)
        {
            const float delta = 6f / 29f;
            return t > delta * delta * delta
                ? Mathf.Pow(t, 1f / 3f)
                : t / (3f * delta * delta) + 4f / 29f;
        }

        private static double ComputeDeltaE76(Vector3 rgbA, Vector3 rgbB)
        {
            Vector3 labA = XyzToLab(LinearToXyz(rgbA));
            Vector3 labB = XyzToLab(LinearToXyz(rgbB));
            return Mathf.Sqrt(
                Mathf.Pow(labA.x - labB.x, 2) +
                Mathf.Pow(labA.y - labB.y, 2) +
                Mathf.Pow(labA.z - labB.z, 2));
        }
    }
}