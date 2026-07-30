Shader "Koiusa/Effects/CharacterDeathDissolve"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        [HDR] _EdgeColor("Dissolve Edge", Color) = (0.25,0.8,1,1)
        _DissolveAmount("Dissolve Amount", Range(0,1)) = 0
        _EdgeWidth("Edge Width", Range(0.001,0.25)) = 0.08
        _NoiseScale("Noise Scale", Float) = 7
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull Back
        ZWrite On

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EdgeColor;
                float _DissolveAmount;
                float _EdgeWidth;
                float _NoiseScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(baseSample.a - 0.1h);

                float3 noisePosition = floor(input.positionWS * _NoiseScale * 3.0) * 0.333333;
                float noise = frac(sin(dot(noisePosition, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                float threshold = _DissolveAmount * 1.05 - 0.025;
                float distanceToEdge = noise - threshold;
                clip(distanceToEdge);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse = saturate(dot(normalize(input.normalWS), mainLight.direction));
                half3 litColor = baseSample.rgb * (0.28h + diffuse * mainLight.color * mainLight.shadowAttenuation);
                half edge = 1.0h - smoothstep(0.0h, _EdgeWidth, distanceToEdge);
                half3 color = litColor + _EdgeColor.rgb * edge * 3.0h;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
