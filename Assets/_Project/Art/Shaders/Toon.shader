// Cartoon look for URP: hard-edged light bands, tinted shadows, soft rim, dark outline.
// Outline = "inverted hull": the mesh is drawn a second time, pushed outwards, with front faces culled.
Shader "Construction/Toon"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shadow Tint", Color) = (0.55, 0.5, 0.75, 1)
        _Threshold ("Light/Shadow Threshold", Range(0, 1)) = 0.5
        _Softness ("Band Softness", Range(0.001, 0.3)) = 0.04
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimAmount ("Rim Amount", Range(0, 1)) = 0.25
        _OutlineColor ("Outline Color", Color) = (0.13, 0.09, 0.18, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.02)) = 0.0045
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        // One identical CBUFFER for every pass keeps the shader SRP-Batcher compatible.
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadeColor;
            half _Threshold;
            half _Softness;
            half4 _RimColor;
            half _RimAmount;
            half4 _OutlineColor;
            half _OutlineWidth;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));

                // Half-Lambert -> hard band; multiplying by the shadow term lets cast shadows join the same band.
                half ndl = dot(normalWS, mainLight.direction) * 0.5h + 0.5h;
                half lit = smoothstep(_Threshold - _Softness, _Threshold + _Softness, ndl) * mainLight.shadowAttenuation;

                half3 shade = lerp(_ShadeColor.rgb, half3(1, 1, 1), lit);
                half3 color = _BaseColor.rgb * shade * lerp(half3(1, 1, 1), mainLight.color, 0.5h);

                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half rim = smoothstep(0.6h, 0.75h, 1.0h - saturate(dot(normalWS, viewDirWS))) * lit;
                color += _RimColor.rgb * rim * _RimAmount;

                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float4 positionCS = TransformObjectToHClip(IN.positionOS.xyz);

                // Primitives are centred on their pivot, so "away from the centre" is a smooth outline
                // direction even for cubes, whose real normals are split per face (that would open gaps).
                float3 dirWS = TransformObjectToWorldDir(normalize(IN.positionOS.xyz), false);
                float2 dirCS = mul((float3x3)UNITY_MATRIX_VP, dirWS).xy;
                dirCS = normalize(dirCS + 1e-5);
                dirCS.x *= _ScreenParams.y / _ScreenParams.x;   // keep width uniform on wide screens

                positionCS.xy += dirCS * _OutlineWidth * positionCS.w * 2.0;
                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(_OutlineColor.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
