using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AccessibilityTester.Tests.PlayMode
{
    public class CVDShaderGpuReadbackTests
    {
        private const double DeltaEThreshold = 2.0;
        private Material _material;

        // X-Rite ColorChecker Classic, sRGB 0-255 (Lindbloom sRGB table).
        // Patch 18 (Cyan) clamped to 0 on R — genuinely out-of-gamut in sRGB.
        private static readonly Color[] Patches =
        {
            C(115,80,64),   C(195,151,130), C(94,123,156),
            C(88,108,65),   C(130,129,177), C(100,190,171),
            C(217,122,37),  C(72,91,165),   C(194,84,98),
            C(91,59,107),   C(160,188,60),  C(230,163,42),
            C(46,60,153),   C(71,150,69),   C(177,44,56),
            C(238,200,27),  C(187,82,148),  C(0,135,166),
            C(243,242,237), C(201,201,201), C(161,161,161),
            C(122,122,121), C(83,83,83),    C(50,49,50)
        };

        private static Color C(float r, float g, float b) => new Color(r / 255f, g / 255f, b / 255f);

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

        [OneTimeSetUp]
        public void Setup()
        {
            var shader = Shader.Find("Hidden/AccessibilityTester/CVDSimulation");
            Assert.IsNotNull(shader, "CVDSimulation shader not found.");
            _material = new Material(shader);
        }

        [OneTimeTearDown]
        public void Teardown() => Object.DestroyImmediate(_material);

        [UnityTest] public IEnumerator Protanopia_GpuMatchesCpu() { yield return Run(ProtanopiaMatrix, "Protanopia"); }
        [UnityTest] public IEnumerator Deuteranopia_GpuMatchesCpu() { yield return Run(DeuteranopiaMatrix, "Deuteranopia"); }
        [UnityTest] public IEnumerator Tritanopia_GpuMatchesCpu() { yield return Run(TritanopiaMatrix, "Tritanopia"); }

        private IEnumerator Run(Matrix4x4 matrix, string label)
        {
            _material.SetMatrix("_CVDMatrix", matrix);
            double maxDeltaE = 0;
            int worstPatch = -1;

            for (int i = 0; i < Patches.Length; i++)
            {
                Color srgb = Patches[i];
                Vector3 cpuLinearIn = new Vector3(
                    SrgbToLinear(srgb.r), SrgbToLinear(srgb.g), SrgbToLinear(srgb.b));

                // Render this patch through the actual shader
                var src = new Texture2D(4, 4, TextureFormat.RGBAFloat, false, true);
                Color linearColor = new Color(cpuLinearIn.x, cpuLinearIn.y, cpuLinearIn.z, 1f);
                var fill = new Color[16];
                for (int p = 0; p < 16; p++) fill[p] = linearColor;
                src.SetPixels(fill);
                src.Apply();

                var dest = RenderTexture.GetTemporary(4, 4, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                _material.SetTexture("_BlitTexture", src);
                Graphics.Blit(src, dest, _material);

                yield return null; // let the GPU finish this frame

                var prevActive = RenderTexture.active;
                RenderTexture.active = dest;
                var read = new Texture2D(4, 4, TextureFormat.RGBAFloat, false, true);
                read.ReadPixels(new Rect(0, 0, 4, 4), 0, 0);
                read.Apply();
                RenderTexture.active = prevActive;

                Color gpuPixel = read.GetPixel(0, 0);
                Vector3 gpuLinear = new Vector3(gpuPixel.r, gpuPixel.g, gpuPixel.b);

                Vector3 cpuLinear = ClampVector(ApplyMatrix(matrix, cpuLinearIn));

                double deltaE = ComputeDeltaE76(gpuLinear, cpuLinear);
                if (deltaE > maxDeltaE) { maxDeltaE = deltaE; worstPatch = i; }

                Object.DestroyImmediate(src);
                Object.DestroyImmediate(read);
                RenderTexture.ReleaseTemporary(dest);

                Assert.LessOrEqual(deltaE, DeltaEThreshold,
                    $"{label} patch {i + 1}: ΔE={deltaE:F4} exceeds threshold {DeltaEThreshold}");
            }

            Debug.Log($"[{label} GPU-readback] Max ΔE across 24 patches: {maxDeltaE:F4} (patch {worstPatch + 1})");
        }

        private static Vector3 ApplyMatrix(Matrix4x4 m, Vector3 v) => new Vector3(
            m.m00 * v.x + m.m01 * v.y + m.m02 * v.z,
            m.m10 * v.x + m.m11 * v.y + m.m12 * v.z,
            m.m20 * v.x + m.m21 * v.y + m.m22 * v.z);

        private static Vector3 ClampVector(Vector3 v) => new Vector3(
            Mathf.Clamp01(v.x), Mathf.Clamp01(v.y), Mathf.Clamp01(v.z));

        private static float SrgbToLinear(float c) =>
            c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

        private static Vector3 LinearToXyz(Vector3 rgb) => new Vector3(
            0.4124564f * rgb.x + 0.3575761f * rgb.y + 0.1804375f * rgb.z,
            0.2126729f * rgb.x + 0.7151522f * rgb.y + 0.0721750f * rgb.z,
            0.0193339f * rgb.x + 0.1191920f * rgb.y + 0.9503041f * rgb.z);

        private static Vector3 XyzToLab(Vector3 xyz)
        {
            const float xn = 0.95047f, yn = 1.0f, zn = 1.08883f;
            float f(float t)
            {
                const float d = 6f / 29f;
                return t > d * d * d ? Mathf.Pow(t, 1f / 3f) : t / (3f * d * d) + 4f / 29f;
            }
            float fx = f(xyz.x / xn), fy = f(xyz.y / yn), fz = f(xyz.z / zn);
            return new Vector3(116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz));
        }

        private static double ComputeDeltaE76(Vector3 a, Vector3 b)
        {
            Vector3 labA = XyzToLab(LinearToXyz(a));
            Vector3 labB = XyzToLab(LinearToXyz(b));
            return Mathf.Sqrt(Mathf.Pow(labA.x - labB.x, 2) + Mathf.Pow(labA.y - labB.y, 2) + Mathf.Pow(labA.z - labB.z, 2));
        }
    }
}