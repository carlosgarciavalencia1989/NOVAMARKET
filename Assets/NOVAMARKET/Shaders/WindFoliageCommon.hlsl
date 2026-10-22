// Shared part of "NOVAMARKET/Wind Foliage": material properties and the wind bend.
// Included after DOTS.hlsl in every pass (Core.hlsl must come after the DOTS instancing setup).
#ifndef NOVAMARKET_WIND_FOLIAGE_COMMON
#define NOVAMARKET_WIND_FOLIAGE_COMMON

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float _Smoothness;
    float _WindWeight;
    float _BendHeight;
CBUFFER_END

// Global wind: xy = direction (world X/Z, unit), z = strength (meters of bend at full height), w = gust 0..1.
float4 _WindParams;

float3 ApplyWind(float3 positionOS)
{
    // Height in the model's own space (tree pivots stand on the ground): reliable however the terrain
    // passes each tree's matrix. The pivot only varies the phase from tree to tree.
    float3 world = TransformObjectToWorld(positionOS);
    float3 pivot = TransformObjectToWorld(float3(0, 0, 0));
    float h = saturate(positionOS.y / max(_BendHeight, 0.1));
    float phase = dot(pivot.xz, float2(0.071, 0.113));
    float t = _Time.y;
    float sway = sin(t * 1.1 + phase) * 0.6 + sin(t * 2.3 + phase * 1.7) * 0.3;
    float strength = _WindParams.z * _WindWeight;
    float bend = h * h * strength * (0.45 + 0.55 * _WindParams.w + 0.35 * sway);
    world.xz += _WindParams.xy * bend;
    world.y += sin(t * 5.0 + dot(world.xz, float2(1.3, 1.7))) * 0.025 * strength * h; // leaves flutter
    return world;
}

#endif
