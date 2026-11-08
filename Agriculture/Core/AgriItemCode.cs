using System;
using UnityEngine;

/// <summary>
/// The per-copy codes of agricultural items (one int per copy in the stack's durability list; 0 and 1 mean "no value",
/// what a bought, looted or spawned copy has):
/// <list type="bullet">
/// <item>seeds: their genes (<see cref="PlantGenes.ToCode"/>);</item>
/// <item>produce: 2 + (quality 0-1000 in 10 bits | potency 0-1000 in 10 bits &lt;&lt; 10 | stage 4 bits &lt;&lt; 20 | rare &lt;&lt; 24);</item>
/// <item>water containers: 2 + (decilitres in 12 bits | water kind 4 bits &lt;&lt; 12).</item>
/// </list>
/// </summary>
public static class AgriItemCode
{
    /// <summary>The quality assumed for a copy without a code.</summary>
    public const float UnknownQuality = 0.6f;

    public static int Produce(float quality, float potency, ProduceStage stage, bool rare)
    {
        int q = Mathf.Clamp(Mathf.RoundToInt(quality * 1000f), 0, 1000);
        int p = Mathf.Clamp(Mathf.RoundToInt(potency * 1000f), 0, 1000);
        return 2 + (q | (p << 10) | (((int)stage & 15) << 20) | ((rare ? 1 : 0) << 24));
    }

    public static bool TryProduce(int code, out float quality, out float potency, out ProduceStage stage, out bool rare)
    {
        quality = UnknownQuality;
        potency = 0.3f;
        stage = ProduceStage.Ripe;
        rare = false;
        if (code < 2) return false;
        int x = code - 2;
        quality = Mathf.Clamp01((x & 1023) / 1000f);
        potency = Mathf.Clamp01(((x >> 10) & 1023) / 1000f);
        stage = (ProduceStage)Mathf.Clamp((x >> 20) & 15, 0, 8);
        rare = ((x >> 24) & 1) != 0;
        return true;
    }

    public static float QualityOf(int code) => TryProduce(code, out float q, out _, out _, out _) ? q : UnknownQuality;

    public static int Water(float litres, WaterSourceKind kind)
    {
        int dl = Mathf.Clamp(Mathf.RoundToInt(litres * 10f), 0, 4095);
        return 2 + (dl | (((int)kind & 15) << 12));
    }

    public static bool TryWater(int code, out float litres, out WaterSourceKind kind)
    {
        litres = 0f;
        kind = WaterSourceKind.Well;
        if (code < 2) return false;
        int x = code - 2;
        litres = (x & 4095) / 10f;
        kind = (WaterSourceKind)Mathf.Clamp((x >> 12) & 15, 0, 7);
        return true;
    }

    /// <summary>What a copy is worth relative to a perfect one.</summary>
    public static float ValueFactor(float quality) => 0.1f + 0.9f * Mathf.Pow(Mathf.Clamp01(quality), 1.4f);

    /// <summary>"Poor", "Fair", "Good", "Fine", "Excellent".</summary>
    public static string Grade(float quality)
    {
        if (quality >= 0.88f) return "Excellent";
        if (quality >= 0.72f) return "Fine";
        if (quality >= 0.52f) return "Good";
        if (quality >= 0.32f) return "Fair";
        return "Poor";
    }
}
