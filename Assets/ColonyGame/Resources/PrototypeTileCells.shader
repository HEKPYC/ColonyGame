Shader "ColonyGame/PrototypeTileCells"
{
    Properties { _BaseColor("Surface color", Color) = (0.3,0.6,0.3,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 derivatives = max(fwidth(input.uv), float2(0.00001,0.00001));
                float2 edgePixels = min(input.uv, 1.0-input.uv) / derivatives;
                float outline = 1.0-smoothstep(0.2,0.8,min(edgePixels.x,edgePixels.y));
                // Fade when a cell is too small to resolve; no heavy distant grid.
                float visibility = smoothstep(3.0,7.0,1.0/max(derivatives.x,derivatives.y));
                return half4(_BaseColor.rgb * (1.0 - 0.16*outline*visibility), _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
