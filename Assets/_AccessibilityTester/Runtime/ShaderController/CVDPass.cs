using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace AccessibilityTester.Runtime.ShaderController
{
    internal class CVDPass : ScriptableRenderPass
    {
        private readonly Material _material;
        private static readonly int CVDMatrixID = Shader.PropertyToID("_CVDMatrix");

        public CVDPass(Material material)
        {
            _material = material;
        }

        public void SetMatrix(Matrix4x4 matrix)
        {
            _material.SetMatrix(CVDMatrixID, matrix);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            // Don't blit directly from the back buffer — URP requirement.
            if (resourceData.isActiveTargetBackBuffer)
                return;

            TextureHandle source = resourceData.activeColorTexture;

            var destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = "CVDSimulationTarget";
            destinationDesc.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            RenderGraphUtils.BlitMaterialParameters blitParams = new(source, destination, _material, 0);
            renderGraph.AddBlitPass(blitParams, passName: "CVD Simulation Blit");

            resourceData.cameraColor = destination;
        }
    }
}