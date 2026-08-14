using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace AccessibilityTester.Runtime.ShaderController
{
    internal class LowVisionBlurPass : ScriptableRenderPass
    {
        private readonly Material _material;
        private static readonly int BlurSizeID = Shader.PropertyToID("_BlurSize");

        public LowVisionBlurPass(Material material)
        {
            _material = material;
        }

        public void SetBlurSize(float size) => _material.SetFloat(BlurSizeID, size);

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle source = resourceData.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(source);
            desc.clearBuffer = false;

            desc.name = "BlurHorizontalTarget";
            TextureHandle horizontal = renderGraph.CreateTexture(desc);
            var horizontalParams = new RenderGraphUtils.BlitMaterialParameters(source, horizontal, _material, 0);
            renderGraph.AddBlitPass(horizontalParams, passName: "Low Vision Blur - Horizontal");

            desc.name = "BlurVerticalTarget";
            TextureHandle vertical = renderGraph.CreateTexture(desc);
            var verticalParams = new RenderGraphUtils.BlitMaterialParameters(horizontal, vertical, _material, 1);
            renderGraph.AddBlitPass(verticalParams, passName: "Low Vision Blur - Vertical");

            resourceData.cameraColor = vertical;
        }
    }
}