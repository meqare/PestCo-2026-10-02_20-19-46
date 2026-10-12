// Pencereden giren ışık huzmesi: gölgesiz, tek geçişli, ekleme (additive) karışımlı ucuz sahte volumetrik.
// UV.y = 0 pencere ucu, 1 uzak uç. Kenarlarda ve uzakta söner; kamera yaklaşınca yumuşakça kaybolur.
Shader "PestCo/LightShaft"
{
    Properties
    {
        [HDR] _Color ("Renk", Color) = (1.0, 0.86, 0.55, 1)
        _Intensity ("Yogunluk", Range(0,1)) = 0.22
        _EdgeSoft ("Kenar Yumusakligi", Range(0.05,1)) = 0.5
        _Streak ("Serit Sayisi", Float) = 5
        _StreakAmount ("Serit Belirginligi", Range(0,1)) = 0.35
        _NearFade ("Yakin Solma Mesafesi (m)", Float) = 1.2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Shaft"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off Cull Off ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color; half _Intensity, _EdgeSoft, _Streak, _StreakAmount, _NearFade;
            CBUFFER_END

            struct A { float4 posOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; float3 posWS : TEXCOORD1; };

            V vert(A i)
            {
                V o;
                o.posWS = TransformObjectToWorld(i.posOS.xyz);
                o.posCS = TransformWorldToHClip(o.posWS);
                o.uv = i.uv;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                half side = 1.0h - abs(i.uv.x * 2.0h - 1.0h);
                half edge = smoothstep(0.0h, _EdgeSoft, side);
                half along = (1.0h - i.uv.y);
                along *= along;
                half streak = 1.0h - _StreakAmount + _StreakAmount * (0.5h + 0.5h * sin(i.uv.x * _Streak * 6.2831h));
                half near = saturate(distance(_WorldSpaceCameraPos, i.posWS) / max(_NearFade, 0.01h));
                half a = edge * along * streak * near * _Intensity;
                return half4(_Color.rgb * a, 1);
            }
            ENDHLSL
        }
    }
}
