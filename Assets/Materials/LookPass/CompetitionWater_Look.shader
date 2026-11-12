// Look-pass duplicate of Splashdown/Competition Water.
// The original shader hardcodes ripple amplitude, fresnel reflectivity and body colour,
// so a material cannot raise them. Defaults below reproduce that look (all multipliers = 1).
Shader "Splashdown/Competition Water Look"
{
    Properties
    {
        _BaseMap ("Ripple and crest field", 2D) = "gray" {}
        _SetColors ("Course colour reflection mask", 2D) = "black" {}
        _RippleStrength ("Ripple strength", Range(0, 3)) = 1
        _Reflectivity ("Reflectivity", Range(0, 2)) = 1
        _Saturation ("Saturation", Range(0, 2.5)) = 1
        _Brightness ("Brightness", Range(0, 2.5)) = 1
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
            TEXTURE2D(_SetColors);
            SAMPLER(sampler_SetColors);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _RippleStrength;
                half _Reflectivity;
                half _Saturation;
                half _Brightness;
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
                half rippleAmp = max(0.0h, _RippleStrength);
                half2 slope = rippleAmp * (0.13h * cos(phase) + 0.055h * sin(phase.yx * 1.85));
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
                float2 uv = p * float2(0.17, 0.29) + _BaseMap_ST.zw;
                half2 ripple = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + slope * 0.14).rg;
                half crossing = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv.yx * 0.73 - t * float2(0.018, 0.026)).g;
                half crest = smoothstep(0.55h, 0.82h, ripple.g * 0.64h + crossing * 0.36h);
                half3 body = lerp(half3(0.004, 0.18, 0.58), half3(0.005, 0.64, 0.82), crossing);
                body += half3(0.09, 0.23, 0.18) * ripple.r * facing;
                float2 reflectedPoint = p + reflected.xz * min(9.0, 2.4 / max(0.20, reflected.y)) + slope * 4.0;
                float2 maskUV = (reflectedPoint + float2(40.0, 44.0)) / float2(192.0, 256.0);
                half2 paint = SAMPLE_TEXTURE2D(_SetColors, sampler_SetColors, maskUV).rg;
                half strength = saturate(paint.r + paint.g);
                half3 setTint = (half3(0.91, 0.036, 0.24) * paint.r + half3(0.025, 0.09, 0.68) * paint.g)
                                / max(0.02h, strength);
                body = lerp(body, setTint, strength * crest * 0.70h);
                half luma = dot(body, half3(0.2126h, 0.7152h, 0.0722h));
                body = lerp(luma.xxx, body, _Saturation) * _Brightness;
                half glint = pow(saturate(dot(normal, SafeNormalize(view + sun.direction))), 80.0h);
                body *= lerp(0.76h, 1.0h, sun.shadowAttenuation);
                half reflectMix = saturate(fresnel * 0.78h * _Reflectivity);
                half3 color = lerp(body, skyColor, reflectMix) + sun.color * sun.shadowAttenuation * glint * 1.45h * _Reflectivity;
                color += half3(0.18, 0.28, 0.30) * smoothstep(0.76h, 0.93h, crossing * ripple.g);
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
