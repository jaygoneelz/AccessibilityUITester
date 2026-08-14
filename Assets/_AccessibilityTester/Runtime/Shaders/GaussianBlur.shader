Shader "Hidden/AccessibilityTester/GaussianBlur"
{
    Properties { _BlurSize ("Blur Size", Range(0, 10)) = 2.0 }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float _BlurSize;

        // 9-tap Gaussian kernel, normalized
        static const float kWeights[9] = {
            0.028, 0.066, 0.123, 0.180, 0.206, 0.180, 0.123, 0.066, 0.028
        };

        float4 BlurSample(Varyings input, float2 direction)
        {
            float2 texel = direction * _BlurSize * _BlitTexture_TexelSize.xy;
            float3 sum = float3(0, 0, 0);
            for (int i = -4; i <= 4; i++)
            {
                float2 uv = input.texcoord + texel * i;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb * kWeights[i + 4];
            }
            return float4(sum, 1.0);
        }
        ENDHLSL

        Pass
        {
            Name "BlurHorizontal"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings input) : SV_Target { return BlurSample(input, float2(1, 0)); }
            ENDHLSL
        }

        Pass
        {
            Name "BlurVertical"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings input) : SV_Target { return BlurSample(input, float2(0, 1)); }
            ENDHLSL
        }
    }
}