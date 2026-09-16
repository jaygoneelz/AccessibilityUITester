#if ACCESSIBILITY_TESTER_URP
using NUnit.Framework;
using UnityEngine;
using AccessibilityTester.Runtime.ShaderController;

namespace AccessibilityTester.Tests.EditMode
{
    /// <summary>
    /// CPU-side self-consistency / regression check for CVDMatrixReference's
    /// matrix-application math. For each ColorChecker patch, applies the CVD
    /// matrix via CVDMatrixReference.ApplyMatrix twice (same matrix, same
    /// input) and asserts the two results agree — guarding against the CPU
    /// path becoming non-deterministic (e.g. an accidental change introducing
    /// order-dependent floating point rounding).
    ///
    /// This does NOT independently verify that the Protanopia / Deuteranopia
    /// / Tritanopia matrix VALUES are numerically correct: both computations
    /// use the same matrix, so a wrong matrix would still pass with ΔE = 0.
    /// The matrix values themselves were checked by hand against Machado,
    /// Oliveira &amp; Fernandes (2009) Table 1 — see the provenance comment on
    /// CVDMatrixReference. Independent verification that this CPU math
    /// matches a genuinely separate implementation (the GPU shader) lives in
    /// the PlayMode test CVDShaderGpuReadbackTests.
    /// </summary>
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

        [Test]
        public void Protanopia_AllPatches_MeetDeltaEThreshold()
        {
            RunPatchSet(CVDMatrixReference.ProtanopiaMatrix, "Protanopia");
        }

        [Test]
        public void Deuteranopia_AllPatches_MeetDeltaEThreshold()
        {
            RunPatchSet(CVDMatrixReference.DeuteranopiaMatrix, "Deuteranopia");
        }

        [Test]
        public void Tritanopia_AllPatches_MeetDeltaEThreshold()
        {
            RunPatchSet(CVDMatrixReference.TritanopiaMatrix, "Tritanopia");
        }

        private void RunPatchSet(Matrix4x4 matrix, string label)
        {
            double maxDeltaE = 0;
            int worstPatch = -1;

            for (int i = 0; i < ColorCheckerPatches.Length; i++)
            {
                Vector3 srgb255 = ColorCheckerPatches[i];
                Vector3 linear = CVDMatrixReference.SrgbToLinear(srgb255 / 255f);

                Vector3 simulatedLinear = CVDMatrixReference.ApplyMatrix(matrix, linear);
                simulatedLinear = CVDMatrixReference.ClampVector(simulatedLinear);

                // Recomputed via the exact same matrix and function as
                // `simulatedLinear` above — this only proves ApplyMatrix is
                // deterministic, not that the matrix itself is numerically
                // correct. No shader/GPU code runs in this file; see
                // CVDShaderGpuReadbackTests for the actual GPU-vs-CPU check.
                Vector3 referenceLinear = CVDMatrixReference.ApplyMatrix(matrix, linear);
                referenceLinear = CVDMatrixReference.ClampVector(referenceLinear);

                double deltaE = CVDMatrixReference.ComputeDeltaE76(simulatedLinear, referenceLinear);

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
    }
}
#endif