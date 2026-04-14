Shader "Custom/LiquidAntibioticBeam"
{
    Properties
    {
        [Header(Palette)] _CoreColor("Core Color", Color) = (0.96, 0.99, 1.0, 1.0)
        _FlowColor("Flow Color", Color) = (0.14, 0.86, 1.0, 1.0)
        _StormColor("Storm Color", Color) = (0.76, 0.97, 1.0, 1.0)
        _ContactColor("Contact Color", Color) = (0.96, 0.99, 1.0, 1.0)
        [Header(Body)] _Opacity("Opacity", Range(0, 1)) = 0.92
        _CoreBrightness("Core Brightness", Range(0, 8)) = 1.5
        _RimBrightness("Rim Brightness", Range(0, 8)) = 1.35
        _FlowScrollSpeed("Flow Scroll Speed", Range(0, 16)) = 1.2
        _FlowPulseFrequency("Flow Pulse Frequency", Range(0, 16)) = 1.6
        _WobbleAmplitude("Wobble Amplitude", Range(0, 2)) = 0.08
        _BubbleDriftSpeed("Bubble Drift Speed", Range(0, 16)) = 1.8
        _BeamRole("Beam Role", Range(0, 3)) = 0
        _BodyLayerRole("Body Layer Role", Range(0, 2)) = 1
        _CapShape("Cap Shape", Range(0, 2)) = 0
        _SegmentLength("Segment Length", Float) = 1.0
        _WidthScale("Width Scale", Float) = 0.25
        _CoreWidthMultiplier("Core Width Multiplier", Float) = 0.52
        [Header(Storm)] _StormTwistSpeed("Storm Twist Speed", Float) = 14.0
        _StormIdleIntensity("Storm Idle Intensity", Float) = 0.48
        _StormBurstIntensity("Storm Burst Intensity", Float) = 1.1
        _StormBurstNormalized("Storm Burst Normalized", Range(0, 1)) = 0.0
        _StormShellWidthMultiplier("Storm Shell Width Multiplier", Float) = 1.12
        _StormShellSeparation("Storm Shell Separation", Float) = 0.32
        _StormRingFrequency("Storm Ring Frequency", Float) = 5.4
        _StormRingThickness("Storm Ring Thickness", Float) = 0.18
        _StormTickProgressA("Storm Tick Progress A", Vector) = (1, 1, 1, 1)
        _StormTickProgressB("Storm Tick Progress B", Vector) = (1, 1, 1, 1)
        _StormTickActiveA("Storm Tick Active A", Vector) = (0, 0, 0, 0)
        _StormTickActiveB("Storm Tick Active B", Vector) = (0, 0, 0, 0)
        [Header(Endpoints)] _SourceDischargeIntensity("Source Discharge Intensity", Float) = 1.2
        _TerminalCapIntensity("Terminal Cap Intensity", Float) = 1.12
        _ContactFlareIntensity("Contact Flare Intensity", Float) = 1.28
        _TerminalBlockedByWall("Terminal Blocked By Wall", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            Name "ForwardUnlit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float3 viewDirWS : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _FlowColor;
                half4 _StormColor;
                half4 _ContactColor;
                half _Opacity;
                half _CoreBrightness;
                half _RimBrightness;
                half _FlowScrollSpeed;
                half _FlowPulseFrequency;
                half _WobbleAmplitude;
                half _BubbleDriftSpeed;
                half _BeamRole;
                half _BodyLayerRole;
                half _CapShape;
                half _SegmentLength;
                half _WidthScale;
                half _CoreWidthMultiplier;
                half _StormTwistSpeed;
                half _StormIdleIntensity;
                half _StormBurstIntensity;
                half _StormBurstNormalized;
                half _StormShellWidthMultiplier;
                half _StormShellSeparation;
                half _StormRingFrequency;
                half _StormRingThickness;
                half4 _StormTickProgressA;
                half4 _StormTickProgressB;
                half4 _StormTickActiveA;
                half4 _StormTickActiveB;
                half _SourceDischargeIntensity;
                half _TerminalCapIntensity;
                half _ContactFlareIntensity;
                half _TerminalBlockedByWall;
            CBUFFER_END

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 345.45));
                value += dot(value, value + 34.345);
                return frac(value.x * value.y);
            }

            float ComputeWrappedBand(float coordinate, float phase, float width)
            {
                float wrappedDistance = abs(frac(coordinate - phase) * 2.0 - 1.0);
                return smoothstep(max(0.0001, width), 0.0, wrappedDistance);
            }

            float ComputeElectricArc(float angleUv, float phase, float width)
            {
                float arcA = ComputeWrappedBand(angleUv, phase, width);
                float arcB = ComputeWrappedBand(angleUv, phase + 0.33, width * 1.18);
                float arcC = ComputeWrappedBand(angleUv, phase + 0.67, width * 0.9);
                return saturate(max(arcA, max(arcB, arcC)));
            }

            float ComputeStormTickContribution(float normalizedDistance, float progress, float active, float tickWidth)
            {
                if (progress > 1.0)
                    return 0.0;

                return smoothstep(tickWidth, 0.0, abs(normalizedDistance - saturate(progress))) * saturate(active);
            }

            float ComputeStormTickTrailContribution(float normalizedDistance, float progress, float active, float featherWidth)
            {
                if (progress > 1.0)
                    return saturate(active);

                float saturatedProgress = saturate(progress);
                float trailHead = saturatedProgress + max(0.0001, featherWidth);
                float trailTail = saturatedProgress - max(0.0001, featherWidth);
                return saturate(active) * (1.0 - smoothstep(trailTail, trailHead, normalizedDistance));
            }

            float ComputeStormTickMask(float normalizedDistance, float tickWidth)
            {
                float tickMask = 0.0;
                tickMask += ComputeStormTickContribution(normalizedDistance, _StormTickProgressA.x, _StormTickActiveA.x, tickWidth);
                tickMask += ComputeStormTickContribution(normalizedDistance, _StormTickProgressA.y, _StormTickActiveA.y, tickWidth);
                tickMask += ComputeStormTickContribution(normalizedDistance, _StormTickProgressA.z, _StormTickActiveA.z, tickWidth);
                tickMask += ComputeStormTickContribution(normalizedDistance, _StormTickProgressA.w, _StormTickActiveA.w, tickWidth);
                tickMask += ComputeStormTickContribution(normalizedDistance, _StormTickProgressB.x, _StormTickActiveB.x, tickWidth);
                tickMask += ComputeStormTickContribution(normalizedDistance, _StormTickProgressB.y, _StormTickActiveB.y, tickWidth);
                tickMask += ComputeStormTickContribution(normalizedDistance, _StormTickProgressB.z, _StormTickActiveB.z, tickWidth);
                tickMask += ComputeStormTickContribution(normalizedDistance, _StormTickProgressB.w, _StormTickActiveB.w, tickWidth);
                return saturate(tickMask);
            }

            float ComputeStormTickTrailMask(float normalizedDistance, float featherWidth)
            {
                float trailMask = 0.0;
                trailMask += ComputeStormTickTrailContribution(normalizedDistance, _StormTickProgressA.x, _StormTickActiveA.x, featherWidth);
                trailMask += ComputeStormTickTrailContribution(normalizedDistance, _StormTickProgressA.y, _StormTickActiveA.y, featherWidth);
                trailMask += ComputeStormTickTrailContribution(normalizedDistance, _StormTickProgressA.z, _StormTickActiveA.z, featherWidth);
                trailMask += ComputeStormTickTrailContribution(normalizedDistance, _StormTickProgressA.w, _StormTickActiveA.w, featherWidth);
                trailMask += ComputeStormTickTrailContribution(normalizedDistance, _StormTickProgressB.x, _StormTickActiveB.x, featherWidth);
                trailMask += ComputeStormTickTrailContribution(normalizedDistance, _StormTickProgressB.y, _StormTickActiveB.y, featherWidth);
                trailMask += ComputeStormTickTrailContribution(normalizedDistance, _StormTickProgressB.z, _StormTickActiveB.z, featherWidth);
                trailMask += ComputeStormTickTrailContribution(normalizedDistance, _StormTickProgressB.w, _StormTickActiveB.w, featherWidth);
                return saturate(trailMask);
            }

            float ResolveShapeMask(float2 localPosition, float capShape, float role)
            {
                float radius = length(localPosition);
                float angle = atan2(localPosition.y, localPosition.x);
                float starWave = abs(sin(angle * 4.0)) * 0.28 + abs(sin(angle * 8.0)) * 0.08;
                float softDisc = smoothstep(1.0, 0.08, radius);
                float scallopedDisc = smoothstep(1.02, 0.06, radius * (1.0 + sin(angle * 6.0) * 0.08));
                float starBloom = smoothstep(1.08 + starWave, 0.12, radius);

                if (capShape < 0.5)
                    return scallopedDisc;

                if (capShape < 1.5)
                    return starBloom;

                if (role > 2.5)
                    return smoothstep(1.1, 0.1, length(float2(localPosition.x * 0.78, localPosition.y * 1.36)));

                return softDisc;
            }

            float ResolveBodyLayerOffset(float3 normalOS, float widthScale, float bodyLayerRole)
            {
                float normalLength = max(0.0001, length(normalOS));
                float3 normalizedNormal = normalOS / normalLength;
                float offset = 0.0;

                if (bodyLayerRole < 0.5)
                    offset = -widthScale * (1.0 - saturate(_CoreWidthMultiplier)) * 0.46;
                else if (bodyLayerRole > 1.5)
                    offset = widthScale * max(0.02, (_StormShellWidthMultiplier - 1.0) * 0.74 + _StormShellSeparation * 1.35);

                return dot(normalizedNormal, normalOS) * offset;
            }

            float3 ApplyBodyLayerVertexOffset(float3 positionOS, float3 normalOS)
            {
                if (_BeamRole > 0.5)
                    return positionOS;

                float3 normalizedNormal = normalize(normalOS);
                float offset = ResolveBodyLayerOffset(normalOS, max(0.01, _WidthScale), _BodyLayerRole);
                float stormWave = sin(_Time.y * max(0.1, _StormTwistSpeed) * 0.9 + positionOS.z * 1.6 + positionOS.x * 1.1);

                if (_BodyLayerRole > 1.5)
                    offset += max(0.0, _WidthScale) * 0.08 * stormWave;

                return positionOS + normalizedNormal * offset;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 displacedPositionOS = ApplyBodyLayerVertexOffset(input.positionOS.xyz, input.normalOS);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(displacedPositionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.uv = input.uv;
                output.positionOS = displacedPositionOS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);
                return output;
            }

            half4 RenderBody(Varyings input)
            {
                float normalizedDistance = saturate(input.uv.x);
                float circumferenceUv = frac(input.uv.y);
                float timeValue = _Time.y;
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float edgeFresnel = pow(saturate(1.0 - abs(dot(normalWS, viewDirWS))), 1.42);
                float faceMask = pow(saturate(1.0 - edgeFresnel), 0.72);
                float stormBurstNormalized = saturate(_StormBurstNormalized);
                float stormIdleIntensity = saturate(max(0.0, _StormIdleIntensity));
                float stormBurstIntensity = saturate(max(0.0, _StormBurstIntensity) * stormBurstNormalized);
                float stormIntensity = saturate(stormIdleIntensity + stormBurstIntensity);
                float flowTravel = timeValue * max(0.1, _FlowScrollSpeed);
                float flowPhase = normalizedDistance * max(4.0, _SegmentLength * 1.08) - flowTravel * 0.82;
                float flowWaveA = sin(flowPhase * 6.28318 + circumferenceUv * 12.56636 + flowTravel * 0.18);
                float flowWaveB = sin(flowPhase * 11.3 - circumferenceUv * 18.84954 + timeValue * max(0.1, _BubbleDriftSpeed) * 1.12);
                float flowBand = saturate(0.52 + flowWaveA * 0.26 + flowWaveB * 0.18);
                float shimmer = 0.5 + 0.5 * sin(flowPhase * max(0.35, _FlowPulseFrequency) * 3.14159 + flowWaveB * 0.55);
                float ringPhase = normalizedDistance * max(0.1, _StormRingFrequency) - timeValue * max(0.1, _StormTwistSpeed) * 0.045;
                float ringMask = ComputeWrappedBand(ringPhase + circumferenceUv * 0.5,
                                                    0.5 + flowWaveA * 0.012,
                                                    max(0.05, _StormRingThickness * 0.58));
                float arcMask = ComputeElectricArc(circumferenceUv,
                                                   ringPhase * 0.72 + flowWaveB * 0.02,
                                                   lerp(0.19, 0.075, stormIntensity));
                float tickWidth = lerp(0.15, 0.065, stormIntensity);
                float tickTrailFeather = lerp(0.12, 0.04, stormIntensity);
                float tickMask = ComputeStormTickMask(normalizedDistance, tickWidth);
                float tickTrailMask = ComputeStormTickTrailMask(normalizedDistance, tickTrailFeather);
                float idleStormMask = saturate((ringMask * 0.52 + arcMask * 0.44) * stormIdleIntensity);
                float burstStormMask = saturate((ringMask * 0.82 + arcMask * 0.94) *
                                                tickTrailMask *
                                                (0.26 + stormBurstIntensity * 1.12));
                float stormMask = saturate(idleStormMask + burstStormMask + tickMask * 1.34);
                half3 color = 0.0h;
                half alpha = 0.0h;

                if (_BodyLayerRole < 0.5)
                {
                    float corePulse = saturate(0.5 + shimmer * 0.3 + flowBand * 0.18);
                    color = lerp(_FlowColor.rgb, _CoreColor.rgb, 0.38h) * corePulse * _CoreBrightness * 0.72h;
                    color += _StormColor.rgb * saturate(tickMask + tickTrailMask * 0.42) * 0.18h * _RimBrightness;
                    alpha = saturate(_Opacity * (0.1h + faceMask * 0.42h));
                    return half4(color, alpha);
                }

                if (_BodyLayerRole < 1.5)
                {
                    float sheathGlow = saturate(0.46 + flowBand * 0.54 + shimmer * 0.16);
                    color = lerp(_FlowColor.rgb, _StormColor.rgb, 0.08h) * sheathGlow * (0.42h + faceMask * 0.98h);
                    color += lerp(_FlowColor.rgb, _CoreColor.rgb, 0.22h) * faceMask * _CoreBrightness * 0.24h;
                    color += _StormColor.rgb * saturate(tickMask + tickTrailMask * 0.58) * 0.22h * _RimBrightness;
                    alpha = saturate(_Opacity * (0.28h + faceMask * 0.56h + edgeFresnel * 0.18h));
                    return half4(color, alpha);
                }

                float stormEdge = saturate(edgeFresnel * 1.32 + ringMask * 0.34);
                float stormGlow = saturate(stormMask * stormIntensity * (0.36 + stormIntensity * 0.72));
                float stormNoise = Hash21(floor(input.positionWS.xz * 3.8 + normalizedDistance * 19.0 + timeValue * (max(0.1, _BubbleDriftSpeed) + 0.8)));
                stormGlow = saturate(stormGlow + smoothstep(0.74, 1.0, stormNoise + tickMask * 0.35 + tickTrailMask * 0.22 + stormIntensity * 0.16) * (stormIntensity * 0.34));
                color = lerp(_FlowColor.rgb, _StormColor.rgb, 0.62h) * stormGlow * stormEdge * _RimBrightness;
                color += lerp(_StormColor.rgb, _ContactColor.rgb, 0.32h) * saturate(tickMask + tickTrailMask * 0.38) * (0.84h + stormBurstNormalized * 0.46h);
                color += _CoreColor.rgb * tickMask * 0.16h;
                alpha = saturate(_Opacity * (stormGlow * 0.68h + tickMask * 0.44h + tickTrailMask * 0.2h));
                return half4(color, alpha);
            }

            half4 RenderSource(Varyings input)
            {
                float2 localPosition = input.positionOS.xy * 2.1;
                float radius = length(localPosition);
                float angleUv = frac(atan2(localPosition.y, localPosition.x) / 6.28318 + 0.5);
                float mask = ResolveShapeMask(localPosition, _CapShape, _BeamRole);
                float timeValue = _Time.y;
                float stormBurstNormalized = saturate(_StormBurstNormalized);
                float stormIntensity = saturate(max(0.0, _StormIdleIntensity) + max(0.0, _StormBurstIntensity) * stormBurstNormalized);
                float apertureRing = ComputeWrappedBand(radius * 0.72 - timeValue * max(0.1, _FlowScrollSpeed) * 0.34,
                                                       0.32,
                                                       0.22);
                float electricArc = ComputeElectricArc(angleUv,
                                                       timeValue * max(0.1, _StormTwistSpeed) * 0.085 + radius * 0.16,
                                                       lerp(0.22, 0.09, stormIntensity));
                float coreFlash = smoothstep(0.66, 0.04, radius);
                half3 color = _FlowColor.rgb * mask * (0.38h + apertureRing * 0.54h);
                color += lerp(_FlowColor.rgb, _CoreColor.rgb, 0.42h) * coreFlash * _CoreBrightness * 0.82h;
                color += _StormColor.rgb * electricArc * mask * _SourceDischargeIntensity * (0.54h + stormIntensity * 0.56h);
                color += _ContactColor.rgb * electricArc * apertureRing * 0.28h * _SourceDischargeIntensity;
                half alpha = saturate(mask * _Opacity * (0.48h + electricArc * 0.42h + coreFlash * 0.22h));
                return half4(color, alpha);
            }

            half4 RenderTerminalCap(Varyings input)
            {
                float2 localPosition = input.positionOS.xy * 2.05;
                float radius = length(localPosition);
                float angleUv = frac(atan2(localPosition.y, localPosition.x) / 6.28318 + 0.5);
                float mask = ResolveShapeMask(localPosition, _CapShape, _BeamRole);
                float timeValue = _Time.y;
                float stormBurstNormalized = saturate(_StormBurstNormalized);
                float stormIntensity = saturate(max(0.0, _StormIdleIntensity) + max(0.0, _StormBurstIntensity) * stormBurstNormalized);
                float innerDisc = smoothstep(0.78, 0.08, radius);
                float rimRing = ComputeWrappedBand(radius * 0.86 - timeValue * max(0.1, _FlowScrollSpeed) * 0.28,
                                                   0.42,
                                                   0.16);
                float capArc = ComputeElectricArc(angleUv,
                                                  timeValue * max(0.1, _StormTwistSpeed) * 0.055 + radius * 0.12,
                                                  lerp(0.16, 0.085, stormIntensity));
                half3 color = lerp(_FlowColor.rgb, _ContactColor.rgb, 0.62h) * mask * (0.44h + innerDisc * 0.5h);
                color += lerp(_ContactColor.rgb, _CoreColor.rgb, 0.28h) * innerDisc * _TerminalCapIntensity * _CoreBrightness;
                color += _StormColor.rgb * capArc * rimRing * _RimBrightness * (0.46h + stormIntensity * 0.52h);
                color += _ContactColor.rgb * capArc * 0.32h * _TerminalCapIntensity;
                half alpha = saturate(mask * _Opacity * (0.5h + innerDisc * 0.34h + rimRing * 0.26h));
                return half4(color, alpha);
            }

            half4 RenderContactFlare(Varyings input)
            {
                float2 localPosition = input.positionOS.xy * 2.15;
                float radius = length(float2(localPosition.x * 0.82, localPosition.y * 1.32));
                float angleUv = frac(atan2(localPosition.y, localPosition.x) / 6.28318 + 0.5);
                float mask = ResolveShapeMask(localPosition, _CapShape, _BeamRole);
                float timeValue = _Time.y;
                float wallBoost = lerp(0.75, 1.12, saturate(_TerminalBlockedByWall));
                float fanMask = smoothstep(1.12, 0.1, radius) * smoothstep(-0.28, 0.52, localPosition.x);
                float electricArc = ComputeElectricArc(angleUv,
                                                       timeValue * max(0.1, _StormTwistSpeed) * 0.11 + radius * 0.18,
                                                       0.11);
                float flashBand = ComputeWrappedBand(radius * 0.9 - timeValue * max(0.1, _FlowScrollSpeed) * 0.42,
                                                     0.36,
                                                     0.14);
                half3 color = _ContactColor.rgb * fanMask * _ContactFlareIntensity * (0.56h + flashBand * 0.58h);
                color += _StormColor.rgb * electricArc * fanMask * _RimBrightness * wallBoost * 1.1h;
                color += _CoreColor.rgb * flashBand * fanMask * 0.34h;
                half alpha = saturate(mask * fanMask * _Opacity * (0.58h + electricArc * 0.32h) * wallBoost);
                return half4(color, alpha);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_BeamRole < 0.5)
                    return RenderBody(input);

                if (_BeamRole < 1.5)
                    return RenderSource(input);

                if (_BeamRole < 2.5)
                    return RenderTerminalCap(input);

                return RenderContactFlare(input);
            }
            ENDHLSL
        }
    }
}
