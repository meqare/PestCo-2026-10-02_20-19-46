// Fanus camı (URP). Fresnel Effect -> Alpha: kenarlar parlak, merkez şeffaf. Premultiply blending.
Shader "PestCo/FanusGlass"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color (A = merkez alfa)", Color) = (0.75, 0.92, 0.9, 0.12)
        _EdgeColor("Kenar Rengi", Color) = (0.6, 0.95, 0.9, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.95
        _FresnelPower("Fresnel Power", Range(0.5, 8)) = 3
        _FresnelIntensity("Fresnel Intensity", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half _Smoothness;
                half _FresnelPower;
                half _FresnelIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);

                // Fresnel Effect düğümünün karşılığı: pow(1 - saturate(N.V), Power)
                half fresnel = saturate(pow(1.0 - saturate(dot(N, V)), _FresnelPower) * _FresnelIntensity);

                // Merkez: BaseColor.a (0.12), kenar: 1'e doğru
                half alpha = saturate(_BaseColor.a + fresnel);

                half3 R = reflect(-V, N);
                half3 env = GlossyEnvironmentReflection(R, i.positionWS, 1.0h - _Smoothness, 1.0h,
                                                        GetNormalizedScreenSpaceUV(i.positionCS));

                Light mainLight = GetMainLight();
                float3 H = SafeNormalize(mainLight.direction + V);
                half spec = pow(saturate(dot(N, H)), exp2(10.0 * _Smoothness + 1.0)) * _Smoothness;

                // Premultiply: rgb zaten alfa ile ağırlıklı, yansıma/specular kenarda eklenir.
                half3 rgb = (_BaseColor.rgb * 0.5h + _EdgeColor.rgb * fresnel) * alpha
                          + env * fresnel
                          + spec * mainLight.color;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
