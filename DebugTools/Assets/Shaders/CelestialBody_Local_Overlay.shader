Shader "KSP2/Environment/CelestialBody/CelestialBody_Local_Overlay"
{
    Properties
    {
        [MainTexture] _OverlayTexture ("Overlay Texture", 2D) = "black" {}
        _Strength ("Strength", float) = 0.9
        [Toggle(_USE_PQS_BUFFER)] _NoComputeBuffer ("Use PQS QuadMeshDataBuffer", float) = 0.9
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            // Drawn by PQSRenderer's overlay hook with CommandBuffer.DrawProceduralIndirect (pass 0).
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_local __ _USE_PQS_BUFFER

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "QuadMeshDataBuffer.hlsl"

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OverlayTexture_ST;
                float _Strength;
                float _NoComputeBuffer;
            CBUFFER_END

            TEXTURE2D(_OverlayTexture); SAMPLER(sampler_OverlayTexture);

            v2f vert (appdata v)
            {
                v2f o;
                QuadMeshData data = GetQuadMeshVert(v);
                o.vertex = TransformObjectToHClip(data.position);
                o.uv = data.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_OverlayTexture, sampler_OverlayTexture, i.uv);
                col.a = _Strength;
                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}