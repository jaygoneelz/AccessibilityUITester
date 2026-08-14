using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace AccessibilityTester.Runtime.ShaderController
{
    public class CVDRendererFeature : ScriptableRendererFeature
    {
        public enum CVDType { None, Protanopia, Deuteranopia, Tritanopia }

        [SerializeField] private Shader cvdShader;
        public CVDType simulationType = CVDType.None;

        [SerializeField] private Shader blurShader;
        public bool blurEnabled = false;
        [Range(0, 10)] public float blurSize = 2.0f;

        private Material _material;
        private CVDPass _pass;

        private Material _blurMaterial;
        private LowVisionBlurPass _blurPass;

        // Machado, Oliveira & Fernandes (2009) full-severity transformation
        // matrices. Verified against the authors' own published Table 1
        // (severity = 1.0) at https://www.inf.ufrgs.br/~oliveira/pubs_files/
        // CVD_Simulation/CVD_Simulation.html — exact match to 6 decimal places.
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

        public override void Create()
        {
            if (cvdShader == null)
                cvdShader = Shader.Find("Hidden/AccessibilityTester/CVDSimulation");

            if (cvdShader != null)
            {
                _material = CoreUtils.CreateEngineMaterial(cvdShader);
                _pass = new CVDPass(_material)
                {
                    renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
                };
            }

            if (blurShader == null)
                blurShader = Shader.Find("Hidden/AccessibilityTester/GaussianBlur");

            if (blurShader != null)
            {
                _blurMaterial = CoreUtils.CreateEngineMaterial(blurShader);
                _blurPass = new LowVisionBlurPass(_blurMaterial)
                {
                    renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
                };
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (simulationType != CVDType.None && _material != null && _pass != null)
            {
                _pass.SetMatrix(GetMatrixFor(simulationType));
                renderer.EnqueuePass(_pass);
            }

            if (blurEnabled && _blurMaterial != null && _blurPass != null)
            {
                _blurPass.SetBlurSize(blurSize);
                renderer.EnqueuePass(_blurPass);
            }
        }

        private static Matrix4x4 GetMatrixFor(CVDType type) => type switch
        {
            CVDType.Protanopia => ProtanopiaMatrix,
            CVDType.Deuteranopia => DeuteranopiaMatrix,
            CVDType.Tritanopia => TritanopiaMatrix,
            _ => Matrix4x4.identity
        };

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            CoreUtils.Destroy(_blurMaterial);
        }
    }
}