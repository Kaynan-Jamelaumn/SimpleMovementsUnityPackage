using System;
using System.Globalization;
using UnityEngine;

/// <summary>Male or female body (picks the body objects / blend shapes of the <see cref="CharacterAppearance"/>).</summary>
public enum CharacterBodyType
{
    Male = 0,
    Female = 1,
}

/// <summary>The parts of a face and body the player can choose.</summary>
public enum AppearancePart
{
    Hair,
    Beard,
    Eyes,
    Nose,
    Eyebrows,
    /// <summary>Head / face shape (modular head meshes, or the head's proportions).</summary>
    Head,
    /// <summary>Body model: the default body (the model's own or the placeholder body), or one of the Body Models.</summary>
    Body,
}

/// <summary>
/// What a character looks like: body type, skin, hair, beard, eyes and nose (style indices into the
/// <see cref="CharacterAppearance"/>'s options), their colours and two face sliders. Small and plain so it is saved
/// with the character and sent over the network as a short string (<see cref="Encode"/> / <see cref="Decode"/>).
/// </summary>
[Serializable]
public class CharacterAppearanceData
{
    public CharacterBodyType bodyType = CharacterBodyType.Male;
    public Color skinColor = new Color(0.87f, 0.69f, 0.56f, 1f);
    public int hairStyle = 1;
    public Color hairColor = new Color(0.23f, 0.15f, 0.09f, 1f);
    public int beardStyle = 0;
    public int eyeStyle = 0;
    public Color eyeColor = new Color(0.32f, 0.22f, 0.13f, 1f);
    public int noseStyle = 0;
    public int eyebrowStyle = 0;
    public int headStyle = 0;
    [Tooltip("0 = the default body; 1+ = the Character Appearance's Body Models.")]
    public int bodyStyle = 0;
    [Range(0f, 1f)] public float noseSize = 0.5f;
    [Range(0f, 1f)] public float eyeSize = 0.5f;
    [Tooltip("Shirt colour of the placeholder body.")]
    public Color topColor = new Color(0.42f, 0.36f, 0.27f, 1f);
    [Tooltip("Trouser colour of the placeholder body.")]
    public Color bottomColor = new Color(0.26f, 0.24f, 0.22f, 1f);

    public static readonly Color[] SkinTones =
    {
        new Color(1f, 0.87f, 0.77f), new Color(0.96f, 0.8f, 0.69f), new Color(0.87f, 0.69f, 0.56f), new Color(0.78f, 0.58f, 0.43f),
        new Color(0.64f, 0.45f, 0.32f), new Color(0.5f, 0.34f, 0.24f), new Color(0.36f, 0.24f, 0.17f), new Color(0.25f, 0.17f, 0.12f),
        new Color(0.55f, 0.7f, 0.5f), new Color(0.55f, 0.6f, 0.78f),
    };

    public static readonly Color[] HairColors =
    {
        new Color(0.06f, 0.05f, 0.05f), new Color(0.23f, 0.15f, 0.09f), new Color(0.42f, 0.27f, 0.15f), new Color(0.65f, 0.45f, 0.25f),
        new Color(0.88f, 0.75f, 0.48f), new Color(0.6f, 0.22f, 0.1f), new Color(0.78f, 0.78f, 0.8f), new Color(0.95f, 0.95f, 0.95f),
        new Color(0.3f, 0.45f, 0.85f), new Color(0.75f, 0.25f, 0.6f),
    };

    public static readonly Color[] EyeColors =
    {
        new Color(0.32f, 0.22f, 0.13f), new Color(0.18f, 0.12f, 0.08f), new Color(0.25f, 0.45f, 0.75f), new Color(0.3f, 0.55f, 0.35f),
        new Color(0.5f, 0.55f, 0.6f), new Color(0.6f, 0.45f, 0.2f), new Color(0.75f, 0.2f, 0.2f), new Color(0.6f, 0.35f, 0.8f),
    };

    /// <summary>Clothes of the placeholder body: linen, leather, wool and dyed cloth.</summary>
    public static readonly Color[] ClothColors =
    {
        new Color(0.42f, 0.36f, 0.27f), new Color(0.78f, 0.72f, 0.6f), new Color(0.26f, 0.24f, 0.22f), new Color(0.36f, 0.24f, 0.16f),
        new Color(0.5f, 0.17f, 0.15f), new Color(0.2f, 0.3f, 0.5f), new Color(0.24f, 0.36f, 0.22f), new Color(0.4f, 0.26f, 0.45f),
        new Color(0.12f, 0.12f, 0.13f), new Color(0.85f, 0.85f, 0.82f),
    };

    public CharacterAppearanceData Clone() => (CharacterAppearanceData)MemberwiseClone();

    public int Style(AppearancePart part)
    {
        switch (part)
        {
            case AppearancePart.Hair: return hairStyle;
            case AppearancePart.Beard: return beardStyle;
            case AppearancePart.Eyes: return eyeStyle;
            case AppearancePart.Eyebrows: return eyebrowStyle;
            case AppearancePart.Head: return headStyle;
            case AppearancePart.Body: return bodyStyle;
            default: return noseStyle;
        }
    }

    public void SetStyle(AppearancePart part, int index)
    {
        switch (part)
        {
            case AppearancePart.Hair: hairStyle = index; break;
            case AppearancePart.Beard: beardStyle = index; break;
            case AppearancePart.Eyes: eyeStyle = index; break;
            case AppearancePart.Eyebrows: eyebrowStyle = index; break;
            case AppearancePart.Head: headStyle = index; break;
            case AppearancePart.Body: bodyStyle = index; break;
            default: noseStyle = index; break;
        }
    }

    /// <summary>A random look (only styles allowed for the body type when <paramref name="appearance"/> is given).</summary>
    public static CharacterAppearanceData Random(CharacterAppearance appearance = null, System.Random rng = null)
    {
        rng = rng ?? new System.Random();
        var d = new CharacterAppearanceData
        {
            bodyType = rng.Next(2) == 0 ? CharacterBodyType.Male : CharacterBodyType.Female,
            skinColor = SkinTones[rng.Next(8)],
            hairColor = HairColors[rng.Next(8)],
            eyeColor = EyeColors[rng.Next(6)],
            noseSize = (float)rng.NextDouble(),
            eyeSize = (float)rng.NextDouble(),
            topColor = ClothColors[rng.Next(ClothColors.Length)],
            bottomColor = ClothColors[rng.Next(4)],
        };
        foreach (AppearancePart part in (AppearancePart[])Enum.GetValues(typeof(AppearancePart)))
        {
            if (part == AppearancePart.Body) continue; // the body model is a choice, not a random look
            int count = appearance != null ? appearance.OptionCount(part) : CharacterAppearance.PlaceholderNames(part).Length;
            int pick = count > 0 ? rng.Next(part == AppearancePart.Eyebrows && count > 1 ? count - 1 : count) : 0; // eyebrows: never "None" at random
            for (int tries = 0; appearance != null && tries < 12 && !appearance.IsAllowed(part, pick, d.bodyType); tries++)
                pick = rng.Next(count);
            if (appearance != null && !appearance.IsAllowed(part, pick, d.bodyType))
                pick = 0;
            d.SetStyle(part, pick);
        }
        if (d.bodyType == CharacterBodyType.Female && appearance == null)
            d.beardStyle = 0;
        return d;
    }

    // ------------------------------------------------------------------ wire format
    private const string Version = "a1";

    /// <summary>A short text form ("a1|0|E0B090|2|3B2617|0|1|523821|0|0.50|0.50|0|0|6B5C45|423D38"); older, shorter texts still read.</summary>
    public string Encode()
    {
        var c = CultureInfo.InvariantCulture;
        return string.Join("|", Version, ((int)bodyType).ToString(c), Hex(skinColor), hairStyle.ToString(c), Hex(hairColor),
            beardStyle.ToString(c), eyeStyle.ToString(c), Hex(eyeColor), noseStyle.ToString(c),
            Mathf.Clamp01(noseSize).ToString("0.00", c), Mathf.Clamp01(eyeSize).ToString("0.00", c),
            eyebrowStyle.ToString(c), headStyle.ToString(c), Hex(topColor), Hex(bottomColor), bodyStyle.ToString(c));
    }

    /// <summary>Reads <see cref="Encode"/>'s text; anything missing or broken keeps its default.</summary>
    public static CharacterAppearanceData Decode(string text)
    {
        var d = new CharacterAppearanceData();
        if (string.IsNullOrEmpty(text) || text.Length > 256)
            return d;
        string[] p = text.Split('|');
        if (p.Length < 1 || p[0] != Version)
            return d;
        var c = CultureInfo.InvariantCulture;
        if (p.Length > 1 && int.TryParse(p[1], NumberStyles.Integer, c, out int body)) d.bodyType = body == 1 ? CharacterBodyType.Female : CharacterBodyType.Male;
        if (p.Length > 2) d.skinColor = FromHex(p[2], d.skinColor);
        if (p.Length > 3) d.hairStyle = Index(p[3], c);
        if (p.Length > 4) d.hairColor = FromHex(p[4], d.hairColor);
        if (p.Length > 5) d.beardStyle = Index(p[5], c);
        if (p.Length > 6) d.eyeStyle = Index(p[6], c);
        if (p.Length > 7) d.eyeColor = FromHex(p[7], d.eyeColor);
        if (p.Length > 8) d.noseStyle = Index(p[8], c);
        if (p.Length > 9 && float.TryParse(p[9], NumberStyles.Float, c, out float ns) && !float.IsNaN(ns)) d.noseSize = Mathf.Clamp01(ns);
        if (p.Length > 10 && float.TryParse(p[10], NumberStyles.Float, c, out float es) && !float.IsNaN(es)) d.eyeSize = Mathf.Clamp01(es);
        if (p.Length > 11) d.eyebrowStyle = Index(p[11], c);
        if (p.Length > 12) d.headStyle = Index(p[12], c);
        if (p.Length > 13) d.topColor = FromHex(p[13], d.topColor);
        if (p.Length > 14) d.bottomColor = FromHex(p[14], d.bottomColor);
        if (p.Length > 15) d.bodyStyle = Index(p[15], c);
        return d;
    }

    private static int Index(string s, IFormatProvider c) => int.TryParse(s, NumberStyles.Integer, c, out int i) ? Mathf.Clamp(i, 0, 255) : 0;

    private static string Hex(Color col) => ColorUtility.ToHtmlStringRGB(col);

    private static Color FromHex(string s, Color fallback) =>
        s != null && s.Length == 6 && ColorUtility.TryParseHtmlString("#" + s, out Color col) ? col : fallback;
}
