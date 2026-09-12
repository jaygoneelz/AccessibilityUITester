#if ACCESSIBILITY_TESTER_URP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace AccessibilityTester.Runtime.ShaderController
{
    /// <summary>
    /// URP ScriptableRendererFeature that owns the CVD simulation and low
    /// vision blur passes, and exposes the toggles/parameters the Editor
    /// module bridge (ShaderControllerModule) reads and writes.
    /// </summary>
    public class CVDRendererFeature : ScriptableRendererFeature
    {
        public enum CVDType { None, Protanopia, Deuteranopia, Tritanopia }

        [FormerlySerializedAs("cvdShader")]
        [SerializeField] private Shader _cvdShader;
        public CVDType simulationType = CVDType.None;

        [FormerlySerializedAs("blurShader")]
        [SerializeField] private Shader _blurShader;
        public bool blurEnabled = false;
        [Range(0, 10)] public float blurSize = 2.0f;

        private Material _material;
        private CVDPass _pass;

        private Material _blurMaterial;
        private LowVisionBlurPass _blurPass;

        public override void Create()
        {
            if (_cvdShader == null)
                _cvdShader = Shader.Find("Hidden/AccessibilityTester/CVDSimulation");

            if (_cvdShader != null)
            {
                _material = CoreUtils.CreateEngineMaterial(_cvdShader);
                _pass = new CVDPass(_material)
                {
                    renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
                };
            }

            if (_blurShader == null)
                _blurShader = Shader.Find("Hidden/AccessibilityTester/GaussianBlur");

            if (_blurShader != null)
            {
                _blurMaterial = CoreUtils.CreateEngineMaterial(_blurShader);
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
            CVDType.Protanopia => CVDMatrixReference.ProtanopiaMatrix,
            CVDType.Deuteranopia => CVDMatrixReference.DeuteranopiaMatrix,
            CVDType.Tritanopia => CVDMatrixReference.TritanopiaMatrix,
            _ => Matrix4x4.identity
        };

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            CoreUtils.Destroy(_blurMaterial);
        }
    }
}
#endif