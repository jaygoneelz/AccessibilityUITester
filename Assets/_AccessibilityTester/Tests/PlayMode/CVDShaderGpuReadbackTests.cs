using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AccessibilityTester.Runtime.ShaderController;

namespace AccessibilityTester.Tests.PlayMode
{
    /// <summary>
    /// Verifies the CVD simulation shader's GPU output against the CPU-side
    /// matrix math by blitting each ColorChecker patch and reading back the
    /// rendered pixel.
    /// </summary>
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

        [OneTimeSetUp]
        public void Setup()
        {
            var shader = Shader.Find("Hidden/AccessibilityTester/CVDSimulation");
            Assert.IsNotNull(shader, "CVDSimulation shader not found.");
            _material = new Material(shader);
        }

        [OneTimeTearDown]
        public void Teardown() => Object.DestroyImmediate(_material);

        [UnityTest] public IEnumerator Protanopia_GpuMatchesCpu() { yield return Run(CVDMatrixReference.ProtanopiaMatrix, "Protanopia"); }
        [UnityTest] public IEnumerator Deuteranopia_GpuMatchesCpu() { yield return Run(CVDMatrixReference.DeuteranopiaMatrix, "Deuteranopia"); }
        [UnityTest] public IEnumerator Tritanopia_GpuMatchesCpu() { yield return Run(CVDMatrixReference.TritanopiaMatrix, "Tritanopia"); }

        private IEnumerator Run(Matrix4x4 matrix, string label)
        {
            _material.SetMatrix("_CVDMatrix", matrix);
            double maxDeltaE = 0;
            int worstPatch = -1;

            for (int i = 0; i < Patches.Length; i++)
            {
                Color srgb = Patches[i];
                Vector3 cpuLinearIn = new Vector3(
                    CVDMatrixReference.SrgbToLinear(srgb.r), CVDMatrixReference.SrgbToLinear(srgb.g), CVDMatrixReference.SrgbToLinear(srgb.b));

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

                Vector3 cpuLinear = CVDMatrixReference.ClampVector(CVDMatrixReference.ApplyMatrix(matrix, cpuLinearIn));

                double deltaE = CVDMatrixReference.ComputeDeltaE76(gpuLinear, cpuLinear);
                if (deltaE > maxDeltaE) { maxDeltaE = deltaE; worstPatch = i; }

                Object.DestroyImmediate(src);
                Object.DestroyImmediate(read);
                RenderTexture.ReleaseTemporary(dest);

                Assert.LessOrEqual(deltaE, DeltaEThreshold,
                    $"{label} patch {i + 1}: ΔE={deltaE:F4} exceeds threshold {DeltaEThreshold}");
            }

            Debug.Log($"[{label} GPU-readback] Max ΔE across 24 patches: {maxDeltaE:F4} (patch {worstPatch + 1})");
        }
    }
}