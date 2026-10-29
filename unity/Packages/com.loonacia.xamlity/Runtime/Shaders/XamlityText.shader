Shader "Xamlity/Text"
{
    Properties
    {
        _MainTex ("Font Atlas", 2D) = "white" {}
    }
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

            float4x4 _XamlityProjection;
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float3 position : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = mul(_XamlityProjection, float4(input.position, 1));
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float distance = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                float width = max(fwidth(distance), 0.0001);
                float coverage = smoothstep(0.5 - width, 0.5 + width, distance);
                return half4(input.color.rgb, input.color.a * coverage);
            }
            ENDHLSL
        }
    }
}
