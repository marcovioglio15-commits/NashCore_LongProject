Shader "Cel Shader/Toon Diffuse ECS"
{
    // ECS/URP port of "Cel Shader/Toon Diffuse".
    Properties
    {
        [MainTexture] _MainTex("Texture", 2D) = "white" {}
        _AmbientColor("Ambient Color", Color) = (0,0,0,1)
        _AmbientColorIntensity("Ambient Color Intensity", Range(0,5)) = 0.5
        _ShadowSoftness("Shadow Softness", Range(0,0.5)) = 0.1
        _ShadowScatter("Shadow Scatter", Range(0,10)) = 5
        _ShadowRangeMin("Shadow Range Min", Range(0,1)) = 0.54
        _ShadowRangeMax("Shadow Range Max", Range(-2,2)) = -0.4
        // Hidden property used by DOTS deformation path (ignored for classic GOs)
        // In DOTS/ECS rendering this value is set per-instance 
        // to point to the correct slice of the deformation buffer.
        // Zero means no offset (for non-DOTS fallback) 
        [HideInInspector] _ComputeMeshIndex("Compute Mesh Buffer Index Offset", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ToonDiffuseECS"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            // Shader Model 4.5 is required for DOTS deformation buffers on desktop APIs.
            #pragma target 4.5
            #pragma vertex ToonPassVertex
            #pragma fragment ToonPassFragment
            #pragma multi_compile_fog

            // DOTS.hlsl injects DOTS instancing variants and keywords.
            // Core.hlsl and Lighting.hlsl replace UnityCG/Lighting includes.
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // IMPORTANT:
            // Keeping every material property inside UnityPerMaterial is mandatory for SRP Batcher.
            // If any property is declared outside this CBUFFER, BRG/Entities rendering can emit warnings
            // and batching will be broken.
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _AmbientColor;
                float _AmbientColorIntensity;
                float _ShadowSoftness;
                float _ShadowScatter;
                float _ShadowRangeMin;
                float _ShadowRangeMax;
                float _ComputeMeshIndex;
            CBUFFER_END

            // URP/HLSL texture declaration style (replaces "sampler2D + tex2D").
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            #if defined(DOTS_INSTANCING_ON)
            // Registers DOTS-overridable per-instance metadata.
            // Exposed _ComputeMeshIndex so each entity can point to its own slice
            // inside the deformation buffer.
            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float, _ComputeMeshIndex)
            UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
            #define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, var)
            #else
            // Non-DOTS fallback (read regular material property).
            #define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) var
            #endif

            // Matches Entities Graphics deformation buffer layout.
            // The field order must stay consistent with the producer side..
            struct DeformedVertexData
            {
                float3 Position;
                float3 Normal;
                float3 Tangent;
            };

            // StructuredBuffer slot for current-frame skinned/deformed data (for Entities Graphics)
            // register(t1) means this buffer is expected to be bound at slot t1 by the renderer,
            // which is currently handled by the Hybrid Renderer V2's shader bindings.
            StructuredBuffer<DeformedVertexData> _DeformedMeshData : register(t1);

            // Compared to legacy:
            // - adds SV_VertexID (needed to index deformation buffer)
            // - adds per-instance macros for DOTS instancing.
            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Compared to v2f:
            // - stores fog as scalar factor (URP style)
            // - includes stereo/instance plumbing macros for SRP compatibility.
            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Same function as "invLerp" (with little guard against degenerate ranges).
            float InverseLerp(float minValue, float maxValue, float value)
            {
                float range = maxValue - minValue;
                if (abs(range) < 0.0001)
                {
                    return 0.0;
                }

                return (value - minValue) / range;
            }

            Varyings ToonPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                // Required SRP instance/stereo setup.
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionOS = input.positionOS;
                float3 normalOS = input.normalOS;

                #if defined(UNITY_DOTS_INSTANCING_ENABLED)
                    // DOTS deformation path:
                    // 1) read per-instance mesh start index
                    // 2) fetch deformed vertex using vertexID
                    // 3) replace object-space position/normal before transform.
                    uint meshStartIndex = asuint(UNITY_ACCESS_HYBRID_INSTANCED_PROP(_ComputeMeshIndex, float));
                    DeformedVertexData deformedVertex = _DeformedMeshData[meshStartIndex + input.vertexID];
                    positionOS = deformedVertex.Position;
                    normalOS = deformedVertex.Normal;
                #endif

                // URP helper transforms (object -> clip/world), 
                // replacing UnityObjectToClipPos/UnityObjectToWorldNormal.
                VertexPositionInputs vertexPositionInputs = GetVertexPositionInputs(positionOS);
                VertexNormalInputs vertexNormalInputs = GetVertexNormalInputs(normalOS);

                output.positionCS = vertexPositionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = NormalizeNormalPerVertex(vertexNormalInputs.normalWS);
                output.fogFactor = ComputeFogFactor(vertexPositionInputs.positionCS.z);

                return output;
            }

            half4 ToonPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // URP texture sampling equivalent to tex2D.
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);

                // Main light retrieval in URP.
                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;
                float ramp = dot(lightDir, normalWS);

                // Toon ramp math ported 1:1.
                float remapOut = InverseLerp(-1.0, 1.0, ramp);
                float shadowScatter = _ShadowScatter / 50.0;
                float shadowFloor = floor(remapOut / shadowScatter);
                float shadowRemapIn = InverseLerp(1.0 / shadowScatter, 0.0, shadowFloor);
                float shadowRemapOut = lerp(_ShadowRangeMin, _ShadowRangeMax, shadowRemapIn);

                // Same color composition model:
                // smooth toon shadow term + ambient tint, then multiplied by base texture.
                float3 ambientTerm = _AmbientColor.rgb * _AmbientColorIntensity;
                float3 lighting = smoothstep(0.0, _ShadowSoftness, shadowRemapOut) + ambientTerm;
                float3 texLighting = albedo.rgb + lighting;
                float3 finalColor = albedo.rgb * texLighting;

                // URP fog application.
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
