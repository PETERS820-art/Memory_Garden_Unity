Shader "MemoryGarden/Memory Painterly"
{
    // Usage:
    // 1. Create a material using MemoryGarden/Memory Painterly.
    // 2. Assign _BrushRampTex, _BrushGrainTex, _DryBrushTex, _WatercolorTex, and _EdgeBreakTex.
    // 3. Apply the material to a sphere, cube, or environment mesh.
    // 4. Increase EdgeBreakStrength, EdgeDistortion, and DryBrushStrength for stronger brushy edges.
    // 5. Adjust BaseColor, ShadowColor, LightTintColor, EmotionTintColor, and RimColor for palette changes.
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Color", Color) = (0.55, 0.63, 0.72, 1)
        _LightTintColor ("Light Tint Color", Color) = (1, 0.97, 0.93, 1)
        _AccentColor ("Accent Color", Color) = (1, 0.82, 0.6, 1)
        _AccentColorStrength ("Accent Color Strength", Range(0, 1)) = 0.42
        // Flatness controls: reduce volume and compress the lighting into soft painted bands.
        _FlattenAmount ("Flatten Amount", Range(0, 1)) = 0.58
        _LightRangeCompression ("Light Range Compression", Range(0, 1)) = 0.66
        _ShadeSteps ("Shade Steps", Range(1, 6)) = 3
        _NormalFlatten ("Normal Flatten", Range(0, 1)) = 0.55
        _EmotionTintColor ("Emotion Tint Color", Color) = (0.94, 0.9, 0.86, 1)
        _EmotionTintStrength ("Emotion Tint Strength", Range(0, 1)) = 0.15
        _StrokeDensity ("Stroke Density", Range(0, 1)) = 0.52
        _StrokeContrast ("Stroke Contrast", Range(0.5, 4)) = 1.9
        _Saturation ("Saturation", Range(0, 2)) = 1
        _Brightness ("Brightness", Range(0, 2)) = 1
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowSoftness ("Shadow Softness", Range(0.001, 0.5)) = 0.12
        _RampInfluence ("Ramp Influence", Range(0, 1)) = 0.7
        _BrushGrainStrength ("Brush Grain Strength", Range(0, 1)) = 0.25
        _BrushGrainScale ("Brush Grain Scale", Range(0.1, 10)) = 2
        _DryBrushStrength ("Dry Brush Strength", Range(0, 1)) = 0.35
        _DryBrushScale ("Dry Brush Scale", Range(0.1, 10)) = 1.4
        _WatercolorStrength ("Watercolor Strength", Range(0, 1)) = 0.35
        _WatercolorScale ("Watercolor Scale", Range(0.1, 10)) = 1.2
        // View-projected brush controls: project strokes in screen/view space to feel more like painted canvas.
        _ViewProjectionBlend ("View Projection Blend", Range(0, 1)) = 0.35
        _ViewBrushScale ("View Brush Scale", Range(0.25, 8)) = 2.2
        _ViewBrushStrength ("View Brush Strength", Range(0, 1)) = 0.38
        _ScreenGrainStrength ("Screen Grain Strength", Range(0, 1)) = 0.22
        // Growth transition controls: painterly effect grows outward from the focused memory item.
        [HideInInspector] _RuntimeTransitionActive ("Runtime Transition Active", Float) = 0
        _GrowthOrigin ("Growth Origin", Vector) = (0, 0, 0, 0)
        _GrowthRadius ("Growth Radius", Float) = 0
        _GrowthMaxRadius ("Growth Max Radius", Float) = 12
        _GrowthSoftness ("Growth Softness", Float) = 1.2
        _GrowthNoiseStrength ("Growth Noise Strength", Range(0, 2)) = 0.5
        _GrowthBlend ("Growth Blend", Range(0, 1)) = 0
        _EdgeBreakStrength ("Edge Break Strength", Range(0, 1)) = 0.45
        _EdgeBreakScale ("Edge Break Scale", Range(0.1, 10)) = 1.3
        _EdgeDistortion ("Edge Distortion", Range(0, 1)) = 0.18
        // Brushy shadow edge controls: directly break the light/shadow threshold with brush textures.
        _ShadowEdgeBreakStrength ("Shadow Edge Break Strength", Range(0, 1)) = 0.58
        _ShadowEdgeNoiseScale ("Shadow Edge Noise Scale", Range(0.1, 10)) = 1.85
        _ShadowEdgeBrushInfluence ("Shadow Edge Brush Influence", Range(0, 1)) = 0.62
        _RimColor ("Rim Color", Color) = (0.85, 0.92, 1, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.2
        _RimPower ("Rim Power", Range(0.5, 8)) = 3

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
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                half4 _BaseColor;
                half4 _ShadowColor;
                half4 _LightTintColor;
                half4 _AccentColor;
                half4 _EmotionTintColor;
                half4 _RimColor;
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
                half _BrushGrainScale;
                half _DryBrushStrength;
                half _DryBrushScale;
                half _WatercolorStrength;
                half _WatercolorScale;
                half _ViewProjectionBlend;
                half _ViewBrushScale;
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
                half _EdgeBreakScale;
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

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);
                Light mainLight = GetMainLight(input.shadowCoord);
                half additionalLightMaskBoost = 0.0h;
                half3 combinedAdditionalLightColor = 0.0h.xxx;

                #if defined(_ADDITIONAL_LIGHTS)
                    uint additionalLightsCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(additionalLightsCount)
                        Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS);
                        half3 additionalLightDirWS = normalize(additionalLight.direction);
                        half additionalShadowAttenuation = saturate(additionalLight.shadowAttenuation * additionalLight.distanceAttenuation);
                        half additionalNdotL = saturate(dot(normalWS, additionalLightDirWS));

                        additionalLightMaskBoost += additionalNdotL * additionalShadowAttenuation;
                        combinedAdditionalLightColor += additionalLight.color * additionalShadowAttenuation;
                    LIGHT_LOOP_END
                #endif

                float2 worldUV = input.positionWS.xz;
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float2 centeredScreenUV = (screenUV - 0.5) * _ViewBrushScale;

                float2 brushGrainUV = input.uv * _BrushGrainScale + worldUV * (0.035 * _BrushGrainScale);
                float2 dryBrushUV = input.uv * _DryBrushScale + worldUV * (0.045 * _DryBrushScale);
                float2 watercolorUV = input.uv * _WatercolorScale + worldUV * (0.03 * _WatercolorScale);
                float2 edgeBreakUV = input.uv * _EdgeBreakScale + worldUV * (0.05 * _EdgeBreakScale);

                float2 viewGrainUV = centeredScreenUV * _BrushGrainScale;
                float2 viewDryBrushUV = centeredScreenUV * _DryBrushScale;
                float2 viewWatercolorUV = centeredScreenUV * _WatercolorScale;
                float2 viewEdgeBreakUV = centeredScreenUV * _EdgeBreakScale;
                float2 shadowEdgeViewUV = centeredScreenUV * (_EdgeBreakScale * _ShadowEdgeNoiseScale);
                float2 shadowEdgeWorldUV = input.uv * (_EdgeBreakScale * _ShadowEdgeNoiseScale) + worldUV * (0.06 * _EdgeBreakScale * _ShadowEdgeNoiseScale);

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

                half brushGrain = lerp(brushGrainWorld, brushGrainView, grainBlend);
                brushGrain = lerp(brushGrain, brushGrain + brushGrainView * 0.35h, _ScreenGrainStrength);
                half dryBrush = lerp(dryBrushWorld, dryBrushView, viewBlend);
                half watercolor = lerp(watercolorWorld, watercolorView, saturate(viewBlend * 0.45h));
                half edgeBreak = lerp(edgeBreakWorld, edgeBreakView, viewBlend);

                half grain01 = saturate(brushGrain * 0.5h + 0.5h);
                half dry01 = saturate(dryBrush * 0.5h + 0.5h);
                half watercolor01 = saturate(watercolor * 0.5h + 0.5h);
                half edge01 = saturate(edgeBreak * 0.5h + 0.5h);

                // Flatness controls: reduce 3D volume by flattening normals and compressing light range into bands.
                half flatten = saturate(_FlattenAmount);
                half3 flattenedNormalWS = normalize(lerp(normalWS, -viewDirWS, saturate(flatten * _NormalFlatten)));
                half ndotlRaw = saturate(dot(normalWS, mainLight.direction));
                half ndotlFlattened = saturate(dot(flattenedNormalWS, mainLight.direction));
                half ndotl = saturate(lerp(ndotlRaw, ndotlFlattened, flatten));
                half compressedLight = 0.5h + (ndotl - 0.5h) * (1.0h - _LightRangeCompression * (0.78h + flatten * 0.18h));
                compressedLight = saturate(lerp(ndotl, compressedLight, saturate(_LightRangeCompression + flatten * 0.35h)));
                half bandedLight = SoftBandQuantize(compressedLight, _ShadeSteps, lerp(0.34h, 0.18h, flatten));
                half painterlyLight = saturate(lerp(compressedLight, bandedLight, saturate(0.45h + flatten * 0.45h)));

                // Brushy shadow edge controls: these textures directly distort the lighting threshold instead of only tinting final color.
                half shadowEdgeWorld = SAMPLE_TEXTURE2D(_EdgeBreakTex, sampler_EdgeBreakTex, shadowEdgeWorldUV).r * 2.0h - 1.0h;
                half shadowDryWorld = SAMPLE_TEXTURE2D(_DryBrushTex, sampler_DryBrushTex, shadowEdgeWorldUV * (_DryBrushScale / max(_EdgeBreakScale, 0.001h))).r * 2.0h - 1.0h;
                half shadowEdgeView = SAMPLE_TEXTURE2D(_EdgeBreakTex, sampler_EdgeBreakTex, shadowEdgeViewUV).r * 2.0h - 1.0h;
                half shadowDryView = SAMPLE_TEXTURE2D(_DryBrushTex, sampler_DryBrushTex, shadowEdgeViewUV * (_DryBrushScale / max(_EdgeBreakScale, 0.001h))).r * 2.0h - 1.0h;
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
                half realtimeShadow = saturate(mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                lightMask *= lerp(1.0h, realtimeShadow, 0.78h);
                lightMask = saturate(lightMask + saturate(additionalLightMaskBoost));

                half watercolorMask = watercolor * _WatercolorStrength;
                half3 baseColor = _BaseColor.rgb;
                half3 paintedBase = baseColor * (1.0h + watercolorMask * 0.12h);
                half accentMix = saturate(watercolor01 * 0.25h + dry01 * 0.18h) * _WatercolorStrength;
                paintedBase = lerp(paintedBase, paintedBase * _AccentColor.rgb, accentMix * saturate(0.18h + lightMask * 0.22h));

                half3 shadowColor = paintedBase * _ShadowColor.rgb;
                half3 lightColor = paintedBase * _LightTintColor.rgb;
                lightColor *= lerp(1.0h.xxx, saturate(rampSample * 1.25h), _RampInfluence * 0.16h);
                half3 color = lerp(shadowColor, lightColor, lightMask);

                half3 ambient = SampleSH(normalWS) * baseColor * lerp(0.26h, 0.18h, flatten);
                color += ambient * saturate(1.0h - lightMask * 0.38h);

                half strokeFieldA = saturate(watercolor01 * 0.42h + dry01 * 0.36h + grain01 * 0.22h);
                half strokeFieldB = saturate(dry01 * 0.42h + edge01 * 0.38h + (1.0h - grain01) * 0.2h);
                half strokeFieldC = saturate(edge01 * 0.3h + grain01 * 0.42h + watercolor01 * 0.28h);

                half strokeMaskLight = StrokeMask(strokeFieldA, _StrokeDensity, _StrokeContrast) * saturate(0.3h + lightMask * 0.9h);
                half strokeMaskShadow = StrokeMask(strokeFieldB, saturate(_StrokeDensity + 0.08h), _StrokeContrast) * saturate(1.05h - lightMask);
                half strokeMaskBreakup = StrokeMask(strokeFieldC, saturate(_StrokeDensity - 0.1h), _StrokeContrast * 0.85h);

                half3 warmStrokeColor = paintedBase * lerp(_LightTintColor.rgb, _AccentColor.rgb, 0.78h);
                half3 coolStrokeColor = paintedBase * lerp(_ShadowColor.rgb, _AccentColor.rgb, 0.34h);
                half3 paperStrokeColor = paintedBase * lerp(1.0h.xxx, _LightTintColor.rgb, 0.12h);

                color = lerp(color, warmStrokeColor, strokeMaskLight * _AccentColorStrength);
                color = lerp(color, coolStrokeColor, strokeMaskShadow * (_AccentColorStrength * 0.95h));
                color = lerp(color, paperStrokeColor, strokeMaskBreakup * (_BrushGrainStrength * 0.42h + _WatercolorStrength * 0.15h));

                half3 grainTint = 1.0h.xxx + brushGrain * (_BrushGrainStrength * 0.16h);
                color *= grainTint;

                half3 lightHue = lerp(1.0h.xxx, saturate(mainLight.color + combinedAdditionalLightColor), 0.2h);
                color *= lightHue;
                half emotionBrushMask = StrokeMask(saturate(watercolor01 * 0.58h + grain01 * 0.22h + edge01 * 0.2h), saturate(_StrokeDensity - 0.16h), _StrokeContrast * 0.75h);
                color = lerp(color, color * _EmotionTintColor.rgb, saturate(_EmotionTintStrength) * lerp(0.38h, 0.92h, emotionBrushMask));

                half rim = pow(saturate(1.0h - dot(normalWS, viewDirWS)), max(_RimPower, 0.001h)) * _RimStrength * (1.0h - flatten * 0.22h);
                color += _RimColor.rgb * rim * (0.28h + 0.45h * saturate(lightMask + 0.15h));

                half neutralLightMask = saturate(0.42h + ndotlRaw * 0.38h);
                half3 neutralShadowColor = lerp(baseColor * 0.72h, shadowColor, 0.35h);
                half3 neutralLightColor = lerp(baseColor * 0.95h, lightColor, 0.22h);
                half3 neutralColor = lerp(neutralShadowColor, neutralLightColor, neutralLightMask);

                half growthMask = ComputeGrowthMask(input.positionWS, edgeBreak, watercolor);
                half growthBlend = lerp(1.0h, saturate(growthMask * _GrowthBlend), saturate(_RuntimeTransitionActive));
                color = lerp(neutralColor, color, growthBlend);

                color = ApplySaturation(color, _Saturation);
                color *= _Brightness;
                color = MixFog(color, input.fogFactor);
                return half4(saturate(color), 1.0h);
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

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

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
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
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

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
