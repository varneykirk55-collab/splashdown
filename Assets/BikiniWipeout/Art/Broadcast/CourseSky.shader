Shader "Skybox/Splashdown Outdoor"
{
    Properties { _MainTex ("Painted horizon", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings Vert(float3 positionOS : POSITION)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(positionOS);
                output.direction = positionOS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                float2 uv = float2(atan2(d.x, d.z) / (2.0 * PI) + 0.5, asin(clamp(d.y, -1.0, 1.0)) / PI + 0.5);
                return half4(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb, 1);
            }
            ENDHLSL
        }
    }
}
