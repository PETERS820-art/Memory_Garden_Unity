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
        _BackgroundLumaThreshold ("Background Luma Threshold", Range(0, 2)) = 0.62
        _BackgroundLumaKnee ("Background Luma Knee", Range(0.01, 2)) = 0.38
        _BrightSceneAbsorption ("Bright Scene Absorption", Range(0, 1.5)) = 0.75
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.8
        _EdgeStrength ("Edge Strength", Range(0, 2)) = 0.35
        _AlphaSoftness ("Alpha Softness", Range(0, 8)) = 1.0
        _SpecularStrength ("Specular Strength", Range(0, 2)) = 0.45
        _SpecularPower ("Specular Power", Range(8, 128)) = 48
        _ReflectionStrength ("Reflection Strength", Range(0, 2)) = 0.28
        _RefractionStrength ("Refraction Strength", Range(0, 0.05)) = 0.006
        _NoiseStrength ("Distortion Strength", Range(0, 0.05)) = 0.004
        _NoiseScale ("Distortion Scale", Range(4, 48)) = 12
        _DebugView ("Debug View", Range(0, 4)) = 0
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

            TEXTURE2D_X(_MemoryUIBlurTexture);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half4 _SpecularColor;
                half4 _ReflectionColor;
                half _BlurPixels;
                half _TintStrength;
                half _BackgroundInfluence;
                half _BackgroundLumaThreshold;
                half _BackgroundLumaKnee;
                half _BrightSceneAbsorption;
                half _FresnelPower;
                half _EdgeStrength;
                half _AlphaSoftness;
                half _SpecularStrength;
                half _SpecularPower;
                half _ReflectionStrength;
                half _RefractionStrength;
                half _NoiseStrength;
                half _NoiseScale;
                half _DebugView;
            CBUFFER_END

            float _MemoryUIBlurAvailable;

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

            half3 SampleSceneFallbackBlur(float2 uv, float2 texelSize, half blurPixels)
            {
                half blur = max(blurPixels, 6.0h);
                float2 offsetA = texelSize * blur * 1.25;
                float2 offsetB = texelSize * blur * 2.5;
                float2 offsetC = texelSize * blur * 4.0;

                half3 scene = SampleSceneColor(uv) * 0.10h;
                scene += SampleSceneColor(uv + float2(offsetA.x, 0.0)) * 0.09h;
                scene += SampleSceneColor(uv - float2(offsetA.x, 0.0)) * 0.09h;
                scene += SampleSceneColor(uv + float2(0.0, offsetA.y)) * 0.09h;
                scene += SampleSceneColor(uv - float2(0.0, offsetA.y)) * 0.09h;
                scene += SampleSceneColor(uv + float2(offsetA.x, offsetA.y)) * 0.06h;
                scene += SampleSceneColor(uv + float2(-offsetA.x, offsetA.y)) * 0.06h;
                scene += SampleSceneColor(uv + float2(offsetA.x, -offsetA.y)) * 0.06h;
                scene += SampleSceneColor(uv + float2(-offsetA.x, -offsetA.y)) * 0.06h;
                scene += SampleSceneColor(uv + float2(offsetB.x, 0.0)) * 0.035h;
                scene += SampleSceneColor(uv - float2(offsetB.x, 0.0)) * 0.035h;
                scene += SampleSceneColor(uv + float2(0.0, offsetB.y)) * 0.035h;
                scene += SampleSceneColor(uv - float2(0.0, offsetB.y)) * 0.035h;
                scene += SampleSceneColor(uv + float2(offsetB.x, offsetB.y)) * 0.025h;
                scene += SampleSceneColor(uv + float2(-offsetB.x, offsetB.y)) * 0.025h;
                scene += SampleSceneColor(uv + float2(offsetB.x, -offsetB.y)) * 0.025h;
                scene += SampleSceneColor(uv + float2(-offsetB.x, -offsetB.y)) * 0.025h;
                scene += SampleSceneColor(uv + float2(offsetC.x, 0.0)) * 0.015h;
                scene += SampleSceneColor(uv - float2(offsetC.x, 0.0)) * 0.015h;
                scene += SampleSceneColor(uv + float2(0.0, offsetC.y)) * 0.015h;
                scene += SampleSceneColor(uv - float2(0.0, offsetC.y)) * 0.015h;

                return scene;
            }

            half3 SampleRendererFeatureBlur(float2 uv)
            {
                if (_MemoryUIBlurAvailable > 0.5)
                {
                    return SAMPLE_TEXTURE2D_X(_MemoryUIBlurTexture, sampler_LinearClamp, uv).rgb;
                }

                return 0.0h;
            }

            half3 SampleBlurredScene(float2 uv, float2 texelSize, half blurPixels)
            {
                half3 fallbackScene = SampleSceneFallbackBlur(uv, texelSize, blurPixels);
                half3 featureScene = SampleRendererFeatureBlur(uv);
                half featureLuma = dot(featureScene, half3(0.2126h, 0.7152h, 0.0722h));

                if (_MemoryUIBlurAvailable > 0.5 && featureLuma > 0.001h)
                {
                    return lerp(featureScene, fallbackScene, 0.12h);
                }

                return fallbackScene;
            }

            half3 CompressBackgroundHighlights(half3 color, half threshold, half knee)
            {
                half luma = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                if (luma <= threshold)
                {
                    return color;
                }

                half safeThreshold = max(0.001h, threshold);
                half safeKnee = max(0.01h, knee);
                half excess = luma - safeThreshold;
                half compressedLuma = safeThreshold + (excess / (1.0h + (excess / safeKnee)));
                return color * (compressedLuma / max(luma, 0.001h));
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
                half3 fallbackBlurredScene = SampleSceneFallbackBlur(refractedUv, texelSize, blurStrength);
                half3 featureBlurredScene = SampleRendererFeatureBlur(refractedUv);
                half3 blurredScene = SampleBlurredScene(refractedUv, texelSize, blurStrength);

                if (_DebugView > 2.5h)
                {
                    return half4(saturate(featureBlurredScene), 0.96h);
                }

                if (_DebugView > 1.5h)
                {
                    return half4(saturate(fallbackBlurredScene), 0.96h);
                }

                if (_DebugView > 0.5h)
                {
                    half3 debugColor = half3(frac(input.uv.x * 6.0), frac(input.uv.y * 6.0), 1.0h - frac((input.uv.x + input.uv.y) * 3.0));
                    debugColor = lerp(debugColor, half3(1.0h, 1.0h, 1.0h), fresnel * 0.35h);
                    return half4(debugColor, 0.85h);
                }

                Light mainLight = GetMainLight();
                half3 lightDir = normalize(mainLight.direction);
                half3 halfVector = normalize(lightDir + viewDirWS);
                half specular = pow(saturate(dot(normalWS, halfVector)), _SpecularPower) * (_SpecularStrength * 1.15h);
                half rimSpecular = pow(saturate(dot(reflect(-lightDir, normalWS), viewDirWS)), max(8.0h, _SpecularPower * 0.5h)) * (_SpecularStrength * 0.62h);
                half reflection = smoothstep(-0.2h, 0.75h, reflectDir.y) * (_ReflectionStrength * 1.12h) * saturate(fresnel * 1.15h);
                half grazing = pow(saturate(1.0h - abs(dot(normalWS, lightDir))), 3.0h) * 0.18h;
                half featureBlurActive = (_MemoryUIBlurAvailable > 0.5 && dot(featureBlurredScene, half3(0.2126h, 0.7152h, 0.0722h)) > 0.001h) ? 1.0h : 0.0h;

                half blurMix = saturate(lerp(0.92h, 1.18h, featureBlurActive) * _BackgroundInfluence);
                half blurGain = lerp(1.15h, 1.34h, featureBlurActive) * max(1.0h, _BackgroundInfluence * 0.96h);
                half3 boostedBlur = CompressBackgroundHighlights(
                    blurredScene * blurGain,
                    _BackgroundLumaThreshold,
                    _BackgroundLumaKnee);
                half boostedLuma = dot(boostedBlur, half3(0.2126h, 0.7152h, 0.0722h));
                half safeThreshold = _BackgroundLumaThreshold;
                half safeKnee = max(_BackgroundLumaKnee, 0.01h);
                half brightMask = saturate((boostedLuma - safeThreshold) / safeKnee);
                brightMask = brightMask * brightMask * (3.0h - (2.0h * brightMask));
                half absorption = saturate(brightMask * _BrightSceneAbsorption);
                half absorptionTintScale = 0.95h - (0.40h * fresnel);
                half3 absorptionTint = _BaseColor.rgb * absorptionTintScale;
                half3 absorbedBlur = lerp(boostedBlur, absorptionTint, absorption);
                blurMix *= (1.0h - (0.42h * absorption));
                half tintStrength = saturate(
                    lerp(_TintStrength * 0.42h, _TintStrength * 0.24h, featureBlurActive) +
                    (brightMask * _BrightSceneAbsorption * 0.22h));
                half3 tintedBlur = lerp(absorbedBlur, _BaseColor.rgb, tintStrength);
                half3 combined = lerp(_BaseColor.rgb * 0.06h, tintedBlur, blurMix);
                combined += _ReflectionColor.rgb * (reflection + grazing);
                combined += _SpecularColor.rgb * (specular + rimSpecular);
                combined += _EdgeColor.rgb * fresnel * _EdgeStrength;

                half transmissionAlpha = lerp(0.76h, 0.92h, featureBlurActive);
                transmissionAlpha = max(transmissionAlpha, _BaseColor.a * 6.0h);
                half alpha = saturate((transmissionAlpha + (fresnel * 0.04h) + (reflection * 0.02h)) * min(1.0h, max(0.0h, _AlphaSoftness)));
                return half4(combined, alpha);
            }
            ENDHLSL
        }
    }
}
