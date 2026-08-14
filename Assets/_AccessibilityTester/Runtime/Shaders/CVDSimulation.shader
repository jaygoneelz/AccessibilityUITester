Shader "Hidden/AccessibilityTester/CVDSimulation"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "CVDSimulationPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4x4 _CVDMatrix;

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                float3 simulated = mul((float3x3)_CVDMatrix, color);
                return float4(simulated, 1.0);
            }
            ENDHLSL
        }
    }
}