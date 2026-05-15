// Water for the terrain package's water meshes (see MeshGenerator.GenerateWaterMesh), for URP and the Built-in
// render pipeline (HDRP: assign your own water materials on the TerrainGenerator). It needs no textures - it
// reads everything from the water mesh:
//   vertex color r  = water depth (0 at the shoreline, 1 at 10+ units deep)  -> shallow/deep colour, shore foam,
//                                                                              wave fade near the shore
//   vertex color g  = water type (0.25 ocean, 0.5 lake, 0.75 pond, 1 river, 1.25 waterfall) -> wave size
//   uv2 (TEXCOORD1) = flow direction x speed in world X/Z (rivers; 0 on still water) -> ripples move downstream,
//                                                                              faster and foamier on rapids
// Waves and ripples are computed in world space, so they continue seamlessly across chunks.
// It is in a Resources folder so it is always included in builds; the terrain uses it automatically for
// any water type with no material assigned on the TerrainGenerator.
Shader "SimpleMovements/Water"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.22, 0.58, 0.62, 0.45)
        _DeepColor ("Deep Color", Color) = (0.04, 0.2, 0.36, 0.88)
        _DepthRange ("Depth Colour Range", Range(0.05, 1)) = 0.5
        _FoamColor ("Foam Color", Color) = (0.94, 0.97, 1, 1)
        _ShoreFoam ("Shore Foam Width", Range(0, 0.3)) = 0.06
        _RapidsFoam ("Rapids Foam", Range(0, 1)) = 0.5
        _ForceFoam ("Foam Everywhere (waterfalls)", Range(0, 1)) = 0
        _WaveHeight ("Wave Height", Range(0, 3)) = 0.4
        _WaveLength ("Wave Length", Float) = 22
        _WaveSpeed ("Wave Speed", Float) = 1.1
        _RippleScale ("Ripple Scale", Float) = 0.45
        _RippleStrength ("Ripple Strength", Range(0, 1)) = 0.35
        _FlowSpeed ("Flow Speed", Float) = 0.8
        _Smoothness ("Smoothness", Range(0, 1)) = 0.88
        _SkyColor ("Reflected Sky Color", Color) = (0.62, 0.76, 0.9, 1)
        _FresnelPower ("Fresnel Power", Range(1, 8)) = 4
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    fixed4 _ShallowColor, _DeepColor, _FoamColor, _SkyColor;
    float _DepthRange, _ShoreFoam, _RapidsFoam, _ForceFoam;
    float _WaveHeight, _WaveLength, _WaveSpeed;
    float _RippleScale, _RippleStrength, _FlowSpeed;
    float _Smoothness, _FresnelPower;

    // Main (directional) light: URP sets _MainLight*, the Built-in pipeline _WorldSpaceLightPos0/_LightColor0;
    // the other pipeline's values stay zero.
    float4 _MainLightPosition;
    half4 _MainLightColor;
    half4 _LightColor0;

    struct appdata
    {
        float4 vertex : POSITION;
        float4 color : COLOR;
        float2 flow : TEXCOORD1;
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float3 worldPos : TEXCOORD0;
        float3 normal : TEXCOORD1;
        float2 flow : TEXCOORD2;
        float2 water : TEXCOORD3; // x = depth (0-1), y = type
        UNITY_FOG_COORDS(4)
    };

    // How big waves are on each water type (from the mesh's type channel).
    float WaveScale(float type)
    {
        float t = type * 4.0;
        if (t < 1.5) return 1.0;   // ocean
        if (t < 2.5) return 0.35;  // lake
        if (t < 3.5) return 0.12;  // pond
        if (t < 4.5) return 0.06;  // river
        return 0.0;                // waterfall
    }

    // Three travelling sine waves in world space; returns the height and its slope (d/dx, d/dz).
    float3 Waves(float2 p, float time)
    {
        float3 result = 0;
        float2 dirs[3] = { float2(0.8, 0.6), float2(-0.45, 0.89), float2(0.97, -0.24) };
        float lengths[3] = { 1.0, 0.61, 0.37 };
        float amps[3] = { 1.0, 0.45, 0.2 };
        [unroll]
        for (int i = 0; i < 3; i++)
        {
            float k = 6.2831853 / max(0.5, _WaveLength * lengths[i]);
            float phase = dot(dirs[i], p) * k + time * _WaveSpeed * (1.0 + 0.3 * i);
            result.x += amps[i] * sin(phase);
            result.yz += amps[i] * k * cos(phase) * dirs[i];
        }
        return result * 0.5;
    }

    v2f vert(appdata v)
    {
        v2f o;
        float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
        float depth = saturate(v.color.r);
        // Full waves from ~2.5 units deep; none at the shoreline, so they never climb the bank.
        float amplitude = _WaveHeight * WaveScale(v.color.g) * saturate(depth * 4.0);
        float3 wave = Waves(worldPos.xz, _Time.y) * amplitude;
        worldPos.y += wave.x;

        o.worldPos = worldPos;
        o.normal = normalize(float3(-wave.y, 1.0, -wave.z));
        o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
        o.flow = v.flow;
        o.water = float2(depth, v.color.g);
        UNITY_TRANSFER_FOG(o, o.pos);
        return o;
    }

    float Hash(float2 p)
    {
        p = frac(p * float2(123.34, 456.21));
        p += dot(p, p + 45.32);
        return frac(p.x * p.y);
    }

    float Noise(float2 p)
    {
        float2 i = floor(p), f = frac(p);
        float2 u = f * f * (3.0 - 2.0 * f);
        return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
    }

    float Ripples(float2 p)
    {
        return Noise(p) * 0.65 + Noise(p * 2.3 + 17.0) * 0.35;
    }

    // Slope of the ripple pattern at p (finite differences).
    float2 RippleSlope(float2 p)
    {
        const float e = 0.15;
        float h = Ripples(p);
        return float2(Ripples(p + float2(e, 0)) - h, Ripples(p + float2(0, e)) - h) / e;
    }

    fixed4 frag(v2f i) : SV_Target
    {
        float depth = i.water.x;
        float isFall = max(step(1.1, i.water.y), _ForceFoam);
        float speed = length(i.flow);

        // Flow mapping: two copies of the ripples drift downstream half a cycle apart and cross-fade,
        // so the pattern moves with the current without stretching. Still water drifts slowly.
        float2 flow = i.flow * _FlowSpeed + float2(0.05, 0.03);
        float cycle = 2.0;
        float phase0 = frac(_Time.y / cycle);
        float phase1 = frac(_Time.y / cycle + 0.5);
        float weight0 = 1.0 - abs(1.0 - 2.0 * phase0);
        float2 p = i.worldPos.xz * _RippleScale;
        float2 slope = RippleSlope(p - flow * phase0 * cycle * _RippleScale) * weight0
                     + RippleSlope(p - flow * phase1 * cycle * _RippleScale + 0.37) * (1.0 - weight0);
        float rippleStrength = _RippleStrength * (1.0 + 0.5 * saturate(speed * 0.5));
        float3 normal = normalize(i.normal + float3(-slope.x, 0, -slope.y) * rippleStrength * 0.35);

        // Shallow-to-deep colour and opacity.
        float deep = saturate(depth / max(0.05, _DepthRange));
        fixed4 color = lerp(_ShallowColor, _DeepColor, deep);

        // Lighting: main light diffuse + specular, sky reflection by Fresnel.
        bool urpLight = any(_MainLightColor.rgb > 0);
        float3 lightDir = normalize(urpLight ? _MainLightPosition.xyz : _WorldSpaceLightPos0.xyz + float3(0, 1e-4, 0));
        float3 lightColor = urpLight ? _MainLightColor.rgb : _LightColor0.rgb;
        float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
        float diffuse = saturate(dot(normal, lightDir)) * 0.6 + 0.4;
        float3 halfDir = normalize(lightDir + viewDir);
        float specPower = exp2(10.0 * _Smoothness + 1.0);
        float specular = pow(saturate(dot(normal, halfDir)), specPower) * _Smoothness;
        float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);

        float3 ambient = max(unity_AmbientSky.rgb, 0.25);
        float3 rgb = color.rgb * (lightColor * diffuse + ambient * 0.5);
        rgb = lerp(rgb, _SkyColor.rgb * max(ambient, lightColor * 0.8), fresnel * 0.6);
        rgb += lightColor * specular;
        float alpha = saturate(color.a + fresnel * 0.25 + specular);

        // Foam: a band along the shore, on fast water (rapids) and all over waterfalls.
        // (moved by the same two-phase flow offsets as the ripples, which stay small however long the game runs)
        float2 q = i.worldPos.xz * 0.9;
        float foamNoise = Ripples(q - flow * phase0 * cycle * 0.9) * weight0
                        + Ripples(q - flow * phase1 * cycle * 0.9 + 0.37) * (1.0 - weight0);
        float shore = 1.0 - smoothstep(0.0, max(0.001, _ShoreFoam), depth);
        float rapids = saturate((speed - 1.2) * 0.5) * _RapidsFoam;
        float foam = saturate(max(shore * 0.8, max(rapids, isFall)) * smoothstep(0.35, 0.75, foamNoise + max(rapids, isFall) * 0.4));
        rgb = lerp(rgb, _FoamColor.rgb * (diffuse * 0.8 + 0.3), foam * _FoamColor.a);
        alpha = max(alpha, foam * 0.9);

        fixed4 result = fixed4(rgb, alpha);
        UNITY_APPLY_FOG(i.fogCoord, result);
        return result;
    }
    ENDCG

    // Universal Render Pipeline
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            ENDCG
        }
    }

    // Built-in render pipeline
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            ENDCG
        }
    }

    Fallback Off
}
