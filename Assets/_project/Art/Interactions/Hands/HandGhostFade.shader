Shader "MemoryGarden/Hand Ghost Fade"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (0.82, 0.9, 1, 0.55)
        _EmissionMap ("Emission Map", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0.55, 0.78, 1.2, 1)
        _EmissionStrength ("Emission Strength", Range(0, 8)) = 1.6
        _Alpha ("Alpha", Range(0, 1)) = 0.55
        _FadeOrigin ("Fade Origin (Object Space)", Vector) = (0, 0, 0, 0)
        _FadeStartRadius ("Fade Start Radius", Range(0, 1)) = 0.02
        _FadeEndRadius ("Fade End Radius", Range(0, 1)) = 0.12
        [HDR] _RimColor ("Rim Color", Color) = (0.8, 0.92, 1.25, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimStrength ("Rim Strength", Range(0, 4)) = 1.2
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                float fogCoord : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float4 _RimColor;
                float4 _FadeOrigin;
                float _EmissionStrength;
                float _Alpha;
                float _FadeStartRadius;
                float _FadeEndRadius;
                float _RimPower;
                float _RimStrength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.uv = input.uv;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = normalize(normalInputs.normalWS);
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                output.fogCoord = ComputeFogFactor(positionInputs.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 viewDirWS = normalize(input.viewDirWS);
                float3 normalWS = normalize(input.normalWS);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 emissionSample = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb;

                float objectDistance = distance(input.positionOS, _FadeOrigin.xyz);
                float fadeRange = max(_FadeEndRadius - _FadeStartRadius, 0.0001);
                float fade = saturate((objectDistance - _FadeStartRadius) / fadeRange);

                float rim = pow(saturate(1.0 - dot(normalWS, viewDirWS)), _RimPower) * _RimStrength;

                half3 baseColor = baseSample.rgb * _BaseColor.rgb;
                half3 emission = emissionSample * _EmissionColor.rgb * _EmissionStrength;
                half3 color = (baseColor * 0.35h) + emission + (_RimColor.rgb * rim);
                half alpha = baseSample.a * _BaseColor.a * _Alpha * fade;

                color = MixFog(color, input.fogCoord);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
