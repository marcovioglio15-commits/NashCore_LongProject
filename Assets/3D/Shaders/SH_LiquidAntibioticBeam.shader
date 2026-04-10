Shader "NashCore/LiquidAntibioticBeam"
{
    Properties
    {
        [Header(Colors)] _BeamColorA("Beam Color A", Color) = (0.2, 0.95, 1.0, 1.0)
        _BeamColorB("Beam Color B", Color) = (0.75, 1.0, 1.0, 1.0)
        _CoreColor("Core Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _RimColor("Rim Color", Color) = (0.05, 0.45, 0.65, 1.0)
        [Header(Beam)] _Opacity("Opacity", Range(0, 1)) = 0.85
        _CoreBrightness("Core Brightness", Range(0, 8)) = 2.0
        _RimBrightness("Rim Brightness", Range(0, 8)) = 1.0
        _FlowScrollSpeed("Flow Scroll Speed", Range(0, 16)) = 4.0
        _FlowPulseFrequency("Flow Pulse Frequency", Range(0, 16)) = 2.0
        _WobbleAmplitude("Wobble Amplitude", Range(0, 2)) = 0.2
        _BubbleDriftSpeed("Bubble Drift Speed", Range(0, 16)) = 2.5
        _BodyProfile("Body Profile", Range(0, 2)) = 0
        _BeamRole("Beam Role", Range(0, 2)) = 0
        _CapShape("Cap Shape", Range(0, 2)) = 0
        _SegmentLength("Segment Length", Float) = 1.0
        _WidthScale("Width Scale", Float) = 1.0
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
            CBUFFER_END

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 345.45));
                value += dot(value, value + 34.345);
                return frac(value.x * value.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half ResolveBodyProfileMask(float2 uv, float3 positionOS)
            {
                float axial = saturate(positionOS.z * 0.5 + 0.5);
                float radial = length(positionOS.xy * 2.0);
                float roundedTube = saturate(1.0 - pow(radial, 1.3) * 1.04);
                float taperedJet = saturate((1.0 - radial * (1.22 - axial * 0.26)) * (0.72 + axial * 0.28));
                float denseRibbon = saturate(1.0 - max(abs(positionOS.x) * 2.35, abs(positionOS.y) * 3.1));

                if (_BodyProfile < 0.5)
                    return roundedTube;

                if (_BodyProfile < 1.5)
                    return taperedJet;

                return denseRibbon;
            }

            half ResolveCapShapeMask(float2 uv)
            {
                float2 centeredUv = uv * 2.0 - 1.0;
                float radius = length(centeredUv);
                float angle = atan2(centeredUv.y, centeredUv.x);
                float bubbleBurst = saturate(1.0 - radius * 1.08) + saturate(0.12 - abs(radius - 0.62)) * 1.25;
                float starBloom = saturate(1.0 - radius * (1.3 + sin(angle * 6.0) * 0.28));
                float softDisc = saturate(1.0 - radius * 1.12);

                if (_CapShape < 0.5)
                    return bubbleBurst;

                if (_CapShape < 1.5)
                    return starBloom;

                return softDisc;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float timeValue = _Time.y;
                float pulse = 0.94 + 0.06 * sin(timeValue * max(0.25, _FlowPulseFrequency) * 6.28318);
                float liquidFlow = sin((uv.x * max(1.0, _SegmentLength) * 4.8) - timeValue * _FlowScrollSpeed * 2.4 + uv.y * 3.2);
                float wobble = sin(uv.x * 8.0 + timeValue * _BubbleDriftSpeed * 1.2) * _WobbleAmplitude * 0.03;
                float bubbleNoise = Hash21(float2(floor(uv.x * 12.0 + timeValue * _BubbleDriftSpeed), floor(uv.y * 9.0 + timeValue * 0.7)));
                float bodyRadial = saturate(length(input.positionOS.xy * 2.0));
                float bodyBubbleMask = smoothstep(0.86, 1.0, bubbleNoise) * smoothstep(0.92, 0.1, bodyRadial + wobble * 0.35);
                float capBubbleMask = smoothstep(0.86, 1.0, bubbleNoise) * smoothstep(0.82, 0.12, abs(uv.y * 2.0 - 1.0 + wobble));
                float bodyHighlightTrack = smoothstep(0.16, 0.0, abs(bodyRadial - 0.24 + liquidFlow * 0.018));
                float capHighlightTrack = smoothstep(0.34, 0.0, abs((uv.y * 2.0 - 1.0) + 0.24 + liquidFlow * 0.03));
                float bodyInnerFill = smoothstep(0.62, 0.0, bodyRadial + wobble * 0.2);
                float capInnerFill = smoothstep(0.78, 0.0, abs(uv.y * 2.0 - 1.0 + wobble));
                half4 gradientColor = lerp(_BeamColorA, _BeamColorB, saturate(uv.x * 0.58 + liquidFlow * 0.12 + 0.24));
                half bodyMask = ResolveBodyProfileMask(float2(uv.x, saturate(uv.y + wobble)), input.positionOS);
                half capMask = ResolveCapShapeMask(float2(uv.x, uv.y));
                half roleMask = _BeamRole < 0.5 ? bodyMask : capMask;
                half edgeGlow = _BeamRole < 0.5
                    ? smoothstep(0.58, 0.98, bodyRadial) * roleMask * _RimBrightness
                    : smoothstep(0.7, 1.0, abs(uv.y * 2.0 - 1.0)) * roleMask * _RimBrightness;
                half coreMask = _BeamRole < 0.5
                    ? saturate(roleMask * bodyInnerFill * 0.9 + bodyHighlightTrack * roleMask)
                    : saturate(roleMask * capInnerFill * 0.75 + capHighlightTrack * roleMask);
                half baseColorPresence = _BeamRole < 0.5 ? 1.12h : 0.72h;
                half alphaPresence = _BeamRole < 0.5 ? 0.62h : 0.24h;
                half baseGlow = _BeamRole < 0.5 ? 0.24h : 0.12h;
                half3 color = gradientColor.rgb * lerp(baseColorPresence, 1.38h, roleMask);
                color += gradientColor.rgb * baseGlow;
                color += _BeamColorB.rgb * (_BeamRole < 0.5 ? bodyBubbleMask : capBubbleMask) * 0.28h;
                color += _CoreColor.rgb * coreMask * _CoreBrightness * pulse * 1.08h;
                color += _RimColor.rgb * edgeGlow * 0.72h;

                if (_BeamRole > 0.5)
                {
                    float2 centeredUv = uv * 2.0 - 1.0;
                    float capGlow = saturate(1.0 - length(centeredUv) * 0.85);
                    color += _CoreColor.rgb * capGlow * _CoreBrightness * 0.62h;
                }

                half alpha = saturate(lerp(alphaPresence, 1.0h, roleMask) * _Opacity * pulse);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
