// Shared texturing for the "SimpleMovements/Terrain" shader (SimpleMovementsTerrain.shader), used by both its URP
// and its Built-in pipeline versions. Pipeline independent: no lighting here, only the ground's colour and gloss.
//
// Biome textures: _TextureArray, one layer per biome (TerrainGenerator.BiomeDefinitions order).
// Splat maps: _SplatMaps, one layer per 4 biomes (weights in RGBA), sampled with the mesh's second UV (0-1 per chunk).
// Mapping: textures are projected in world space - from above on gentle ground, and from the sides as well on
// steep ground (tri-planar), so cliffs, mountain faces and abrupt drops don't stretch them. World-space
// projection also makes every texture continue seamlessly across chunk borders.
#ifndef SIMPLEMOVEMENTS_TERRAIN_INCLUDED
#define SIMPLEMOVEMENTS_TERRAIN_INCLUDED

// The material's values go in the UnityPerMaterial buffer (URP's SRP Batcher needs that). Where the buffer macros
// aren't defined (the Built-in pipeline's surface shader analysis, or an editor's HLSL checker) they're plain values.
#if defined(CBUFFER_START) && !defined(SHADER_TARGET_SURFACE_ANALYSIS)
#define SM_MATERIAL_BUFFER_START CBUFFER_START(UnityPerMaterial)
#define SM_MATERIAL_BUFFER_END CBUFFER_END
#else
#define SM_MATERIAL_BUFFER_START
#define SM_MATERIAL_BUFFER_END
#endif

SM_MATERIAL_BUFFER_START
float _TextureTiling;
float _TextureArrayLength;
float _SplatMapCount;
float _BiomeCount;
float _TextureBlendSharpness;
float _UVRotationStrength;
float _UVScaleVariation;
float _UVNoiseStrength;
float _UVNoiseScale;
float _NoiseSeedOffset;
float _TriplanarStrength;
float _TriplanarSharpness;
float _TriplanarSlopeStart;
float _TriplanarSlopeEnd;
float _Smoothness;
float _WetnessDarkening;
float _WetnessSmoothness;
SM_MATERIAL_BUFFER_END

// Weather, set globally by the WeatherSystem (all 0 without one): ground wet from rain, snow settled while it
// snows, and permanent snow on the terrain above a height (_SMSnowCaps on, _SMSnowLine = its world height).
// Rain and snow are the weather where the viewer is, so they fade out with distance from it: _SMWeatherArea =
// (viewer x, viewer z, full-strength radius, 1 / fade distance); radius 0 = everywhere.
float _SMWeatherWetness;
float4 _SMWeatherArea;
float _SMWeatherSnow;
float _SMSnowCaps;
float _SMSnowLine;

#if defined(SHADER_TARGET_SURFACE_ANALYSIS)
    // The Built-in pipeline's surface shader analysis only needs code it can parse.
#define TERRAIN_DECLARE_ARRAY(name) UNITY_DECLARE_TEX2DARRAY(name)
#define TERRAIN_SAMPLE_ARRAY_GRAD(name, coord, dx, dy) UNITY_SAMPLE_TEX2DARRAY(name, coord)
#define TERRAIN_SAMPLE_ARRAY_LOD(name, coord, lod) UNITY_SAMPLE_TEX2DARRAY_LOD(name, coord, lod)
#define TERRAIN_DECLARE_TEX(name) sampler2D name
#define TERRAIN_SAMPLE_TEX_LOD(name, uv, lod) tex2Dlod(name, float4(uv, 0, lod))
#else
#define TERRAIN_DECLARE_ARRAY(name) Texture2DArray name; SamplerState sampler##name
#define TERRAIN_SAMPLE_ARRAY_GRAD(name, coord, dx, dy) name.SampleGrad(sampler##name, coord, dx, dy)
#define TERRAIN_SAMPLE_ARRAY_LOD(name, coord, lod) name.SampleLevel(sampler##name, coord, lod)
#define TERRAIN_DECLARE_TEX(name) Texture2D name; SamplerState sampler##name
#define TERRAIN_SAMPLE_TEX_LOD(name, uv, lod) name.SampleLevel(sampler##name, uv, lod)
#endif

TERRAIN_DECLARE_ARRAY(_TextureArray);
TERRAIN_DECLARE_ARRAY(_SplatMaps);
TERRAIN_DECLARE_TEX(_WetnessMap);

float TerrainHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float TerrainValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = TerrainHash(i);
    float b = TerrainHash(i + float2(1, 0));
    float c = TerrainHash(i + float2(0, 1));
    float d = TerrainHash(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Per-layer turn and scale (Shader UV Rotation Strength / Scale Variation), so biomes don't share one tiling grid.
float2x2 TerrainLayerTransform(float layer)
{
    float h = TerrainHash(float2(layer * 7.13 + 1.7, _NoiseSeedOffset));
    float angle = _UVRotationStrength * 6.2831853 * h;
    float s, c;
    sincos(angle, s, c);
    float scale = pow(max(_UVScaleVariation, 0.01), h * 2.0 - 1.0);
    return float2x2(c, -s, s, c) * scale;
}

// One biome layer, projected along the axes whose weight is non-zero (gradients from the unrotated projections,
// so skipped projections cost nothing and mip selection stays right).
float3 TerrainSampleLayer(float layer, float3 blend, float2 uvX, float2 uvY, float2 uvZ,
                          float2 dxX, float2 dyX, float2 dxY, float2 dyY, float2 dxZ, float2 dyZ)
{
    float2x2 m = TerrainLayerTransform(layer);
    float3 color = 0;
    [branch]
    if (blend.y > 0)
        color += blend.y * TERRAIN_SAMPLE_ARRAY_GRAD(_TextureArray, float3(mul(m, uvY), layer), mul(m, dxY), mul(m, dyY)).rgb;
    [branch]
    if (blend.x > 0)
        color += blend.x * TERRAIN_SAMPLE_ARRAY_GRAD(_TextureArray, float3(mul(m, uvX), layer), mul(m, dxX), mul(m, dyX)).rgb;
    [branch]
    if (blend.z > 0)
        color += blend.z * TERRAIN_SAMPLE_ARRAY_GRAD(_TextureArray, float3(mul(m, uvZ), layer), mul(m, dxZ), mul(m, dyZ)).rgb;
    return color;
}

// How much each projection contributes: only from above on gentle ground, fading into all three between the
// tri-planar start and end slopes. Projections that barely contribute are dropped (fewer texture reads).
float3 TerrainProjectionWeights(float3 normalWS)
{
    float3 n = normalize(normalWS);
    float3 w = pow(abs(n), max(_TriplanarSharpness, 1.0));
    w /= max(w.x + w.y + w.z, 1e-5);
    float slope = degrees(acos(saturate(abs(n.y))));
    float amount = _TriplanarStrength * smoothstep(_TriplanarSlopeStart, max(_TriplanarSlopeEnd, _TriplanarSlopeStart + 0.01), slope);
    w = lerp(float3(0, 1, 0), w, amount);
    w *= step(0.03, w);
    return w / max(w.x + w.y + w.z, 1e-5);
}

// The ground's colour and gloss at a point. splatUV: the mesh's second UV.
void TerrainSurface(float3 positionWS, float3 normalWS, float2 splatUV, out float3 albedo, out float smoothness)
{
    float3 blend = TerrainProjectionWeights(normalWS);

    // World-space projections, with an optional smooth offset that breaks up the tiling.
    float2 offset = 0;
    if (_UVNoiseStrength > 0)
    {
        float2 q = positionWS.xz * _UVNoiseScale + _NoiseSeedOffset;
        offset = (float2(TerrainValueNoise(q), TerrainValueNoise(q + 17.31)) - 0.5) * _UVNoiseStrength;
    }
    float2 uvY = positionWS.xz * _TextureTiling + offset;
    float2 uvX = positionWS.zy * _TextureTiling + offset;
    float2 uvZ = positionWS.xy * _TextureTiling + offset;
    float2 dxX = ddx(uvX), dyX = ddy(uvX);
    float2 dxY = ddx(uvY), dyY = ddy(uvY);
    float2 dxZ = ddx(uvZ), dyZ = ddy(uvZ);

    int maps = (int) min(_SplatMapCount, 4);
    int layers = (int) min(_BiomeCount, _TextureArrayLength);
    float3 color = 0;
    float total = 0;
    [loop]
    for (int map = 0; map < maps; map++)
    {
        float4 weights = TERRAIN_SAMPLE_ARRAY_LOD(_SplatMaps, float3(splatUV, map), 0);
        // Blend Sharpness: above 1, the strongest biome takes over more of each transition.
        weights = pow(max(weights, 0), max(_TextureBlendSharpness, 0.01));
        [unroll]
        for (int channel = 0; channel < 4; channel++)
        {
            float w = weights[channel];
            float layer = map * 4 + channel;
            [branch]
            if (w > 0.004 && layer < layers)
            {
                color += w * TerrainSampleLayer(layer, blend, uvX, uvY, uvZ, dxX, dyX, dxY, dyY, dxZ, dyZ);
                total += w;
            }
        }
    }
    albedo = total > 0 ? color / total : float3(0.5, 0.5, 0.5);

    // Wet ground near water, and everywhere after rain: darker and glossier.
    float wet = saturate(TERRAIN_SAMPLE_TEX_LOD(_WetnessMap, splatUV, 0).r);
    float weatherHere = _SMWeatherArea.z > 0 ? 1.0 - saturate((distance(positionWS.xz, _SMWeatherArea.xy) - _SMWeatherArea.z) * _SMWeatherArea.w) : 1.0;
    wet = max(wet, 0.8 * saturate(_SMWeatherWetness) * weatherHere);
    albedo *= 1.0 - wet * _WetnessDarkening;
    smoothness = lerp(_Smoothness, max(_Smoothness, _WetnessSmoothness), wet);

    // Snow on ground facing up (not on cliffs): settling on the flattest ground first while it snows, and
    // lying permanently above the snow line, its edge broken up by noise.
    [branch]
    if (_SMWeatherSnow > 0.001 || _SMSnowCaps > 0.5)
    {
        float flatness = saturate((normalize(normalWS).y - 0.5) / 0.35);
        float drift = TerrainValueNoise(positionWS.xz * 0.15 + 7.3);
        float settled = saturate(_SMWeatherSnow * weatherHere * (1.4 + 0.8 * (0.5 - drift)) - (1.0 - flatness) * 0.8);
        float caps = _SMSnowCaps * saturate((positionWS.y + (drift - 0.5) * 24.0 - _SMSnowLine) / 25.0) * saturate(flatness * 1.5);
        float snow = saturate(max(settled, caps));
        albedo = lerp(albedo, float3(0.9, 0.92, 0.95), snow);
        smoothness = lerp(smoothness, 0.3, snow);
    }
}

#endif
