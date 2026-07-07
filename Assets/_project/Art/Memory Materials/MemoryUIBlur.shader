Shader "Hidden/MemoryGarden/MemoryUIBlur"
{
    HLSLINCLUDE
        #pragma exclude_renderers gles

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float _BlurOffset;
        float4 _BlitTexture_TexelSize;

        float2 GetBlurUV(Varyings input)
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return UnityStereoTransformScreenSpaceTex(input.texcoord);
        }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Downsample"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDownsample

            half4 FragDownsample(Varyings input) : SV_Target
            {
                float2 uv = GetBlurUV(input);
                float2 texelSize = _BlitTexture_TexelSize.xy;
                float2 offset = texelSize * max(0.5, _BlurOffset);
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb * 0.40h;
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(offset.x, offset.y)).rgb * 0.15h;
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-offset.x, offset.y)).rgb * 0.15h;
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(offset.x, -offset.y)).rgb * 0.15h;
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-offset.x, -offset.y)).rgb * 0.15h;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Kawase"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragKawase

            half4 FragKawase(Varyings input) : SV_Target
            {
                float2 uv = GetBlurUV(input);
                float2 texelSize = _BlitTexture_TexelSize.xy;
                float2 offset = texelSize * max(0.5, _BlurOffset);
                half3 color = 0.0h;
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(offset.x, offset.y)).rgb;
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-offset.x, offset.y)).rgb;
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(offset.x, -offset.y)).rgb;
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-offset.x, -offset.y)).rgb;
                color *= 0.25h;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
