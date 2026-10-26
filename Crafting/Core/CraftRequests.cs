using System.Collections.Generic;

/// <summary>What a player asks a crafting station to do (one value type, so a network layer can carry it). Add new kinds at the END.</summary>
public enum CraftRequestKind : byte
{
    PutItem, TakePiece, ScrapPiece, Move, Turn, SetIntent,
    AddFuel, Bellows, Damper, Oven,
    Crush, DiscardFragment, Wash, Sieve,
    Flux, TapSlag, ExtractBloom, StirMelt, Skim, Pour, TakeMelt,
    Strike, Fold, Weld, Split, FormFastener,
    Agitate, Clay, Descale, Grind, File, Polish, Coat, Sharpen,
    FillVat, DrainVat, VatHeat, StirVat,
    Scrape, Stretch, Stake, Condition, Dye, Slick, Skive, Cut,
    StartAssembly, Attach, Detach, JointWork, Finish, Inspect,
    Assay, Suspend, Resume, Wait,
    Carve,
    BrushOre,
    Rive, BendWood, SawCut, StackWood, SandWood, Varnish,
    Spin,
}

/// <summary>
/// One request to a crafting station. The fields mean what the kind says (see <see cref="CraftStation.Request"/>):
/// <c>piece</c> the piece it is about, <c>other</c> a second piece, <c>id</c> a fuel, flux, tool, mould, pattern, design,
/// role or joint, <c>i0-i2</c>, <c>a-d</c>, <c>flag</c> and <c>points</c> its numbers.
/// </summary>
public struct CraftRequest
{
    public CraftRequestKind kind;
    public string piece, other, id;
    /// <summary>PutItem: the item (offline / host) or its Item Database id (network).</summary>
    public ItemSO item;
    public string itemId;
    public int count, i0, i1, i2;
    public float a, b, c, d;
    public bool flag;
    public CraftLocation location;
    public List<float> points;

    public CraftRequest(CraftRequestKind kind, string piece = null)
    {
        this.kind = kind;
        this.piece = piece;
        other = id = itemId = null;
        item = null;
        count = i0 = i1 = i2 = 0;
        a = b = c = d = 0f;
        flag = false;
        location = CraftLocation.Air;
        points = null;
    }
}
