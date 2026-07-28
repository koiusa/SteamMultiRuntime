Shader "Koiusa/Effects/GuardShield"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color", Color) = (0.03, 0.35, 0.8, 0.18)
        [HDR] _EdgeColor ("Edge Color", Color) = (0.15, 1.2, 2.5, 1)
        [HDR] _ImpactColor ("Impact Color", Color) = (1.5, 2.5, 4, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.65
        _RimPower ("Rim Power", Range(0.25, 8)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0, 8)) = 2
        _HexScale ("Hex Scale", Range(1, 40)) = 7
        _HexWidth ("Hex Line Width", Range(0.005, 0.25)) = 0.055
        _HexIntensity ("Hex Intensity", Range(0, 5)) = 1.25
        _FlowSpeed ("Flow Speed", Range(-5, 5)) = 0.6
        _PulseDensity ("Pulse Density", Range(0, 20)) = 5
        _PulseIntensity ("Pulse Intensity", Range(0, 3)) = 0.35
        _ImpactPosition ("Impact Position (World)", Vector) = (0, 0, 0, 0)
        _ImpactRadius ("Impact Radius", Range(0.01, 5)) = 0.65
        _ImpactWidth ("Impact Ring Width", Range(0.01, 1)) = 0.12
        _ImpactStrength ("Impact Strength", Range(0, 5)) = 0
        _IntersectionDistance ("Environment Intersection Distance", Range(0.001, 1)) = 0.18
        _IntersectionIntensity ("Environment Intersection Intensity", Range(0, 8)) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "GuardShield"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                half3 normalOS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half4 _ImpactColor;
                half _Opacity;
                half _RimPower;
                half _RimIntensity;
                half _HexScale;
                half _HexWidth;
                half _HexIntensity;
                half _FlowSpeed;
                half _PulseDensity;
                half _PulseIntensity;
                float4 _ImpactPosition;
                half _ImpactRadius;
                half _ImpactWidth;
                half _ImpactStrength;
                half _IntersectionDistance;
                half _IntersectionIntensity;
            CBUFFER_END

            // Returns distance to the nearest edge of a pointy-top hexagonal grid.
            float HexEdge(float2 p)
            {
                const float2 ratio = float2(1.0, 1.7320508);
                float2 a = frac(p / ratio) - 0.5;
                float2 b = frac((p + ratio * 0.5) / ratio) - 0.5;
                float2 cell = dot(a, a) < dot(b, b) ? a : b;
                cell *= ratio;
                float edgeDistance = 0.5 - max(abs(cell.x) * 0.8660254 + abs(cell.y) * 0.5, abs(cell.y));
                return 1.0 - smoothstep(_HexWidth, _HexWidth * 2.0, edgeDistance);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

                half rim = pow(saturate(1.0h - abs(dot(normalWS, viewDirectionWS))), _RimPower);
                // Triplanar projection keeps cell size consistent and avoids spherical-UV stretching at the poles.
                half3 blend = pow(abs(normalize(input.normalOS)), 4.0h);
                blend /= max(blend.x + blend.y + blend.z, 0.0001h);
                float3 hexPosition = input.positionOS * _HexScale;
                half hexX = HexEdge(hexPosition.zy);
                half hexY = HexEdge(hexPosition.xz);
                half hexZ = HexEdge(hexPosition.xy);
                half hex = dot(half3(hexX, hexY, hexZ), blend);
                half pulse = 0.5h + 0.5h * sin((input.positionOS.y + _Time.y * _FlowSpeed) * _PulseDensity * TWO_PI);

                float impactDistance = distance(input.positionWS, _ImpactPosition.xyz);
                half impactRing = 1.0h - smoothstep(_ImpactWidth, _ImpactWidth * 2.0h,
                    abs(impactDistance - _ImpactRadius));
                impactRing *= _ImpactStrength;

                float2 screenUv = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawSceneDepth = SampleSceneDepth(screenUv);
                float sceneEyeDepth = LinearEyeDepth(rawSceneDepth, _ZBufferParams);
                float shieldEyeDepth = -TransformWorldToView(input.positionWS).z;
                float depthSeparation = max(sceneEyeDepth - shieldEyeDepth, 0.0);
                half contact = 1.0h - smoothstep(0.0h, _IntersectionDistance, depthSeparation);
                contact *= _IntersectionIntensity * (0.85h + 0.15h * sin(_Time.y * 12.0h));

                half energy = rim * _RimIntensity
                    + hex * _HexIntensity
                    + pulse * _PulseIntensity
                    + impactRing
                    + contact;
                half3 color = _BaseColor.rgb + _EdgeColor.rgb * energy;
                color = lerp(color, _ImpactColor.rgb, saturate(impactRing + contact));
                half alpha = saturate(_BaseColor.a + rim + hex * 0.35h + impactRing + contact) * _Opacity;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
