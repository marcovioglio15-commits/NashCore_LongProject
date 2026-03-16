Shader "Cel Shader/Toon Diffuse Blur"
{
    Properties
    {
        // Added 16/03/2026
        [Header(Main Maps)]
        
        [MainTexture]
        _MainTex("Texture", 2D) = "white" {}

        
        [NoScaleOffset]
        _NormalMap("Normal Map", 2D) = "bump" {}

        
        _BumpScale("Bump Scale", Range(0,2)) = 1

        [Header(Toon Lighting)]
        
        _AmbientColor("Ambient Color", Color) = (0,0,0,1)

        
        _AmbientColorIntensity("Ambient Color Intensity", Range(0,5)) = 0.5

        
        _ShadowSoftness("Shadow Softness", Range(0,0.5)) = 0.1

        
        _ShadowScatter("Shadow Scatter", Range(0,10)) = 5

        
        _ShadowRangeMin("Shadow Range Min", Range(0,1)) = 0.54

        
        _ShadowRangeMax("Shadow Range Max", Range(-2,2)) = -0.4

        // Added 16/03/2026: These controls introduce a texture-space blur instead of a full-screen post-process
        [Header(Texture Blur)]
        
        _BlurRadius("Blur Radius", Range(0,4)) = 1

        
        _BlurStrength("Blur Strength", Range(0,1)) = 0.35

        // Added 16/03/2026: Distance styling reinforces the Spore-like background separation through
        // atmospheric flattening, desaturation and extra blur as objects move away from the camera.
        [Header(Distance Separation)]
        
        _DistanceBlendStart("Distance Blend Start", Float) = 8

        
        _DistanceBlendEnd("Distance Blend End", Float) = 30

        
        _DistanceBlurBoost("Distance Blur Boost", Range(0,1)) = 0.35

        
        _DistanceBlurScale("Distance Blur Scale", Range(1,3)) = 1.6

        
        _DistanceTintColor("Distance Tint Color", Color) = (0.82,0.9,1,1)

        
        _DistanceTintStrength("Distance Tint Strength", Range(0,1)) = 0.2

        
        _DistanceSaturation("Distance Saturation", Range(0,1)) = 0.72

        
        _DistanceContrast("Distance Contrast", Range(0.25,1.25)) = 0.82
    }

    SubShader
    {
        Blend SrcAlpha OneMinusSrcAlpha

        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "LightMode" = "UniversalForward"
        }

        Pass
        {
            Name "ToonDiffuseTransparencyBlur"

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            // Added 16/03/2026: World position is carried to the fragment stage so the shader can
            // increase blur and atmospheric flattening with camera distance.
            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float3 normalWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _NormalMap;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize; // Added 16/03/2026
            float _BumpScale;
            float4 _AmbientColor;
            float _AmbientColorIntensity;
            float _ShadowSoftness;
            float _ShadowScatter;
            float _ShadowRangeMin;
            float _ShadowRangeMax;
            float _BlurRadius; // Added 16/03/2026
            float _BlurStrength; // Added 16/03/2026
            float _DistanceBlendStart; // Added 16/03/2026
            float _DistanceBlendEnd; // Added 16/03/2026
            float _DistanceBlurBoost; // Added 16/03/2026
            float _DistanceBlurScale; // Added 16/03/2026
            float4 _DistanceTintColor; // Added 16/03/2026
            float _DistanceTintStrength; // Added 16/03/2026
            float _DistanceSaturation; // Added 16/03/2026
            float _DistanceContrast; // Added 16/03/2026

            // Added 16/03/2026
            struct BlurAccumulator
            {
                float3 colorSum;
                float alphaSum;
                float colorWeightSum;
                float kernelWeightSum;
            };

            float InverseLerpSafe(float minValue, float maxValue, float value) // Added 16/03/2026
            {
                float range = max(abs(maxValue - minValue), 0.0001);
                return saturate((value - minValue) / range);
            }

            float3 DecodeToonNormal(float4 normalSample, float bumpScale) // Added 16/03/2026
            {
                float3 normalTS;
                normalTS.xy = normalSample.wy * 2.0 - 1.0;
                normalTS.xy *= bumpScale;
                normalTS.z = sqrt(saturate(1.0 - dot(normalTS.xy, normalTS.xy)));
                normalTS = normalTS.xzy;
                return normalize(normalTS);
            }

            void AccumulateBlurSample(float2 uv, float kernelWeight, inout BlurAccumulator accumulator) // Added 16/03/2026
            {
                float4 sampleColor = tex2D(_MainTex, uv);
                float colorWeight = kernelWeight * max(sampleColor.a, 0.0001);
                accumulator.colorSum += sampleColor.rgb * colorWeight;
                accumulator.alphaSum += sampleColor.a * kernelWeight;
                accumulator.colorWeightSum += colorWeight;
                accumulator.kernelWeightSum += kernelWeight;
            }

            float4 SampleBlurredMainTexture(float2 uv, float2 texelSize, float blurRadius) // Added 16/03/2026
            {
                float2 offset = texelSize * blurRadius;
                BlurAccumulator accumulator = (BlurAccumulator)0;

                AccumulateBlurSample(uv, 4.0, accumulator);
                AccumulateBlurSample(uv + float2(offset.x, 0.0), 2.0, accumulator);
                AccumulateBlurSample(uv - float2(offset.x, 0.0), 2.0, accumulator);
                AccumulateBlurSample(uv + float2(0.0, offset.y), 2.0, accumulator);
                AccumulateBlurSample(uv - float2(0.0, offset.y), 2.0, accumulator);
                AccumulateBlurSample(uv + offset, 1.0, accumulator);
                AccumulateBlurSample(uv - offset, 1.0, accumulator);
                AccumulateBlurSample(uv + float2(offset.x, -offset.y), 1.0, accumulator);
                AccumulateBlurSample(uv + float2(-offset.x, offset.y), 1.0, accumulator);

                return float4(
                    accumulator.colorSum / max(accumulator.colorWeightSum, 0.0001),
                    accumulator.alphaSum / max(accumulator.kernelWeightSum, 0.0001));
            }

            float GetDistanceBlendFactor(float3 worldPosition, float3 cameraPosition, float distanceStart, float distanceEnd) // Added 16/03/2026
            {
                float safeDistanceEnd = max(distanceEnd, distanceStart + 0.0001);
                float distanceToCamera = distance(worldPosition, cameraPosition);
                return InverseLerpSafe(distanceStart, safeDistanceEnd, distanceToCamera);
            }

            float3 ApplyDistanceSeparation(
                float3 color,
                float distanceFactor,
                float3 tintColor,
                float tintStrength,
                float saturationAtMaxDistance,
                float contrastAtMaxDistance) // Added 16/03/2026
            {
                float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
                float saturation = lerp(1.0, saturationAtMaxDistance, distanceFactor);
                float contrast = lerp(1.0, contrastAtMaxDistance, distanceFactor);
                float3 saturatedColor = lerp(luminance.xxx, color, saturation);
                float3 contrastedColor = saturate((saturatedColor - 0.5.xxx) * contrast + 0.5.xxx);
                float tintBlend = saturate(distanceFactor * tintStrength);
                return lerp(contrastedColor, contrastedColor * tintColor, tintBlend);
            }

            float3 EvaluateToonLighting(
                float3 albedo,
                float ramp,
                float3 ambientColor,
                float ambientIntensity,
                float shadowSoftness,
                float shadowScatter,
                float shadowRangeMin,
                float shadowRangeMax) // Added 16/03/2026
            {
                float scatter = max(shadowScatter / 50.0, 0.0001);
                float remap = InverseLerpSafe(-1.0, 1.0, ramp);
                float shadowFloor = floor(remap / scatter);
                float shadowRemap = InverseLerpSafe(1.0 / scatter, 0.0, shadowFloor);
                float shadowRange = lerp(shadowRangeMin, shadowRangeMax, shadowRemap);
                float3 lighting = smoothstep(0.0, shadowSoftness, shadowRange) + ambientColor * ambientIntensity;
                return albedo * (albedo + lighting);
            }

            float4 EvaluateStylizedToonColor(
                float2 uv,
                float3 meshNormal,
                float3 toonNormal,
                float3 lightDirection,
                float2 texelSize,
                float blurRadius,
                float blurStrength,
                float distanceFactor,
                float distanceBlurBoost,
                float distanceBlurScale,
                float3 distanceTintColor,
                float distanceTintStrength,
                float distanceSaturation,
                float distanceContrast,
                float3 ambientColor,
                float ambientIntensity,
                float shadowSoftness,
                float shadowScatter,
                float shadowRangeMin,
                float shadowRangeMax) // Added 16/03/2026
            {
                float effectiveBlurStrength = saturate(blurStrength + distanceFactor * distanceBlurBoost);
                float effectiveBlurRadius = max(blurRadius, 0.0) * lerp(1.0, distanceBlurScale, distanceFactor);
                float4 baseSample = tex2D(_MainTex, uv);
                float4 blurredSample = baseSample;

                if (effectiveBlurStrength > 0.0001 && effectiveBlurRadius > 0.0001)
                {
                    blurredSample = SampleBlurredMainTexture(uv, texelSize, effectiveBlurRadius);
                }

                float4 stylizedSample = lerp(baseSample, blurredSample, effectiveBlurStrength);
                stylizedSample.rgb = ApplyDistanceSeparation(
                    stylizedSample.rgb,
                    distanceFactor,
                    distanceTintColor,
                    distanceTintStrength,
                    distanceSaturation,
                    distanceContrast);

                float ramp = dot(dot(lightDirection, meshNormal).xxx, toonNormal);
                float3 finalColor = EvaluateToonLighting(
                    stylizedSample.rgb,
                    ramp,
                    ambientColor,
                    ambientIntensity,
                    shadowSoftness,
                    shadowScatter,
                    shadowRangeMin,
                    shadowRangeMax);

                return float4(finalColor, stylizedSample.a);
            }

            // Added 16/03/2026: Normal decoding is isolated so the fragment path stays compact 
            float3 ResolveToonNormal(float2 uv)
            {
                float4 normalSample = tex2D(_NormalMap, uv);
                return DecodeToonNormal(normalSample, _BumpScale);
            }

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = normalize(UnityObjectToWorldNormal(input.normal)); // Added 16/03/2026
                output.positionWS = mul(unity_ObjectToWorld, input.vertex).xyz; // Added 16/03/2026
                UNITY_TRANSFER_FOG(output, output.vertex);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float3 toonNormal = ResolveToonNormal(input.uv); // Added 16/03/2026
                float distanceFactor = GetDistanceBlendFactor(
                    input.positionWS,
                    _WorldSpaceCameraPos.xyz,
                    _DistanceBlendStart,
                    _DistanceBlendEnd); // Added 16/03/2026

                // Added 16/03/2026: The shared evaluator blurs the albedo, applies atmospheric separation
                // and then runs the toon ramp so the object remains readable even when softened.
                float4 finalColor = EvaluateStylizedToonColor(
                    input.uv,
                    input.normalWS,
                    toonNormal,
                    lightDir,
                    _MainTex_TexelSize.xy,
                    _BlurRadius,
                    _BlurStrength,
                    distanceFactor,
                    _DistanceBlurBoost,
                    _DistanceBlurScale,
                    _DistanceTintColor.rgb,
                    _DistanceTintStrength,
                    _DistanceSaturation,
                    _DistanceContrast,
                    _AmbientColor.rgb,
                    _AmbientColorIntensity,
                    _ShadowSoftness,
                    _ShadowScatter,
                    _ShadowRangeMin,
                    _ShadowRangeMax);

                UNITY_APPLY_FOG(input.fogCoord, finalColor);
                return finalColor;
            }

            ENDCG
        }
    }
}
