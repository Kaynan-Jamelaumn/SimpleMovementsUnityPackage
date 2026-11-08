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
    DebarkWood, SplitLog,
    CleanFibre, ScourFibre, OpenFibre, CardFibre, CombFibre, SpinFibre, PlyYarn, MeasureYarn, ReelYarn,
    LayoutPattern, MeasureFit,
    TestGem, IdentifyGem, EvaluateGem, OrientGem, CleaveGem, SawGem, FacetGem, PolishGem,
    FormShank, SeatSetting, FinishJewel,
    CleanGem, GrindGem, WeaveCloth,
    /// <summary>Work done by routine (<c>i0</c> = the <see cref="CraftRoutine"/>): the station works out how well from the player's practice.</summary>
    Routine,
    /// <summary>
    /// The player took up (<c>flag</c> true) or put down (false) a tool at a 3D work place (<c>i0</c> = the
    /// <c>CraftHandsOnMode</c>): in between, their work there claims the full pace of the hands (<c>d</c> = 1).
    /// </summary>
    HandsOn,
    /// <summary>Empties the quench tank and fills it with the quenchant <c>id</c> (one of the station's: its item is used up).</summary>
    FillTank,
    /// <summary>Winds the yarn of bobbin <c>other</c> onto bobbin <c>piece</c> (same fibre, same plies): one bobbin for a long warp.</summary>
    JoinYarn,
    /// <summary>A setting: the cover pulled over the drying stack to <c>a</c> (0 open sun - 1 the whole stack shaded).</summary>
    StackCover,
    /// <summary>
    /// Rips a board to a taper (the saw along a marked line): from <c>a</c> (0-1 along it) at <c>c</c> mm wide to <c>b</c>
    /// at <c>d</c> mm; <c>i0</c> = how far the saw wandered from the line, in thousandths.
    /// </summary>
    RipCut,
    /// <summary>Tries a finished item carried in the bag (<c>itemId</c>: its runtime id) at the bench: what it is shows, on the item and in the journal.</summary>
    InspectItem,
}

/// <summary>
/// One request to a crafting station. The fields mean what the kind says (see <see cref="CraftStation.Request"/>):
/// <c>piece</c> the piece it is about, <c>other</c> a second piece, <c>id</c> a fuel, flux, tool, mould, pattern, design,
/// role or joint, <c>i0-i2</c>, <c>a-d</c>, <c>flag</c> and <c>points</c> its numbers. <c>seq</c> numbers a network
/// client's request so that the station's answer finds it.
/// </summary>
public struct CraftRequest
{
    public CraftRequestKind kind;
    /// <summary>
    /// A network client's number for this request: the server answers it (done or not, and why) once the client's copy of
    /// the workshop shows what it did. 0 = no answer wanted (offline and on the host the answer is the call's own result;
    /// a setting the hand turns). The window and the 3D work places number theirs apart (<see cref="CraftAnswerWaits"/>).
    /// </summary>
    public int seq;
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
        seq = 0;
    }

    /// <summary>
    /// A setting the hand turns (the air vent, a vat's fire, the oven dial): only its latest value matters. The window
    /// sends the latest one at most ten times a second (<see cref="CraftSettingQueue"/>; a network client's station does
    /// the same) and answers it at once (no answer comes back for it).
    /// </summary>
    public static bool IsSetting(CraftRequestKind kind) =>
        kind == CraftRequestKind.Damper || kind == CraftRequestKind.VatHeat || kind == CraftRequestKind.Oven || kind == CraftRequestKind.StackCover;
}

/// <summary>
/// The settings a hand turns (<see cref="CraftRequest.IsSetting"/>) on their way to the station. A slider dragged changes
/// its value every frame, but only the latest value matters: each setting - the forge's air vent, the furnace's, a vat's
/// fire, the oven - keeps only its latest request, and they go together at most <see cref="PerSecond"/> times a second,
/// and all at once when the hands leave the work. So a setting being turned never uses up the pace of requests the
/// guard keeps for the work (<see cref="CraftRequestGuard.Paced"/>), and the last value turned is the one that stays.
/// The window holds them where the rules run (offline, the host), a network client's station before it sends them.
/// </summary>
public sealed class CraftSettingQueue
{
    /// <summary>Most times a second the settings turned go to the station.</summary>
    public const double PerSecond = 10;

    private readonly Dictionary<long, CraftRequest> latest = new Dictionary<long, CraftRequest>();
    private readonly List<long> order = new List<long>();
    private double next = double.NegativeInfinity;

    /// <summary>Settings turned and not sent yet.</summary>
    public int Count => latest.Count;

    /// <summary>Which setting a request turns: its kind, the vat (<c>i0</c>) and the fire (<c>flag</c>: the furnace's air vent, not the forge's).</summary>
    public static long Key(in CraftRequest r) => (long)r.kind << 40 | (long)(r.i0 & 0xFFFFF) << 1 | (r.flag ? 1L : 0L);

    /// <summary>A setting was turned: its value replaces the one still waiting for that setting.</summary>
    public void Put(in CraftRequest r)
    {
        long k = Key(r);
        if (!latest.ContainsKey(k))
            order.Add(k);
        latest[k] = r;
    }

    /// <summary>
    /// The settings that go at <paramref name="now"/> (seconds, real time), in <paramref name="into"/> (cleared first) in
    /// the order they were first turned: every one waiting, once a tenth of a second has passed since the last went - or
    /// at once with <paramref name="all"/> (the hands leave the work, the areas read them back). False when none go.
    /// </summary>
    public bool Take(double now, List<CraftRequest> into, bool all = false)
    {
        into.Clear();
        if (latest.Count == 0 || !all && now < next)
            return false;
        next = now + 1 / PerSecond;
        foreach (long k in order)
            into.Add(latest[k]);
        latest.Clear();
        order.Clear();
        return true;
    }

    /// <summary>Forgets the settings waiting (the station they were for is gone).</summary>
    public void Clear()
    {
        latest.Clear();
        order.Clear();
    }
}
