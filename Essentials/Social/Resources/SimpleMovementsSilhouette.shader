// Highlight of other players (see PlayerHighlights): drawn as an extra material on a character's renderers, for URP
// and the Built-in render pipeline. Two uses, one shader:
//   _ZTest 4 (LessEqual) + _RimOnly 1 -> a coloured glow along the visible edges of the character
//   _ZTest 5 (Greater)   + _RimOnly 0 -> a flat, translucent silhouette where walls hide the character
// Additive, no depth writes, no textures. It is in a Resources folder so it is always included in builds.
Shader "SimpleMovements/Player Silhouette"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 1, 0.45, 0.5)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimOnly ("Rim Only (0 = flat fill)", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+20" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            ZTest [_ZTest]
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _RimPower;
            half _RimOnly;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half facing = saturate(dot(normalize(i.normal), normalize(i.viewDir)));
                half rim = pow(1.0h - facing, _RimPower);
                half a = lerp(1.0h, rim, _RimOnly) * _Color.a;
                return fixed4(_Color.rgb, a);
            }
            ENDCG
        }
    }
    Fallback Off
}
