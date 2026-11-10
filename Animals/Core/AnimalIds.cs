using System;

/// <summary>
/// The item ids of animal husbandry: "animal:feed/&lt;food&gt;" (feed made for animals),
/// "animal:livestock/&lt;species&gt;/&lt;breed&gt;" (a young animal in a crate) and "animal:tool/&lt;tool&gt;" (a care tool:
/// a brush, a hoof pick, a pitchfork). Every machine makes the same item from the same id (see <c>AnimalItems</c>), so
/// saves, merchants and the network resolve them through the Item Database. Phase 3 adds
/// "animal:product/&lt;species&gt;/&lt;product&gt;" for eggs, milk and wool.
/// </summary>
public static class AnimalIds
{
    public const string Prefix = "animal:";
    public const string FeedKind = "feed";
    public const string LivestockKind = "livestock";
    public const string ToolKind = "tool";
    public const string ProductKind = "product";

    public static string Feed(string food) => $"{Prefix}{FeedKind}/{food}";
    /// <summary>A care tool of animal husbandry's own ("animal:tool/soft_brush").</summary>
    public static string Tool(string tool) => $"{Prefix}{ToolKind}/{tool}";
    /// <summary>A product a living animal gives ("animal:product/egg").</summary>
    public static string Product(string product) => $"{Prefix}{ProductKind}/{product}";
    public static string Livestock(string species, string breed) =>
        string.IsNullOrEmpty(breed) ? $"{Prefix}{LivestockKind}/{species}" : $"{Prefix}{LivestockKind}/{species}/{breed}";

    public static bool IsAnimalId(string id) => id != null && id.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Splits an id into its kind and the rest ("feed", "hay"; "livestock", "chicken/leghorn").</summary>
    public static bool Parse(string id, out string kind, out string key)
    {
        kind = key = null;
        if (!IsAnimalId(id)) return false;
        string rest = id.Substring(Prefix.Length);
        int slash = rest.IndexOf('/');
        if (slash <= 0 || slash >= rest.Length - 1) return false;
        kind = rest.Substring(0, slash);
        key = rest.Substring(slash + 1);
        return true;
    }

    public static bool ParseLivestock(string id, out string species, out string breed)
    {
        species = breed = null;
        if (!Parse(id, out string kind, out string key) || kind != LivestockKind) return false;
        int slash = key.IndexOf('/');
        species = slash < 0 ? key : key.Substring(0, slash);
        breed = slash < 0 ? "" : key.Substring(slash + 1);
        return species.Length > 0;
    }

    /// <summary>An id fit for content (no spaces, slashes or colons: they would break item ids and saves).</summary>
    public static bool ValidKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        foreach (char c in key)
            if (char.IsWhiteSpace(c) || c == '/' || c == ':' || c == '|') return false;
        return true;
    }
}
