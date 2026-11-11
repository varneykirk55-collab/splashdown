Shader "Splashdown/Competition Water"
{
    Properties
    {
        _BaseMap ("Caustic pattern", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fog : TEXCOORD1;
            };

            Varyings Vert(float3 positionOS : POSITION)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz;
                float t = _Time.y;
                float2 phase = float2(dot(p, float2(1.6, 0.7)) - t * 1.4,
                                      dot(p, float2(-0.8, 2.2)) + t * 1.15);
                half2 slope = 0.13h * cos(phase) + 0.055h * sin(phase.yx * 1.85);
                half3 normal = normalize(half3(slope.x, 1, slope.y));
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = float4(GetNormalizedScreenSpaceUV(input.positionCS), 0, 1);
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light sun = GetMainLight(shadowCoord);
                half facing = saturate(dot(view, normal));
                half fresnel = 0.07h + 0.72h * pow(1.0h - facing, 4.0h);
                half3 reflected = reflect(-view, normal);
                half4 sky = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflected, 3);
                half3 horizon = lerp(half3(0.20, 0.41, 0.56), half3(0.08, 0.27, 0.55), saturate(reflected.y));
                half3 skyColor = max(horizon, DecodeHDREnvironment(sky, unity_SpecCube0_HDR));
                float2 uv = p * 0.115 + _BaseMap_ST.zw + slope * 0.20;
                half caustic = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).r;
                half swell = 0.5h + 0.5h * sin(dot(p, float2(0.18, 0.13)) + slope.y + t * 0.34);
                half3 body = lerp(half3(0.009, 0.085, 0.24), half3(0.015, 0.31, 0.43), swell);
                body += half3(0.08, 0.19, 0.15) * caustic * facing;
                half glint = pow(saturate(dot(normal, SafeNormalize(view + sun.direction))), 110.0h);
                body *= lerp(0.68h, 1.0h, sun.shadowAttenuation);
                half3 color = lerp(body, skyColor, fresnel) + sun.color * sun.shadowAttenuation * glint * 1.7h;
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
