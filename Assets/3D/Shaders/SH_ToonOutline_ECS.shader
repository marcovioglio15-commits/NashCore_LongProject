Shader "Cel Shader/Toon Outline ECS"
{
    // ECS/URP port of "Cel Shader/Toon Outline".
    Properties
    {
        _OutlineThickness("Outline Thickness", Range(0,10)) = 1
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        // Hidden property used by DOTS deformation path (ignored for classic GameObjects)
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
            Name "ToonOutlineECS"
            Tags
            {
                "LightMode" = "SRPDefaultUnlit"
            }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex OutlinePassVertex
            #pragma fragment OutlinePassFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // SRP Batcher requirement: all material properties must live inside UnityPerMaterial.
            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineThickness;
                float _ComputeMeshIndex;
            CBUFFER_END

            #if defined(DOTS_INSTANCING_ON)
            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float, _ComputeMeshIndex)
            UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
            #define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, var)
            #else
            #define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) var
            #endif

            struct DeformedVertexData
            {
                float3 Position;
                float3 Normal;
                float3 Tangent;
            };

            StructuredBuffer<DeformedVertexData> _DeformedMeshData : register(t1);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlinePassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionOS = input.positionOS;
                float3 normalOS = input.normalOS;

                #if defined(UNITY_DOTS_INSTANCING_ENABLED)
                    uint meshStartIndex = asuint(UNITY_ACCESS_HYBRID_INSTANCED_PROP(_ComputeMeshIndex, float));
                    DeformedVertexData deformedVertex = _DeformedMeshData[meshStartIndex + input.vertexID];
                    positionOS = deformedVertex.Position;
                    normalOS = deformedVertex.Normal;
                #endif

                float outlineThickness = _OutlineThickness / 250.0;
                float3 normalDirection = SafeNormalize(normalOS);
                float3 extrudedPositionOS = positionOS + normalDirection * outlineThickness;
                VertexPositionInputs vertexPositionInputs = GetVertexPositionInputs(extrudedPositionOS);
                output.positionCS = vertexPositionInputs.positionCS;
                return output;
            }

            half4 OutlinePassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(_OutlineColor.rgb, _OutlineColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
