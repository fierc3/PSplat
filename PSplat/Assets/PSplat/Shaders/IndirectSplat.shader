Shader "PSplat/IndirectSplat"
{
    Properties
    {
        _Color("Tint", Color) = (1,1,1,1)
    }
        SubShader
    {
        Tags { "Queue" = "Geometry" }
        Pass
        {
            ZWrite On
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct SplatData
            {
                float3 position;
                float size;
                float4 color;
            };

            StructuredBuffer<SplatData> _Splats;

            struct appdata
            {
                float4 vertex : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                SplatData splat = _Splats[v.instanceID];
                float3 worldPos = splat.position + v.vertex.xyz * splat.size;
                v2f o;
                o.pos = UnityObjectToClipPos(float4(worldPos, 1.0));
                o.color = splat.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}
