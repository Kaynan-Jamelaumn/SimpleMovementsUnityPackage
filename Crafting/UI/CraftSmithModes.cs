using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The hammer comes up again after a blow: no blow sooner than <see cref="Recovery"/> after the last - the station's
/// guard holds every player's hammer to that pace (its blows: two at once, then one each 0.4 s), so the window never sends
/// a blow it would refuse. A press made sooner waits for the hammer before it starts to rise. One hammer for the forge
/// and the anvil alike.
/// </summary>
internal static class SmithHammerClock
{
    public const float Recovery = 0.42f;
    private static float readyAt;

    /// <summary>Seconds until the hammer is up again (0: it is).</summary>
    public static float Left(float now)
    {
        if (readyAt - now > Recovery) readyAt = now; // a clock from another session
        return Mathf.Max(0f, readyAt - now);
    }

    public static bool Ready(float now) => Left(now) <= 0f;

    /// <summary>A blow was struck at <paramref name="now"/>.</summary>
    public static void Struck(float now) => readyAt = now + Recovery;
}

/// <summary>
/// The bellows fill again between pumps: the station's guard takes five pumps a second from every player (four at once),
/// so the forge's and the furnace's bellows - in the window and in 3D - send one at most every <see cref="Refill"/>
/// seconds and never meet its refusal. The air of a pump made sooner is not lost: it goes with the next one sent, or on
/// its own once the bellows have filled (<see cref="Flush"/>).
/// </summary>
internal static class BellowsClock
{
    public const float Refill = 0.2f;
    private static float sentAt = -9f;

    /// <summary>Can a pump go to the station at <paramref name="now"/>? If it can, it is taken as sent.</summary>
    private static bool Take(float now)
    {
        if (sentAt - now > Refill) sentAt = now - Refill; // a clock from another session
        if (now - sentAt < Refill) return false;
        sentAt = now;
        return true;
    }

    /// <summary>A pump of <paramref name="strength"/>: the air to send now, with what was <paramref name="pending"/> (0: wait, it stays pending).</summary>
    public static float Pump(float now, float strength, ref float pending)
    {
        pending = Mathf.Min(1f, pending + Mathf.Max(0f, strength));
        return Flush(now, ref pending);
    }

    /// <summary>The air still pending, once the bellows have filled again by <paramref name="now"/> (else 0: it stays pending).</summary>
    public static float Flush(float now, ref float pending)
    {
        if (pending <= 0f || !Take(now)) return 0f;
        float a = pending;
        pending = 0f;
        return a;
    }
}

/// <summary>
/// The forge: feed the fire, work the bellows (each pump a puff; a steady rhythm builds the draft, frantic pumping only
/// tires the arm), open or choke the air, and put the work in the fire - all of it, or only the stretch dragged along it
/// - by clicking the coals where it should lie: low in the coals is the heart of the fire (the hottest place, hotter still
/// in front of the tuyere while the bellows blow), high is the edge. The column at the hearth's side shows the heat each
/// place can reach. Click the piece in the fire to take it out, or drag it deeper or shallower.
/// <para>Forge welding, with the hands: sprinkle flux along the hot bar (each stretch takes what falls on it; a bare
/// stretch scales and will not weld), fold the bar (grab its far end and lay it over onto the near end) or stack a second
/// hot bar on it, then set the seam with the flat of the hammer while it is at welding heat: each blow closes the seam
/// where it lands by how hard it is struck and how ready that stretch is - its heat, its flux, its clean faces (the three
/// gauges). Back into the fire between heats; flux it again. A seam that cools open stays open (layers gape apart).
/// A test station has quick buttons for all of it.</para>
/// </summary>
public sealed class CraftForgeMode : CraftMode
{
    private enum Work { None, Flux, Fold, Pick }
    private enum Press { None, Bellows, Heap, Coals, Tongs, Span, Flux, Fold, Hammer, Partner }

    private const float HeartY = 48f, EdgeY = 132f, AnvilY = 282f;

    private readonly CraftBellowsPump pump = new CraftBellowsPump();
    private readonly CraftHammer hammer = new CraftHammer();
    private readonly CraftCoverage coverage = new CraftCoverage();
    private TMP_Dropdown fuel, flux;
    private Button fluxButton;
    private TextMeshProUGUI info;
    private readonly List<string> fuels = new List<string>(), fluxes = new List<string>();
    private float depthChosen = 0.7f;
    private int spanFrom = -1, spanTo = -1, dragStart = -1;
    private Vector2Int spanWas = new Vector2Int(-1, -1);
    private bool spanMoved;
    private float x0, mm, len, cy, halfW;
    private Rect bellowsArea, hearth, heap, barRect;
    private float[] starts = new float[0];
    private float puffShown, shovel = -1f, tongsY, tongsFrom, airPending;
    private Work work;
    private Press press;
    private string fluxFor;
    private bool foldGrab, pendingRaise;
    private float foldDrag, aimX = -1f;
    private int blows;
    private readonly List<(string id, Rect at, string why)> partners = new List<(string, Rect, string)>();
    private string said = "", saidSeen = "";
    private CraftLocation saidWhere;

    public override string Title => "Forge";
    public override bool Available(CraftStation s) => s.Has(CraftTools.Forge);
    public override string Hint => "Pump the bellows steadily; hold on the heap to shovel fuel; click the coals where it should lie (low is hottest) - drag along the bar first to heat only part. Right-click puts down." + ByHandHint("work the bellows by hand");
    // setting a weld is struck with the flat of the hammer; the bellows and the shovel are the hand's other work
    public override string ArmWords(int level) => CraftHands.FeelingAt(level, Selected != null && Selected.kind == CraftPieceKind.Metal && CraftForging.SeamOpen(Selected) > 0);
    public override CraftHandsOnMode HandsOnMode => CraftHandsOnMode.Bellows;

    /// <summary>How deep in the coals the next piece goes (the anvil's piece goes in there too, when it is put in from 3D).</summary>
    public float Depth => depthChosen;
    /// <summary>The stretch chosen to go in the fire (segments, inclusive), or false for the whole piece.</summary>
    public bool Span(out int from, out int to)
    {
        from = Mathf.Min(spanFrom, spanTo);
        to = Mathf.Max(spanFrom, spanTo);
        return spanFrom >= 0;
    }

    protected override void FillHandsOn(ref CraftHandsOnSetup setup)
    {
        setup.fuel = fuels.Count > 0 && fuel != null ? fuels[Mathf.Clamp(fuel.value, 0, fuels.Count - 1)] : null;
        setup.depth = depthChosen;
        setup.span = Span(out setup.from, out setup.to);
    }

    public override void TakeBackHandsOn(in CraftHandsOnSetup setup)
    {
        said = ""; // the work at the 3D place came after whatever was said here
        airPending = 0f;
        if (setup.depth > 0f) depthChosen = Mathf.Clamp(setup.depth, 0.2f, 1f);
        int f = setup.fuel != null ? fuels.IndexOf(setup.fuel) : -1;
        if (f >= 0 && fuel != null) fuel.SetValueWithoutNotify(f);
        if (setup.span) { spanFrom = setup.from; spanTo = setup.to; }
        else spanFrom = spanTo = -1;
    }

    public override void Build(RectTransform controls)
    {
        RectTransform r1 = Row(controls);
        fuel = FuelDrop(r1, fuels, 180f);
        if (QuickBench)
        {
            Btn(r1, "Add fuel", () => { AddFuel(1); }, 100f);
            Btn(r1, "+3", () => { AddFuel(3); }, 44f);
        }
        SettingSlider(r1, "Air vent", 0f, 1f, () => (float)Ws.Forge.damper, v => Send(new CraftRequest(CraftRequestKind.Damper) { a = v }),
            v => v < 0.02f ? "shut" : $"{v * 100:0}%", false, 64f);
        RectTransform r2 = Row(controls);
        if (QuickBench)
        {
            Btn(r2, "Into the fire", () => PutIn(depthChosen), 120f, UITheme.ButtonStyle.Primary);
            Btn(r2, "Out", () => TakeOut(Selected), 60f);
        }
        Btn(r2, "Whole piece", WholePiece, 110f, UITheme.ButtonStyle.Ghost);
        Btn(r2, "Turn it", TurnIt, 90f);
        HandsOnButton(r2, "Bellows by hand");
        RectTransform r3 = Row(controls);
        // Welding fluxes only: a furnace flux does nothing for a weld.
        fluxes.Clear();
        foreach (CraftSupplyDefinition s in Rules.Supplies)
            if (s.FluxFor(CraftFluxUse.Weld)) fluxes.Add(s.id);
        flux = CountedDrop(r3, fluxes, 160f, "No welding flux");
        fluxButton = Btn(r3, QuickBench ? "Flux it" : "Sprinkle flux…", () => { if (work == Work.Flux) FinishFlux(); else Begin(Work.Flux); }, 120f);
        Btn(r3, QuickBench ? "Fold and weld" : "Fold…", () => Begin(Work.Fold), 120f);
        Btn(r3, QuickBench ? "Weld to…" : "Weld on a bar…", () => Begin(Work.Pick), 120f);
        if (QuickBench)
            Btn(r3, "Into the ashes", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Ashes }), 120f);
        info = InfoRow(controls);
        ShowWork();
    }

    private string FluxId => fluxes.Count > 0 && flux != null ? fluxes[Mathf.Clamp(flux.value, 0, fluxes.Count - 1)] : null;
    private string FluxName => FluxId != null ? Rules.Supply(FluxId)?.Name.ToLowerInvariant() ?? FluxId : "welding flux";

    private bool AddFuel(int n)
    {
        if (fuels.Count == 0 || fuel == null) return false;
        string id = fuels[Mathf.Clamp(fuel.value, 0, fuels.Count - 1)];
        if (Have(id) == 0)
        {
            CraftFuel f = Rules.Fuel(id);
            said = f != null && f.burnsAnyWood ? $"You have no {f.Name.ToLowerInvariant()}: any wood burns as it (planks, logs, offcuts)." : $"You have no {f?.Name.ToLowerInvariant() ?? id}: pick a fuel you have.";
            shovel = -1f;
            return false;
        }
        string burnt = BurntAs(id); // any wood burnt as firewood: what of it went on
        Send(new CraftRequest(CraftRequestKind.AddFuel) { id = id, count = n }, (ok, why) =>
        {
            if (ok) said = burnt != null ? $"On the fire as firewood: {burnt}." : n > 1 ? "More fuel on the fire." : "A shovel of fuel on the fire.";
        });
        return true;
    }

    // ------------------------------------------------------------------ the fire
    /// <summary>The piece's height in the coals for a depth (0.2 the edge - 1 the heart), and the depth at a height.</summary>
    private static float DepthY(double depth) => EdgeY - (Mathf.Clamp((float)depth, 0.2f, 1f) - 0.2f) / 0.8f * (EdgeY - HeartY);
    private static float DepthAt(float y) => 0.2f + 0.8f * Mathf.Clamp01((EdgeY - y) / (EdgeY - HeartY));

    private static string DepthWords(double depth) => depth >= 0.85 ? "deep in the heart of the coals" : depth >= 0.55 ? "well down in the coals" : depth >= 0.35 ? "in the coals" : "at the edge of the coals";

    /// <summary>
    /// Where the bar held in the tongs is now (its centre line): where it lay, moved as far as the hand has moved since it
    /// took hold - grabbing it anywhere on it moves it nothing. Drawn up out of the coals it rises clear of them.
    /// </summary>
    private float HeldY(CraftPiece p) => Mathf.Clamp(DepthY(p.depth) + (tongsY - tongsFrom), HeartY, hearth.yMax + 12f);

    /// <summary>The tongs barely moved since they took hold: a click (take it out), not a drag.</summary>
    private bool TongsStill => Mathf.Abs(tongsY - tongsFrom) <= 6f;

    /// <summary>What a place in the coals can bring <paramref name="p"/> to now, in words (and numbers with a pyrometer).</summary>
    private string Reach(CraftPiece p, double depth)
    {
        if (Ws.Forge.temperature < 300)
            return "the fire is all but out";
        double t = CraftThermal.ForgeHeatAt(Ws, depth);
        string name = CraftMetallurgy.HeatName(t, ui.Dark);
        string gauge = Gauge ? $" (about {CraftText.Celsius(t)})" : "";
        if (!IsMetal(p) || p.kind != CraftPieceKind.Metal)
            return $"it can reach {(string.IsNullOrEmpty(name) ? "little heat" : name)} here{gauge}";
        CraftAlloyProfile prof = p.Profile(Rules);
        double weld = CraftForging.WeldingHeat(Rules, prof);
        string what = t > prof.BurnTemperature ? "hot enough to burn it - do not leave it there" : t >= weld - 10 ? "welding heat" : t >= prof.HotWorkMin ? "forging heat, not welding heat" : "not even forging heat";
        return $"it can reach {(string.IsNullOrEmpty(name) ? "little heat" : name)} here{gauge}: {what}";
    }

    private bool Gauge => Station.Reveals || Station.HasInstrument(CraftInstruments.Thermometer);

    /// <summary>Into the fire at <paramref name="depth"/>: the stretch chosen (an explicit span, so a stretch of only the first segment is not the whole piece) or the whole piece.</summary>
    private void PutIn(float depth)
    {
        CraftPiece p = Selected;
        if (!IsMetal(p)) { said = "Pick a piece of metal (or wood) to put in the fire."; return; }
        depthChosen = Mathf.Clamp(depth, 0.2f, 1f);
        var r = new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Fire, a = depthChosen };
        bool part = Span(out int from, out int to) && to < p.segments.Count;
        if (part) { r.i0 = from; r.i1 = to; r.flag = true; }
        string id = p.id;
        float d = depthChosen;
        Send(r, (ok, why) => { if (ok && ui.SelectedId == id) said = $"In the fire, {DepthWords(d)}{(part ? ", the framed stretch only" : "")}."; });
    }

    /// <summary>
    /// The whole piece in the fire: one lying in it on a framed stretch is laid in all along at once (as deep as it is);
    /// otherwise the next time it goes in, all of it does.
    /// </summary>
    private void WholePiece()
    {
        spanFrom = spanTo = -1;
        CraftPiece p = Selected;
        if (IsMetal(p) && p.location == CraftLocation.Fire && (p.from > 0 || p.to < p.segments.Count - 1))
        {
            string id = p.id;
            float d = Mathf.Clamp((float)p.depth, 0.2f, 1f);
            Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Fire, a = d }, (ok, why) =>
            {
                if (ok && ui.SelectedId == id) said = $"All of it in the fire now, {DepthWords(d)}.";
            });
            return;
        }
        said = IsMetal(p) && p.location == CraftLocation.Fire ? "All of it is in the fire." : "The whole piece goes in the fire.";
    }

    /// <summary>Deeper or shallower in the coals, keeping what is in the fire.</summary>
    private void MoveInFire(CraftPiece p, float depth)
    {
        depthChosen = Mathf.Clamp(depth, 0.2f, 1f);
        var r = new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Fire, a = depthChosen };
        int last = p.segments.Count - 1;
        if (p.from > 0 || p.to < last) { r.i0 = Mathf.Clamp(p.from, 0, last); r.i1 = Mathf.Clamp(p.to, 0, last); r.flag = true; }
        float d = depthChosen;
        Send(r, (ok, why) => { if (ok) said = $"Now {DepthWords(d)}."; });
    }

    private void TakeOut(CraftPiece p)
    {
        if (p == null) return;
        blows = 0;
        Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Air }, (ok, why) => { if (ok) said = "Out of the fire, onto the anvil."; });
    }

    private void TurnIt()
    {
        CraftPiece p = Selected;
        if (!IsMetal(p)) { said = "Pick a piece to turn."; return; }
        Send(new CraftRequest(CraftRequestKind.Turn, p.id), (ok, why) => { if (ok) said = "Turned over: the other face lies down now."; });
    }

    // ------------------------------------------------------------------ the work of the hands
    private void Begin(Work w)
    {
        CraftPiece p = Selected;
        said = "";
        if (p == null || p.kind != CraftPieceKind.Metal) { said = "Pick a metal piece first."; return; }
        if (work == w) { CancelWork(); return; } // the same button again puts it down
        if (work == Work.Flux) FinishFlux();
        if (QuickBench)
        {
            QuickWork(w, p);
            return;
        }
        string why;
        switch (w)
        {
            case Work.Flux:
                if (FluxId == null) { said = "There is no welding flux in the rules of this station."; return; }
                if (Have(FluxId) == 0) { said = $"You have no {FluxName}: {ItemOf(FluxId)} is needed."; return; }
                if (p.location != CraftLocation.Air && p.location != CraftLocation.Fire) { said = $"It is {CraftStationUI.LocationText(p)}: flux goes on in the fire or on the anvil."; return; }
                if (p.MaxTemperature < 600) { said = "Heat it to a red heat first: flux only melts and sticks on hot metal."; return; }
                if (!CraftWorkshop.CanFluxPiece(Rules.Supply(FluxId), p, out why)) { said = why; return; }
                coverage.Reset(p.segments.Count);
                fluxFor = p.id;
                break;
            case Work.Fold:
                if (!Ws.CanFold(p, out why)) { said = why; return; }
                foldGrab = false;
                foldDrag = 0f;
                break;
            case Work.Pick:
                if (!Station.Has(CraftTools.Anvil)) { said = "There is no anvil here."; return; }
                if (p.location != CraftLocation.Air) { said = "Take it out onto the anvil first."; return; }
                if (CraftForging.SeamOpen(p) > 0) { said = "Its last weld is not set yet: strike it shut at welding heat first."; return; }
                if (p.MeanTemperature < p.Profile(Rules).HotWorkMin) { said = "It is far too cold to weld: bring it to welding heat first."; return; }
                break;
        }
        work = w;
        ShowWork();
    }

    /// <summary>A test station's quick buttons: done at once, judged as the bar is now.</summary>
    private void QuickWork(Work w, CraftPiece p)
    {
        switch (w)
        {
            case Work.Flux:
                if (FluxId != null) Send(new CraftRequest(CraftRequestKind.Flux, p.id) { id = FluxId, i0 = 2, count = 1, a = 1f }, (ok, why) => { if (ok) said = "Fluxed all over."; });
                break;
            case Work.Fold:
                Send(new CraftRequest(CraftRequestKind.Fold, p.id) { b = 1f, count = CraftRequestGuard.MinWeldBlows }, (ok, why) => { if (ok) said = LastRecord(p.id); });
                break;
            case Work.Pick:
                foreach (CraftPiece q in Ws.Pieces)
                    if (q != p && Ws.CanWeld(p, q, out _))
                    {
                        string id = p.id;
                        Send(new CraftRequest(CraftRequestKind.Weld, p.id) { other = q.id, b = 1f, count = CraftRequestGuard.MinWeldBlows }, (ok, why) => { if (ok) said = LastRecord(id); });
                        return;
                    }
                said = "Another hot bar of the same length must lie on the anvil to weld it to.";
                break;
        }
    }

    /// <summary>The flux pot is put down: what was sprinkled lands (a stretch it missed gets none).</summary>
    private void FinishFlux()
    {
        if (work != Work.Flux)
            return;
        work = Work.None;
        ShowWork();
        string id = fluxFor;
        fluxFor = null;
        float cover = coverage.Mean(0.8f);
        var points = new List<float>(coverage.Count);
        for (int i = 0; i < coverage.Count; i++) points.Add(Mathf.Clamp01(coverage[i] / 0.8f));
        coverage.Reset(0);
        CraftPiece p = Ws.Piece(id);
        if (p == null || points.Count != p.segments.Count)
            return;
        if (cover < 0.05f) { said = "Hardly any flux went on: none of it was used."; return; }
        int bare = 0;
        foreach (float v in points) if (v < 0.4f) bare++;
        Send(new CraftRequest(CraftRequestKind.Flux, id) { id = FluxId, i0 = 2, count = 1, a = cover, points = points }, (ok, why) =>
        {
            if (ok) said = $"The flux melts over {cover * 100:0}% of it{(bare > 0 ? $": {bare} bare stretch{(bare == 1 ? "" : "es")} will scale and not weld" : ".")}";
        });
    }

    /// <summary>The hands put down what they hold, nothing more is done (a right-click, or the same button again).</summary>
    private void CancelWork()
    {
        switch (work)
        {
            case Work.Flux: FinishFlux(); return;
            case Work.Fold: said = foldDrag > 0f ? "You let the end go: it springs back." : ""; break;
            case Work.Pick: said = ""; break;
        }
        work = Work.None;
        foldGrab = false;
        foldDrag = 0f;
        ShowWork();
    }

    private void ShowWork()
    {
        TMP_Text t = fluxButton != null ? fluxButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (t != null) t.text = QuickBench ? "Flux it" : work == Work.Flux ? "Done fluxing" : "Sprinkle flux…";
    }

    /// <summary>The fold laid over: the seam is left open, for the blows to set.</summary>
    private void Fold(CraftPiece p)
    {
        work = Work.None;
        foldGrab = false;
        foldDrag = 0f;
        blows = 0;
        spanFrom = spanTo = -1; // a new bar: half as long
        string id = p.id;
        said = "Folding it over...";
        Send(new CraftRequest(CraftRequestKind.Fold, id) { flag = true, b = 1f, count = 1 }, (ok, why) =>
        {
            if (ui.SelectedId != id) return;
            said = ok ? "Folded over: now set the seam with the hammer while it is at welding heat - every stretch of it (the strip under the bar)." : "";
        });
    }

    private void Weld(CraftPiece p, string other)
    {
        work = Work.None;
        blows = 0;
        spanFrom = spanTo = -1;
        string id = p.id;
        said = "Laying the bars on each other...";
        Send(new CraftRequest(CraftRequestKind.Weld, id) { other = other, flag = true, b = 1f, count = 1 }, (ok, why) =>
        {
            if (ui.SelectedId != id) return;
            said = ok ? "Stacked: now set the seam with the hammer while both are at welding heat - every stretch of it (the strip under the bar)." : "";
        });
    }

    /// <summary>A setting blow (the flat of the hammer, struck flat): read back what it did to the seam.</summary>
    private void SetBlow(CraftPiece p, CraftBlow b)
    {
        SmithHammerClock.Struck(Time.unscaledTime);
        blows++;
        string id = p.id;
        var before = new double[p.segments.Count];
        for (int i = 0; i < before.Length; i++) before[i] = p.segments[i].seamOpen;
        int at = p.SegmentAt(b.position);
        double force = b.force;
        string feel = b.slipped ? b.Words : "";
        Send(new CraftRequest(CraftRequestKind.Strike, id) { a = (float)b.position, i0 = (int)CraftStrikeTool.Flat, i1 = (int)CraftStrikeFace.Flat, b = (float)b.force, c = (float)b.tilt },
            (ok, why) =>
            {
                CraftPiece q = Ws.Piece(id);
                if (!ok || q == null || ui.SelectedId != id) return;
                string sound = "";
                foreach (CraftEvent e in AnswerEvents)
                    if (e.Kind == CraftEventKind.Struck && e.Piece == id) sound = CraftLook.StrikeSound(e.Id);
                said = $"{feel}{(feel.Length > 0 ? " " : "")}{SetWords(q, before, at, force)} {sound}".Trim();
            });
    }

    /// <summary>What a setting blow did, from the bar as it is now: the seam closing, or what held it back.</summary>
    private string SetWords(CraftPiece q, double[] before, int at, double force)
    {
        double open = CraftForging.SeamOpen(q);
        if (open <= 0)
            return "The weld is set all along: it is one bar again.";
        double closed = 0, was = 0;
        int n = 0;
        if (before.Length == q.segments.Count)
            for (int i = Mathf.Max(0, at - 2); i <= Mathf.Min(q.segments.Count - 1, at + 2); i++) { closed += before[i] - q.segments[i].seamOpen; was += before[i]; n++; }
        closed /= Mathf.Max(1, n);
        was /= Mathf.Max(1, n);
        string left = $"{(1 - open) * 100:0}% of the seam set.";
        if (was < CraftForging.SeamSet)
            return $"That stretch is set already: strike where the strip under the bar is still open. {left}";
        if (closed > Math.Min(0.12, was * 0.5))
            return $"The seam closes under the blow. {left}";
        MetalSegment s = q.segments[Mathf.Clamp(at, 0, q.segments.Count - 1)];
        CraftForging.WeldFactors(Rules, q, s, out double heat, out double fl, out double clean);
        if (heat < 0.5) return $"It does not take: too cold here to weld - back into the fire to welding heat. {left}";
        if (fl < 0.25) return $"It does not take: no flux on this stretch - sprinkle flux in the fire and take another heat. {left}";
        if (clean < 0.5) return $"It does not take: scale on the faces - flux it, so the scale melts off, and take another heat. {left}";
        if (force < 0.4) return $"Too light a blow to set it. {left}";
        return $"It barely closes: strike harder, while it is at its hottest. {left}";
    }

    public override void Leave()
    {
        FinishFlux();
        hammer.Lower();
        pendingRaise = false;
        airPending = 0f; // a quick pump's air not yet sent: the bellows are let go
        shovel = -1f;
        work = Work.None;
        foldGrab = false;
        foldDrag = 0f;
        press = Press.None;
        dragStart = -1;
        LiveDraw = false;
        ShowWork();
    }

    public override void SelectionChanging(string from, string to)
    {
        Leave(); // the flux in the pot lands on the piece it was sprinkled on, while it is still the one picked
        spanFrom = spanTo = -1;
        blows = 0;
        said = "";
        partners.Clear();
    }

    public override void Tick(float dt)
    {
        pump.Tick(dt);
        hammer.Tick(dt);
        float late = BellowsClock.Flush(Time.unscaledTime, ref airPending); // the air of a quick pump, once the bellows have filled
        if (late > 0f) Send(new CraftRequest(CraftRequestKind.Bellows) { a = late });
        puffShown = Mathf.Max(0f, puffShown - dt * 3f);
        if (shovel >= 0f)
        {
            if (!ui.PointerHeld || press != Press.Heap || !heap.Contains(ui.Hover)) shovel = -1f;
            else if ((shovel += dt) >= 0.6f) { shovel = 0f; AddFuel(1); }
        }
        CraftPiece p = Selected;
        // Flux falls where the pot is held over the bar, as long as it is held there.
        if (work == Work.Flux && p != null && p.id == fluxFor && press == Press.Flux && ui.PointerHeld && PieceAt(ui.Hover, p, out int seg))
            coverage.Paint(seg, dt * 2.2f);
        // A press made while the hammer was still coming up: it starts to rise now.
        if (pendingRaise)
        {
            if (!ui.PointerHeld) pendingRaise = false;
            else if (SmithHammerClock.Ready(Time.unscaledTime)) { pendingRaise = false; hammer.Raise(); }
        }
        LiveDraw = hammer.Raised || pendingRaise || foldGrab || press == Press.Tongs || work == Work.Flux && ui.PointerHeld;
    }

    // ------------------------------------------------------------------ drawing
    public override void Draw(CraftPixels c)
    {
        CraftFireState f = Ws.Forge;
        CraftPiece p = Selected;
        hearth = new Rect(200, 30, 470, 120);
        c.Rect(hearth.xMin - 20, hearth.yMin - 20, hearth.xMax + 20, hearth.yMax, new Color(0.18f, 0.15f, 0.13f, 1f));
        Color glow = CraftArt.Heat(f.temperature, ui.Dark, out float k);
        for (int i = 0; i < 60; i++)
        {
            float x = hearth.xMin + 8 + (i * 37 % 450), y = hearth.yMin + 10 + (i * 53 % 90);
            Color coal = Color.Lerp(new Color(0.1f, 0.09f, 0.08f), glow, k * (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 3 + i)));
            c.Disc(x, y, 9 + i % 4, coal);
        }
        DrawHeatColumn(c, p);
        if (f.level > 0.05)
            for (int i = 0; i < 14; i++)
            {
                float x = hearth.xMin + 20 + i * 32, h = (float)Mathf.Min(55f, (float)(f.level * 40 * (0.7 + 0.3 * Mathf.Sin(Time.unscaledTime * 7 + i * 1.7f)) * (0.6 + f.air)));
                c.Line(x, hearth.yMax, x + 6 * Mathf.Sin(Time.unscaledTime * 5 + i), hearth.yMax + h, new Color(1f, 0.55f, 0.15f, 0.5f), 6f);
            }
        c.Halo(hearth.center.x, hearth.center.y, 260, glow, k * 0.35f);
        // The bellows and how hard they blow.
        bellowsArea = new Rect(20, 40, 150, 90);
        float squash = 1f - 0.4f * puffShown;
        c.Polygon(new List<Vector2> { new Vector2(30, 85), new Vector2(150, 85 - 30 * squash), new Vector2(150, 85 + 30 * squash) }, new Color(0.45f, 0.3f, 0.18f));
        c.Rect(150, 80, 200, 90, Iron);
        CraftArt.Label(c, 20, 24, "BLAST", CraftArt.LabelInk);
        Meter(c, 64, 24, 106, 10, (float)f.air, new Color(0.6f, 0.8f, 1f));
        // The fuel heap: hold on it to shovel.
        heap = new Rect(20, 150, 150, 70);
        for (int i = 0; i < 26; i++)
            c.Disc(heap.xMin + 12 + (i * 29 % 126), heap.yMin + 8 + (i * 17 % 40) * (1f - (i * 29 % 126) / 260f), 8 + i % 3, new Color(0.12f, 0.11f, 0.1f));
        CraftArt.Label(c, heap.xMin, heap.yMax + 4, "FUEL", CraftArt.LabelDim);
        if (shovel >= 0f) Meter(c, heap.xMin + 40, heap.yMax + 4, heap.width - 40, 8, shovel / 0.6f, new Color(1f, 0.8f, 0.4f));
        partners.Clear();
        starts = new float[0];
        if (!IsMetal(p))
            return;
        bool inFire = p.location == CraftLocation.Fire, onAnvil = p.location == CraftLocation.Air;
        if (!inFire && !onAnvil)
            return; // elsewhere (the tank, the oven...): the info line says where
        // The piece fills the hearth's width; in the fire it lies at its depth, on the anvil above the hearth.
        mm = Fit(p, 470, 60);
        len = (float)(p.Length / mm);
        x0 = hearth.xMin + (hearth.width - len) * 0.5f;
        double wide = 1;
        foreach (MetalSegment s in p.segments) wide = Math.Max(wide, s.width);
        halfW = (float)(wide / mm) * 0.5f;
        cy = inFire ? (press == Press.Tongs ? HeldY(p) : DepthY(p.depth)) : AnvilY;
        barRect = new Rect(x0, cy - halfW - 4, len, halfW * 2 + 8);
        if (onAnvil)
            c.Rect(x0 - 24, cy - halfW - 22, x0 + len + 24, cy + halfW + 12, Iron); // the anvil's face under it
        starts = CraftArt.DrawTop(c, Rules, p, new Rect(x0, cy - 45, len, 90), mm, null, p.faceUp == 1, ui.Dark);
        DrawClay(c, p);
        DrawFluxSheen(c, p);
        if (inFire)
        {
            // The stretch in the coals is buried in them.
            int a = Mathf.Clamp(p.from, 0, p.segments.Count - 1), b = Mathf.Clamp(p.to, 0, p.segments.Count - 1);
            float xa = starts[a], xb = starts[b + 1];
            for (int i = 0; i < 40; i++)
            {
                float x = xa + (i * 41 % 100) / 100f * (xb - xa), y = cy - halfW + (i * 17 % 10) / 10f * halfW * 2f;
                if (i % 3 != 0) c.Disc(x, y, 5 + i % 3, Color.Lerp(new Color(0.12f, 0.1f, 0.09f), glow, k * 0.7f));
            }
            c.Frame(xa, cy - halfW - 3, xb, cy + halfW + 3, new Color(glow.r, glow.g, glow.b, 0.5f), 1f);
            DrawFaces(c, p);
        }
        else if (spanFrom >= 0 && work == Work.None)
        {
            int a = Mathf.Clamp(Mathf.Min(spanFrom, spanTo), 0, p.segments.Count - 1), b = Mathf.Clamp(Mathf.Max(spanFrom, spanTo), 0, p.segments.Count - 1);
            c.Frame(starts[a], cy - halfW - 5, starts[b + 1], cy + halfW + 5, Mark, 2f);
        }
        if (CraftForging.SeamOpen(p) > 0) DrawSeam(c, p);
        switch (work)
        {
            case Work.Flux: DrawFlux(c, p); break;
            case Work.Fold: DrawFold(c, p); break;
            case Work.Pick: DrawPartners(c, p); break;
        }
        if (onAnvil && work == Work.None && CraftForging.SeamOpen(p) > 0) DrawSetting(c, p);
    }

    /// <summary>The hearth's side: how hot each place in the coals can bring the piece now, its welding heat and burning marked.</summary>
    private void DrawHeatColumn(CraftPixels c, CraftPiece p)
    {
        const float x = 676f, w = 12f;
        CraftArt.Label(c, 662, 154, "HEAT", CraftArt.LabelInk);
        CraftAlloyProfile prof = IsMetal(p) && p.kind == CraftPieceKind.Metal ? p.Profile(Rules) : null;
        double weld = prof != null ? CraftForging.WeldingHeat(Rules, prof) : double.MaxValue;
        for (float y = hearth.yMin; y < hearth.yMax; y += 2f)
        {
            double t = CraftThermal.ForgeHeatAt(Ws, DepthAt(y + 1f));
            Color h = CraftArt.Heat(t, ui.Dark, out float s);
            c.Rect(x, y, x + w, y + 2f, Color.Lerp(new Color(0.08f, 0.07f, 0.07f), h, Mathf.Max(0.15f, s)));
            if (prof != null && t > prof.BurnTemperature) c.Rect(x + w, y, x + w + 4f, y + 2f, Harm);
            else if (t >= weld - 10) c.Rect(x + w, y, x + w + 4f, y + 2f, Good);
        }
        c.Frame(x - 1, hearth.yMin - 1, x + w + 1, hearth.yMax + 1, new Color(1f, 1f, 1f, 0.2f), 1f);
        // Where the pointer is over the coals - or where the bar held in the tongs would lie: that depth, across the hearth.
        Vector2 h2 = ui.Hover;
        if (press == Press.Tongs && IsMetal(p) && p.location == CraftLocation.Fire)
        {
            float ty = HeldY(p);
            if (!TongsStill && ty <= hearth.yMax)
                CraftArt.Dashed(c, new Vector2(hearth.xMin, ty), new Vector2(x - 2, ty), new Color(1f, 1f, 1f, 0.3f), 1f, 5f);
        }
        else if (h2.x >= 0 && hearth.Contains(h2) && press == Press.None)
            CraftArt.Dashed(c, new Vector2(hearth.xMin, h2.y), new Vector2(x - 2, h2.y), new Color(1f, 1f, 1f, 0.3f), 1f, 5f);
    }

    /// <summary>Which face lies down in the coals and runs hotter (turn it so they even out).</summary>
    private void DrawFaces(CraftPixels c, CraftPiece p)
    {
        // Each face's heat against the other's: level (the green band) when they are even; past it, turn it.
        float bias = (float)p.faceBias; // face 1's lead over face 0's (face 1 is down while face 0 is up)
        float down = Mathf.Clamp01(0.5f + (p.faceUp == 0 ? bias : -bias)), up = 1f - down;
        var even = (0.42f, 0.58f, GoodBand);
        CraftArt.Label(c, 20, 304, "FACE UP", CraftArt.LabelInk);
        Meter(c, 86, 304, 90, 10, up, new Color(1f, 0.7f, 0.35f), even);
        CraftArt.Label(c, 20, 288, "DOWN", CraftArt.LabelInk);
        Meter(c, 86, 288, 90, 10, down, new Color(1f, 0.55f, 0.25f), even);
    }

    /// <summary>Clay on the spine (what the quench painted), so it is not forgotten in the fire.</summary>
    private void DrawClay(CraftPixels c, CraftPiece p)
    {
        if (starts.Length != p.segments.Count + 1)
            return;
        float sg = p.faceUp == 1 ? 1f : -1f; // the spine is away from the edge (the edge is drawn on the far side)
        for (int i = 0; i < p.segments.Count; i++)
        {
            float t = Mathf.Clamp01((float)p.segments[i].clay / 5f);
            if (t <= 0.02f) continue;
            float w = (float)p.segments[i].width / mm * 0.5f;
            c.Rect(starts[i], cy, starts[i + 1], cy + sg * w, new Color(0.62f, 0.52f, 0.4f, 0.35f + 0.55f * t));
        }
    }

    /// <summary>Molten flux on the bar: a glassy sheen where it lies (a bare stretch has none).</summary>
    private void DrawFluxSheen(CraftPixels c, CraftPiece p)
    {
        if (starts.Length != p.segments.Count + 1)
            return;
        for (int i = 0; i < p.segments.Count; i++)
        {
            float f = Mathf.Clamp01((float)p.segments[i].flux);
            if (f <= 0.05f) continue;
            float w = (float)p.segments[i].width / mm * 0.5f;
            c.Rect(starts[i], cy - w * 0.8f, starts[i + 1], cy + w * 0.8f, new Color(0.85f, 0.95f, 1f, 0.12f + 0.25f * f));
        }
    }

    /// <summary>The seam of a weld, stretch by stretch under the bar: dark where it is open, gold half set, green set.</summary>
    private void DrawSeam(CraftPixels c, CraftPiece p)
    {
        if (starts.Length != p.segments.Count + 1)
            return;
        float y1 = cy - halfW - 6, y0 = y1 - 8;
        CraftArt.Label(c, x0 - CraftArt.LabelWidth("SEAM") - 6, y0 - 1, "SEAM", CraftArt.LabelInk);
        for (int i = 0; i < p.segments.Count; i++)
        {
            float set = 1f - Mathf.Clamp01((float)p.segments[i].seamOpen);
            Color col = set >= 0.98f ? Good : Color.Lerp(new Color(0.5f, 0.12f, 0.08f), Fair, set);
            c.Rect(starts[i] + 0.5f, y0, starts[i + 1] - 0.5f, y1, new Color(0f, 0f, 0f, 0.6f));
            c.Rect(starts[i] + 0.5f, y0, starts[i + 1] - 0.5f, y0 + 1f + 7f * set, col);
        }
    }

    private void DrawFlux(CraftPixels c, CraftPiece p)
    {
        if (p.id != fluxFor || coverage.Count != p.segments.Count || starts.Length != p.segments.Count + 1)
            return;
        for (int i = 0; i < p.segments.Count; i++)
        {
            float w = (float)p.segments[i].width / mm, cov = Mathf.Clamp01(coverage[i] / 0.8f);
            int grains = Mathf.RoundToInt(cov * 10);
            for (int g = 0; g < grains; g++)
                c.Disc(Mathf.Lerp(starts[i], starts[i + 1], (g * 37 % 10) / 10f), cy + (g * 53 % 11 - 5) / 10f * w, 1.3f, new Color(0.95f, 0.95f, 0.85f, 0.9f));
        }
        if (ui.PointerHeld && press == Press.Flux && PieceAt(ui.Hover, p, out _))
            for (int g = 0; g < 8; g++)
                c.Disc(ui.Hover.x + (g * 7 % 9 - 4) * 2f, ui.Hover.y + 10 + g * 3f, 1.2f, new Color(1f, 1f, 0.9f, 0.8f));
        float y = cy + halfW + 8;
        CraftArt.Label(c, x0 - CraftArt.LabelWidth("FLUX") - 6, y, "FLUX", CraftArt.LabelInk);
        Meter(c, x0, y, len, 10, coverage.Mean(0.8f), new Color(0.95f, 0.95f, 0.8f), (0.8f, 1f, GoodBand));
    }

    /// <summary>The far end lifted and laid back over the bar like a page; the near end (green when the end is over it) is where it must go.</summary>
    private void DrawFold(CraftPixels c, CraftPiece p)
    {
        float mid = x0 + len * 0.5f, end = x0 + len - foldDrag * len, target = x0 + len * 0.15f;
        bool there = foldDrag >= 0.85f;
        c.Rect(x0, cy - halfW - 4, target, cy + halfW + 4, new Color(there ? 0.3f : 0.2f, there ? 0.8f : 0.5f, 0.3f, there ? 0.45f : 0.25f));
        Color metal = Color.Lerp(CraftArt.MetalColor(Rules, p), CraftArt.Heat(p.MeanTemperature, ui.Dark, out float g2), g2);
        if (foldDrag > 0f)
        {
            c.Rect(Mathf.Min(mid, end), cy - halfW, Mathf.Max(mid, end), cy + halfW, new Color(metal.r, metal.g, metal.b, 0.85f));
            c.Line(end, cy - halfW - 4, end, cy + halfW + 4, Mark, 2f);
        }
        CraftArt.Dashed(c, new Vector2(mid, cy - halfW - 8), new Vector2(mid, cy + halfW + 8), new Color(1f, 1f, 1f, 0.5f));
        CraftArt.Label(c, mid - CraftArt.LabelWidth("FOLD") * 0.5f, cy + halfW + 12, "FOLD", CraftArt.LabelDim);
        CraftArt.Label(c, x0, cy - halfW - 20, "HERE", there ? Good : CraftArt.LabelDim);
        c.Frame(x0 + len * 0.6f, cy - halfW - 4, x0 + len, cy + halfW + 4, new Color(1f, 0.8f, 0.4f, foldGrab ? 0.15f : 0.5f), 1f);
    }

    /// <summary>The bars that could be stacked on this one: in a row along the top, named, the ones not ready dimmed with why.</summary>
    private void DrawPartners(CraftPixels c, CraftPiece p)
    {
        var all = new List<(CraftPiece q, string why)>();
        foreach (CraftPiece q in Ws.Pieces)
        {
            if (q == p || q.kind != CraftPieceKind.Metal || q.location != CraftLocation.Air && q.location != CraftLocation.Fire) continue;
            string why = q.location == CraftLocation.Fire ? "IN THE FIRE" : Ws.CanWeld(p, q, out string w) ? "" : w.StartsWith("They are not the same length") ? "LENGTH" : w.StartsWith("They are far too cold") ? "TOO COLD" : "NOT READY";
            all.Add((q, why));
        }
        all.Sort((a, b) => a.why.Length.CompareTo(b.why.Length) != 0 ? a.why.Length.CompareTo(b.why.Length) : b.q.MeanTemperature.CompareTo(a.q.MeanTemperature));
        for (int n = 0; n < all.Count && n < 3; n++)
        {
            (CraftPiece q, string why) = all[n];
            var at = new Rect(190 + n * 142, 356, 136, 40);
            bool ok = why.Length == 0;
            c.Rect(at.xMin, at.yMin, at.xMax, at.yMax, new Color(0f, 0f, 0f, ok ? 0.55f : 0.35f));
            float qm = Fit(q, 124, 12);
            CraftArt.DrawTop(c, Rules, q, new Rect(at.xMin + 6, at.yMin + 20, (float)(q.Length / qm), 16), qm, ok ? (Color?)null : new Color(0.35f, 0.35f, 0.38f), false, ui.Dark);
            string name = CraftArt.LabelFit(CraftLook.Name(Rules, q, ui.Known), at.width - 10);
            CraftArt.Label(c, at.xMin + 5, at.yMin + 4, ok ? name : why, ok ? CraftArt.LabelInk : CraftArt.LabelDim);
            if (ok && at.Contains(ui.Hover)) c.Frame(at.xMin, at.yMin, at.xMax, at.yMax, Mark, 2f);
            partners.Add((q.id, at, why));
        }
    }

    /// <summary>
    /// Setting the seam: the hammer over the bar (its face as wide as it bears), and for the stretch under it the three
    /// things a weld needs - its heat against the welding band, its flux, its clean faces - and the blow's charge.
    /// </summary>
    private void DrawSetting(CraftPixels c, CraftPiece p)
    {
        float hx = aimX >= x0 && aimX <= x0 + len ? aimX : ui.Hover.x;
        bool over = hx >= x0 && hx <= x0 + len;
        int seg = over ? Mathf.Clamp(SegmentAtPixel(p, hx, x0, mm), 0, p.segments.Count - 1) : -1;
        // The gauges, for the stretch under the hammer.
        if (seg >= 0)
        {
            MetalSegment s = p.segments[seg];
            CraftAlloyProfile prof = p.Profile(Rules);
            double weld = CraftForging.WeldingHeat(Rules, prof), lo = weld - 450, hi = weld + 50;
            CraftForging.WeldFactors(Rules, p, s, out _, out double fl, out double clean);
            Color hc = CraftArt.Heat(s.Temperature, ui.Dark, out float hk);
            // The welding band: it barely takes at its foot (amber), well from its middle up (green) - as the seam's rule ramps.
            CraftArt.Label(c, 16, 374, "HEAT", CraftArt.LabelInk);
            Meter(c, 64, 374, 120, 10, (float)((s.Temperature - lo) / (hi - lo)), hk > 0.05f ? hc : new Color(0.35f, 0.3f, 0.3f),
                ((float)((weld - 150 - lo) / (hi - lo)), (float)((weld - 75 - lo) / (hi - lo)), FairBand),
                ((float)((weld - 75 - lo) / (hi - lo)), 1f, GoodBand));
            CraftArt.Label(c, 16, 358, "FLUX", CraftArt.LabelInk);
            Meter(c, 64, 358, 120, 10, (float)fl, new Color(0.95f, 0.95f, 0.8f), (0.3f, 1f, GoodBand));
            CraftArt.Label(c, 16, 342, "CLEAN", CraftArt.LabelInk);
            Meter(c, 64, 342, 120, 10, (float)clean, new Color(0.7f, 0.75f, 0.8f), (0.6f, 1f, GoodBand));
        }
        float charge = (float)hammer.Charge;
        bool wavers = hammer.Wavers;
        CraftArt.Label(c, 16, 326, "BLOW", CraftArt.LabelInk);
        Meter(c, 64, 326, 120, 10, (float)hammer.Expected(Hands), wavers ? Harm : new Color(1f, 0.8f, 0.4f)); // as it would land: a tired arm's lighter
        if (!over)
            return;
        // The hammer's face, as wide as the flat of it bears on the bar; at rest an outline, raised it comes down where it is.
        float fw = (float)(CraftForging.Contact(CraftStrikeTool.Flat) / mm), reach = fw * 0.5f + fw * 0.6f;
        c.Rect(hx - reach, cy - halfW, hx + reach, cy + halfW, new Color(1f, 1f, 1f, 0.06f));
        float lift = pendingRaise ? 0f : 30f * charge, y0 = cy + halfW + 8 + lift;
        if (hammer.Raised) c.Rect(hx - fw * 0.5f, y0, hx + fw * 0.5f, y0 + 14, new Color(0.4f, 0.4f, 0.44f, 0.9f));
        c.Frame(hx - fw * 0.5f, y0, hx + fw * 0.5f, y0 + 14, pendingRaise ? CraftArt.LabelDim : new Color(0.75f, 0.75f, 0.8f), 1.5f);
    }

    private float SegX(CraftPiece p, int i)
    {
        if (starts.Length == p.segments.Count + 1)
            return starts[Mathf.Clamp(i, 0, p.segments.Count)];
        double pos = 0;
        for (int k = 0; k < i && k < p.segments.Count; k++) pos += p.segments[k].length;
        return x0 + (float)(pos / mm);
    }

    /// <summary>Is <paramref name="pos"/> on the bar as it is drawn (in the fire or on the anvil)? Which segment.</summary>
    private bool PieceAt(Vector2 pos, CraftPiece p, out int seg)
    {
        seg = -1;
        if (p == null || starts.Length != p.segments.Count + 1 || !barRect.Contains(pos))
            return false;
        seg = Mathf.Clamp(SegmentAtPixel(p, pos.x, x0, mm), 0, p.segments.Count - 1);
        return true;
    }

    // ------------------------------------------------------------------ the pointer
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Selected;
        if (phase == CraftPointer.Cancel)
        {
            // Puts down what the hands hold: the hammer comes down without a blow, a drag lets go.
            if (hammer.Raised || pendingRaise) { hammer.Lower(); pendingRaise = false; said = "You lower the hammer."; }
            else if (press == Press.Fold && foldGrab) { foldGrab = false; foldDrag = 0f; said = "You let the end go: it springs back."; }
            else if (press == Press.Span) { dragStart = -1; spanFrom = spanWas.x; spanTo = spanWas.y; }
            else if (press == Press.Tongs) { said = "You leave it where it was."; }
            else if (work != Work.None) CancelWork();
            shovel = -1f;
            press = Press.None;
            return;
        }
        if (phase == CraftPointer.Scroll || phase == CraftPointer.Move)
        {
            if (phase == CraftPointer.Move && IsMetal(p)) aimX = pos.x;
            return;
        }
        if (phase == CraftPointer.Down)
        {
            press = Press.None;
            PressDown(pos, p);
            return;
        }
        if (phase == CraftPointer.Drag)
        {
            aimX = pos.x;
            switch (press)
            {
                case Press.Span:
                {
                    if (p == null || dragStart < 0) break;
                    int seg = Mathf.Clamp(SegmentAtPixel(p, pos.x, x0, mm), 0, p.segments.Count - 1);
                    if (seg != dragStart || spanMoved)
                    {
                        spanMoved = true;
                        spanFrom = dragStart;
                        spanTo = seg;
                    }
                    break;
                }
                case Press.Fold:
                    if (foldGrab) foldDrag = Mathf.Clamp01((x0 + len - pos.x) / Mathf.Max(1f, len)); // the end follows the hand
                    break;
                case Press.Tongs:
                    tongsY = pos.y;
                    break;
            }
            return;
        }
        // Up: the gesture this press began ends.
        Press was = press;
        press = Press.None;
        if (p == null)
            return;
        switch (was)
        {
            case Press.Span:
                if (spanMoved && spanFrom >= 0)
                {
                    int a = Mathf.Min(spanFrom, spanTo), b = Mathf.Max(spanFrom, spanTo);
                    said = b > a ? $"The framed stretch ({(b - a + 1) * 100 / Mathf.Max(1, p.segments.Count)}% of it) goes in the fire: click the coals." : "Drag over more of it to frame a stretch.";
                    if (b == a) spanFrom = spanTo = -1; // a stretch is two segments at the least: a click is not one
                }
                dragStart = -1;
                break;
            case Press.Tongs:
            {
                // A click anywhere on the bar takes it out; a drag lays it as much deeper or shallower as the hand moved,
                // and drawn up out of the coals it comes out.
                tongsY = pos.y;
                float to = HeldY(p);
                if (TongsStill || to > hearth.yMax) TakeOut(p);
                else MoveInFire(p, DepthAt(to));
                break;
            }
            case Press.Fold:
                if (!foldGrab) break;
                foldGrab = false;
                if (foldDrag >= 0.85f) Fold(p);
                else { foldDrag = 0f; said = "Not brought right over: it springs back. Lay the end all the way onto the near end (the green mark)."; }
                break;
            case Press.Hammer:
                if (pendingRaise) { pendingRaise = false; said = "The hammer had not come up yet: wait for it before the next blow."; break; }
                if (!hammer.Raised) break;
                {
                    // A setting blow: the flat of the hammer, struck flat (the hand's own tremble aside).
                    float length = Mathf.Max(1f, len);
                    CraftBlow blow = hammer.Release(Mathf.Clamp01((pos.x - x0) / length), 0, Hands);
                    SetBlow(p, blow);
                }
                break;
        }
    }

    private void PressDown(Vector2 pos, CraftPiece p)
    {
        aimX = pos.x;
        if (bellowsArea.Contains(pos))
        {
            press = Press.Bellows;
            float s = (float)pump.Pump(Hands);
            puffShown = 1f;
            float air = BellowsClock.Pump(Time.unscaledTime, s, ref airPending); // a pump too soon goes with the next
            if (air > 0f) Send(new CraftRequest(CraftRequestKind.Bellows) { a = air });
            Station.GetComponentInChildren<CraftStationVisuals>()?.Puff();
            return;
        }
        if (heap.Contains(pos) && !QuickBench)
        {
            press = Press.Heap;
            shovel = 0f;
            return;
        }
        if (!IsMetal(p))
        {
            if (hearth.Contains(pos)) said = "Pick a piece to put in the fire.";
            return;
        }
        switch (work)
        {
            case Work.Pick:
                foreach ((string id, Rect at, string why) in partners)
                    if (at.Contains(pos))
                    {
                        press = Press.Partner;
                        if (why.Length == 0) Weld(p, id);
                        else
                        {
                            CraftPiece q = Ws.Piece(id);
                            said = q != null && q.location == CraftLocation.Fire ? "That bar is in the fire: take it out at welding heat first (pick it in the list)."
                                : Ws.CanWeld(p, q, out string w) ? "" : w;
                        }
                        return;
                    }
                return;
            case Work.Flux:
                if (PieceAt(pos, p, out _)) press = Press.Flux;
                return;
            case Work.Fold:
                if (PieceAt(pos, p, out _) && pos.x > x0 + len * 0.6f)
                {
                    press = Press.Fold;
                    foldGrab = true;
                    foldDrag = Mathf.Clamp01((x0 + len - pos.x) / Mathf.Max(1f, len));
                }
                else if (PieceAt(pos, p, out _)) said = "Grab the far (right) end of the bar.";
                return;
        }
        if (p.location == CraftLocation.Fire)
        {
            if (PieceAt(pos, p, out _)) { press = Press.Tongs; tongsY = tongsFrom = pos.y; } // click: out; drag: deeper or shallower
            else if (hearth.Contains(pos)) { press = Press.Coals; MoveInFire(p, DepthAt(pos.y)); }
            return;
        }
        if (p.location != CraftLocation.Air)
        {
            if (hearth.Contains(pos)) said = $"It is {CraftStationUI.LocationText(p)}: take it out there first.";
            return;
        }
        if (CraftForging.SeamOpen(p) > 0 && PieceAt(pos, p, out _))
        {
            // Setting the seam: the hammer rises while held (once it is up again from the last blow).
            press = Press.Hammer;
            if (SmithHammerClock.Ready(Time.unscaledTime)) hammer.Raise();
            else pendingRaise = true;
            return;
        }
        if (PieceAt(pos, p, out int seg))
        {
            press = Press.Span;
            dragStart = seg;
            spanMoved = false;
            spanWas = new Vector2Int(spanFrom, spanTo);
            return;
        }
        if (hearth.Contains(pos))
        {
            press = Press.Coals;
            PutIn(DepthAt(pos.y));
        }
    }

    public override void Refresh()
    {
        RefreshCounts(fuel, fuels);
        RefreshCounts(flux, fluxes);
        if (info == null)
            return;
        Forget(ref said, ref saidSeen, ref saidWhere, Selected);
        info.text = (How(Selected) + " " + said).Trim();
    }

    /// <summary>What to do now, from the piece as it is.</summary>
    private string How(CraftPiece p)
    {
        if (!IsMetal(p))
            return "Pick a piece of metal on the station to heat it.";
        switch (work)
        {
            case Work.Flux:
                return $"<b>Sprinkling {FluxName}</b>: hold over the hot bar and move along it - each stretch takes what falls on it. {coverage.Mean(0.8f) * 100:0}% covered. <i>Done fluxing</i> when finished (right-click puts the pot down too).";
            case Work.Fold:
                return "<b>Folding</b>: grab the far (right) end and lay it over onto the near (left) end - the green mark - then let go.";
            case Work.Pick:
                return partners.Count == 0 ? "<b>Welding on a bar</b>: no other bar lies on the anvil or in the fire. Put the other bar on the station and heat it too."
                    : "<b>Welding on a bar</b>: click the other bar (the boxes at the top) to stack it on this one; dimmed ones are not ready.";
        }
        double open = CraftForging.SeamOpen(p);
        if (p.location == CraftLocation.Fire)
        {
            bool downHotter = (p.faceBias > 0) == (p.faceUp == 0); // faceBias: face 1's lead; face 1 is down while face 0 is up
            string faces = Math.Abs(p.faceBias) > 0.15 && downHotter ? " The face down in the coals runs hotter: turn it (Turn it) so it heats evenly." : "";
            string seam = open > 0 ? $" The seam is {(1 - open) * 100:0}% set: take it out at welding heat and strike it shut." : "";
            if (press == Press.Tongs && !TongsStill)
            {
                // In the tongs: where it would lie if let go now.
                float to = HeldY(p);
                return to > hearth.yMax ? "Out of the coals: let go to take it out onto the anvil."
                    : $"Let go to lay it {DepthWords(DepthAt(to))}: {Reach(p, DepthAt(to))}.";
            }
            return $"In the fire, {DepthWords(p.depth)}: {Reach(p, p.depth)}. Click it to take it out; drag it, or click the coals, to move it.{seam}{faces}";
        }
        if (p.location != CraftLocation.Air)
            return $"It is {CraftStationUI.LocationText(p)}.";
        if (open > 0)
        {
            CraftAlloyProfile prof = p.Profile(Rules);
            bool hot = false;
            foreach (MetalSegment s in p.segments) hot |= s.seamOpen > 0 && s.Temperature > CraftForging.WeldingHeat(Rules, prof) - 150;
            return hot
                ? $"<b>Setting the weld</b>: hold and let go on the bar to strike with the flat of the hammer; each blow closes the seam where it lands, as far as that stretch is hot, fluxed and clean (the gauges). {(1 - open) * 100:0}% set, {blows} blow{(blows == 1 ? "" : "s")}."
                : $"<b>The seam is {(1 - open) * 100:0}% set</b> and it has cooled below welding heat: flux it again and take another welding heat (click the coals), then strike the open stretches.";
        }
        Vector2 h = ui.Hover;
        string here = h.x >= 0 && hearth.Contains(h) ? $" There, {DepthWords(DepthAt(h.y))}, {Reach(p, DepthAt(h.y))}." : "";
        string fire = Ws.Forge.temperature < 300 ? " The fire is out: shovel fuel on (hold on the heap) and pump the bellows." : "";
        string flux = p.MaxTemperature > 800 && p.flux < 0.2 ? " Hot enough to flux: sprinkle flux on it before folding or welding." : "";
        return $"On the anvil. Click the coals where it should lie: low is the heart of the fire, high the edge.{here}{fire}{flux}";
    }
}

/// <summary>
/// The anvil: the bar seen from the side (its thickness, drawn taller when it is thin) and from above. Press on the bar
/// and hold to raise the hammer (the longer, the harder - until the arm tires and the blow wavers: the gauge turns red),
/// drag up or down while holding to tilt its face (a bevel on the face that is up - a face held near level strikes
/// flat), release to strike where you aim. The hammer comes up again before the next blow. The tool decides what a blow
/// does: the flat face spreads, the cross peen draws it out or spreads it, the fuller grooves it, the horn bends it (the
/// way you choose), the hardy and the hot chisel cut it (the cut grows blow by blow), the drift opens an eye, the
/// flatter smooths, the dishing stake cups a plate. The face that is up is the gold line; the section where the hammer
/// would fall is drawn, turned a quarter when the bar is on its edge.
/// </summary>
public sealed class CraftAnvilMode : CraftMode
{
    private readonly CraftHammer hammer = new CraftHammer();
    private CraftStrikeTool tool = CraftStrikeTool.Flat;
    private CraftStrikeFace face = CraftStrikeFace.Flat;
    private readonly List<Button> toolButtons = new List<Button>();
    private Button faceButton, chiselButton, hornButton;
    private TMP_Dropdown purpose;
    private readonly List<(string design, string role)> purposes = new List<(string, string)>();
    private bool intentPending;
    private TextMeshProUGUI blowText;
    private float x0, mm, len, sideScale = 3f, topY = 322f, aim, tilt, pressY;
    private bool pressing, pendingRaise, hornUp = true;
    private float flipShown = -1f, lastDrawAt, chiselAt = -1f;
    private string lastBlow = "", lastSeen = "";
    private CraftLocation lastWhere;
    // Work of several blows done in the hands only: rivets cut and headed, a buckle bent (the station makes them at the end).
    private enum Job { None, Rivets, Buckle }
    private Job job;
    private float jobDone;
    private static readonly CraftStrikeTool[] ToolOrder =
    {
        CraftStrikeTool.Flat, CraftStrikeTool.PeenAcross, CraftStrikeTool.PeenAlong, CraftStrikeTool.Fuller, CraftStrikeTool.Horn,
        CraftStrikeTool.Hardy, CraftStrikeTool.Drift, CraftStrikeTool.Flatter, CraftStrikeTool.Dishing, CraftStrikeTool.Straighten,
    };

    public override string Title => "Anvil";
    public override bool Available(CraftStation s) => s.Has(CraftTools.Anvil);
    public override string Hint => "Hold on the bar to raise the hammer, let go to strike; drag up or down as you hold to tilt it (a bevel). Below a dull red, steel tears. Wheel: the tool. Right-click lowers the hammer." + ByHandHint("hammer it by hand");
    public override string ArmWords(int level) => CraftHands.FeelingAt(level, true);
    public override CraftHandsOnMode HandsOnMode => CraftHandsOnMode.Hammer;
    public override string Busy => job == Job.Rivets ? "making the rivets" : job == Job.Buckle ? "bending the buckle" : null;

    protected override void FillHandsOn(ref CraftHandsOnSetup setup)
    {
        setup.tool = tool;
        setup.face = face;
        setup.bendDown = !hornUp; // over the horn, the way chosen here
        // Into the fire from the anvil: as deep, and as much of it, as the forge was set to.
        CraftForgeMode forge = ui.Area<CraftForgeMode>();
        setup.depth = forge != null ? forge.Depth : 0.7f;
        if (forge != null) setup.span = forge.Span(out setup.from, out setup.to);
    }

    public override void TakeBackHandsOn(in CraftHandsOnSetup setup)
    {
        if (Enum.IsDefined(typeof(CraftStrikeTool), setup.tool)) tool = setup.tool;
        if (Enum.IsDefined(typeof(CraftStrikeFace), setup.face)) face = setup.face;
        hornUp = !setup.bendDown;
        lastBlow = ""; // the blows at the 3D anvil came after the window's last
        ShowSelection();
    }

    private static string TabName(CraftStrikeTool t) => t switch
    {
        CraftStrikeTool.Flat => "Flat face",
        CraftStrikeTool.PeenAcross => "Peen: draw out",
        CraftStrikeTool.PeenAlong => "Peen: spread",
        CraftStrikeTool.Fuller => "Fuller",
        CraftStrikeTool.Horn => "Horn",
        CraftStrikeTool.Hardy => "Hardy",
        CraftStrikeTool.Drift => "Drift",
        CraftStrikeTool.Flatter => "Flatter",
        CraftStrikeTool.Dishing => "Dishing",
        CraftStrikeTool.Straighten => "Straighten",
        CraftStrikeTool.Chisel => "Hot chisel",
        _ => CraftForging.ToolName(t),
    };

    /// <summary>What a blow of the tool does, in a few words.</summary>
    private static string ToolWords(CraftStrikeTool t) => t switch
    {
        CraftStrikeTool.Flat => "the flat face thins it and spreads it both ways; tilted, it forges a bevel",
        CraftStrikeTool.PeenAcross => "the cross peen across the bar draws it out (longer)",
        CraftStrikeTool.PeenAlong => "the cross peen along the bar spreads it (wider)",
        CraftStrikeTool.Fuller => "the fuller grooves it down the middle",
        CraftStrikeTool.Horn => "over the horn it bends, the way chosen",
        CraftStrikeTool.Hardy => "on the hardy it is cut from below, a little more each blow",
        CraftStrikeTool.Drift => "the drift punches and opens an eye",
        CraftStrikeTool.Flatter => "the flatter smooths the hammer marks, moving little metal",
        CraftStrikeTool.Dishing => "the dishing hammer cups a plate",
        CraftStrikeTool.Straighten => "struck on the face it straightens a bend",
        CraftStrikeTool.Chisel => "the hot chisel stays where it is set and is driven through blow by blow (strike well away from it to set it there)",
        _ => "",
    };

    public override void Build(RectTransform controls)
    {
        toolButtons.Clear();
        RectTransform r1 = Row(controls, 32f, 4f);
        RectTransform r2 = Row(controls, 32f, 4f);
        for (int i = 0; i < ToolOrder.Length; i++)
        {
            CraftStrikeTool t = ToolOrder[i];
            Button b = Btn(i < 5 ? r1 : r2, TabName(t), () => PickTool(t), 0f, UITheme.ButtonStyle.Tab);
            toolButtons.Add(b);
        }
        RectTransform r3 = Row(controls);
        faceButton = Btn(r3, "On its face", () => { face = face == CraftStrikeFace.Flat ? CraftStrikeFace.Edge : CraftStrikeFace.Flat; ShowSelection(); }, 120f);
        Btn(r3, "Turn over", () => Send(new CraftRequest(CraftRequestKind.Turn, ui.SelectedId)), 100f);
        chiselButton = QuickBench ? Btn(r3, "Cut here (chisel)", QuickChisel, 140f) : Btn(r3, "Hot chisel", () => PickTool(CraftStrikeTool.Chisel), 100f, UITheme.ButtonStyle.Tab);
        Btn(r3, QuickBench ? "Make rivets" : "Rivets…", () => StartJob(Job.Rivets), QuickBench ? 110f : 80f);
        Btn(r3, QuickBench ? "Bend a buckle" : "Buckle…", () => StartJob(Job.Buckle), QuickBench ? 110f : 80f);
        RectTransform r4 = Row(controls);
        // What the bar is for - a design's metal part, as the carving bench says it for a blank: its target shape is then
        // dashed over the bar, with the length ticks and the target section.
        purposes.Clear();
        var names = new List<string> { "(nothing yet)" };
        foreach (CraftDesign d in Rules.Designs)
            if (d != null)
                foreach (CraftComponentSpec c in d.components)
                    if (CraftRules.Castable(c))
                    {
                        purposes.Add((d.id, c.role));
                        names.Add($"{d.Name}: {c.Name}");
                    }
        Text(r4, "For", 13f, 30f);
        purpose = Drop(r4, names, 0, 250f);
        purpose.onValueChanged.AddListener(OnPurpose);
        HandsOnButton(r4, "Take up the hammer");
        // The way over the horn, shown with the Horn picked: on this row, so the row of the work's buttons (wider at a
        // test station) never runs out of the column.
        hornButton = Btn(r4, "Bend: far end up", () => { hornUp = !hornUp; ShowSelection(); }, 150f);
        blowText = InfoRow(controls, 48f); // what happened: its own wrapping line, inside the window
        ShowSelection();
        SyncPurpose();
    }

    /// <summary>The menu shows what the metal piece picked is for (nothing is sent); with no metal picked it is idle.</summary>
    private void SyncPurpose()
    {
        if (purpose == null) return;
        CraftPiece p = Selected;
        bool metal = p != null && p.kind == CraftPieceKind.Metal;
        if (purpose.interactable != metal) purpose.interactable = metal; // a wooden piece's purpose is said at the carving bench
        int k = metal && !string.IsNullOrEmpty(p.intentDesign) ? purposes.FindIndex(x => x.design == p.intentDesign && x.role == p.intentRole) : -1;
        if (purpose.value != k + 1) purpose.SetValueWithoutNotify(k + 1);
    }

    /// <summary>The bar is said to be for a design's part (or for nothing yet): its target shows over it from now.</summary>
    private void OnPurpose(int v)
    {
        CraftPiece p = Selected;
        if (p == null || p.kind != CraftPieceKind.Metal)
        {
            lastBlow = p == null ? "Pick a bar first." : "A wooden piece is given its purpose at the carving bench.";
            SyncPurpose();
            return;
        }
        bool some = v > 0 && v <= purposes.Count;
        string id = p.id;
        CraftDesign d = some ? Rules.Design(purposes[v - 1].design) : null;
        CraftComponentSpec part = d?.Component(purposes[v - 1].role);
        string what = part != null ? $"the {part.Name.ToLowerInvariant()} for the {d.Name.ToLowerInvariant()}" : "";
        intentPending = true;
        Send(new CraftRequest(CraftRequestKind.SetIntent, id) { id = some ? purposes[v - 1].design : "", other = some ? purposes[v - 1].role : "" }, (ok, why) =>
        {
            intentPending = false;
            if (!ok) { SyncPurpose(); return; }
            if (ui.SelectedId == id)
                lastBlow = some ? $"To be {what}: its shape is dashed in green over the bar, the shortest and longest it may be ticked under it." : "It is meant for nothing in particular now.";
        });
    }

    private void PickTool(CraftStrikeTool t)
    {
        tool = t;
        if (t != CraftStrikeTool.Chisel) chiselAt = -1f;
        ShowSelection();
    }

    private void ShowSelection()
    {
        for (int i = 0; i < toolButtons.Count; i++) UITheme.SetSelected(toolButtons[i], ToolOrder[i] == tool);
        if (chiselButton != null && !QuickBench) UITheme.SetSelected(chiselButton, tool == CraftStrikeTool.Chisel);
        TMP_Text t = faceButton != null ? faceButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (t != null) t.text = face == CraftStrikeFace.Flat ? "On its face" : "On its edge";
        if (hornButton != null)
        {
            hornButton.gameObject.SetActive(tool == CraftStrikeTool.Horn);
            TMP_Text h = hornButton.GetComponentInChildren<TMP_Text>(true);
            if (h != null) h.text = hornUp ? "Bend: far end up" : "Bend: far end down";
        }
    }

    public override void Tick(float dt)
    {
        hammer.Tick(dt);
        if (pendingRaise)
        {
            if (!ui.PointerHeld) pendingRaise = false;
            else if (SmithHammerClock.Ready(Time.unscaledTime)) { pendingRaise = false; hammer.Raise(); }
        }
        LiveDraw = hammer.Raised || pendingRaise || flipShown >= 0f && Selected != null && Mathf.Abs(flipShown - (Selected.faceUp == 1 ? 180f : 0f)) > 0.5f;
    }

    public override void Leave()
    {
        hammer.Lower();
        pendingRaise = pressing = false;
        job = Job.None;
        jobDone = 0f;
        LiveDraw = false;
    }

    public override void SelectionChanging(string from, string to)
    {
        Leave();
        chiselAt = -1f;
        lastBlow = "";
        flipShown = -1f;
    }

    public override void SelectionChanged(string to) => SyncPurpose();

    /// <summary>A test station: the chisel cuts through at once where the pointer last aimed.</summary>
    private void QuickChisel()
    {
        CraftPiece p = Selected;
        if (p == null) { lastBlow = "Pick a piece first."; return; }
        string id = p.id;
        Send(new CraftRequest(CraftRequestKind.Split, id) { a = aim }, (ok, why) => { if (ok) lastBlow = "Cut through with the chisel."; });
    }

    /// <summary>Rivets or a buckle: checked first, asked twice (the whole rod goes into them), then made with firm blows.</summary>
    private void StartJob(Job j)
    {
        CraftPiece p = Selected;
        if (p == null) { lastBlow = "Pick a piece first."; return; }
        if (job == j) { job = Job.None; lastBlow = "You put that aside."; return; }
        var kind = j == Job.Rivets ? CraftFastenerKind.Rivet : CraftFastenerKind.Buckle;
        if (!Ws.CanFormFastener(p, kind, out string why, out int count)) { lastBlow = why; return; }
        string what = j == Job.Rivets ? $"about {count} rivet{(count == 1 ? "" : "s")}" : count > 1 ? $"{count} buckles" : "a buckle";
        if (!Confirm("fasteners:" + p.id + j, $"Click again to make the whole {CraftLook.ShapeWord(Rules, p)} into {what}."))
            return;
        if (QuickBench)
        {
            MakeFasteners(p, j);
            return;
        }
        job = j;
        jobDone = 0f;
        lastBlow = job == Job.Rivets ? "Cut and head the rivets: firm blows on the end of the rod." : "Bend the buckle round the horn: firm blows.";
    }

    /// <summary>A blow on a job: firm blows count fully, light ones a little; done after about four.</summary>
    private void JobBlow(CraftPiece p, CraftBlow b)
    {
        jobDone += Mathf.Clamp01((float)b.force * 1.4f) * 0.28f;
        if (jobDone < 1f)
        {
            lastBlow = b.slipped ? $"{b.Words} ({jobDone * 100:0}%)" : $"{(b.force > 0.6 ? "A firm blow" : "A light blow")}: it takes shape ({jobDone * 100:0}%).";
            return;
        }
        Job j = job;
        job = Job.None;
        MakeFasteners(p, j);
    }

    private void MakeFasteners(CraftPiece p, Job j)
    {
        var kind = j == Job.Rivets ? CraftFastenerKind.Rivet : CraftFastenerKind.Buckle;
        Send(new CraftRequest(CraftRequestKind.FormFastener, p.id) { i0 = (int)kind }, (ok, why) =>
        {
            if (!ok) return;
            int made = 0;
            foreach (CraftEvent e in AnswerEvents)
                if (e.Kind == CraftEventKind.PieceCreated && Ws.Piece(e.Piece) is CraftPiece f && f.kind == CraftPieceKind.Fasteners) made = f.fasteners.count;
            lastBlow = j == Job.Rivets ? $"{made} rivet{(made == 1 ? "" : "s")} cut and headed." : made > 1 ? $"{made} buckles bent." : "The buckle is bent.";
        });
    }

    // ------------------------------------------------------------------ drawing
    public override void Draw(CraftPixels c)
    {
        // The anvil: horn on the left, the face, the hardy hole.
        c.Polygon(new List<Vector2> { new Vector2(60, 150), new Vector2(170, 130), new Vector2(170, 160) }, Iron);
        c.Rect(170, 110, 640, 160, Iron);
        c.Rect(250, 30, 560, 110, new Color(0.17f, 0.17f, 0.19f));
        c.Rect(600, 150, 616, 160, new Color(0.05f, 0.05f, 0.06f));
        CraftPiece p = Selected;
        if (!IsMetal(p))
            return;
        // Room for the design's length and its width too, so the target shows how far it still has to be drawn out or
        // spread - inside the top view, as the piece is.
        CraftShapeTarget shape = CraftArt.TargetShape(Rules, p, out _);
        float targetWide = 0f;
        if (shape != null)
            foreach (CraftProfilePoint pt in shape.profile) targetWide = Mathf.Max(targetWide, pt.width);
        mm = Mathf.Max(Fit(p, 560, 110), shape != null ? Mathf.Max(shape.lengthMax / 560f, targetWide / 110f) : 0f);
        len = (float)(p.Length / mm);
        float span = Mathf.Max(len, shape != null ? shape.lengthMax / mm : 0f);
        x0 = 170 + (470 - span) * 0.5f;
        // The side view shows the thickness magnified (up to × 3) - never taller than 95 px, so it stays clear of the top view.
        double thick = 0.5;
        foreach (MetalSegment s in p.segments) thick = Math.Max(thick, s.thickness);
        if (shape != null)
            for (int i = 0; i <= 16; i++) { shape.At(i / 16f, out _, out float th); thick = Math.Max(thick, th); }
        sideScale = Mathf.Min(3f, 95f * mm / (float)thick);
        // Turning it over rolls it about its length: the section turns, and from above the edge goes to the other side.
        float now = Time.unscaledTime, target = p.faceUp == 1 ? 180f : 0f;
        flipShown = flipShown < 0f ? target : Mathf.MoveTowards(flipShown, target, (now - lastDrawAt) * 600f);
        lastDrawAt = now;
        float flip = Mathf.Cos(flipShown * Mathf.Deg2Rad);
        CraftArt.DrawSide(c, Rules, p, new Rect(x0, 160, len, 40), mm, sideScale, ui.Dark);
        float[] starts = CraftArt.DrawTop(c, Rules, p, new Rect(x0, topY - 50, len, 100), mm, null, flip < 0f, ui.Dark);
        CraftArt.DrawTargetProfile(c, Rules, p, x0, 160, mm, true, sideScale);
        CraftArt.DrawTargetProfile(c, Rules, p, x0, topY, mm, false);
        CraftArt.Label(c, 12, 176, Mathf.Abs(sideScale - 1f) >= 0.05f ? $"SIDE ×{sideScale:0.#}" : "SIDE", CraftArt.LabelDim);
        CraftArt.Label(c, 12, topY - 5, "TOP", CraftArt.LabelDim);
        DrawCuts(c, p, starts, flip < 0f);
        if (p.kind == CraftPieceKind.Metal && CraftForging.SeamOpen(p) > 0) DrawSeam(c, p, starts);
        // The section where the hammer would fall: the face that is up takes a tilted blow's bevel.
        Vector2 h = ui.Hover;
        float hx = pressing || h.x < 0 ? x0 + aim * len : Mathf.Clamp(h.x, x0, x0 + len);
        int seg = Mathf.Clamp(SegmentAtPixel(p, Mathf.Clamp(hx, x0, x0 + len), x0, mm), 0, p.segments.Count - 1);
        DrawSectionBox(c, p, seg, flip);
        // Too cold to forge here: the side view warns where the hammer would fall.
        CraftAlloyProfile prof = p.Profile(Rules);
        MetalSegment sg = p.segments[seg];
        if (p.kind == CraftPieceKind.Metal && sg.body.temp < prof.HotWorkMin - 200 && prof.Steel)
            c.Rect(starts[seg], 156, starts[seg + 1], 160, Harm);
        if (p.location != CraftLocation.Air)
        {
            c.Rect(0, 0, c.Width, c.Height, new Color(0f, 0f, 0f, 0.45f));
            string where = CraftStationUI.LocationText(p).ToUpperInvariant() + (p.location == CraftLocation.Fire ? ": CLICK TO TAKE IT OUT" : "");
            CraftArt.Label(c, 250, 210, CraftArt.LabelFit(where, 300), CraftArt.LabelInk);
            return;
        }
        DrawHammer(c, p, starts, flip < 0f, hx);
    }

    /// <summary>
    /// A forge weld's seam still open, stretch by stretch on the anvil's face under the bar (as the forge draws it): dark
    /// where it is open, gold half set, green set - the flat blows here close it too.
    /// </summary>
    private static void DrawSeam(CraftPixels c, CraftPiece p, float[] starts)
    {
        if (starts.Length != p.segments.Count + 1)
            return;
        const float y0 = 134f, y1 = 142f; // on the anvil's face, clear of the length ticks and the too-cold mark under the side view
        CraftArt.Label(c, starts[0] - CraftArt.LabelWidth("SEAM") - 6, y0 - 1, "SEAM", CraftArt.LabelInk);
        for (int i = 0; i < p.segments.Count; i++)
        {
            float set = 1f - Mathf.Clamp01((float)p.segments[i].seamOpen);
            Color col = set >= 0.98f ? Good : Color.Lerp(new Color(0.5f, 0.12f, 0.08f), Fair, set);
            c.Rect(starts[i] + 0.5f, y0, starts[i + 1] - 0.5f, y1, new Color(0f, 0f, 0f, 0.6f));
            c.Rect(starts[i] + 0.5f, y0, starts[i + 1] - 0.5f, y0 + 1f + 7f * set, col);
        }
    }

    /// <summary>
    /// Cuts made on the hardy or with the chisel: a notch as deep as the cut has gone - up from the anvil's face for the
    /// hardy, down from the top for the chisel (from above a groove across the bar); the chisel where it stands.
    /// </summary>
    private void DrawCuts(CraftPixels c, CraftPiece p, float[] starts, bool flipped)
    {
        var dark = new Color(0.03f, 0.03f, 0.03f, 0.95f);
        for (int i = 0; i < p.segments.Count; i++)
        {
            MetalSegment s = p.segments[i];
            float cut = Mathf.Clamp01((float)s.cut);
            if (cut <= 0.01f) continue;
            float mx = (starts[i] + starts[i + 1]) * 0.5f, w = (float)s.width / mm * 0.5f;
            float t = Mathf.Max(1f, (float)s.thickness / mm * sideScale), depth = Mathf.Max(2f, t * cut), floor = 160f + (float)s.dish / mm;
            if (s.cutFromAbove)
            {
                c.Rect(mx - 1, topY - w, mx + 1, topY + w, new Color(dark.r, dark.g, dark.b, 0.3f + 0.65f * cut));
                c.Rect(mx - 1, floor + t - depth, mx + 1, floor + t, dark);
            }
            else
            {
                c.Polygon(new List<Vector2> { new Vector2(mx - 3, topY - w), new Vector2(mx + 3, topY - w), new Vector2(mx, topY - w + cut * w * 2f) }, dark);
                c.Rect(mx - 1, floor, mx + 1, floor + depth, dark);
            }
        }
        if (tool == CraftStrikeTool.Chisel && chiselAt >= 0f)
        {
            float jx = x0 + chiselAt * len;
            c.Line(jx, 150, jx, 390, new Color(1f, 0.6f, 0.4f, 0.8f), 2f);
        }
    }

    /// <summary>The section box: the segment where the hammer would fall, magnified; turned a quarter when the bar lies on its edge.</summary>
    private void DrawSectionBox(CraftPixels c, CraftPiece p, int seg, float flip)
    {
        var box = new Rect(12, 8, 220, 96);
        MetalSegment sg = p.segments[seg];
        bool hasTarget = CraftArt.TargetAt(Rules, p, seg, out float tw, out float tt, out float ta, out bool edged);
        if (face == CraftStrikeFace.Edge)
        {
            DrawSectionOnEdge(c, sg, box, CraftArt.MetalColor(Rules, p), p.edges);
            CraftArt.Label(c, box.xMin + 4, box.yMax - 14, "ON ITS EDGE", CraftArt.LabelDim);
            return;
        }
        float scale = CraftArt.SectionScale(sg, box, hasTarget ? tw : 0f, hasTarget ? tt : 0f);
        Vector2 o = CraftArt.DrawSection(c, sg, box, CraftArt.MetalColor(Rules, p), scale, flip, p.kind == CraftPieceKind.Wood, p.edges);
        if (hasTarget) CraftArt.DrawTargetSection(c, o, scale, tw, tt, ta, edged, p.IntendedEdges(Rules, p.edges) >= 2, flip); // the target's own edges
        // The face that is up: a bright line along it.
        c.Line(o.x - (float)sg.width * 0.5f * scale, o.y + (float)sg.thickness * 0.5f * scale + 3f, o.x + (float)sg.width * 0.5f * scale, o.y + (float)sg.thickness * 0.5f * scale + 3f, Mark, 1.5f);
    }

    /// <summary>The section stood on its edge (a quarter turn): the width up, the thickness across - the hammer comes down on the edge.</summary>
    private static void DrawSectionOnEdge(CraftPixels c, MetalSegment s, Rect r, Color metal, int edges)
    {
        c.Rect(r.xMin, r.yMin, r.xMax, r.yMax, new Color(0f, 0f, 0f, 0.45f));
        c.Frame(r.xMin, r.yMin, r.xMax, r.yMax, new Color(1f, 1f, 1f, 0.12f), 1f);
        float scale = Mathf.Max(0.5f, Mathf.Min((r.width - 24f) / Mathf.Max(1f, (float)s.thickness), (r.height - 24f) / Mathf.Max(4f, (float)s.width)));
        Vector2 o = r.center;
        float hw = (float)s.width * 0.5f * scale, ht = (float)s.thickness * 0.5f * scale;
        float bl = Mathf.Min((float)s.bevelWidthL * scale, edges >= 2 ? hw : hw * 1.9f), br = Mathf.Min((float)s.bevelWidthR * scale, edges >= 2 ? hw : hw * 1.9f);
        float left = -ht + (float)s.bevelDepthL * scale, right = ht - (float)s.bevelDepthR * scale;
        if (right < left + 0.5f) { float m = (left + right) * 0.5f; left = m - 0.25f; right = m + 0.25f; }
        // (x, y) of the lying section turned a quarter: its width now runs up, the edge at the top.
        Vector2 P(float across, float up) => new Vector2(o.x + across, o.y + up);
        var pts = edges >= 2
            ? new List<Vector2> { P(-ht, -hw + bl), P(-ht, hw - bl), P(left, hw), P(right, hw), P(ht, hw - br), P(ht, -hw + br), P(right, -hw), P(left, -hw) }
            : new List<Vector2> { P(-ht, -hw), P(-ht, hw - bl), P(left, hw), P(right, hw), P(ht, hw - br), P(ht, -hw) };
        c.Polygon(pts, Color.Lerp(metal, Color.white, 0.12f));
        c.Outline(pts, CraftArt.Outline, 1f);
        c.Line(o.x - ht, o.y + hw + 3f, o.x + ht, o.y + hw + 3f, Mark, 1.5f); // the hammer falls on the edge that is up
    }

    /// <summary>
    /// The hammer over the top view: its face as wide as the tool bears (the faint band: where the blow still reaches), an
    /// outline at rest, filled as it rises; tilted, a wedge shows the bevel it forges on the edge side of the face up.
    /// </summary>
    private void DrawHammer(CraftPixels c, CraftPiece p, float[] starts, bool flipped, float hx)
    {
        if (hx < x0 - 10 || hx > x0 + len + 10)
            return;
        float fw = Mathf.Max(3f, (float)(CraftForging.Contact(tool) / mm)), reach = CraftForging.Cuts(tool) || tool == CraftStrikeTool.Drift ? fw * 0.5f : fw * 0.5f + fw * 0.6f;
        float wide = 0f;
        foreach (MetalSegment s in p.segments) wide = Mathf.Max(wide, (float)s.width);
        float hw = wide / mm * 0.5f;
        c.Line(hx, 150, hx, Mathf.Min(396f, topY + hw + 30), new Color(1f, 1f, 1f, 0.12f), 1f);
        c.Rect(hx - reach, topY - hw, hx + reach, topY + hw, new Color(1f, 1f, 1f, 0.05f));
        float charge = (float)hammer.Charge;
        Color edge = pendingRaise ? new Color(0.62f, 0.62f, 0.66f) : new Color(0.85f, 0.85f, 0.9f);
        if (hammer.Raised) c.Rect(hx - fw * 0.5f, topY - hw, hx + fw * 0.5f, topY + hw, new Color(0.45f, 0.45f, 0.5f, 0.12f + 0.3f * charge));
        c.Frame(hx - fw * 0.5f, topY - hw, hx + fw * 0.5f, topY + hw, edge, 1.5f);
        // Tilted (past the level the hands hold anyway): the bevel side shaded, a wedge on the face.
        float t = Mathf.Abs(tilt);
        if (tool == CraftStrikeTool.Flat && t > (float)CraftForging.FlatTilt && pressing)
        {
            float sg = flipped ? -1f : 1f;
            float strength = (t - (float)CraftForging.FlatTilt) / (1f - (float)CraftForging.FlatTilt);
            c.Rect(hx - fw * 0.5f, topY + sg * hw, hx + fw * 0.5f, topY + sg * (hw - Mathf.Max(3f, hw * 0.5f * strength)), new Color(1f, 0.75f, 0.3f, 0.35f + 0.4f * strength));
            c.Polygon(new List<Vector2> { new Vector2(hx - fw * 0.5f, topY + hw + 8), new Vector2(hx + fw * 0.5f, topY + hw + 8), new Vector2(hx + fw * 0.5f, topY + hw + 8 + 10 * strength) }, Mark);
        }
        if (tool == CraftStrikeTool.Horn)
        {
            // The way the far end will swing.
            float dir = (hornUp ? 1f : -1f) * 10f;
            c.Line(hx, topY, hx + 30, topY + dir * 0.4f, Mark, 2f);
            c.Line(hx + 30, topY + dir * 0.4f, hx + 50, topY + dir, Mark, 2f);
        }
        // The blow's charge (red once it is held too long and wavers), and the cut where the hardy or the chisel works.
        bool wavers = hammer.Raised && hammer.Held > hammer.fullCharge + hammer.holdBeforeWaver;
        CraftArt.Label(c, 12, 106, "BLOW", CraftArt.LabelInk);
        Meter(c, 58, 106, 100, 10, charge, wavers ? Harm : new Color(1f, 0.8f, 0.4f));
        if (job != Job.None)
        {
            CraftArt.Label(c, 12, 118, "JOB", CraftArt.LabelInk);
            Meter(c, 58, 118, 100, 10, jobDone, new Color(0.6f, 0.85f, 1f));
        }
        else if (CraftForging.Cuts(tool))
        {
            int at = tool == CraftStrikeTool.Chisel && chiselAt >= 0f ? p.SegmentAt(chiselAt) : Mathf.Clamp(SegmentAtPixel(p, hx, x0, mm), 0, p.segments.Count - 1);
            CraftArt.Label(c, 12, 118, "CUT", CraftArt.LabelInk);
            Meter(c, 58, 118, 100, 10, (float)p.segments[at].cut, new Color(0.6f, 0.85f, 1f));
        }
    }

    // ------------------------------------------------------------------ the pointer
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Selected;
        if (phase == CraftPointer.Cancel)
        {
            // The hammer comes down without a blow; with nothing raised, a job is put aside.
            if (hammer.Raised || pendingRaise) lastBlow = "You lower the hammer.";
            else if (job != Job.None) { job = Job.None; lastBlow = "You put that aside."; }
            hammer.Lower();
            pendingRaise = pressing = false;
            return;
        }
        if (phase == CraftPointer.Scroll)
        {
            // The wheel: the next tool (or the one before).
            int i = Array.IndexOf(ToolOrder, tool);
            PickTool(ToolOrder[((i < 0 ? 0 : i) + (scroll > 0 ? 1 : ToolOrder.Length - 1)) % ToolOrder.Length]);
            return;
        }
        if (!IsMetal(p))
            return;
        float l = (float)(p.Length / mm);
        aim = Mathf.Clamp01((pos.x - x0) / Mathf.Max(1f, l));
        if (phase == CraftPointer.Move)
            return;
        if (phase == CraftPointer.Down)
        {
            tilt = 0f;
            pressY = pos.y;
            // In the fire: a click lays it on the anvil with the tongs (the forge's own take-out, the 3D anvil's Fire key).
            if (p.location == CraftLocation.Fire)
            {
                OutOfTheFire(p);
                return;
            }
            if (pos.x < x0 - 10 || pos.x > x0 + l + 10 || pos.y < 140 || pos.y > 395)
                return;
            if (p.location != CraftLocation.Air)
            {
                lastBlow = NotOnTheAnvil(p);
                return;
            }
            if (p.kind == CraftPieceKind.Wood && tool != CraftStrikeTool.Straighten)
            {
                // said before the hammer rises, as the station would refuse the blow
                lastBlow = "Wood is shaped with a rasp and a knife, not the hammer: here it is only straightened (Straighten).";
                return;
            }
            pressing = true;
            if (SmithHammerClock.Ready(Time.unscaledTime)) hammer.Raise();
            else pendingRaise = true;
            return;
        }
        if (phase == CraftPointer.Drag)
        {
            // Tilt from where the press began: within 8 px of it the face stays level, a full tilt 60 px beyond (the 3D
            // anvil's rule, by its screen).
            tilt = (float)CraftForging.TiltFrom(pos.y - pressY, 8, 60);
            return;
        }
        // Up: strike.
        bool was = pressing;
        pressing = false;
        if (!was)
            return;
        if (pendingRaise)
        {
            pendingRaise = false;
            lastBlow = "The hammer had not come up yet: wait for it before the next blow.";
            return;
        }
        if (!hammer.Raised)
            return;
        if (job != Job.None)
        {
            SmithHammerClock.Struck(Time.unscaledTime);
            JobBlow(p, hammer.Release(aim, 0, Hands));
            return;
        }
        Strike(p);
    }

    /// <summary>Out of the fire with the tongs, onto the anvil (as the forge takes it out).</summary>
    private void OutOfTheFire(CraftPiece p)
    {
        string id = p.id;
        hammer.Lower();
        pendingRaise = pressing = false;
        Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Air }, (ok, why) => { if (ok && ui.SelectedId == id) lastBlow = "Out of the fire, onto the anvil."; });
    }

    /// <summary>What a piece that is not lying on the anvil needs before it is worked here: where it is taken out, and how.</summary>
    private static string NotOnTheAnvil(CraftPiece p)
    {
        switch (p.location)
        {
            case CraftLocation.Fire: return "It is in the fire: click the anvil to take it out onto it with the tongs.";
            case CraftLocation.Quench:
            case CraftLocation.Oven:
            case CraftLocation.Ashes: return $"It is {CraftStationUI.LocationText(p)}: take it out at the Quench to work it here.";
            case CraftLocation.Assembled: return "It is fitted to an item: take it off (Assemble) to forge it.";
            case CraftLocation.Steam: return "It is in the steam box: drag it out at the Woodshop (Steam & bend) to straighten it here.";
            default: return $"It is {CraftStationUI.LocationText(p)}: take it out onto the anvil to work it.";
        }
    }

    private void Strike(CraftPiece p)
    {
        // The chisel stays where it was set on the bar: every blow drives it there (aimed well away from it, it is set there instead).
        if (tool == CraftStrikeTool.Chisel && (chiselAt < 0f || Mathf.Abs(aim - chiselAt) * len > 12f)) chiselAt = aim;
        float at = tool == CraftStrikeTool.Chisel ? chiselAt : aim;
        // Over the horn, the way chosen (by the drawing: the far end up or down, as it lies now). Up the drawing is the
        // edge's side with the left face up - a bend toward the edge, negative - and the spine's with the right face up.
        float t = tilt;
        if (tool == CraftStrikeTool.Horn) t = (hornUp ? -1f : 1f) * (p.faceUp == 1 ? -1f : 1f);
        CraftBlow blow = hammer.Release(at, tool == CraftStrikeTool.Horn ? 0 : t, Hands);
        double sent = tool == CraftStrikeTool.Horn ? t : blow.tilt;
        double position = tool == CraftStrikeTool.Chisel ? chiselAt : blow.position;
        SmithHammerClock.Struck(Time.unscaledTime);
        string id = p.id;
        int seg = p.SegmentAt(position);
        string feel = CraftForging.BlowFeel(Rules, p, seg); // how that stretch takes the blow, as it is before it lands
        double seamBefore = CraftForging.SeamOpen(p);
        string words = blow.Words;
        Send(new CraftRequest(CraftRequestKind.Strike, id) { a = (float)position, i0 = (int)tool, i1 = (int)face, b = (float)blow.force, c = (float)sent }, (ok, why) =>
        {
            if (!ok || ui.SelectedId != id) return;
            // This blow's own sound, and what it did that can be seen.
            string sound = "", cut = "";
            foreach (CraftEvent e in AnswerEvents)
            {
                if (e.Piece != id) continue;
                if (e.Kind == CraftEventKind.Struck) sound = CraftLook.StrikeSound(e.Id);
                else if (e.Kind == CraftEventKind.Cut) cut = " It parts: cut in two.";
            }
            CraftPiece q = Ws.Piece(id);
            if (cut.Length == 0 && q != null && CraftForging.Cuts(tool) && seg < q.segments.Count)
                cut = $" The cut is {Mathf.Clamp01((float)q.segments[seg].cut) * 100:0}% through.";
            if (cut.Length > 0 && tool == CraftStrikeTool.Chisel) chiselAt = -1f;
            string seam = q != null && seamBefore > 0 && CraftForging.SeamOpen(q) < seamBefore - 0.005 ? $" The weld's seam closes ({(1 - CraftForging.SeamOpen(q)) * 100:0}% set)." : "";
            lastBlow = $"{words}{feel} {sound}{cut}{seam}".Trim();
        });
    }

    /// <summary>
    /// How the stretch under the hammer moves at its heat, in words (empty at forging heat): steel tears cold, a soft
    /// metal is worked cold and hardens, the rest moves less and less below a bright red - as <see cref="CraftForging.Gives"/>.
    /// </summary>
    private string HeatWords(CraftPiece p, MetalSegment s)
    {
        CraftAlloyProfile prof = p.Profile(Rules);
        double t = s.body.temp;
        if (t >= prof.HotWorkMin) return "";
        if (prof.Steel && t < prof.HotWorkMin - 200) return "<color=#ff7a5c>Too cold to forge here: steel tears below a dull red - back into the fire.</color> ";
        if (CraftForging.Gives(Rules, prof, s) < CraftForging.BarelyMoves) return "Too cold to forge here: hard and cold, it barely moves - back into the fire. ";
        // Soft metals stay workable cold (copper, brass, silver, gold): they move, and harden with the work.
        bool soft = !prof.Steel && CraftForging.ColdPlasticity(prof) >= 0.5;
        if (soft) return "Worked cold here: it still moves, and hardens as it is worked - anneal it (heat it to a dull red) when it stiffens. ";
        string heat = CraftMetallurgy.HeatName(prof.HotWorkMin, ui.Dark); // this metal's own forging heat
        heat = string.IsNullOrEmpty(heat) || heat == "black heat" ? "its forging heat" : heat.StartsWith("a ", StringComparison.Ordinal) ? heat : "a " + heat;
        return $"Cooling here: it moves less and less below {heat}, and hardens if it is worked cold. ";
    }

    public override void Refresh()
    {
        if (blowText == null)
            return;
        CraftPiece p = Selected;
        if (!intentPending && purpose != null && !purpose.IsExpanded) SyncPurpose(); // what another hand (or the casting, a fitting) gave it
        Forget(ref lastBlow, ref lastSeen, ref lastWhere, p); // the last blow's words go once the piece is somewhere else
        if (!IsMetal(p))
        {
            blowText.text = "Pick a piece of metal on the station to forge it.";
            return;
        }
        ShowSelection();
        var sb = new System.Text.StringBuilder();
        sb.Append($"<b>{CraftText.Capital(TabName(tool))}</b>: {ToolWords(tool)}. ");
        if (p.location != CraftLocation.Air)
            sb.Append(NotOnTheAnvil(p)).Append(' ');
        else
        {
            sb.Append($"{(p.faceUp == 0 ? "Left" : "Right")} face up (the gold line). ");
            // Too cold where the hammer would fall?
            Vector2 h = ui.Hover;
            if (h.x >= x0 && h.x <= x0 + len && p.kind == CraftPieceKind.Metal)
                sb.Append(HeatWords(p, p.segments[Mathf.Clamp(SegmentAtPixel(p, h.x, x0, mm), 0, p.segments.Count - 1)]));
            else if (p.kind == CraftPieceKind.Wood)
                sb.Append(CraftForging.WoodGive(p) < 0.5 ? "Wood: only Straighten - cold it mostly springs back; steamed hot (the steam box) it straightens. " : "Soft from the steam: straighten it now, before it cools. ");
            if (CraftArt.TargetShape(Rules, p, out _) is CraftShapeTarget t)
                sb.Append(p.Length < t.lengthMin * 0.97 ? "Shorter than it should be: draw it out to the green ticks. "
                    : p.Length > t.lengthMax * 1.03 ? "Longer than it should be: cut the extra off on the hardy or with the hot chisel. "
                    : "Long enough: now bring it to the green outline. ");
            else if (CraftLook.Bloom(Rules, p))
                sb.Append("A bloom: close it up at welding heat with flat blows until it is a sound bar - then say what it is for (For). ");
            else if (p.kind == CraftPieceKind.Metal && string.IsNullOrEmpty(p.intentDesign))
                sb.Append("Meant for nothing yet: say what it is for (For) and its shape is dashed over it. ");
            // A forge weld still open (folded or stacked at the forge): the blows here set it too, as far as it is ready.
            double open = p.kind == CraftPieceKind.Metal ? CraftForging.SeamOpen(p) : 0;
            if (open > 0)
                sb.Append($"The weld's seam is {(1 - open) * 100:0}% set (the strip under the bar): flat blows at welding heat close it where they land - the Forge shows its heat, flux and clean faces. ");
            if (job != Job.None) sb.Append(job == Job.Rivets ? "Making rivets: firm blows. " : "Bending a buckle: firm blows. ");
        }
        sb.Append(lastBlow);
        blowText.text = sb.ToString().Trim();
    }
}

/// <summary>
/// Quenching and tempering, with the hands: drag the hot work from the bench - or straight from the forge's fire - and
/// let it down into the tank (upright, point first, or flat - which warps it): how low you let go is how much of it goes
/// under. In the tank, hold it and keep it moving up and down along it, or round; side to side warps it, and held still a
/// vapour jacket clings to it and it stays soft. Drag it up out of the tank to lift it out. Drag it into the oven to
/// temper (hold on the firebox to kindle the oven at the dial's heat, hold again to rake the fire out; hold on the oven
/// to take it out). Drag it onto the ash box and rake the ash over it to anneal slowly; hold on the ashes to dig it out.
/// Paint clay along the spine before hardening (thicker where you go over it more; wipe it off where it should not be):
/// the edge is left bare and hardens, the clayed spine stays tough. Fill the tank with another quenchant the station
/// keeps. A test station has quick buttons for all of it.
/// </summary>
public sealed class CraftQuenchMode : CraftMode
{
    private enum Hold { None, Firebox, OvenDoor, DigOut }
    private enum Press { None, Hold, Drag, Lift, Rake, Clay, Quick }

    private const float Surface = 200f, Upright = 70f;
    private static readonly Color Clay = new Color(0.62f, 0.52f, 0.4f, 1f);

    private Slider depth, clay;
    private TMP_Dropdown entry, medium;
    private Button clayButton, wipeButton;
    private TextMeshProUGUI info;
    private readonly List<string> media = new List<string>();
    private Rect tank, ovenBox, firebox, chamber, ashBox, bench, mouth;
    private readonly List<(Vector2 at, float t)> moves = new List<(Vector2, float)>();
    private CraftQuenchMotion sent = CraftQuenchMotion.Still;
    private float motionTimer, ovenDial = 200f, depthChosen = 1f, dropDepth;
    private int clayFrom = -1, clayTo = -1;
    private float mmT;
    private bool painting, wiping, burying, hungOverTank;
    private string clayFor;
    private Vector2 hand = new Vector2(-1, -1), tankHand = new Vector2(-1, -1), grab;
    private readonly CraftCoverage clayCoat = new CraftCoverage();
    private readonly CraftShake rake = new CraftShake();
    private float buried;
    private Hold hold;
    private Press press;
    private float holdTime;
    private string said = "", saidSeen = "";
    private CraftLocation saidWhere;

    public override string Title => "Quench";
    public override bool Available(CraftStation s) => s.Has(CraftTools.QuenchTank) || s.Has(CraftTools.Oven) || s.Has(CraftTools.Ashes);
    public override string Hint => "Drag the hot piece from the bench or the fire into the tank, the oven or the ashes; keep it moving in the tank, drag it up to lift it out. Hold on the firebox to light the oven. Right-click puts down." + ByHandHint("quench it by hand");
    public override CraftHandsOnMode HandsOnMode => Station.Has(CraftTools.QuenchTank) ? CraftHandsOnMode.Quench : CraftHandsOnMode.None;

    private bool HasTank => Station.Has(CraftTools.QuenchTank);
    private bool HasOven => Station.Has(CraftTools.Oven);
    private bool HasAshes => Station.Has(CraftTools.Ashes);
    private bool Gauge => Station.Reveals || Station.HasInstrument(CraftInstruments.Thermometer);
    private bool Flat => entry != null && entry.value == 1;
    private float EntryAngle => Flat ? 0.9f : 0f;

    protected override void FillHandsOn(ref CraftHandsOnSetup setup)
    {
        setup.depth = depthChosen;
        setup.entryAngle = EntryAngle;
    }

    public override void TakeBackHandsOn(in CraftHandsOnSetup setup)
    {
        said = ""; // the work at the 3D place came after whatever was said here
        if (entry != null) entry.SetValueWithoutNotify(setup.entryAngle >= 0.5f ? 1 : 0);
        if (setup.depth > 0f) depthChosen = Mathf.Clamp01(setup.depth);
    }

    public override void Build(RectTransform controls)
    {
        if (HasTank)
        {
            RectTransform r1 = Row(controls);
            entry = Drop(r1, new[] { "Upright, point first", "Flat (it warps)" }, 0, 190f);
            if (QuickBench)
            {
                depth = Slide(r1, "Depth", 0.2f, 1f, depthChosen, false, 50f, v => $"{v * 100:0}%");
                Btn(r1, "Plunge", () => Plunge(depth != null ? depth.value : 1f), 90f, UITheme.ButtonStyle.Primary);
                Btn(r1, "Lift out", () => LiftOut(Selected), 90f);
            }
            RectTransform r2 = Row(controls);
            media.Clear();
            media.AddRange(Station.Quenchants);
            medium = CountedDrop(r2, media, 190f);
            int now = media.FindIndex(m => string.Equals(m, Ws.Quench.medium, StringComparison.OrdinalIgnoreCase));
            if (now >= 0) medium.SetValueWithoutNotify(now);
            Btn(r2, "Fill the tank", FillTank, 120f);
            HandsOnButton(r2, "Quench by hand", 170f);
        }
        if (HasOven)
        {
            RectTransform r3 = Row(controls);
            if (Ws.Oven.setpoint > Ws.Ambient) ovenDial = (float)Ws.Oven.setpoint;
            SettingSlider(r3, "Oven dial", (float)CraftOvenState.DialMin, (float)CraftOvenState.DialMax, () => Ws.Oven.setpoint > Ws.Ambient ? (float)Ws.Oven.setpoint : ovenDial,
                v => { ovenDial = v; if (Ws.Oven.setpoint > 0) Send(new CraftRequest(CraftRequestKind.Oven) { a = v }); }, v => $"{v:0} °C", true, 70f);
            if (QuickBench)
            {
                Btn(r3, "Light the oven", () => Send(new CraftRequest(CraftRequestKind.Oven) { a = ovenDial }), 120f);
                Btn(r3, "Into the oven", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Oven }), 110f);
                Btn(r3, "Oven off", () => Send(new CraftRequest(CraftRequestKind.Oven) { a = 0 }), 80f, UITheme.ButtonStyle.Ghost);
            }
        }
        if (HasTank || QuickBench && HasAshes)
        {
            RectTransform r4 = Row(controls);
            if (HasTank)
            {
                clay = Slide(r4, "Clay", 0.5f, 5f, 2f, false, 40f, v => $"{v:0.#} mm");
                if (QuickBench)
                    Btn(r4, "Clay it", QuickClay, 80f);
                else
                {
                    clayButton = Btn(r4, "Paint clay…", ToggleClay, 120f);
                    wipeButton = Btn(r4, "Wipe off", ToggleWipe, 90f, UITheme.ButtonStyle.Tab);
                }
            }
            if (QuickBench && HasAshes)
            {
                Btn(r4, "Bury in ashes", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Ashes, a = 1f }), 120f);
                Btn(r4, "Out of the ashes", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Air }), 130f, UITheme.ButtonStyle.Ghost);
            }
        }
        info = InfoRow(controls);
        ShowClay();
    }

    // ------------------------------------------------------------------ the tank
    private void Plunge(float d)
    {
        CraftPiece p = Selected;
        if (!IsMetal(p)) { said = "Pick a piece to quench."; return; }
        if (!HasTank) { said = "There is no quench tank here."; return; }
        depthChosen = Mathf.Clamp(d, 0.05f, 1f);
        sent = CraftQuenchMotion.Still;
        string id = p.id;
        float dd = depthChosen;
        bool flat = Flat;
        tankHand = new Vector2(tank.center.x - 40f, Surface + 30f);
        Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Quench, a = depthChosen, b = EntryAngle }, (ok, why) =>
        {
            if (!ok || ui.SelectedId != id) return;
            string seen = "";
            foreach (CraftEvent e in AnswerEvents)
                if ((e.Kind == CraftEventKind.Quenched || e.Kind == CraftEventKind.QuenchFlare) && e.Piece == id) seen = e.Text;
            said = $"{(flat ? "In flat" : dd >= 0.97f ? "In, all of it" : $"In, the far {dd * 100:0}% of it")}. {seen} Keep it moving.".Trim();
        });
    }

    private void LiftOut(CraftPiece p)
    {
        if (p == null || p.location != CraftLocation.Quench) return;
        string id = p.id;
        sent = CraftQuenchMotion.Still; // out of the tank: how it was moved no longer matters
        moves.Clear();
        Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Air }, (ok, why) => { if (ok && ui.SelectedId == id) said = "Lifted out of the tank."; });
    }

    /// <summary>Nobody holds it any more: still in the tank (as the model must know it: the vapour jacket comes back).</summary>
    private void EndMotion()
    {
        CraftPiece p = Selected;
        if (sent != CraftQuenchMotion.Still && p != null && p.location == CraftLocation.Quench)
            Send(new CraftRequest(CraftRequestKind.Agitate, p.id) { i0 = (int)CraftQuenchMotion.Still });
        sent = CraftQuenchMotion.Still;
        moves.Clear();
    }

    private void FillTank()
    {
        if (media.Count == 0 || medium == null) return;
        string id = media[Mathf.Clamp(medium.value, 0, media.Count - 1)];
        CraftQuenchant q = Rules.Quenchant(id);
        if (q == null) { said = "That quenchant is not known here."; return; }
        foreach (CraftPiece p in Ws.Pieces)
            if (p.location == CraftLocation.Quench) { said = "Lift the piece out of the tank first."; return; }
        if (Have(id) == 0) { said = $"You have no {ItemOf(id)} to fill it with."; return; }
        bool same = string.Equals(id, Ws.Quench.medium, StringComparison.OrdinalIgnoreCase);
        if (!Confirm("fill:" + id, same ? $"Click again to empty the tank and fill it with fresh {q.Name.ToLowerInvariant()}." : $"Click again to empty the tank and fill it with {q.Name.ToLowerInvariant()}."))
            return;
        Send(new CraftRequest(CraftRequestKind.FillTank) { id = id }, (ok, why) => { if (ok) said = $"The tank is filled with {q.Name.ToLowerInvariant()}: fresh and cool."; });
    }

    // ------------------------------------------------------------------ clay
    private void ToggleClay()
    {
        CraftPiece p = Selected;
        if (painting)
        {
            FinishClay();
            return;
        }
        if (p == null || p.kind != CraftPieceKind.Metal) { said = "Pick a blade first."; return; }
        if (p.location != CraftLocation.Air) { said = "Clay goes on with the piece on the bench."; return; }
        if (p.MaxTemperature > 200) { said = "Let it cool first: clay goes on a cold blade (it is dried and then heated with it)."; return; }
        // Without clay in the bag the brush still wipes off what is on: only clay that goes on is used.
        bool on = false;
        foreach (MetalSegment s in p.segments) on |= s.clay > 0.05;
        if (!HaveClay && !on) { said = $"You have no {ItemOf(ClayId)}."; return; }
        painting = true;
        wiping = !HaveClay;
        clayFor = p.id;
        clayCoat.Reset(p.segments.Count);
        for (int i = 0; i < p.segments.Count; i++) clayCoat.Set(i, (float)p.segments[i].clay);
        said = wiping ? $"You have no {ItemOf(ClayId)}: the brush can only wipe clay off." : "";
        ShowClay();
    }

    private string ClayId => Rules.FirstSupply(CraftSupplyKind.Clay)?.id;
    /// <summary>Is there clay to paint on (or none is needed: no clay supply in the rules)?</summary>
    private bool HaveClay => ClayId == null || Have(ClayId) != 0;

    /// <summary>Wipe off / paint: the brush takes clay away, or lays it on (only with clay in the bag).</summary>
    private void ToggleWipe()
    {
        if (wiping && !HaveClay) { said = $"You have no {ItemOf(ClayId)}: the brush can only wipe clay off."; return; }
        wiping = !wiping;
        ShowClay();
    }

    /// <summary>The brush put down: the coat as painted goes on (a stretch wiped bare has none); refused, the brush stays in hand.</summary>
    private void FinishClay()
    {
        if (!painting)
            return;
        painting = false;
        wiping = false;
        ShowClay();
        string id = clayFor;
        clayFor = null;
        CraftPiece p = Ws.Piece(id);
        if (p == null || clayCoat.Count != p.segments.Count)
            return;
        List<float> coat = clayCoat.ToList();
        bool changed = false;
        for (int i = 0; i < coat.Count; i++) changed |= Mathf.Abs(coat[i] - (float)p.segments[i].clay) > 0.01f;
        if (!changed) { said = "The clay is as it was."; return; }
        Send(new CraftRequest(CraftRequestKind.Clay, id) { i0 = 0, i1 = coat.Count - 1, a = clay != null ? clay.value : 2f, points = coat }, (ok, why) =>
        {
            if (ok) { said = $"{ClayWords(Ws.Piece(id))} Let it dry, then heat it and quench: the bare edge hardens, the clayed spine stays tough."; return; }
            if (why != CraftAnswerWaits.NoAnswer && ui.SelectedId == id && Ws.Piece(id) is CraftPiece back && back.segments.Count == coat.Count)
            {
                // A real refusal: the brush is still in hand, the coat as it was painted.
                painting = true;
                clayFor = id;
                clayCoat.Reset(coat.Count);
                for (int i = 0; i < coat.Count; i++) clayCoat.Set(i, coat[i]);
                ShowClay();
            }
        });
    }

    private void QuickClay()
    {
        CraftPiece p = Selected;
        if (p == null) return;
        int a = clayFrom >= 0 ? Mathf.Min(clayFrom, clayTo) : 0, b = clayFrom >= 0 ? Mathf.Max(clayFrom, clayTo) : p.segments.Count - 1;
        string id = p.id;
        Send(new CraftRequest(CraftRequestKind.Clay, id) { i0 = a, i1 = b, a = clay != null ? clay.value : 2f }, (ok, why) => { if (ok) said = ClayWords(Ws.Piece(id)); });
    }

    /// <summary>The coat in words: how thick, how even, where.</summary>
    private static string ClayWords(CraftPiece p)
    {
        if (p == null) return "";
        double sum = 0, max = 0;
        int on = 0;
        foreach (MetalSegment s in p.segments) { if (s.clay > 0.05) { on++; sum += s.clay; max = Math.Max(max, s.clay); } }
        if (on == 0) return "No clay is on it now.";
        double mean = sum / on;
        string where = on >= p.segments.Count ? "all along the spine" : on * 3 >= p.segments.Count * 2 ? "along most of the spine" : "on part of the spine";
        return $"Clay {where}, about {mean:0.#} mm{(max > mean * 1.6 ? ", thicker in places" : "")}.";
    }

    private void ShowClay()
    {
        TMP_Text t = clayButton != null ? clayButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (t != null) t.text = painting ? "Done claying" : "Paint clay…";
        if (wipeButton != null)
        {
            wipeButton.interactable = painting;
            UITheme.SetSelected(wipeButton, painting && wiping);
        }
    }

    public override void Leave()
    {
        FinishClay(); // the coat goes on the piece it was painted on
        // Raked ash stays on it: buried as far as it was.
        CraftPiece p = Selected;
        if (burying && p != null && buried > 0.05f)
            Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Ashes, a = Mathf.Max(0.05f, buried) });
        burying = false;
        EndMotion();
        press = Press.None;
        hold = Hold.None;
        hand = new Vector2(-1, -1);
        LiveDraw = false;
    }

    public override void SelectionChanging(string from, string to)
    {
        Leave();
        clayFrom = clayTo = -1;
        clayCoat.Reset(0);
        said = "";
        tankHand = new Vector2(-1, -1);
    }

    public override void SelectionChanged(string to)
    {
        CraftPiece p = Ws.Piece(to);
        sent = p != null && p.location == CraftLocation.Quench ? p.motion : CraftQuenchMotion.Still;
    }

    // ------------------------------------------------------------------ drawing
    public override void Draw(CraftPixels c)
    {
        tank = HasTank ? new Rect(40, 20, 380, 180) : Rect.zero;
        ovenBox = HasOven ? new Rect(460, 20, 220, 150) : Rect.zero;
        firebox = HasOven ? new Rect(472, 26, 196, 22) : Rect.zero;
        chamber = HasOven ? new Rect(475, 60, 190, 95) : Rect.zero;
        ashBox = HasAshes ? new Rect(460, 190, 220, 50) : Rect.zero;
        bench = new Rect(20, 262, 680, 120);
        mouth = new Rect(20, 262, 120, 120);
        // The bench.
        c.Rect(bench.xMin, bench.yMin, bench.xMax, bench.yMax, new Color(0.3f, 0.22f, 0.15f, 1f));
        c.Rect(bench.xMin, bench.yMax - 6, bench.xMax, bench.yMax, new Color(0.38f, 0.28f, 0.19f, 1f));
        CraftPiece p = Selected;
        if (HasTank) DrawTank(c);
        if (HasOven) DrawOven(c);
        if (HasAshes)
        {
            c.Rect(ashBox.xMin, ashBox.yMin, ashBox.xMax, ashBox.yMax, new Color(0.5f, 0.48f, 0.45f));
            // The ashes' name says what the hands do there while they do it (the bar beside it: how far along).
            string ashes = hold == Hold.DigOut ? "DIGGING IT OUT" : burying ? "COVERED" : "ASHES";
            CraftArt.Label(c, ashBox.xMin + 4, ashBox.yMax + 4, ashes, hold == Hold.DigOut || burying ? CraftArt.LabelInk : CraftArt.LabelDim);
        }
        if (hold != Hold.None)
        {
            // How long the hold still has to go: under the firebox (its name says which), beside the oven's and the ashes' name.
            Rect r = hold == Hold.Firebox ? new Rect(firebox.xMin, firebox.yMax + 3, firebox.width, 5)
                : hold == Hold.OvenDoor ? AfterLabel(ovenBox.xMin + 4, ovenBox.yMax - 12, "TAKING IT OUT", chamber.xMax)
                : AfterLabel(ashBox.xMin + 4, ashBox.yMax + 4, "DIGGING IT OUT", ashBox.xMax);
            Meter(c, r.xMin, r.yMin, r.width, r.height, holdTime / HoldSeconds(hold), Mark);
        }
        if (!IsMetal(p))
            return;
        // One scale for the bench, the hands and the tank: hanging upright from the tongs (190 px at 70°: 178 px) it fits
        // between the tank's floor and its surface (180 px).
        mmT = Mathf.Max(0.05f, Mathf.Max((float)(p.Length / 190.0), MaxWidth(p) / 50f));
        float len = (float)(p.Length / mmT);
        // The forge's fire, at the bench's end: a piece in it is shown there, and can be dragged out of it.
        if (p.location == CraftLocation.Fire && press != Press.Drag)
        {
            c.Rect(mouth.xMin, mouth.yMin, mouth.xMax, mouth.yMax, new Color(0.12f, 0.08f, 0.06f, 1f));
            Color g = CraftArt.Heat(Ws.Forge.temperature, ui.Dark, out float k);
            c.Halo(mouth.center.x, mouth.yMin + 20, 70, g, k * 0.5f);
            CraftArt.Label(c, mouth.xMin + 4, mouth.yMax + 4, "IN THE FIRE", Mark);
        }
        switch (p.location)
        {
            case CraftLocation.Air:
            case CraftLocation.Fire:
                if (press == Press.Drag && hand.x >= 0)
                    DrawHeld(c, p, len);
                else if (burying)
                    DrawInAshes(c, p, buried);
                else
                {
                    float bx = p.location == CraftLocation.Fire ? mouth.xMin + 10 : bench.center.x - len * 0.5f;
                    DrawLying(c, p, new Vector2(bx, 322f), len);
                }
                break;
            case CraftLocation.Quench:
                DrawInTank(c, p, len);
                break;
            case CraftLocation.Oven:
                if (HasOven)
                {
                    float om = Mathf.Max(mmT, Mathf.Max((float)(p.Length / (chamber.width - 16)), MaxWidth(p) / 40f));
                    CraftArt.DrawTop(c, Rules, p, new Rect(chamber.xMin + 8, chamber.center.y - 45, (float)(p.Length / om), 90), om, null, p.faceUp == 1, ui.Dark);
                }
                break;
            case CraftLocation.Ashes:
                DrawInAshes(c, p, Mathf.Clamp01((float)p.depth));
                break;
        }
        if (QuickBench && clayFrom >= 0 && p.location == CraftLocation.Air)
        {
            float bx = bench.center.x - len * 0.5f;
            c.Rect(bx + (float)(SegStart(p, Mathf.Min(clayFrom, clayTo)) / mmT), 352, bx + (float)(SegStart(p, Mathf.Max(clayFrom, clayTo) + 1) / mmT), 358, Clay);
        }
    }

    private static float MaxWidth(CraftPiece p)
    {
        double w = 1;
        foreach (MetalSegment s in p.segments) w = Math.Max(w, s.width);
        return (float)w;
    }

    private void DrawTank(CraftPixels c)
    {
        CraftQuenchant m = Rules.Quenchant(Ws.Quench.medium);
        c.Rect(tank.xMin - 8, tank.yMin - 8, tank.xMax + 8, Surface + 6, new Color(0.3f, 0.22f, 0.15f));
        c.Rect(tank.xMin, tank.yMin, tank.xMax, Surface, MediumColor(m));
        c.Rect(tank.xMin, Surface - 2, tank.xMax, Surface, new Color(1f, 1f, 1f, 0.25f));
        CraftArt.Label(c, tank.xMin + 6, tank.yMin + 6, CraftArt.LabelFit((m?.Name ?? Ws.Quench.medium).ToUpperInvariant(), tank.width - 12), new Color(1f, 1f, 1f, 0.55f));
    }

    private static Color MediumColor(CraftQuenchant m) => m != null && m.id.Contains("oil") ? new Color(0.45f, 0.35f, 0.12f, 0.92f)
        : m != null && m.id.Contains("blood") ? new Color(0.5f, 0.05f, 0.05f, 0.92f) : m != null && m.id.Contains("frost") ? new Color(0.55f, 0.75f, 0.85f, 0.9f) : Water;

    private void DrawOven(CraftPixels c)
    {
        c.Rect(ovenBox.xMin, ovenBox.yMin, ovenBox.xMax, ovenBox.yMax, Iron);
        float warm = Mathf.Clamp01((float)((Ws.Oven.temperature - Ws.Ambient) / 600.0));
        c.Rect(chamber.xMin, chamber.yMin, chamber.xMax, chamber.yMax, Color.Lerp(new Color(0.08f, 0.08f, 0.09f), new Color(0.45f, 0.18f, 0.08f), warm));
        bool lit = Ws.Oven.setpoint > 0;
        c.Rect(firebox.xMin, firebox.yMin, firebox.xMax, firebox.yMax, lit ? new Color(0.9f, 0.45f, 0.12f) : new Color(0.12f, 0.1f, 0.09f));
        if (lit)
            for (int i = 0; i < 8; i++)
                c.Line(firebox.xMin + 12 + i * 24, firebox.yMax, firebox.xMin + 14 + i * 24 + 4 * Mathf.Sin(Time.unscaledTime * 6 + i), firebox.yMax + 8 + 4 * Mathf.Sin(Time.unscaledTime * 5 + i), new Color(1f, 0.6f, 0.2f, 0.7f), 4f);
        // The firebox is held to kindle the oven or rake it out: its name says which while the hold runs (the bar under it).
        if (hold == Hold.Firebox) CraftArt.Label(c, firebox.xMin + 6, firebox.yMin + 6, lit ? "RAKING IT OUT" : "KINDLING", CraftArt.LabelInk);
        else CraftArt.Label(c, firebox.xMin + 6, firebox.yMin + 6, "FIREBOX", CraftArt.LabelDim);
        // So does the oven's, while the door is held to take the piece out.
        if (hold == Hold.OvenDoor) CraftArt.Label(c, ovenBox.xMin + 4, ovenBox.yMax - 13, "TAKING IT OUT", CraftArt.LabelInk);
        else CraftArt.Label(c, ovenBox.xMin + 4, ovenBox.yMax - 13, "OVEN", CraftArt.LabelDim);
    }

    /// <summary>A bar 6 px high beside the label <paramref name="text"/> drawn at (<paramref name="x"/>, <paramref name="y"/>), to <paramref name="right"/>.</summary>
    private static Rect AfterLabel(float x, float y, string text, float right)
    {
        float from = x + CraftArt.LabelWidth(text) + 6f;
        return new Rect(from, y, Mathf.Max(20f, right - from), 6f);
    }

    /// <summary>A piece lying flat (on the bench, at the fire's mouth), the clay on its spine.</summary>
    private void DrawLying(CraftPixels c, CraftPiece p, Vector2 at, float len)
    {
        float[] s = CraftArt.DrawTop(c, Rules, p, new Rect(at.x, at.y - 45, len, 90), mmT, null, p.faceUp == 1, ui.Dark);
        Vector2 dir = Vector2.right;
        DrawClayAlong(c, p, at, dir, mmT, p.id == clayFor && painting ? clayCoat : null);
        if (painting && p.id == clayFor && s.Length == p.segments.Count + 1)
            CraftArt.Label(c, at.x, at.y + MaxWidth(p) / mmT * 0.5f + 8, wiping ? "WIPING" : "PAINTING", CraftArt.LabelInk);
    }

    /// <summary>
    /// Clay on the spine half of a piece drawn along <paramref name="dir"/> from <paramref name="start"/>: the darker and
    /// fuller the thicker (the coat being painted, when given).
    /// </summary>
    private void DrawClayAlong(CraftPixels c, CraftPiece p, Vector2 start, Vector2 dir, float mmPx, CraftCoverage coat)
    {
        Vector2 side = new Vector2(-dir.y, dir.x) * (p.faceUp == 1 ? 1f : -1f); // the spine is away from the edge
        float pos = 0f;
        for (int i = 0; i < p.segments.Count; i++)
        {
            MetalSegment s = p.segments[i];
            float l = (float)s.length / mmPx, w = (float)s.width / mmPx * 0.5f;
            float mmClay = coat != null && coat.Count == p.segments.Count ? coat[i] : (float)s.clay;
            float t = Mathf.Clamp01(mmClay / 5f);
            if (t > 0.02f)
            {
                Vector2 a = start + dir * pos, b = start + dir * (pos + l);
                c.Polygon(new List<Vector2> { a, b, b + side * w * (0.5f + 0.5f * t), a + side * w * (0.5f + 0.5f * t) }, new Color(Clay.r, Clay.g, Clay.b, 0.35f + 0.55f * t));
            }
            pos += l;
        }
    }

    /// <summary>
    /// A piece along a line - held at <paramref name="start"/> by its near end, the far end toward <paramref name="dir"/> -
    /// each segment at its width, glowing at its heat; under the surface of the tank the medium covers it.
    /// </summary>
    private void DrawAlong(CraftPixels c, CraftPiece p, Vector2 start, Vector2 dir, bool overTank)
    {
        Vector2 n = new Vector2(-dir.y, dir.x);
        Color metal = CraftArt.MetalColor(Rules, p);
        Color under = MediumColor(Rules.Quenchant(Ws.Quench.medium));
        float pos = 0f;
        foreach (MetalSegment s in p.segments)
        {
            float l = (float)s.length / mmT, w = Mathf.Max(1.5f, (float)s.width / mmT * 0.5f);
            Vector2 a = start + dir * pos, b = start + dir * (pos + l + 0.6f);
            var quad = new List<Vector2> { a - n * w, b - n * w, b + n * w, a + n * w };
            Color col = CraftArt.MetalSurface(metal, s);
            Color h = CraftArt.Heat(Math.Max(s.edge.temp, s.body.temp), ui.Dark, out float g);
            if (g > 0f) col = Color.Lerp(col, h, g);
            if (overTank && (a.y + b.y) * 0.5f < Surface) col = Color.Lerp(col, under, 0.45f);
            col.a = 1f;
            c.Polygon(quad, col);
            pos += l;
        }
        DrawClayAlong(c, p, start, dir, mmT, null);
        c.Line(start.x, start.y, start.x - dir.x * 12f, start.y - dir.y * 12f + 14f, new Color(0.25f, 0.25f, 0.28f), 3f); // the tongs
    }

    private Vector2 EntryDir => Flat ? Vector2.right : new Vector2(Mathf.Cos(-Upright * Mathf.Deg2Rad), Mathf.Sin(-Upright * Mathf.Deg2Rad));

    /// <summary>How much of a piece held at <paramref name="at"/> (its near end) is under the surface (0-1).</summary>
    private float DepthHeldAt(Vector2 at, float len, bool flat)
    {
        if (flat) return at.y < Surface ? 1f : 0f;
        float drop = len * Mathf.Sin(Upright * Mathf.Deg2Rad), farY = at.y - drop;
        return Mathf.Clamp01((Surface - farY) / Mathf.Max(1f, drop));
    }

    /// <summary>How far a piece hanging upright from the tongs reaches below the hand (px; 0 lying flat).</summary>
    private float Hang(CraftPiece p, bool flat) => flat ? 0f : (float)(p.Length / mmT) * Mathf.Sin(Upright * Mathf.Deg2Rad);

    /// <summary>
    /// The highest the hand can hold a piece over the tank and still let it down into it: upright, until its far end is out
    /// of the medium (with the tongs over the bench - a dip of the far end only); flat, down at the surface.
    /// </summary>
    private float TankReach(CraftPiece p, bool flat) => Mathf.Max(bench.yMin, Surface + Hang(p, flat));

    /// <summary>
    /// Is the piece carried at <paramref name="at"/> hanging over the tank? Over the tank itself; and once it has been
    /// brought over it, held higher too (over the bench), as long as its far end still reaches down to the surface - a
    /// piece only picked up from the bench lies on it.
    /// </summary>
    private bool OverTank(CraftPiece p, Vector2 at) => HasTank && at.x >= tank.xMin - 20 && at.x <= tank.xMax
        && (at.y < bench.yMin || hungOverTank && at.y < TankReach(p, Flat));

    /// <summary>
    /// The hand over or in the tank, kept where the piece can hang: inside the tank's width, the far end never through its
    /// floor (it is as deep as it goes there: all of it under).
    /// </summary>
    private Vector2 HandOverTank(CraftPiece p, Vector2 at, bool flat)
    {
        float len = (float)(p.Length / mmT), drop = Hang(p, flat);
        float x = flat ? Mathf.Clamp(at.x, tank.xMin + Mathf.Min(len * 0.5f + 4f, tank.width * 0.5f), tank.xMax - Mathf.Min(len * 0.5f + 4f, tank.width * 0.5f))
            : Mathf.Clamp(at.x, tank.xMin + 10f, tank.xMax - 10f - len * Mathf.Cos(Upright * Mathf.Deg2Rad));
        return new Vector2(x, Mathf.Max(at.y, tank.yMin + (flat ? 6f : drop + 2f)));
    }

    /// <summary>Where the tongs hold a piece left hanging in the tank: as high as its depth (the far end under the surface, as far as it went in).</summary>
    private Vector2 TongsInTank(CraftPiece p)
    {
        bool flat = p.entryAngle >= 0.5;
        return flat ? new Vector2(tank.center.x, Surface - 30f) : new Vector2(tank.xMin + 60f + (tank.width - 140f) * 0.3f, Surface + Hang(p, false) * (1f - (float)p.depth));
    }

    /// <summary>The piece in the hands, being carried: over the tank it hangs as it will go in, and the part that would go under shows.</summary>
    private void DrawHeld(CraftPixels c, CraftPiece p, float len)
    {
        if (OverTank(p, hand))
        {
            Vector2 at = HandOverTank(p, hand, Flat);
            dropDepth = DepthHeldAt(at, len, Flat);
            Vector2 start = Flat ? new Vector2(at.x - len * 0.5f, at.y) : at;
            DrawAlong(c, p, start, EntryDir, true);
            if (dropDepth > 0f)
                CraftArt.Label(c, tank.xMin + 6, tank.yMin + 20, Flat ? "ALL OF IT" : $"{dropDepth * 100:0}% UNDER", Mark);
        }
        else
            DrawAlong(c, p, new Vector2(hand.x - len * 0.5f, hand.y), Vector2.right, false);
    }

    /// <summary>In the tank: hanging from the tongs at the depth it went in (or the hand holds it), the part under the surface in the medium.</summary>
    private void DrawInTank(CraftPixels c, CraftPiece p, float len)
    {
        bool flat = p.entryAngle >= 0.5;
        Vector2 dir = flat ? Vector2.right : EntryUpright;
        if (tankHand.x < 0 || press != Press.Lift)
            tankHand = TongsInTank(p); // where the hand left it
        Vector2 start = flat ? new Vector2(tankHand.x - len * 0.5f, tankHand.y) : tankHand;
        DrawAlong(c, p, start, dir, true);
        // Steam or bubbles off the hot part under the surface.
        double hot = p.MaxTemperature;
        for (int i = 0; i < (int)Math.Min(24, hot / 40); i++)
            c.Disc(tank.xMin + 20 + (i * 47 % (int)(tank.width - 40)), Surface - 6 - (i * 13 % 18), 2 + i % 3, new Color(1f, 1f, 1f, 0.3f));
    }

    private static Vector2 EntryUpright => new Vector2(Mathf.Cos(-Upright * Mathf.Deg2Rad), Mathf.Sin(-Upright * Mathf.Deg2Rad));

    private void DrawInAshes(CraftPixels c, CraftPiece p, float cover)
    {
        if (!HasAshes)
            return;
        float am = Mathf.Max(mmT, Mathf.Max((float)(p.Length / (ashBox.width - 20)), MaxWidth(p) / 30f));
        CraftArt.DrawTop(c, Rules, p, new Rect(ashBox.xMin + 10, ashBox.center.y - 45, (float)(p.Length / am), 90), am, null, p.faceUp == 1, ui.Dark);
        for (int i = 0; i < 40; i++)
            if (i * 0.025f < cover)
                c.Disc(ashBox.xMin + 8 + (i * 47 % 204), ashBox.yMin + 6 + (i * 13 % 36), 6, new Color(0.58f, 0.56f, 0.53f));
        if (burying)
        {
            Rect r = AfterLabel(ashBox.xMin + 4, ashBox.yMax + 4, "COVERED", ashBox.xMax); // beside the ashes' name, which says so
            Meter(c, r.xMin, r.yMin, r.width, r.height, cover, new Color(0.75f, 0.73f, 0.7f));
        }
    }

    private static double SegStart(CraftPiece p, int i)
    {
        double pos = 0;
        for (int k = 0; k < i && k < p.segments.Count; k++) pos += p.segments[k].length;
        return pos;
    }

    private static float HoldSeconds(Hold h) => h == Hold.Firebox ? 1.5f : h == Hold.DigOut ? 1f : 0.6f;

    /// <summary>Is <paramref name="pos"/> on the piece as it lies on the bench (or at the fire's mouth)?</summary>
    private bool OnLyingPiece(Vector2 pos, CraftPiece p, out int seg)
    {
        seg = -1;
        float len = (float)(p.Length / mmT), bx = p.location == CraftLocation.Fire ? mouth.xMin + 10 : bench.center.x - len * 0.5f, hw = MaxWidth(p) / mmT * 0.5f + 6f;
        if (pos.x < bx || pos.x > bx + len || Mathf.Abs(pos.y - 322f) > hw)
            return false;
        seg = Mathf.Clamp(SegmentAtPixel(p, pos.x, bx, mmT), 0, p.segments.Count - 1);
        return true;
    }

    // ------------------------------------------------------------------ the pointer
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Selected;
        if (phase == CraftPointer.Cancel)
        {
            // The hands let go of what they do, nothing more: the piece carried stays where it was, raking stops (it is
            // still on the bench), a hold is let go, the brush is put down (its coat stays on).
            if (press == Press.Drag) said = "You put it back down.";
            else if (burying) { burying = false; said = "You leave it on the bench."; }
            else if (press == Press.Lift) { EndMotion(); said = "You let go: it hangs still in the tank."; }
            else if (painting) FinishClay();
            press = Press.None;
            hold = Hold.None;
            hand = new Vector2(-1, -1);
            return;
        }
        if (phase == CraftPointer.Scroll || phase == CraftPointer.Move)
            return;
        if (phase == CraftPointer.Down)
        {
            press = Press.None;
            Down(pos, p);
            return;
        }
        if (phase == CraftPointer.Drag)
        {
            switch (press)
            {
                case Press.Drag:
                    hand = pos;
                    if (p != null && HasTank && pos.x >= tank.xMin - 20 && pos.x <= tank.xMax && pos.y < bench.yMin) hungOverTank = true;
                    break;
                case Press.Lift:
                    // The piece moves as the hand moves, from where it was taken hold of.
                    if (p != null) tankHand = HandOverTank(p, pos + grab, p.entryAngle >= 0.5);
                    moves.Add((pos, ui.PointerTime));
                    break;
                case Press.Rake:
                    if (ashBox.Contains(pos)) rake.Add(pos.x, ui.PointerTime);
                    buried = Mathf.Clamp01(rake.Shakes / 8f);
                    break;
                case Press.Quick:
                    if (p != null && clayFrom >= 0) clayTo = Mathf.Clamp(SegmentAtPixel(p, pos.x, bench.center.x - (float)(p.Length / mmT) * 0.5f, mmT), 0, p.segments.Count - 1);
                    break;
            }
            return;
        }
        // Up.
        Press was = press;
        press = Press.None;
        hold = Hold.None;
        if (p == null)
            return;
        switch (was)
        {
            case Press.Drag: Drop(p, pos); break;
            case Press.Lift:
            {
                float len = (float)(p.Length / mmT);
                if (DepthHeldAt(tankHand, len, p.entryAngle >= 0.5) < 0.02f && !QuickBench) LiftOut(p);
                else EndMotion();
                break;
            }
            case Press.Rake:
                if (burying && buried > 0.05f)
                {
                    string id = p.id;
                    float b = Mathf.Max(0.05f, buried);
                    burying = false;
                    Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Ashes, a = b }, (ok, why) =>
                    {
                        if (ok && ui.SelectedId == id) said = b >= 0.95f ? "Buried deep in the ashes: it will cool very slowly (annealed soft)." : "Partly buried: it cools faster than it would deep in the ashes.";
                    });
                }
                break;
        }
        hand = new Vector2(-1, -1);
    }

    private void Down(Vector2 pos, CraftPiece p)
    {
        if (HasOven && firebox.Contains(pos) && !QuickBench) { press = Press.Hold; hold = Hold.Firebox; holdTime = 0f; return; }
        if (!IsMetal(p))
            return;
        if (QuickBench)
        {
            // The test bench: clicks do it at once.
            press = Press.Quick;
            if (HasTank && tank.Contains(pos)) { Plunge(depth != null ? depth.value : 1f); return; }
            if (HasOven && ovenBox.Contains(pos)) { Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Oven }); return; }
            if (HasAshes && ashBox.Contains(pos)) { Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Ashes, a = 1f }); return; }
            if (p.location == CraftLocation.Air && OnLyingPiece(pos, p, out int seg)) clayFrom = clayTo = seg;
            return;
        }
        switch (p.location)
        {
            case CraftLocation.Oven:
                if (ovenBox.Contains(pos)) { press = Press.Hold; hold = Hold.OvenDoor; holdTime = 0f; }
                return;
            case CraftLocation.Ashes:
                if (ashBox.Contains(pos)) { press = Press.Hold; hold = Hold.DigOut; holdTime = 0f; }
                return;
            case CraftLocation.Quench:
            {
                // Taking hold of it where it hangs - on the piece over the tank, or its tongs, over the bench too - moves
                // nothing: only the hand's movement does.
                Vector2 tongs = TongsInTank(p);
                if (pos.x >= tank.xMin - 10 && pos.x <= tank.xMax + 10 && pos.y < Mathf.Max(bench.yMin, tongs.y + 24f))
                {
                    press = Press.Lift;
                    grab = tongs - pos;
                    tankHand = tongs;
                    moves.Clear();
                    moves.Add((pos, ui.PointerTime));
                }
                return;
            }
            case CraftLocation.Air:
            case CraftLocation.Fire:
                break;
            default:
                said = $"It is {CraftStationUI.LocationText(p)}: not here.";
                return;
        }
        if (burying)
        {
            if (ashBox.Contains(pos)) { press = Press.Rake; rake.Add(pos.x, ui.PointerTime); }
            return;
        }
        if (painting && p.id == clayFor)
        {
            if (p.location == CraftLocation.Air && OnLyingPiece(pos, p, out _)) press = Press.Clay;
            return;
        }
        if (OnLyingPiece(pos, p, out _))
        {
            press = Press.Drag;
            hand = pos;
            hungOverTank = false;
        }
    }

    /// <summary>Let go of the piece carried: into the tank (as deep as it hung), the oven, onto the ashes - or back on the bench.</summary>
    private void Drop(CraftPiece p, Vector2 pos)
    {
        float len = (float)(p.Length / mmT);
        if (OverTank(p, pos))
        {
            float d = DepthHeldAt(HandOverTank(p, pos, Flat), len, Flat);
            if (d >= 0.05f) Plunge(d);
            else said = "Let it down lower: it did not reach the surface.";
            return;
        }
        if (HasOven && ovenBox.Contains(pos))
        {
            string id = p.id;
            Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Oven }, (ok, why) => { if (ok && ui.SelectedId == id) said = OvenWords(); });
            return;
        }
        if (HasAshes && ashBox.Contains(pos))
        {
            burying = true;
            buried = 0f;
            rake.Clear();
            said = "Rake the ash over it: drag back and forth over the ash box.";
            return;
        }
        if (p.location == CraftLocation.Fire)
        {
            string id = p.id;
            Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Air }, (ok, why) => { if (ok && ui.SelectedId == id) said = "Out of the fire, on the bench."; });
        }
    }

    /// <summary>The motion of the hand in the tank, from where it went over the last moments; the holds.</summary>
    public override void Tick(float dt)
    {
        CraftPiece p = Selected;
        if (hold != Hold.None)
        {
            if (!ui.PointerHeld) hold = Hold.None;
            else if ((holdTime += dt) >= HoldSeconds(hold))
            {
                Hold h = hold;
                hold = Hold.None;
                switch (h)
                {
                    case Hold.Firebox:
                        bool lit = Ws.Oven.setpoint > 0;
                        float to = ovenDial;
                        Send(new CraftRequest(CraftRequestKind.Oven) { a = lit ? 0f : to });
                        said = lit ? "The fire is raked out of the oven." : $"The oven is kindled: set to {to:0} °C.";
                        break;
                    case Hold.OvenDoor:
                    case Hold.DigOut:
                        if (p != null) Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Air }, (ok, why) => { if (ok) said = h == Hold.DigOut ? "Dug out of the ashes." : "Out of the oven."; });
                        break;
                }
            }
        }
        // The brush: clay goes on (or comes off) where it is held over the piece, as long as it is held there.
        if (press == Press.Clay && painting && p != null && p.id == clayFor && ui.PointerHeld && OnLyingPiece(ui.Hover, p, out int seg))
        {
            if (!wiping && !HaveClay) { wiping = true; ShowClay(); said = $"You have no {ItemOf(ClayId)}: the brush can only wipe clay off."; }
            if (wiping) clayCoat.Wipe(seg, dt * 4f);
            else clayCoat.Paint(seg, dt * 1.2f * (clay != null ? clay.value : 2f), clay != null ? clay.value : 2f);
        }
        LiveDraw = press == Press.Drag || press == Press.Lift || press == Press.Rake || press == Press.Clay || hold != Hold.None;
        if (p == null || p.location != CraftLocation.Quench)
        {
            moves.Clear();
            return;
        }
        motionTimer += dt;
        if (motionTimer < 0.3f)
            return;
        motionTimer = 0f;
        CraftQuenchMotion m = CraftQuenchMotion.Still;
        // Only the last moment's moves: up and down, side to side, round, or held still.
        float now = Time.unscaledTime;
        moves.RemoveAll(x => now - x.t > 0.6f);
        if (press == Press.Lift && moves.Count >= 4 && ui.PointerHeld)
        {
            float dx = 0, dy = 0, turn = 0;
            for (int i = 1; i < moves.Count; i++)
            {
                Vector2 d = moves[i].at - moves[i - 1].at;
                dx += Mathf.Abs(d.x);
                dy += Mathf.Abs(d.y);
                if (i > 1) turn += Vector3.Cross(moves[i - 1].at - moves[i - 2].at, d).z;
            }
            if (dx + dy > 20)
                m = Mathf.Abs(turn) > (dx + dy) * 6 ? CraftQuenchMotion.Swirl : dy > dx * 1.5f ? CraftQuenchMotion.UpDown : dx > dy * 1.5f ? CraftQuenchMotion.SideToSide : CraftQuenchMotion.Swirl;
        }
        // How deep the hand holds it now (moving it up and down changes it a little; lifted high, only the tip stays in).
        float len = (float)(p.Length / mmT);
        float held = press == Press.Lift ? DepthHeldAt(tankHand, len, p.entryAngle >= 0.5) : (float)p.depth;
        bool deeper = press == Press.Lift && held >= 0.02f && Mathf.Abs(held - (float)p.depth) > 0.1f;
        if (m != sent || deeper)
        {
            sent = m;
            Send(new CraftRequest(CraftRequestKind.Agitate, p.id) { i0 = (int)m, a = deeper ? Mathf.Max(0.02f, held) : -1f });
        }
    }

    private string OvenWords()
    {
        bool lit = Ws.Oven.setpoint > Ws.Ambient;
        if (!lit) return "The oven is cold: hold on its firebox to kindle it.";
        double t = Ws.Oven.temperature, set = Ws.Oven.setpoint;
        string state = Gauge ? $"it reads {CraftText.Celsius(t)}" : Math.Abs(t - set) < 15 ? "at its heat" : t < set ? "still warming" : "cooling to it";
        return $"Tempering in the oven, set to {set:0} °C ({state}).";
    }

    private static string MotionWords(CraftQuenchMotion m) => m switch
    {
        CraftQuenchMotion.UpDown => "moving it up and down - good",
        CraftQuenchMotion.Swirl => "moving it round - good",
        CraftQuenchMotion.SideToSide => "moving it side to side - it warps!",
        _ => "held still - a vapour jacket clings to it",
    };

    public override void Refresh()
    {
        RefreshCounts(medium, media);
        ShowClay();
        if (info == null)
            return;
        CraftPiece p = Selected;
        Forget(ref said, ref saidSeen, ref saidWhere, p);
        string how;
        if (!IsMetal(p)) how = "Pick a hot piece to quench, or a blade to clay.";
        else if (painting)
            how = $"<b>{(wiping ? "Wiping clay off" : "Painting clay")}</b>: hold and move along the spine (thicker where you go over it more, up to {(clay != null ? clay.value : 2f):0.#} mm){(wiping ? "" : "; Wipe off takes it away where it should not be")}. The edge is left bare. <i>Done claying</i> when finished.";
        else if (burying) how = $"<b>Burying</b>: drag back and forth over the ash box to rake ash over it ({buried * 100:0}% covered), then let go.";
        else if (p.location == CraftLocation.Quench)
            how = $"In the tank, {(p.entryAngle >= 0.5 ? "flat" : p.depth >= 0.97 ? "all of it" : $"the far {p.depth * 100:0}% of it")}: {MotionWords(sent)}. Hold it and keep it moving up and down along it, or round; side to side warps it; held still it stays soft. Drag it up out of the tank to lift it out.";
        else if (p.location == CraftLocation.Oven) how = $"{OvenWords()} Hold on the oven to take it out.";
        else if (p.location == CraftLocation.Ashes) how = "Cooling slowly in the ashes. Hold on the ashes to dig it out.";
        else if (p.location == CraftLocation.Air || p.location == CraftLocation.Fire)
        {
            var where = new List<string>();
            if (HasTank) where.Add("down into the tank (how low you let go is how much goes under - over the tank, raise it again to dip only the far end)");
            if (HasOven) where.Add("into the oven");
            if (HasAshes) where.Add("onto the ash box");
            string from = p.location == CraftLocation.Fire ? "Drag it from the fire " : "Drag it ";
            string oven = HasOven ? $" Hold on the oven's firebox to {(Ws.Oven.setpoint > 0 ? "rake its fire out" : "kindle it")}." : "";
            how = QuickBench ? "Click the tank, the oven or the ash box." : $"{from}{string.Join(", ", where)}.{oven}";
            // A blade of hardening steel still to harden, and a harsh quenchant in the tank (water, brine): it cracks - said first.
            string harsh = HasTank ? CraftLook.HarshQuench(Rules, p, Rules.Quenchant(Ws.Quench.medium), Station.Quenchants) : "";
            if (harsh.Length > 0) how = $"<color=#FFB070>{harsh}</color> {how}";
        }
        else how = $"It is {CraftStationUI.LocationText(p)}.";
        info.text = (how + " " + said).Trim();
    }
}
