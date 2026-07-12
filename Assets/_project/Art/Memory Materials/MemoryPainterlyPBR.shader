Shader "MemoryGarden/Memory Painterly PBR"
{
    // Usage:
    // 1. Assign this shader to environment/item materials that should preserve their own PBR textures.
    // 2. Put each item's base map, normal map, metallic/smoothness map, occlusion map, and UV scale on the item material.
    // 3. Use dedicated emotion target materials with the same shader and set _MemoryBlend to 1 for palette tuning.
    // 4. At runtime, the manager can copy only painterly/emotion values from the emotion material while item PBR textures stay local.
    Properties
    {
        [HideInInspector] _WorkflowMode ("Workflow Mode", Float) = 1
        [Enum(Opaque, 0, Transparent, 1)] _Surface ("Surface Type", Float) = 0
        [Enum(Alpha, 0, Premultiply, 1, Multiply, 2)] _Blend ("Blend Mode", Float) = 0
        [Enum(Off, 0, Front, 1, Back, 2)] _Cull ("Render Face", Float) = 2
        [ToggleUI] _AlphaClip ("Alpha Clipping", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [ToggleUI] _ReceiveShadows ("Receive Shadows", Float) = 1
        [ToggleUI] _SpecularHighlights ("Specular Highlights", Float) = 1
        [ToggleUI] _EnvironmentReflections ("Environment Reflections", Float) = 1
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
        _QueueOffset ("Queue Offset", Range(-50, 50)) = 0

        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _PainterlyBaseColor ("Painterly Base Color", Color) = (1, 1, 1, 0)
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 2)) = 1
        _MetallicGlossMap ("Metallic Smoothness Map", 2D) = "black" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _OcclusionMap ("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionMap ("Emission Map", 2D) = "black" {}
        _HighlightBoost ("Highlight Boost", Range(0, 4)) = 1
        [HDR] _TranslucencyColor ("Translucency Color", Color) = (1, 0.82, 0.62, 1)
        _TranslucencyStrength ("Translucency Strength", Range(0, 4)) = 0
        _TranslucencyPower ("Translucency Power", Range(0.25, 8)) = 2
        _TranslucencyWrap ("Translucency Wrap", Range(0, 1)) = 0.4
        _TranslucencyViewDependency ("Translucency View Dependency", Range(0, 4)) = 1.25
        _TranslucencyHalo ("Translucency Halo", Range(0, 2)) = 0
        _PainterlyScale ("Painterly Scale", Range(0.1, 16)) = 1

        _MemoryBlend ("Memory Blend", Range(0, 1)) = 0

        // Flatness controls.
        _ShadowColor ("Shadow Color", Color) = (0.55, 0.63, 0.72, 1)
        _LightTintColor ("Light Tint Color", Color) = (1, 0.97, 0.93, 1)
        _AccentColor ("Accent Color", Color) = (1, 0.82, 0.6, 1)
        _AccentColorStrength ("Accent Color Strength", Range(0, 1)) = 0.42
        _EmotionTintColor ("Emotion Tint Color", Color) = (0.94, 0.9, 0.86, 1)
        _EmotionTintStrength ("Emotion Tint Strength", Range(0, 1)) = 0.15
        _RimColor ("Rim Color", Color) = (0.85, 0.92, 1, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.2
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _FlattenAmount ("Flatten Amount", Range(0, 1)) = 0.58
        _LightRangeCompression ("Light Range Compression", Range(0, 1)) = 0.66
        _ShadeSteps ("Shade Steps", Range(1, 6)) = 3
        _NormalFlatten ("Normal Flatten", Range(0, 1)) = 0.55

        _StrokeDensity ("Stroke Density", Range(0, 1)) = 0.52
        _StrokeContrast ("Stroke Contrast", Range(0.5, 4)) = 1.9
        _Saturation ("Saturation", Range(0, 2)) = 1
        _Brightness ("Brightness", Range(0, 2)) = 1
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowSoftness ("Shadow Softness", Range(0.001, 0.5)) = 0.12
        _RampInfluence ("Ramp Influence", Range(0, 1)) = 0.7
        _BrushGrainStrength ("Brush Grain Strength", Range(0, 1)) = 0.25
        _DryBrushStrength ("Dry Brush Strength", Range(0, 1)) = 0.35
        _WatercolorStrength ("Watercolor Strength", Range(0, 1)) = 0.35
        _EdgeBreakStrength ("Edge Break Strength", Range(0, 1)) = 0.45
        _EdgeDistortion ("Edge Distortion", Range(0, 1)) = 0.18

        // View-projected brush controls.
        _ViewProjectionBlend ("View Projection Blend", Range(0, 1)) = 0.35
        _ViewBrushStrength ("View Brush Strength", Range(0, 1)) = 0.38
        _ScreenGrainStrength ("Screen Grain Strength", Range(0, 1)) = 0.22

        // Brushy shadow edge controls.
        _ShadowEdgeBreakStrength ("Shadow Edge Break Strength", Range(0, 1)) = 0.58
        _ShadowEdgeNoiseScale ("Shadow Edge Noise Scale", Range(0.1, 4)) = 1.85
        _ShadowEdgeBrushInfluence ("Shadow Edge Brush Influence", Range(0, 1)) = 0.62

        [HideInInspector] _RuntimeTransitionActive ("Runtime Transition Active", Float) = 0
        _GrowthOrigin ("Growth Origin", Vector) = (0, 0, 0, 0)
        _GrowthRadius ("Growth Radius", Float) = 0
        _GrowthMaxRadius ("Growth Max Radius", Float) = 12
        _GrowthSoftness ("Growth Softness", Float) = 1.2
        _GrowthNoiseStrength ("Growth Noise Strength", Range(0, 2)) = 0.5
        _GrowthBlend ("Growth Blend", Range(0, 1)) = 0

        [NoScaleOffset] _BrushRampTex ("Brush Ramp Tex", 2D) = "white" {}
        [NoScaleOffset] _BrushGrainTex ("Brush Grain Tex", 2D) = "gray" {}
        [NoScaleOffset] _DryBrushTex ("Dry Brush Tex", 2D) = "gray" {}
        [NoScaleOffset] _WatercolorTex ("Watercolor Tex", 2D) = "gray" {}
        [NoScaleOffset] _EdgeBreakTex ("Edge Break Tex", 2D) = "gray" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local_fragment _ALPHAMODULATE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicGlossMap);
            SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_OcclusionMap);
            SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_BrushRampTex);
            SAMPLER(sampler_BrushRampTex);
            TEXTURE2D(_BrushGrainTex);
            SAMPLER(sampler_BrushGrainTex);
            TEXTURE2D(_DryBrushTex);
            SAMPLER(sampler_DryBrushTex);
            TEXTURE2D(_WatercolorTex);
            SAMPLER(sampler_WatercolorTex);
            TEXTURE2D(_EdgeBreakTex);
            SAMPLER(sampler_EdgeBreakTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _PainterlyBaseColor;
                half4 _ShadowColor;
                half4 _LightTintColor;
                half4 _AccentColor;
                half4 _EmotionTintColor;
                half4 _RimColor;
                half4 _EmissionColor;
                half4 _TranslucencyColor;
                half _Cutoff;
                half _ReceiveShadows;
                half _SpecularHighlights;
                half _EnvironmentReflections;
                half _BumpScale;
                half _Metallic;
                half _Smoothness;
                half _OcclusionStrength;
                half _HighlightBoost;
                half _TranslucencyStrength;
                half _TranslucencyPower;
                half _TranslucencyWrap;
                half _TranslucencyViewDependency;
                half _TranslucencyHalo;
                half _PainterlyScale;
                half _MemoryBlend;
                half _AccentColorStrength;
                half _FlattenAmount;
                half _LightRangeCompression;
                half _ShadeSteps;
                half _NormalFlatten;
                half _EmotionTintStrength;
                half _StrokeDensity;
                half _StrokeContrast;
                half _Saturation;
                half _Brightness;
                half _ShadowThreshold;
                half _ShadowSoftness;
                half _RampInfluence;
                half _BrushGrainStrength;
                half _DryBrushStrength;
                half _WatercolorStrength;
                half _ViewProjectionBlend;
                half _ViewBrushStrength;
                half _ScreenGrainStrength;
                half _RuntimeTransitionActive;
                float4 _GrowthOrigin;
                half _GrowthRadius;
                half _GrowthMaxRadius;
                half _GrowthSoftness;
                half _GrowthNoiseStrength;
                half _GrowthBlend;
                half _EdgeBreakStrength;
                half _EdgeDistortion;
                half _ShadowEdgeBreakStrength;
                half _ShadowEdgeNoiseScale;
                half _ShadowEdgeBrushInfluence;
                half _RimStrength;
                half _RimPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                half3 viewDirWS : TEXCOORD5;
                half4 tangentWS : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half PainterlyLuminance(half3 color)
            {
                return dot(color, half3(0.299h, 0.587h, 0.114h));
            }

            half3 ApplySaturation(half3 color, half saturation)
            {
                half luminance = PainterlyLuminance(color);
                return lerp(luminance.xxx, color, saturation);
            }

            half StrokeMask(half field, half center, half contrast)
            {
                half width = 0.24h / max(contrast, 0.001h);
                return smoothstep(center - width, center + width, field);
            }

            half SoftBandQuantize(half value, half steps, half softness)
            {
                half safeSteps = max(steps, 1.0h);
                half scaled = saturate(value) * safeSteps;
                half band = floor(scaled);
                half fracPart = scaled - band;
                half blend = smoothstep(0.5h - softness, 0.5h + softness, fracPart);
                return saturate((band + blend) / safeSteps);
            }

            half ComputeGrowthMask(float3 positionWS, half edgeBreak, half watercolor)
            {
                half softness = max(_GrowthSoftness, 0.0001h);
                half maxRadius = max(_GrowthMaxRadius, 0.0001h);
                half growthProgress = saturate(_GrowthRadius / maxRadius);
                half growthNoise = (edgeBreak * 0.68h + watercolor * 0.32h) * _GrowthNoiseStrength;
                half noisyDistance = distance(positionWS, _GrowthOrigin.xyz) + growthNoise * softness * lerp(0.7h, 1.2h, growthProgress);
                half mask = 1.0h - smoothstep(_GrowthRadius - softness, _GrowthRadius + softness, noisyDistance);
                return saturate(mask);
            }

            half ComputeTranslucencyTerm(half3 normalWS, half3 viewDirWS, half3 lightDirWS)
            {
                half backNdotL = saturate(dot(-normalWS, lightDirWS));
                half wrappedBack = saturate((backNdotL + _TranslucencyWrap) / (1.0h + _TranslucencyWrap));
                half viewScatter = pow(saturate(dot(viewDirWS, -lightDirWS)), max(_TranslucencyViewDependency, 0.001h));
                half halo = pow(saturate(1.0h - abs(dot(normalWS, viewDirWS))), 2.0h) * _TranslucencyHalo;
                return pow(wrappedBack, max(_TranslucencyPower, 0.001h)) * _TranslucencyStrength * (1.0h + viewScatter + halo);
            }

            float2 GetPerEyeNormalizedScreenSpaceUV(float4 positionCS)
            {
                float2 screenUV = GetNormalizedScreenSpaceUV(positionCS);

                #if defined(UNITY_SINGLE_PASS_STEREO)
                    float4 scaleOffset = unity_StereoScaleOffset[unity_StereoEyeIndex];
                    float2 safeScale = max(scaleOffset.xy, float2(0.00001, 0.00001));
                    screenUV = (screenUV - scaleOffset.zw) / safeScale;
                #endif

                return saturate(screenUV);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = half4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale());
                output.uv = input.uv;
                output.shadowCoord = GetShadowCoord(vertexInput);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 baseUV = TRANSFORM_TEX(input.uv, _BaseMap);
                half painterlyScale = max(_PainterlyScale, 0.0001h);

                half4 albedoSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
                half alpha = saturate(albedoSample.a * _BaseColor.a);
                #if defined(_ALPHATEST_ON)
                    clip(alpha - _Cutoff);
                #endif

                half3 pbrAlbedo = albedoSample.rgb * _BaseColor.rgb;
                half painterlyBaseOverride = step(0.001h, _PainterlyBaseColor.a);
                half3 painterlyBaseTint = lerp(_BaseColor.rgb, _PainterlyBaseColor.rgb, painterlyBaseOverride);
                half3 painterlySourceAlbedo = albedoSample.rgb * painterlyBaseTint;

                half4 normalSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, baseUV);
                half3 normalTS = UnpackNormalScale(normalSample, _BumpScale);
                half3 normalWS = normalize(input.normalWS);
                half3 tangentWS = normalize(input.tangentWS.xyz);
                half tangentSign = input.tangentWS.w;
                half3 bitangentWS = normalize(cross(normalWS, tangentWS) * tangentSign);
                half3x3 tangentToWorld = half3x3(tangentWS, bitangentWS, normalWS);
                normalWS = normalize(TransformTangentToWorld(normalTS, tangentToWorld));

                half4 metallicGlossSample = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, baseUV);
                half metallic = saturate(max(_Metallic, metallicGlossSample.r));
                half smoothness = saturate(max(_Smoothness, metallicGlossSample.a));
                half occlusionSample = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, baseUV).g;
                half occlusion = lerp(1.0h, occlusionSample, _OcclusionStrength);
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, baseUV).rgb * _EmissionColor.rgb;

                half3 viewDirWS = normalize(input.viewDirWS);
                Light mainLight = GetMainLight(input.shadowCoord);
                half3 lightDirWS = normalize(mainLight.direction);
                half ndotlPBR = saturate(dot(normalWS, lightDirWS));
                half shadowAttenuation = saturate(mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                shadowAttenuation = lerp(1.0h, shadowAttenuation, saturate(_ReceiveShadows));
                half specularPower = exp2(2.0h + smoothness * 10.0h);
                half highlightBoost = max(_HighlightBoost, 0.0h);
                half3 specularColor = lerp(half3(0.04h, 0.04h, 0.04h), pbrAlbedo, metallic);

                half3 ambient = SampleSH(normalWS) * pbrAlbedo * occlusion;
                ambient *= saturate(_EnvironmentReflections);
                half3 diffuse = pbrAlbedo * mainLight.color * ndotlPBR * shadowAttenuation;
                half3 additionalDiffuse = 0.0h.xxx;
                half3 additionalSpecular = 0.0h.xxx;
                half3 translucency = _TranslucencyColor.rgb * mainLight.color
                    * ComputeTranslucencyTerm(normalWS, viewDirWS, lightDirWS)
                    * shadowAttenuation;
                half additionalLightMaskBoost = 0.0h;
                half3 combinedAdditionalLightColor = 0.0h.xxx;

                #if defined(_ADDITIONAL_LIGHTS)
                    uint additionalLightsCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(additionalLightsCount)
                        Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS);
                        half3 additionalLightDirWS = normalize(additionalLight.direction);
                        half additionalShadowAttenuation = saturate(additionalLight.shadowAttenuation * additionalLight.distanceAttenuation);
                        half additionalNdotLPBR = saturate(dot(normalWS, additionalLightDirWS));
                        half3 additionalHalfDirWS = SafeNormalize(additionalLightDirWS + viewDirWS);
                        half additionalNdotH = saturate(dot(normalWS, additionalHalfDirWS));
                        half additionalSpecularTerm = pow(additionalNdotH, specularPower) * additionalNdotLPBR * additionalShadowAttenuation;

                        additionalDiffuse += pbrAlbedo * additionalLight.color * additionalNdotLPBR * additionalShadowAttenuation;
                        additionalSpecular += specularColor * additionalLight.color * additionalSpecularTerm * lerp(0.35h, 1.2h, smoothness);
                        translucency += _TranslucencyColor.rgb * additionalLight.color
                            * ComputeTranslucencyTerm(normalWS, viewDirWS, additionalLightDirWS)
                            * additionalShadowAttenuation;
                        additionalLightMaskBoost += additionalNdotLPBR * additionalShadowAttenuation;
                        combinedAdditionalLightColor += additionalLight.color * additionalShadowAttenuation;
                    LIGHT_LOOP_END
                #endif

                half3 halfDirWS = SafeNormalize(lightDirWS + viewDirWS);
                half ndoth = saturate(dot(normalWS, halfDirWS));
                half specularTerm = pow(ndoth, specularPower) * ndotlPBR * shadowAttenuation;
                half3 specular = specularColor * mainLight.color * specularTerm * lerp(0.35h, 1.2h, smoothness);
                specular *= saturate(_SpecularHighlights) * highlightBoost;
                additionalSpecular *= saturate(_SpecularHighlights) * highlightBoost;

                half3 pbrColor = ambient + diffuse + additionalDiffuse + specular + additionalSpecular + emission;

                float2 worldUV = input.positionWS.xz * painterlyScale;
                float2 screenUV = GetPerEyeNormalizedScreenSpaceUV(input.positionCS);
                float2 centeredScreenUV = (screenUV - 0.5) * painterlyScale;

                float2 brushGrainUV = baseUV * painterlyScale + worldUV * (0.035 * painterlyScale);
                float2 dryBrushUV = baseUV * painterlyScale + worldUV * (0.045 * painterlyScale);
                float2 watercolorUV = baseUV * painterlyScale + worldUV * (0.03 * painterlyScale);
                float2 edgeBreakUV = baseUV * painterlyScale + worldUV * (0.05 * painterlyScale);

                float2 viewGrainUV = centeredScreenUV;
                float2 viewDryBrushUV = centeredScreenUV;
                float2 viewWatercolorUV = centeredScreenUV;
                float2 viewEdgeBreakUV = centeredScreenUV;
                float2 shadowEdgeViewUV = centeredScreenUV * _ShadowEdgeNoiseScale;
                float2 shadowEdgeWorldUV = baseUV * (painterlyScale * _ShadowEdgeNoiseScale) + worldUV * (0.06 * painterlyScale * _ShadowEdgeNoiseScale);

                half brushGrainWorld = SAMPLE_TEXTURE2D(_BrushGrainTex, sampler_BrushGrainTex, brushGrainUV).r * 2.0h - 1.0h;
                half dryBrushWorld = SAMPLE_TEXTURE2D(_DryBrushTex, sampler_DryBrushTex, dryBrushUV).r * 2.0h - 1.0h;
                half watercolorWorld = SAMPLE_TEXTURE2D(_WatercolorTex, sampler_WatercolorTex, watercolorUV).r * 2.0h - 1.0h;
                half edgeBreakWorld = SAMPLE_TEXTURE2D(_EdgeBreakTex, sampler_EdgeBreakTex, edgeBreakUV).r * 2.0h - 1.0h;

                half brushGrainView = SAMPLE_TEXTURE2D(_BrushGrainTex, sampler_BrushGrainTex, viewGrainUV).r * 2.0h - 1.0h;
                half dryBrushView = SAMPLE_TEXTURE2D(_DryBrushTex, sampler_DryBrushTex, viewDryBrushUV).r * 2.0h - 1.0h;
                half watercolorView = SAMPLE_TEXTURE2D(_WatercolorTex, sampler_WatercolorTex, viewWatercolorUV).r * 2.0h - 1.0h;
                half edgeBreakView = SAMPLE_TEXTURE2D(_EdgeBreakTex, sampler_EdgeBreakTex, viewEdgeBreakUV).r * 2.0h - 1.0h;

                half viewBlend = saturate(_ViewProjectionBlend * _ViewBrushStrength);
                half grainBlend = saturate(_ViewProjectionBlend * (0.45h + _ScreenGrainStrength));
                half screenGrainStrength = _ScreenGrainStrength;

                #if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                    // Screen-space brush projection causes binocular mismatch in VR, so fall back to world/object anchored brush fields.
                    viewBlend = 0.0h;
                    grainBlend = 0.0h;
                    screenGrainStrength = 0.0h;
                #endif

                half brushGrain = lerp(brushGrainWorld, brushGrainView, grainBlend);
                brushGrain = lerp(brushGrain, brushGrain + brushGrainView * 0.35h, screenGrainStrength);
                half dryBrush = lerp(dryBrushWorld, dryBrushView, viewBlend);
                half watercolor = lerp(watercolorWorld, watercolorView, saturate(viewBlend * 0.45h));
                half edgeBreak = lerp(edgeBreakWorld, edgeBreakView, viewBlend);

                half grain01 = saturate(brushGrain * 0.5h + 0.5h);
                half dry01 = saturate(dryBrush * 0.5h + 0.5h);
                half watercolor01 = saturate(watercolor * 0.5h + 0.5h);
                half edge01 = saturate(edgeBreak * 0.5h + 0.5h);

                half flatten = saturate(_FlattenAmount);
                half3 flattenedNormalWS = normalize(lerp(normalWS, -viewDirWS, saturate(flatten * _NormalFlatten)));
                half ndotlRaw = saturate(dot(normalWS, lightDirWS));
                half ndotlFlattened = saturate(dot(flattenedNormalWS, lightDirWS));
                half ndotl = saturate(lerp(ndotlRaw, ndotlFlattened, flatten));
                half compressedLight = 0.5h + (ndotl - 0.5h) * (1.0h - _LightRangeCompression * (0.78h + flatten * 0.18h));
                compressedLight = saturate(lerp(ndotl, compressedLight, saturate(_LightRangeCompression + flatten * 0.35h)));
                half bandedLight = SoftBandQuantize(compressedLight, _ShadeSteps, lerp(0.34h, 0.18h, flatten));
                half painterlyLight = saturate(lerp(compressedLight, bandedLight, saturate(0.45h + flatten * 0.45h)));

                half shadowEdgeWorld = SAMPLE_TEXTURE2D(_EdgeBreakTex, sampler_EdgeBreakTex, shadowEdgeWorldUV).r * 2.0h - 1.0h;
                half shadowDryWorld = SAMPLE_TEXTURE2D(_DryBrushTex, sampler_DryBrushTex, shadowEdgeWorldUV).r * 2.0h - 1.0h;
                half shadowEdgeView = SAMPLE_TEXTURE2D(_EdgeBreakTex, sampler_EdgeBreakTex, shadowEdgeViewUV).r * 2.0h - 1.0h;
                half shadowDryView = SAMPLE_TEXTURE2D(_DryBrushTex, sampler_DryBrushTex, shadowEdgeViewUV).r * 2.0h - 1.0h;
                half shadowEdgeNoise = lerp(shadowEdgeWorld * 0.58h + shadowDryWorld * 0.42h, shadowEdgeView * 0.58h + shadowDryView * 0.42h, viewBlend);
                shadowEdgeNoise += (watercolor * 0.22h + brushGrain * 0.12h) * _ShadowEdgeBrushInfluence;

                half boundaryNoise = edgeBreak * _EdgeBreakStrength + dryBrush * _DryBrushStrength;
                half edgeShift = shadowEdgeNoise * (_ShadowEdgeBreakStrength * (0.28h + _ShadowEdgeBrushInfluence * 0.3h)) + boundaryNoise * (_EdgeDistortion * 0.35h);
                half distortedLight = saturate(painterlyLight + edgeShift);
                half threshold = saturate(_ShadowThreshold + edgeShift * (0.9h + _ShadowEdgeBrushInfluence * 0.55h));
                half softness = max(_ShadowSoftness * lerp(1.1h, 0.82h, flatten), 0.001h);
                half toonMask = smoothstep(threshold - softness, threshold + softness, distortedLight);

                half rampCoord = saturate(lerp(distortedLight, toonMask, _RampInfluence));
                half rampV = saturate(0.5h + shadowEdgeNoise * 0.24h + watercolor * 0.08h);
                half3 rampSample = SAMPLE_TEXTURE2D(_BrushRampTex, sampler_BrushRampTex, float2(rampCoord, rampV)).rgb;
                half rampValue = saturate(PainterlyLuminance(rampSample));
                half lightMask = saturate(lerp(toonMask, rampValue, _RampInfluence));
                lightMask *= lerp(1.0h, shadowAttenuation, 0.78h);
                lightMask = saturate(lightMask + saturate(additionalLightMaskBoost));

                half albedoLuma = PainterlyLuminance(painterlySourceAlbedo);
                half3 flattenedPainterlyBase = lerp(albedoLuma.xxx * painterlyBaseTint, painterlyBaseTint, 0.42h);
                half painterlyTextureFlatten = saturate(flatten * 0.6h + _MemoryBlend * 0.35h);
                half3 painterlyBase = lerp(painterlySourceAlbedo, flattenedPainterlyBase, painterlyTextureFlatten);
                half watercolorMask = watercolor * _WatercolorStrength;
                half3 paintedBase = painterlyBase * (1.0h + watercolorMask * 0.12h);
                half accentMix = saturate(watercolor01 * 0.25h + dry01 * 0.18h) * _WatercolorStrength;
                paintedBase = lerp(paintedBase, paintedBase * _AccentColor.rgb, accentMix * saturate(0.18h + lightMask * 0.22h));

                half3 shadowColor = paintedBase * _ShadowColor.rgb;
                half3 lightColor = paintedBase * _LightTintColor.rgb;
                lightColor *= lerp(1.0h.xxx, saturate(rampSample * 1.25h), _RampInfluence * 0.16h);
                half3 painterlyColor = lerp(shadowColor, lightColor, lightMask);

                painterlyColor += SampleSH(normalWS) * painterlyBase * lerp(0.26h, 0.18h, flatten) * saturate(1.0h - lightMask * 0.38h);

                half strokeFieldA = saturate(watercolor01 * 0.42h + dry01 * 0.36h + grain01 * 0.22h);
                half strokeFieldB = saturate(dry01 * 0.42h + edge01 * 0.38h + (1.0h - grain01) * 0.2h);
                half strokeFieldC = saturate(edge01 * 0.3h + grain01 * 0.42h + watercolor01 * 0.28h);

                half strokeMaskLight = StrokeMask(strokeFieldA, _StrokeDensity, _StrokeContrast) * saturate(0.3h + lightMask * 0.9h);
                half strokeMaskShadow = StrokeMask(strokeFieldB, saturate(_StrokeDensity + 0.08h), _StrokeContrast) * saturate(1.05h - lightMask);
                half strokeMaskBreakup = StrokeMask(strokeFieldC, saturate(_StrokeDensity - 0.1h), _StrokeContrast * 0.85h);

                half3 warmStrokeColor = paintedBase * lerp(_LightTintColor.rgb, _AccentColor.rgb, 0.78h);
                half3 coolStrokeColor = paintedBase * lerp(_ShadowColor.rgb, _AccentColor.rgb, 0.34h);
                half3 paperStrokeColor = paintedBase * lerp(1.0h.xxx, _LightTintColor.rgb, 0.12h);

                painterlyColor = lerp(painterlyColor, warmStrokeColor, strokeMaskLight * _AccentColorStrength);
                painterlyColor = lerp(painterlyColor, coolStrokeColor, strokeMaskShadow * (_AccentColorStrength * 0.95h));
                painterlyColor = lerp(painterlyColor, paperStrokeColor, strokeMaskBreakup * (_BrushGrainStrength * 0.42h + _WatercolorStrength * 0.15h));
                painterlyColor *= 1.0h.xxx + brushGrain * (_BrushGrainStrength * 0.16h);
                painterlyColor *= lerp(1.0h.xxx, saturate(mainLight.color + combinedAdditionalLightColor), 0.2h);

                half emotionBrushMask = StrokeMask(saturate(watercolor01 * 0.58h + grain01 * 0.22h + edge01 * 0.2h), saturate(_StrokeDensity - 0.16h), _StrokeContrast * 0.75h);
                painterlyColor = lerp(painterlyColor, painterlyColor * _EmotionTintColor.rgb, saturate(_EmotionTintStrength) * lerp(0.38h, 0.92h, emotionBrushMask));

                half rim = pow(saturate(1.0h - dot(normalWS, viewDirWS)), max(_RimPower, 0.001h)) * _RimStrength * (1.0h - flatten * 0.22h);
                painterlyColor += _RimColor.rgb * rim * (0.28h + 0.45h * saturate(lightMask + 0.15h));

                half growthMask = ComputeGrowthMask(input.positionWS, edgeBreak, watercolor);
                half transitionMask = lerp(1.0h, saturate(growthMask * _GrowthBlend), saturate(_RuntimeTransitionActive));
                half painterlyBlend = saturate(_MemoryBlend) * transitionMask;

                half3 finalColor = lerp(pbrColor, painterlyColor, painterlyBlend);
                finalColor += translucency;
                finalColor = ApplySaturation(finalColor, _Saturation);
                finalColor *= _Brightness;
                finalColor = MixFog(finalColor, input.fogFactor);
                half3 hdrSafeFinalColor = max(finalColor, 0.0h.xxx);

                #if defined(_ALPHAMODULATE_ON)
                    half3 multiplyColor = lerp(1.0h.xxx, saturate(hdrSafeFinalColor), alpha);
                    return half4(multiplyColor, alpha);
                #elif defined(_ALPHAPREMULTIPLY_ON)
                    return half4(hdrSafeFinalColor * alpha, alpha);
                #else
                    return half4(hdrSafeFinalColor, alpha);
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            half SampleAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(uv, _BaseMap)).a * _BaseColor.a;
            }

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = GetShadowPositionHClip(input);
                output.uv = input.uv;
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                #if defined(_ALPHATEST_ON)
                    clip(SampleAlpha(input.uv) - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            half SampleAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(uv, _BaseMap)).a * _BaseColor.a;
            }

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                #if defined(_ALPHATEST_ON)
                    clip(SampleAlpha(input.uv) - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }
    }

    CustomEditor "MemoryPainterlyPBRShaderGUI"
    FallBack Off
}
