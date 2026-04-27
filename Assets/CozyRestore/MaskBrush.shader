Shader "Hidden/CozyRestore/MaskBrush"
{
    Properties
    {
        _MainTex ("Mask", 2D) = "white" {}
        _BrushCenter ("Brush Center", Vector) = (0.5,0.5,0,0)
        _BrushSize ("Brush Size", Vector) = (0.05,0.05,0,0)
        _BrushTangent ("Brush Tangent", Vector) = (1,0,0,0)
        _BrushSoftness ("Brush Softness", Range(0.001,1)) = 0.35
        _BrushStrength ("Brush Strength", Range(0,1)) = 0.8
        _ExclusionCount ("Exclusion Count", Float) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _BrushCenter;
            float4 _BrushSize;
            float4 _BrushTangent;
            float _BrushSoftness;
            float _BrushStrength;
            int _ExclusionCount;
            float4 _ExclusionRects[4];

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

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed current = tex2D(_MainTex, i.uv).r;
                for (int r = 0; r < _ExclusionCount; r++)
                {
                    float4 rect = _ExclusionRects[r];
                    if (i.uv.x >= rect.x && i.uv.x <= rect.z && i.uv.y >= rect.y && i.uv.y <= rect.w)
                    {
                        return fixed4(current, current, current, 1);
                    }
                }

                float2 size = max(_BrushSize.xy, float2(0.0001, 0.0001));
                float2 tangentSource = _BrushTangent.xy;
                float2 tangent = dot(tangentSource, tangentSource) > 0.0001 ? normalize(tangentSource) : float2(1, 0);
                float2 bitangent = float2(-tangent.y, tangent.x);
                float2 rawDelta = i.uv - _BrushCenter.xy;
                float2 delta = abs(float2(dot(rawDelta, tangent), dot(rawDelta, bitangent))) / size;
                float distanceToBrush = max(delta.x, delta.y);
                float erase = 1.0 - smoothstep(1.0 - _BrushSoftness, 1.0, distanceToBrush);
                current = saturate(current - erase * _BrushStrength);
                return fixed4(current, current, current, 1);
            }
            ENDCG
        }
    }
}
