Shader "MemoryGarden/UI/Frosted Glass"
{
    Properties
    {
        [MainColor] _BaseColor ("Tint Color", Color) = (0.22, 0.22, 0.26, 0.18)
        [HDR] _EdgeColor ("Edge Color", Color) = (0.82, 0.76, 0.92, 1)
        [HDR] _SpecularColor ("Specular Color", Color) = (1.0, 0.97, 0.94, 1)
        [HDR] _ReflectionColor ("Reflection Color", Color) = (0.92, 0.94, 1.0, 1)
        _BlurPixels ("Blur Pixels", Range(0, 24)) = 9
        _TintStrength ("Tint Strength", Range(0, 1)) = 0.55
        _BackgroundInfluence ("Background Influence", Range(0, 2)) = 1.0
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.8
        _EdgeStrength ("Edge Strength", Range(0, 2)) = 0.35
        _AlphaSoftness ("Alpha Softness", Range(0, 8)) = 1.0
        _SpecularStrength ("Specular Strength", Range(0, 2)) = 0.45
        _SpecularPower ("Specular Power", Range(8, 128)) = 48
        _ReflectionStrength ("Reflection Strength", Range(0, 2)) = 0.28
        _RefractionStrength ("Refraction Strength", Range(0, 0.05)) = 0.006
        _NoiseStrength ("Distortion Strength", Range(0, 0.05)) = 0.004
        _NoiseScale ("Distortion Scale", Range(4, 48)) = 12
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half4 _SpecularColor;
                half4 _ReflectionColor;
                half _BlurPixels;
                half _TintStrength;
                half _BackgroundInfluence;
                half _FresnelPower;
                half _EdgeStrength;
                half _AlphaSoftness;
                half _SpecularStrength;
                half _SpecularPower;
                half _ReflectionStrength;
                half _RefractionStrength;
                half _NoiseStrength;
                half _NoiseScale;
            CBUFFER_END

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
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);
                output.positionWS = positionInputs.positionWS;
                output.uv = input.uv;
                return output;
            }

            half3 SampleBlurredScene(float2 uv, float2 texelSize, half blurPixels)
            {
                float2 offsetA = texelSize * blurPixels * 1.15;
                float2 offsetB = texelSize * blurPixels * 2.35;
                float2 offsetC = texelSize * blurPixels * 3.6;

                half3 scene = SampleSceneColor(uv) * 0.16h;
                scene += SampleSceneColor(uv + float2(offsetA.x, 0.0)) * 0.11h;
                scene += SampleSceneColor(uv - float2(offsetA.x, 0.0)) * 0.11h;
                scene += SampleSceneColor(uv + float2(0.0, offsetA.y)) * 0.11h;
                scene += SampleSceneColor(uv - float2(0.0, offsetA.y)) * 0.11h;
                scene += SampleSceneColor(uv + float2(offsetA.x, offsetA.y)) * 0.07h;
                scene += SampleSceneColor(uv + float2(-offsetA.x, offsetA.y)) * 0.07h;
                scene += SampleSceneColor(uv + float2(offsetA.x, -offsetA.y)) * 0.07h;
                scene += SampleSceneColor(uv + float2(-offsetA.x, -offsetA.y)) * 0.07h;
                scene += SampleSceneColor(uv + float2(offsetB.x, 0.0)) * 0.04h;
                scene += SampleSceneColor(uv - float2(offsetB.x, 0.0)) * 0.04h;
                scene += SampleSceneColor(uv + float2(0.0, offsetB.y)) * 0.04h;
                scene += SampleSceneColor(uv - float2(0.0, offsetB.y)) * 0.04h;
                scene += SampleSceneColor(uv + float2(offsetC.x, offsetC.y)) * 0.015h;
                scene += SampleSceneColor(uv + float2(-offsetC.x, offsetC.y)) * 0.015h;
                scene += SampleSceneColor(uv + float2(offsetC.x, -offsetC.y)) * 0.015h;
                scene += SampleSceneColor(uv + float2(-offsetC.x, -offsetC.y)) * 0.015h;

                return scene * 0.99h;
            }

            half Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            half SmoothNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - (2.0 * f));

                half a = Hash21(i);
                half b = Hash21(i + float2(1.0, 0.0));
                half c = Hash21(i + float2(0.0, 1.0));
                half d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                uv = UnityStereoTransformScreenSpaceTex(uv);
                float2 texelSize = rcp(_ScreenParams.xy);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                half3 reflectDir = reflect(-viewDirWS, normalWS);
                float2 centeredUv = (input.uv - 0.5) * 2.0;
                half distortionX = SmoothNoise((input.positionWS.xy * _NoiseScale * 0.08) + float2(0.0, _Time.y * 0.02));
                half distortionY = SmoothNoise((input.positionWS.zy * _NoiseScale * 0.08) + float2(17.13, 9.47));
                float2 microDistortion = (float2(distortionX, distortionY) - 0.5) * (_NoiseStrength * 2.0);
                float2 lensOffset = centeredUv * _RefractionStrength * (0.18 + (fresnel * 0.34));
                float2 normalOffset = normalWS.xy * (_RefractionStrength * 0.55);
                float2 refractedUv = saturate(uv + lensOffset + normalOffset + microDistortion);

                half blurStrength = max(_BlurPixels, 0.001h) * (0.9h + (fresnel * 0.15h));
                half3 blurredScene = SampleBlurredScene(refractedUv, texelSize, blurStrength);

                Light mainLight = GetMainLight();
                half3 lightDir = normalize(mainLight.direction);
                half3 halfVector = normalize(lightDir + viewDirWS);
                half specular = pow(saturate(dot(normalWS, halfVector)), _SpecularPower) * _SpecularStrength;
                half rimSpecular = pow(saturate(dot(reflect(-lightDir, normalWS), viewDirWS)), max(8.0h, _SpecularPower * 0.5h)) * (_SpecularStrength * 0.45h);
                half reflection = smoothstep(-0.2h, 0.75h, reflectDir.y) * _ReflectionStrength * saturate(fresnel * 1.15h);
                half grazing = pow(saturate(1.0h - abs(dot(normalWS, lightDir))), 3.0h) * 0.14h;

                half blurMix = saturate(_BackgroundInfluence);
                half blurGain = max(1.0h, _BackgroundInfluence);
                half3 boostedBlur = blurredScene * blurGain;
                half3 tintedBlur = lerp(boostedBlur, _BaseColor.rgb, saturate(_TintStrength));
                half3 combined = lerp(_BaseColor.rgb, tintedBlur, blurMix);
                combined += _ReflectionColor.rgb * (reflection + grazing);
                combined += _SpecularColor.rgb * (specular + rimSpecular);
                combined += _EdgeColor.rgb * fresnel * _EdgeStrength;

                half alpha = saturate((_BaseColor.a + (fresnel * 0.16h) + (reflection * 0.04h)) * max(0.0h, _AlphaSoftness));
                return half4(combined, alpha);
            }
            ENDHLSL
        }
    }
}
