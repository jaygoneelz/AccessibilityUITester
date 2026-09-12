using UnityEngine;

namespace AccessibilityTester.Runtime.ShaderController
{
    /// <summary>
    /// Shared CVD simulation matrices and CIE76 verification math, referenced
    /// by CVDRendererFeature (production) and the CVD accuracy / GPU-readback
    /// tests (verification), so the reference data is defined in exactly one
    /// place.
    /// </summary>
    public static class CVDMatrixReference
    {
        // Machado, Oliveira & Fernandes (2009) full-severity transformation
        // matrices. Verified against the authors' own published Table 1
        // (severity = 1.0) at https://www.inf.ufrgs.br/~oliveira/pubs_files/
        // CVD_Simulation/CVD_Simulation.html — exact match to 6 decimal places.
        public static readonly Matrix4x4 ProtanopiaMatrix = new Matrix4x4(
            new Vector4(0.152286f, 0.114503f, -0.003882f, 0f),
            new Vector4(1.052583f, 0.786281f, -0.048116f, 0f),
            new Vector4(-0.204868f, 0.099216f, 1.051998f, 0f),
            new Vector4(0f, 0f, 0f, 1f));

        public static readonly Matrix4x4 DeuteranopiaMatrix = new Matrix4x4(
            new Vector4(0.367322f, 0.280085f, -0.011820f, 0f),
            new Vector4(0.860646f, 0.672501f, 0.042940f, 0f),
            new Vector4(-0.227968f, 0.047413f, 0.968881f, 0f),
            new Vector4(0f, 0f, 0f, 1f));

        public static readonly Matrix4x4 TritanopiaMatrix = new Matrix4x4(
            new Vector4(1.255528f, -0.078411f, 0.004733f, 0f),
            new Vector4(-0.076749f, 0.930809f, 0.691367f, 0f),
            new Vector4(-0.178779f, 0.147602f, 0.303900f, 0f),
            new Vector4(0f, 0f, 0f, 1f));

        public static Vector3 ApplyMatrix(Matrix4x4 m, Vector3 v) => new Vector3(
            m.m00 * v.x + m.m01 * v.y + m.m02 * v.z,
            m.m10 * v.x + m.m11 * v.y + m.m12 * v.z,
            m.m20 * v.x + m.m21 * v.y + m.m22 * v.z);

        public static Vector3 ClampVector(Vector3 v) => new Vector3(
            Mathf.Clamp01(v.x), Mathf.Clamp01(v.y), Mathf.Clamp01(v.z));

        public static float SrgbToLinear(float c) =>
            c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

        public static Vector3 SrgbToLinear(Vector3 srgb) =>
            new Vector3(SrgbToLinear(srgb.x), SrgbToLinear(srgb.y), SrgbToLinear(srgb.z));

        public static Vector3 LinearToXyz(Vector3 rgb) => new Vector3(
            0.4124564f * rgb.x + 0.3575761f * rgb.y + 0.1804375f * rgb.z,
            0.2126729f * rgb.x + 0.7151522f * rgb.y + 0.0721750f * rgb.z,
            0.0193339f * rgb.x + 0.1191920f * rgb.y + 0.9503041f * rgb.z);

        public static Vector3 XyzToLab(Vector3 xyz)
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

        public static double ComputeDeltaE76(Vector3 rgbA, Vector3 rgbB)
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
