Shader "Custom/UI/LiquidBar"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1, 1, 1, 1)

        [Header(Liquid Palette)]
        _LiquidTint("Liquid Tint", Color) = (1, 1, 1, 1)
        _HighlightColor("Highlight Color", Color) = (1, 1, 1, 1)
        _GlowColor("Glow Color", Color) = (1, 1, 1, 1)

        [Header(Liquid Motion)]
        _FlowSpeed("Flow Speed", Range(0, 8)) = 1.0
        _WaveAmplitude("Surface Motion", Range(0, 1)) = 0.14
        _WaveFrequency("Wave Frequency", Range(0, 12)) = 4.0
        _Viscosity("Viscosity", Range(0, 1)) = 0.72
        _BodyDepth("Body Depth", Range(0, 4)) = 1.8
        _StrataStrength("Strata Strength", Range(0, 4)) = 1.2
        _SubsurfaceStrength("Subsurface Strength", Range(0, 4)) = 1.15
        _SheenVisibility("Sheen Visibility", Range(0, 1)) = 1.0
        _SheenStrength("Sheen Strength", Range(0, 4)) = 1.0
        _SheenSpeed("Sheen Speed", Range(0, 8)) = 1.0
        _SheenWidth("Sheen Width", Range(0.01, 0.5)) = 0.16
        _GlowStrength("Glow Strength", Range(0, 4)) = 0.8

        [Header(Runtime)]
        _FillNormalized("Fill Normalized", Range(0, 1)) = 1.0
        _MovementBlend("Movement Blend", Range(0, 1)) = 0.0
        _MovementDirection("Movement Direction", Range(-1, 1)) = 1.0

        [Header(Unity UI)]
        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 worldPosition : TEXCOORD0;
                float2 texcoord : TEXCOORD1;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            fixed4 _Color;
            fixed4 _LiquidTint;
            fixed4 _HighlightColor;
            fixed4 _GlowColor;
            float _FlowSpeed;
            float _WaveAmplitude;
            float _WaveFrequency;
            float _Viscosity;
            float _BodyDepth;
            float _StrataStrength;
            float _SubsurfaceStrength;
            float _SheenVisibility;
            float _SheenStrength;
            float _SheenSpeed;
            float _SheenWidth;
            float _GlowStrength;
            float _FillNormalized;
            float _MovementBlend;
            float _MovementDirection;

            v2f Vert(appdata_t input)
            {
                v2f output;
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            float ComputeWrappedBand(float coordinate, float phase, float width)
            {
                float wrappedDistance = abs(frac(coordinate - phase) * 2.0 - 1.0);
                return smoothstep(max(0.0001, width), 0.0, wrappedDistance);
            }

            float ComputeLayeredField(float2 uv, float timeValue, float flowSpeed, float viscosity, float movementDirection)
            {
                float slowSpeed = timeValue * lerp(flowSpeed * 0.32, flowSpeed * 0.08, viscosity);
                float mediumSpeed = timeValue * lerp(flowSpeed * 0.74, flowSpeed * 0.14, viscosity);
                float fastSpeed = timeValue * lerp(flowSpeed * 1.2, flowSpeed * 0.22, viscosity);
                float foldA = sin((uv.x * lerp(7.2, 2.9, viscosity) + uv.y * 2.6 + slowSpeed) * 6.2831853);
                float foldB = cos((uv.x * lerp(4.4, 1.6, viscosity) - uv.y * 6.1 - mediumSpeed) * 6.2831853);
                float foldC = sin(((uv.x + uv.y * 0.85) * lerp(9.4, 3.4, viscosity) + fastSpeed + movementDirection * uv.y * 0.45) * 6.2831853);
                return saturate(0.5 + foldA * 0.22 + foldB * 0.17 + foldC * 0.11);
            }

            float ComputeCellField(float2 uv, float timeValue, float flowSpeed, float viscosity)
            {
                float drift = timeValue * lerp(flowSpeed * 0.42, flowSpeed * 0.09, viscosity);
                float cellA = sin((uv.x * lerp(5.4, 2.2, viscosity) + drift) * 6.2831853);
                float cellB = cos((uv.y * lerp(6.8, 3.6, viscosity) - drift * 1.13 + uv.x * 1.8) * 6.2831853);
                float cellC = sin(((uv.x - uv.y) * lerp(3.8, 1.6, viscosity) - drift * 0.71) * 6.2831853);
                return saturate(0.5 + cellA * 0.16 + cellB * 0.16 + cellC * 0.12);
            }

            float ComputeBodyMask(float coordinateY, float bodyDepth)
            {
                float centeredCoordinate = 1.0 - abs(coordinateY * 2.0 - 1.0);
                return saturate(pow(saturate(centeredCoordinate), lerp(1.8, 0.48, saturate(bodyDepth * 0.25))));
            }

            fixed4 Frag(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;
                float timeValue = _Time.y;
                float movementBlend = saturate(_MovementBlend);
                float movementDirection = _MovementDirection >= 0.0 ? 1.0 : -1.0;
                float viscosity = saturate(_Viscosity);
                float layeredField = ComputeLayeredField(uv, timeValue, _FlowSpeed, viscosity, movementDirection);
                float sloshWaveA = sin((uv.y * lerp(_WaveFrequency * 1.4, _WaveFrequency * 0.8, viscosity) + timeValue * (_FlowSpeed * 0.85) + movementDirection * uv.x * 4.8) * 6.2831853);
                float sloshWaveB = cos((uv.x * 2.1 - uv.y * lerp(_WaveFrequency, _WaveFrequency * 0.45, viscosity) - timeValue * (_FlowSpeed * 0.56)) * 6.2831853);
                float2 distortedUv = uv;
                distortedUv.x += sloshWaveA * movementBlend * _WaveAmplitude * 0.018;
                distortedUv.y += (layeredField - 0.5) * _WaveAmplitude * (0.018 + movementBlend * 0.012);
                distortedUv.y += sloshWaveB * (0.006 + 0.01 * viscosity);
                distortedUv = saturate(distortedUv);

                fixed4 sampledColor = (tex2D(_MainTex, distortedUv) + _TextureSampleAdd) * input.color;
                float alpha = sampledColor.a;

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                if (alpha <= 0.0)
                    return fixed4(0, 0, 0, 0);

                float resampledLayeredField = ComputeLayeredField(distortedUv, timeValue, _FlowSpeed, viscosity, movementDirection);
                float resampledCellField = ComputeCellField(distortedUv, timeValue, _FlowSpeed, viscosity);
                float bodyMask = ComputeBodyMask(distortedUv.y, _BodyDepth);
                float innerPressure = saturate(bodyMask * (0.78 + _BodyDepth * 0.18) + resampledLayeredField * _StrataStrength * 0.22);
                float strataPearl = saturate(lerp(resampledLayeredField, resampledCellField, 0.45));
                float strataRibbons = saturate(pow(max(0.0001, strataPearl), lerp(2.0, 0.72, saturate(_StrataStrength * 0.25))));
                float subsurface = saturate(pow(bodyMask, 0.55) * (0.38 + 0.62 * resampledCellField));
                float rim = saturate(pow(1.0 - bodyMask, 1.6));
                float edgeGlow = smoothstep(0.18, 0.0, abs(uv.x - saturate(_FillNormalized)));
                float sheenPhase = frac(timeValue * _SheenSpeed * 0.085 + resampledLayeredField * 0.05);
                float sheen = ComputeWrappedBand(distortedUv.x, sheenPhase, max(0.02, _SheenWidth)) * saturate(_SheenVisibility);
                float sloshHighlight = saturate(movementBlend * (0.3 + 0.5 * abs(sloshWaveA) + 0.2 * abs(sloshWaveB)));
                float3 liquidColor = sampledColor.rgb * _LiquidTint.rgb;
                liquidColor *= 0.92 + innerPressure * (0.26 + _BodyDepth * 0.1);
                liquidColor = lerp(liquidColor,
                                   _HighlightColor.rgb,
                                   saturate(strataRibbons * 0.34 + sheen * _SheenStrength * 0.68));
                liquidColor += _HighlightColor.rgb * (_SubsurfaceStrength * (subsurface * 0.18 + innerPressure * 0.08 + sloshHighlight * 0.1));
                liquidColor += _GlowColor.rgb * (_GlowStrength * (edgeGlow * 0.52 + sloshHighlight * 0.22 + strataRibbons * 0.12));
                liquidColor += _HighlightColor.rgb * rim * 0.05 * _BodyDepth;
                return fixed4(liquidColor, alpha);
            }
            ENDCG
        }
    }
}
