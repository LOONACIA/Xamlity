Shader "Xamlity/Solid"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            float4x4 _XamlityProjection;

            struct Attributes
            {
                float3 position : POSITION;
                half4 color : COLOR;
                float4 shape : TEXCOORD1;
                float4 outerRadii : TEXCOORD2;
                float4 innerRectangle : TEXCOORD3;
                float4 innerRadii : TEXCOORD4;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                half4 color : COLOR;
                float4 shape : TEXCOORD1;
                float4 outerRadii : TEXCOORD2;
                float4 innerRectangle : TEXCOORD3;
                float4 innerRadii : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = mul(_XamlityProjection, float4(input.position, 1));
                output.color = input.color;
                output.shape = input.shape;
                output.outerRadii = input.outerRadii;
                output.innerRectangle = input.innerRectangle;
                output.innerRadii = input.innerRadii;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                output.color.rgb = SRGBToLinear(output.color.rgb);
                #endif
                return output;
            }

            float RoundedBoxDistance(float2 p, float2 halfSize, float4 radii)
            {
                float2 q = abs(p) - halfSize;
                float distance = length(max(q, 0)) + min(max(q.x, q.y), 0);
                // Use each corner's region: normalized unequal radii can exceed a half size.
                q = float2(-p.x, -p.y) - halfSize + radii.x;
                if (q.x > 0 && q.y > 0)
                    distance = max(distance, length(q) - radii.x);
                q = float2(p.x, -p.y) - halfSize + radii.y;
                if (q.x > 0 && q.y > 0)
                    distance = max(distance, length(q) - radii.y);
                q = p - halfSize + radii.z;
                if (q.x > 0 && q.y > 0)
                    distance = max(distance, length(q) - radii.z);
                q = float2(-p.x, p.y) - halfSize + radii.w;
                if (q.x > 0 && q.y > 0)
                    distance = max(distance, length(q) - radii.w);
                return distance;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float coverage = 1;
                if (input.shape.z > 0 && input.shape.w > 0)
                {
                    float outerDistance = RoundedBoxDistance(input.shape.xy, input.shape.zw, input.outerRadii);
                    coverage = saturate(0.5 - outerDistance);
                    if (input.innerRectangle.z > 0 && input.innerRectangle.w > 0)
                    {
                        float innerDistance = RoundedBoxDistance(input.shape.xy - input.innerRectangle.xy,
                            input.innerRectangle.zw, input.innerRadii);
                        coverage *= saturate(0.5 + innerDistance);
                    }
                }
                return half4(input.color.rgb, input.color.a * coverage);
            }
            ENDHLSL
        }
    }
}
