Shader "MiningSafetyAR/ScreenEdgeVignette"
{
    Properties
    {
        _Color ("Edge Color", Color) = (1, 0, 0, 1)
        _Intensity ("Intensity", Range(0,1)) = 0
        _EdgeWidth ("Edge Width", Range(0.05, 0.6)) = 0.28
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            half4 _Color;
            half _Intensity;
            half _EdgeWidth;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                // distance (0 at screen edge, growing toward center) on each axis
                float distX = min(uv.x, 1.0 - uv.x);
                float distY = min(uv.y, 1.0 - uv.y);
                float dist = min(distX, distY);

                float edge = 1.0 - saturate(dist / _EdgeWidth);
                edge = edge * edge; // sharper falloff toward center

                half alpha = edge * _Intensity;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
