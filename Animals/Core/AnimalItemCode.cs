using System;

/// <summary>
/// The per-copy codes of animal items (one int per copy in the stack's durability list, like produce and harvested
/// pieces; 0 and 1 mean "no value": a copy bought from a merchant, looted or spawned):
/// <list type="bullet">
/// <item>feed: 2 + quality (0-1000);</item>
/// <item>livestock (a young animal in a crate): 2 + (sex 2 bits | stage 3 bits &lt;&lt; 2 | seed 24 bits &lt;&lt; 5). A crate
/// without a code is a fresh animal: the world gives it a sex and genes of its own when it is released.</item>
/// <item>a crated animal (Phase 5: one of the world's animals, crated to be sold or traded): 2 + (sex 2 bits | stage 3 bits
/// &lt;&lt; 2 | grade 4 bits &lt;&lt; 5 | crate serial 20 bits &lt;&lt; 9 | 1 &lt;&lt; 29): the animal is the world's crated record of that
/// serial; its grade and stage say what it is worth to anyone, without the record.</item>
/// <item>products (eggs, milk, wool): 2 + (quality 0-100 | rare 1 bit &lt;&lt; 7 | ledger serial 22 bits &lt;&lt; 8): its quality,
/// whether its animal carries a rare trait, and the lot it came from in the world's product ledger (serial 0: not traced, a
/// copy made by hand). Under 2^30, like harvested pieces' codes (a bite of food takes one copy whatever its code: see
/// ProduceFoodSO).</item>
/// </list>
/// </summary>
public static class AnimalItemCode
{
    /// <summary>The quality of a copy without a code.</summary>
    public const float UnknownQuality = 0.6f;

    public static int Feed(float quality) => 2 + (int)Math.Round(AnimalMath.Clamp01(quality) * 1000f);

    public static bool TryFeed(int code, out float quality)
    {
        quality = UnknownQuality;
        if (code < 2) return false;
        int q = code - 2;
        if (q > 1000) return false;
        quality = q / 1000f;
        return true;
    }

    public static int Livestock(AnimalSex sex, GrowthStage stage, int seed)
    {
        int s = (int)sex & 3;
        int st = (int)stage & 7;
        int sd = seed & 0xFFFFFF;
        return 2 + (s | (st << 2) | (sd << 5));
    }

    public static bool TryLivestock(int code, out AnimalSex sex, out GrowthStage stage, out int seed)
    {
        sex = AnimalSex.None;
        stage = GrowthStage.Baby;
        seed = 0;
        if (code < 2) return false;
        int x = code - 2;
        if ((x & CratedBit) != 0) return false; // a crated animal (see TryCrated)
        sex = (AnimalSex)(x & 3);
        stage = (GrowthStage)Math.Min(5, (x >> 2) & 7);
        seed = (x >> 5) & 0xFFFFFF;
        return seed != 0;
    }

    private const int CratedBit = 1 << 29;

    /// <summary>The highest crate serial a crated animal's code holds (20 bits, less one: the code stays under 2^30).</summary>
    public const long MaxCrateSerial = 0xFFFFE;

    /// <summary>A crated animal's code: its sex, stage and grade (0-15), and its crate's serial (1 to <see cref="MaxCrateSerial"/>).</summary>
    public static int Crated(AnimalSex sex, GrowthStage stage, int grade, long serial)
    {
        int s = (int)sex & 3;
        int st = (int)stage & 7;
        int g = Math.Max(0, Math.Min(15, grade));
        int sr = (int)(serial > 0 && serial <= MaxCrateSerial ? serial : 0L);
        return 2 + (s | (st << 2) | (g << 5) | (sr << 9) | CratedBit);
    }

    /// <summary>A crated animal's sex, stage, grade and crate serial; false for any other code (a plain crate, a product).</summary>
    public static bool TryCrated(int code, out AnimalSex sex, out GrowthStage stage, out int grade, out long serial)
    {
        sex = AnimalSex.None;
        stage = GrowthStage.Baby;
        grade = 0;
        serial = 0L;
        if (code < 2) return false;
        int x = code - 2;
        if ((x & CratedBit) == 0) return false;
        sex = (AnimalSex)(x & 3);
        stage = (GrowthStage)Math.Min(5, (x >> 2) & 7);
        grade = (x >> 5) & 15;
        serial = (x >> 9) & 0xFFFFF;
        return serial != 0;
    }

    /// <summary>The highest ledger serial a product's code holds (22 bits, less one: the code stays under 2^30).</summary>
    public const long MaxProductSerial = 0x3FFFFE;

    /// <summary>A quality to the hundredth, as a product copy keeps it.</summary>
    public static float Hundredth(float quality) => (float)Math.Round(AnimalMath.Clamp01(quality) * 100f) / 100f;

    /// <summary>
    /// A product copy's code: its quality (to the hundredth), whether its animal has a rare trait (so its tier, Special
    /// included, is the same on every machine) and the serial of its lot in the product ledger (0: none):
    /// 2 + quality x 100 + 128 x rare + serial x 256.
    /// </summary>
    public static int Product(float quality, long serial, bool rare = false)
    {
        int q = (int)Math.Round(AnimalMath.Clamp01(quality) * 100f);
        long s = serial > 0 && serial <= MaxProductSerial ? serial : 0L;
        return 2 + q + (rare ? 128 : 0) + (int)(s << 8);
    }

    /// <summary>A product copy's quality and ledger serial; false for a copy without a code (bought, looted: quality <see cref="UnknownQuality"/>, no serial).</summary>
    public static bool TryProduct(int code, out float quality, out long serial) => TryProduct(code, out quality, out serial, out _);

    /// <summary>A product copy's quality, ledger serial and whether its animal has a rare trait; false for a copy without a code.</summary>
    public static bool TryProduct(int code, out float quality, out long serial, out bool rare)
    {
        quality = UnknownQuality;
        serial = 0L;
        rare = false;
        if (code < 2) return false;
        int x = code - 2;
        int q = x & 127;
        if (q > 100) return false;
        quality = q / 100f;
        rare = (x & 128) != 0;
        serial = (uint)x >> 8;
        return true;
    }

    /// <summary>What a copy is worth relative to a perfect one (the same curve as produce and harvested pieces).</summary>
    public static float ValueFactor(float quality) => 0.1f + 0.9f * (float)Math.Pow(AnimalMath.Clamp01(quality), 1.4);
}
