// ============================================================================
// File: Assets/Shaders/CASSharpen.shader
// Minimal, compile-proof shader to satisfy "Hidden/Custom/CASSharpen" lookup.
// ============================================================================
Shader "Hidden/Custom/CASSharpen"
{
    Properties { }
    HLSLINCLUDE
        #pragma target 4.5
        // Keep includes minimal & version-safe
        #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

        struct A { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
        struct V { float4 positionCS : SV_Position; float2 uv : TEXCOORD0; };

        V Vert(A v)
        {
            V o;
            o.positionCS = GetFullScreenTriangleVertexPosition(v.positionOS.xy);
            o.uv         = GetFullScreenTriangleTexCoord(v.uv);
            return o;
        }

        float4 Frag(V i) : SV_Target
        {
            // Loud color so you can confirm execution immediately.
            return float4(1, 0, 1, 1); // magenta
        }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "CASSharpen"
            ZWrite Off ZTest Always Blend Off Cull Off
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Frag
            ENDHLSL
        }
    }
    Fallback Off
}
