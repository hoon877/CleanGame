Shader "CozyRestore/MaskedGlass"
{
    Properties
    {
        _CleanColor ("Clean Color", Color) = (0.82,0.95,1,0.16)
        _DirtyColor ("Dirty Color", Color) = (0.42,0.45,0.42,0.78)
        _DirtMask ("Dirt Mask", 2D) = "white" {}
        _MaskOrigin ("Mask Origin", Vector) = (0,0,0,0)
        _MaskAxisA ("Mask Axis A", Vector) = (1,0,0,0)
        _MaskAxisB ("Mask Axis B", Vector) = (0,1,0,0)
        _MaskSize ("Mask Size", Vector) = (1,1,0,0)
        _Glossiness ("Smoothness", Range(0,1)) = 0.85
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0

        sampler2D _DirtMask;
        fixed4 _CleanColor;
        fixed4 _DirtyColor;
        float4 _MaskOrigin;
        float4 _MaskAxisA;
        float4 _MaskAxisB;
        float4 _MaskSize;
        half _Glossiness;

        struct Input
        {
            float3 worldPos;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 delta = IN.worldPos - _MaskOrigin.xyz;
            float2 maskUv = float2(
                dot(delta, normalize(_MaskAxisA.xyz)) / max(_MaskSize.x, 0.0001) + 0.5,
                dot(delta, normalize(_MaskAxisB.xyz)) / max(_MaskSize.y, 0.0001) + 0.5);
            fixed dirt = tex2D(_DirtMask, saturate(maskUv)).r;
            fixed4 color = lerp(_CleanColor, _DirtyColor, dirt);
            o.Albedo = color.rgb;
            o.Alpha = color.a;
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
