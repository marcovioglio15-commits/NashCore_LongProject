Shader "NashCore/LiquidAntibioticBeam"
{
    Properties
    {
        [Header(Colors)] _BeamColorA("Beam Color A", Color) = (0.05, 0.28, 0.77, 1.0)
        _BeamColorB("Beam Color B", Color) = (0.12, 0.64, 0.98, 1.0)
        _CoreColor("Core Color", Color) = (0.68, 0.94, 1.0, 1.0)
        _RimColor("Rim Color", Color) = (0.01, 0.11, 0.42, 1.0)
        [Header(Beam)] _Opacity("Opacity", Range(0, 1)) = 0.9
        _CoreBrightness("Core Brightness", Range(0, 8)) = 1.8
        _RimBrightness("Rim Brightness", Range(0, 8)) = 1.1
        _FlowScrollSpeed("Flow Scroll Speed", Range(0, 16)) = 2.0
        _FlowPulseFrequency("Flow Pulse Frequency", Range(0, 16)) = 1.6
        _WobbleAmplitude("Wobble Amplitude", Range(0, 2)) = 0.1
        _BubbleDriftSpeed("Bubble Drift Speed", Range(0, 16)) = 1.8
        _BodyProfile("Body Profile", Range(0, 2)) = 2
        _BeamRole("Beam Role", Range(0, 2)) = 0
        _CapShape("Cap Shape", Range(0, 2)) = 0
        _SegmentLength("Segment Length", Float) = 1.0
        _WidthScale("Width Scale", Float) = 0.25
        _PrimaryPulseProgress("Primary Pulse Progress", Float) = -1.0
        _SecondaryPulseProgress("Secondary Pulse Progress", Float) = -1.0
        _PulseLengthNormalized("Pulse Length Normalized", Float) = 0.06
        _PulseBrightnessBoost("Pulse Brightness Boost", Float) = 1.0
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
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BeamColorA;
                half4 _BeamColorB;
                half4 _CoreColor;
                half4 _RimColor;
                half _Opacity;
                half _CoreBrightness;
                half _RimBrightness;
                half _FlowScrollSpeed;
                half _FlowPulseFrequency;
                half _WobbleAmplitude;
                half _BubbleDriftSpeed;
                half _BodyProfile;
                half _BeamRole;
                half _CapShape;
                half _SegmentLength;
                half _WidthScale;
                half _PrimaryPulseProgress;
                half _SecondaryPulseProgress;
                half _PulseLengthNormalized;
                half _PulseBrightnessBoost;
                half _TerminalBlockedByWall;
            CBUFFER_END

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 345.45));
                value += dot(value, value + 34.345);
                return frac(value.x * value.y);
            }

            float ComputePulse(float normalizedDistance, float pulseProgress, float pulseLengthNormalized)
            {
                if (pulseProgress < 0.0)
                    return 0.0;

                float normalizedOffset = abs(normalizedDistance - pulseProgress) / max(0.0001, pulseLengthNormalized);
                float mask = saturate(1.0 - normalizedOffset);
                return mask * mask * (3.0 - 2.0 * mask);
            }

            float ResolveBodyMask(float2 uv)
            {
                float centeredY = abs(uv.y * 2.0 - 1.0);
                float roundedTube = saturate(1.0 - pow(centeredY, 1.28) * 1.04);
                float taperedJet = saturate(1.0 - centeredY * (1.18 - uv.x * 0.16));
                float denseRibbon = saturate(1.0 - centeredY * 1.3);

                if (_BodyProfile < 0.5)
                    return roundedTube;

                if (_BodyProfile < 1.5)
                    return taperedJet;

                return denseRibbon;
            }

            float ResolveBubbleBurstMask(float2 localPosition)
            {
                float bubbleA = saturate(1.0 - length(localPosition - float2(-0.58, 0.16)) * 2.45);
                float bubbleB = saturate(1.0 - length(localPosition - float2(-0.08, -0.14)) * 2.25);
                float bubbleC = saturate(1.0 - length(localPosition - float2(0.34, 0.24)) * 2.65);
                float bubbleD = saturate(1.0 - length(localPosition - float2(0.58, -0.18)) * 3.1);
                return max(max(bubbleA, bubbleB), max(bubbleC, bubbleD));
            }

            float ResolveStarMask(float2 localPosition, bool isImpact)
            {
                float radius = length(localPosition);
                float angle = atan2(localPosition.y, localPosition.x);
                float spikeWave = abs(sin(angle * 4.0)) * 0.24 + abs(sin(angle * 8.0)) * 0.11;
                float star = saturate(1.0 - radius * (1.18 + spikeWave));

                if (!isImpact)
                    return star;

                float fan = saturate(1.0 - abs(localPosition.y) * 1.3) * saturate(1.0 - radius * 0.9);

                if (_TerminalBlockedByWall > 0.5)
                    fan *= 1.15;

                return saturate(max(star, fan));
            }

            float ResolveSoftDiscMask(float2 localPosition)
            {
                return saturate(1.0 - length(localPosition) * 1.12);
            }

            float ResolveCapMask(float2 localPosition, bool isImpact)
            {
                if (_CapShape < 0.5)
                    return ResolveBubbleBurstMask(localPosition);

                if (_CapShape < 1.5)
                    return ResolveStarMask(localPosition, isImpact);

                return ResolveSoftDiscMask(localPosition);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float timeValue = _Time.y;
                bool isBody = _BeamRole < 0.5;
                bool isImpact = _BeamRole > 1.5;
                float2 localPosition = input.positionOS.xy * 2.0;
                float normalizedDistance = saturate(uv.x);
                float pulseLengthNormalized = max(0.0025, _PulseLengthNormalized);
                float primaryPulse = ComputePulse(normalizedDistance, _PrimaryPulseProgress, pulseLengthNormalized);
                float secondaryPulse = ComputePulse(normalizedDistance, _SecondaryPulseProgress, pulseLengthNormalized);
                float pulseMask = max(primaryPulse, secondaryPulse * 0.72);
                float shimmerFrequency = max(0.25, _FlowPulseFrequency);
                float scrollPhase = normalizedDistance * max(2.0, _SegmentLength * 0.85) - timeValue * _FlowScrollSpeed * 0.62;
                float liquidWave = sin(scrollPhase * 6.28318 + sin(scrollPhase * 2.35) * 0.55);
                float microWave = sin(scrollPhase * 12.8 - uv.y * 7.2 + timeValue * _BubbleDriftSpeed);
                float slowBreathing = sin(scrollPhase * 2.2 + timeValue * shimmerFrequency * 3.14159);
                float bubbleNoise = Hash21(float2(floor(normalizedDistance * 22.0 + timeValue * _BubbleDriftSpeed),
                                                  floor(uv.y * 8.0 + normalizedDistance * 6.0)));

                if (isBody)
                {
                    float bodyMask = ResolveBodyMask(uv);
                    float centeredY = abs(uv.y * 2.0 - 1.0);
                    float innerBody = smoothstep(0.72, 0.08, centeredY - liquidWave * 0.045);
                    float liquidBand = smoothstep(0.2, 0.0, abs(centeredY - 0.18 + liquidWave * 0.06));
                    float foamBand = smoothstep(0.5, 0.96, centeredY + microWave * _WobbleAmplitude * 0.18);
                    float bubbleMask = smoothstep(0.86, 1.0, bubbleNoise + liquidWave * 0.08) * smoothstep(0.96, 0.42, centeredY);
                    float terminalGlow = smoothstep(0.82, 1.0, normalizedDistance) * smoothstep(0.86, 0.18, centeredY);
                    half4 gradientColor = lerp(_BeamColorA, _BeamColorB, saturate(0.22 + normalizedDistance * 0.42 + liquidWave * 0.12));
                    half3 color = gradientColor.rgb * lerp(0.76h, 1.24h, bodyMask);
                    color += _BeamColorB.rgb * liquidBand * 0.28h;
                    color += _BeamColorB.rgb * bubbleMask * 0.24h;
                    color += _CoreColor.rgb * innerBody * _CoreBrightness * (1.0h + pulseMask * _PulseBrightnessBoost);
                    color += _CoreColor.rgb * liquidBand * 0.34h;
                    color += _RimColor.rgb * foamBand * _RimBrightness * 0.8h;
                    color += _RimColor.rgb * terminalGlow * 0.32h;
                    color += _CoreColor.rgb * terminalGlow * (0.18h + pulseMask * 0.18h);
                    color += gradientColor.rgb * slowBreathing * _WobbleAmplitude * 0.08h;
                    half alpha = saturate(bodyMask * _Opacity * (0.82h + pulseMask * 0.12h));
                    return half4(color, alpha);
                }

                float capMask = ResolveCapMask(localPosition, isImpact);
                float capRadius = length(localPosition);
                float capInner = smoothstep(0.78, 0.12, capRadius - liquidWave * 0.06);
                float capHighlight = smoothstep(0.36, 0.04, abs(capRadius - 0.42 + liquidWave * 0.08));
                float capBubble = ResolveBubbleBurstMask(localPosition * 1.15 + float2(slowBreathing * 0.08, liquidWave * 0.04));
                half4 capGradient = lerp(_BeamColorA, _BeamColorB, saturate(0.38 + localPosition.y * 0.22 + liquidWave * 0.1));
                half3 capColor = capGradient.rgb * lerp(isImpact ? 0.92h : 0.72h, isImpact ? 1.28h : 1.05h, capMask);
                capColor += _BeamColorB.rgb * capBubble * (isImpact ? 0.18h : 0.24h);
                capColor += _CoreColor.rgb * capInner * _CoreBrightness * (isImpact ? 1.08h : 0.92h);
                capColor += _CoreColor.rgb * capHighlight * 0.35h;
                capColor += _RimColor.rgb * capMask * _RimBrightness * (isImpact ? 0.74h : 0.45h);

                if (isImpact)
                {
                    float starFlash = smoothstep(0.42, 0.0, abs(localPosition.y)) * smoothstep(0.98, 0.18, capRadius);
                    capColor += _CoreColor.rgb * starFlash * 0.44h;
                }

                half alpha = saturate(capMask * _Opacity * (isImpact ? 0.72h : 0.58h));
                return half4(capColor, alpha);
            }
            ENDHLSL
        }
    }
}
