using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The forge: feed the fire, work the bellows (each pump a puff; a steady rhythm builds the draft, frantic pumping only
/// tires the arm), open or choke the air, and put the work in the fire - all of it, or only the part dragged on - deep
/// in the coals or near the edge. The coals glow with the fire's heat; the metal glows with its own.
/// <para>Everything is done with the hands: shovel fuel from the heap onto the fire (hold on the heap), sprinkle flux
/// along the hot bar (hold and move along it - every part it misses scales and will not weld), fold the bar (drag its
/// far end back over it) and set the weld with the hammer along the whole seam while it is at welding heat, or stack a
/// second hot bar on it and set that weld the same way. A test station has quick buttons for all of it.</para>
/// </summary>
public sealed class CraftForgeMode : CraftMode
{
    private enum Work { None, Flux, Fold, Weld }

    private readonly CraftBellowsPump pump = new CraftBellowsPump();
    private readonly CraftHammer hammer = new CraftHammer();
    private readonly CraftCoverage coverage = new CraftCoverage();
    private Slider depth, damper;
    private TMP_Dropdown fuel, flux;
    private TextMeshProUGUI info;
    private readonly List<string> fuels = new List<string>(), fluxes = new List<string>();
    private int spanFrom = -1, spanTo = -1, dragStart = -1;
    private float x0, mm, len, pieceY;
    private Rect bellowsArea, hearth, heap;
    private float puffShown, shovel = -1f;
    private Work work;
    private float foldDrag, foldFrom = -1f;
    private bool setting;
    private readonly float[] zones = new float[6];
    private int blows;
    private string partner;
    private readonly List<(string id, Rect at)> partners = new List<(string, Rect)>();
    private string said = "";

    public override string Title => "Forge";
    public override bool Available(CraftStation s) => s.Has(CraftTools.Forge);
    public override string Hint => "Pump the bellows in a steady rhythm; hold on the coal heap to shovel fuel; drag along the piece to choose what goes in, then click the coals." + ByHandHint("work the bellows by hand");
    public override CraftHandsOnMode HandsOnMode => CraftHandsOnMode.Bellows;

    protected override void FillHandsOn(ref CraftHandsOnSetup setup)
    {
        setup.fuel = fuels.Count > 0 ? fuels[Mathf.Clamp(fuel.value, 0, fuels.Count - 1)] : null;
        setup.depth = depth != null ? depth.value : 0.7f;
    }

    public override void Build(RectTransform controls)
    {
        RectTransform r1 = Row(controls);
        fuel = FuelDrop(r1, fuels, 180f);
        if (QuickBench)
        {
            Btn(r1, "Add fuel", () => { AddFuel(1); }, 110f);
            Btn(r1, "+3", () => { AddFuel(3); }, 50f);
        }
        damper = Slide(r1, "Air vent", 0f, 1f, (float)Ws.Forge.damper);
        damper.onValueChanged.AddListener(v => Send(new CraftRequest(CraftRequestKind.Damper) { a = v }));
        RectTransform r2 = Row(controls);
        depth = Slide(r2, "Depth", 0.2f, 1f, 0.7f);
        if (QuickBench)
        {
            Btn(r2, "Into the fire", PutIn, 130f, UITheme.ButtonStyle.Primary);
            Btn(r2, "Out", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Air }), 70f);
        }
        Btn(r2, "Whole piece", () => { spanFrom = spanTo = -1; }, 110f, UITheme.ButtonStyle.Ghost);
        HandsOnButton(r2, "Bellows by hand");
        RectTransform r3 = Row(controls);
        fluxes.Clear();
        var fn = new List<string>();
        foreach (CraftSupplyDefinition s in Rules.Supplies)
            if (s.kind == CraftSupplyKind.Flux && s.id != "arcane_flux") { fluxes.Add(s.id); fn.Add(s.Name); }
        flux = Drop(r3, fn.Count > 0 ? fn : new List<string> { "-" }, 0, 140f);
        Btn(r3, QuickBench ? "Flux it" : "Sprinkle flux…", () => Begin(Work.Flux), 130f);
        Btn(r3, QuickBench ? "Fold and weld" : "Fold and weld…", () => Begin(Work.Fold), 140f);
        Btn(r3, QuickBench ? "Weld to…" : "Weld on a bar…", () => Begin(Work.Weld), 130f);
        if (QuickBench)
            Btn(r3, "Into the ashes", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Ashes }), 130f);
        info = InfoRow(controls);
    }

    private string FluxId => fluxes.Count > 0 ? fluxes[Mathf.Clamp(flux.value, 0, fluxes.Count - 1)] : Rules.FirstSupply(CraftSupplyKind.Flux)?.id ?? "borax";

    private bool AddFuel(int n)
    {
        if (fuels.Count == 0) return false;
        string id = fuels[Mathf.Clamp(fuel.value, 0, fuels.Count - 1)];
        if (Have(id) == 0)
        {
            CraftFuel f = Rules.Fuel(id);
            ui.Message(f != null && f.burnsAnyWood ? $"You have no {f.Name}: any wood burns as it (planks, logs, offcuts)." : $"You have no {f?.Name ?? id}: pick a fuel you have.");
            shovel = -1f;
            return false;
        }
        Send(new CraftRequest(CraftRequestKind.AddFuel) { id = fuels[Mathf.Clamp(fuel.value, 0, fuels.Count - 1)], count = n });
        return true;
    }

    private void PutIn()
    {
        CraftPiece p = Selected;
        if (p == null) { ui.Message("Pick a piece first."); return; }
        var r = new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Fire, a = depth.value };
        if (spanFrom >= 0) { r.i0 = Mathf.Min(spanFrom, spanTo); r.i1 = Mathf.Max(spanFrom, spanTo); }
        Send(r);
    }

    // ------------------------------------------------------------------ the work of the hands
    private void Begin(Work w)
    {
        CraftPiece p = Selected;
        if (p == null || p.kind != CraftPieceKind.Metal) { ui.Message("Pick a metal piece first."); return; }
        if (QuickBench)
        {
            switch (w)
            {
                case Work.Flux: Send(new CraftRequest(CraftRequestKind.Flux, p.id) { id = FluxId, i0 = 2, count = 1, a = 1f }); break;
                case Work.Fold: Send(new CraftRequest(CraftRequestKind.Fold, p.id) { b = 1f, count = CraftRequestGuard.MinWeldBlows }); break;
                case Work.Weld:
                    foreach (CraftPiece q in Ws.Pieces)
                        if (q != p && q.kind == CraftPieceKind.Metal && q.location == CraftLocation.Air && q.MaxTemperature > 700)
                        {
                            Send(new CraftRequest(CraftRequestKind.Weld, p.id) { other = q.id, b = 1f, count = CraftRequestGuard.MinWeldBlows });
                            return;
                        }
                    ui.Message("Another hot piece must be on the anvil to weld it to.");
                    break;
            }
            return;
        }
        if (w != Work.Flux && p.location != CraftLocation.Air) { ui.Message("Take it out of the fire onto the anvil first."); return; }
        if (w != Work.Flux && !Station.Has(CraftTools.Anvil)) { ui.Message("There is no anvil here."); return; }
        work = w;
        setting = false;
        foldDrag = 0f;
        foldFrom = -1f;
        partner = null;
        blows = 0;
        Array.Clear(zones, 0, zones.Length);
        coverage.Reset(p.segments.Count);
        hammer.Lower();
    }

    private void Stop(string why = null)
    {
        work = Work.None;
        setting = false;
        hammer.Lower();
        if (!string.IsNullOrEmpty(why)) ui.Message(why);
    }

    private void FinishFlux(CraftPiece p)
    {
        float cover = coverage.Mean(0.8f);
        Stop();
        if (cover < 0.05f) return;
        Send(new CraftRequest(CraftRequestKind.Flux, p.id) { id = FluxId, i0 = 2, count = 1, a = cover });
        said = $"Flux on {cover * 100:0}% of it{(cover < 0.8f ? ": the bare parts will scale and not weld" : "")}.";
    }

    private void FinishWeld(CraftPiece p)
    {
        float set = 0f;
        foreach (float z in zones) set += Mathf.Min(1f, z);
        set /= zones.Length;
        Work was = work;
        Stop();
        if (set < 0.05f) return;
        if (was == Work.Fold)
            Send(new CraftRequest(CraftRequestKind.Fold, p.id) { b = set, count = blows });
        else if (partner != null)
            Send(new CraftRequest(CraftRequestKind.Weld, p.id) { other = partner, b = set, count = blows });
        said = $"The weld set along {set * 100:0}% of the seam in {blows} blows.";
    }

    public override void Leave()
    {
        Stop();
        shovel = -1f;
    }

    public override void Tick(float dt)
    {
        pump.Tick(dt);
        hammer.Tick(dt);
        puffShown = Mathf.Max(0f, puffShown - dt * 3f);
        if (shovel >= 0f)
        {
            if (!ui.PointerHeld || !heap.Contains(ui.Hover)) shovel = -1f;
            else if ((shovel += dt) >= 0.6f) { shovel = 0f; if (AddFuel(1)) said = "A shovel of fuel on the fire."; }
        }
        CraftPiece p = Selected;
        if (work == Work.Flux && p != null && ui.PointerHeld && PieceAt(ui.Hover, p, out int seg))
            coverage.Paint(seg, dt * 2.2f);
    }

    // ------------------------------------------------------------------ drawing
    public override void Draw(CraftPixels c)
    {
        CraftFireState f = Ws.Forge;
        hearth = new Rect(200, 30, 470, 120);
        c.Rect(hearth.xMin - 20, hearth.yMin - 20, hearth.xMax + 20, hearth.yMax, new Color(0.18f, 0.15f, 0.13f, 1f));
        Color glow = CraftArt.Heat(f.temperature, out float k);
        for (int i = 0; i < 60; i++)
        {
            float x = hearth.xMin + 8 + (i * 37 % 450), y = hearth.yMin + 10 + (i * 53 % 90);
            Color coal = Color.Lerp(new Color(0.1f, 0.09f, 0.08f), glow, k * (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 3 + i)));
            c.Disc(x, y, 9 + i % 4, coal);
        }
        if (f.level > 0.05)
            for (int i = 0; i < 14; i++)
            {
                float x = hearth.xMin + 20 + i * 32, h = (float)(f.level * 70 * (0.7 + 0.3 * Mathf.Sin(Time.unscaledTime * 7 + i * 1.7f)) * (0.6 + f.air));
                c.Line(x, hearth.yMax, x + 6 * Mathf.Sin(Time.unscaledTime * 5 + i), hearth.yMax + h, new Color(1f, 0.55f, 0.15f, 0.5f), 6f);
            }
        c.Halo(hearth.center.x, hearth.center.y, 260, glow, k * 0.35f);
        // The bellows.
        bellowsArea = new Rect(20, 40, 150, 90);
        float squash = 1f - 0.4f * puffShown;
        c.Polygon(new List<Vector2> { new Vector2(30, 85), new Vector2(150, 85 - 30 * squash), new Vector2(150, 85 + 30 * squash) }, new Color(0.45f, 0.3f, 0.18f));
        c.Rect(150, 80, 200, 90, Iron);
        Meter(c, 30, 30, 120, 8, (float)pump.Strength, new Color(0.6f, 0.8f, 1f));
        // The fuel heap: hold on it to shovel.
        heap = new Rect(20, 150, 150, 70);
        for (int i = 0; i < 26; i++)
            c.Disc(heap.xMin + 12 + (i * 29 % 126), heap.yMin + 8 + (i * 17 % 40) * (1f - (i * 29 % 126) / 260f), 8 + i % 3, new Color(0.12f, 0.11f, 0.1f));
        if (shovel >= 0f) Meter(c, heap.xMin, heap.yMax + 2, heap.width, 6, shovel / 0.6f, new Color(1f, 0.8f, 0.4f));
        // The selected piece, above the fire (sunk in the coals when it is in) or on the anvil.
        CraftPiece p = Selected;
        partners.Clear();
        if (!IsMetal(p))
            return;
        mm = Fit(p, 560, 90);
        len = (float)(p.Length / mm);
        x0 = 200 + (470 - len) * 0.5f;
        pieceY = p.location == CraftLocation.Fire ? hearth.yMin + 30 : 260;
        if (work == Work.Fold || work == Work.Weld)
            c.Rect(x0 - 30, pieceY - 18, x0 + len + 30, pieceY - 4, Iron); // the anvil face under it
        float[] starts = CraftArt.DrawTop(c, Rules, p, new Rect(x0, pieceY, len, 90), mm, null, p.faceUp == 1);
        float cy = pieceY + 45f;
        if (spanFrom >= 0 && p.location != CraftLocation.Fire && work == Work.None)
        {
            float a = SegX(p, Mathf.Min(spanFrom, spanTo)), b = SegX(p, Mathf.Max(spanFrom, spanTo) + 1);
            c.Frame(a, pieceY - 5, b, pieceY + 95, Mark, 2f);
        }
        if (p.location == CraftLocation.Fire)
        {
            float a = SegX(p, Mathf.Clamp(p.from, 0, p.segments.Count - 1)), b = SegX(p, Mathf.Clamp(p.to, 0, p.segments.Count - 1) + 1);
            c.Rect(a, hearth.yMin + 20, b, hearth.yMin + 26, new Color(glow.r, glow.g, glow.b, 0.6f));
        }
        switch (work)
        {
            case Work.Flux:
                // Flux grains where it has been sprinkled.
                for (int i = 0; i < p.segments.Count; i++)
                {
                    float w = (float)p.segments[i].width / mm, cov = Mathf.Clamp01(coverage[i] / 0.8f);
                    int grains = Mathf.RoundToInt(cov * 10);
                    for (int g = 0; g < grains; g++)
                        c.Disc(Mathf.Lerp(starts[i], starts[i + 1], (g * 37 % 10) / 10f), cy + (g * 53 % 11 - 5) / 10f * w, 1.3f, new Color(0.95f, 0.95f, 0.85f, 0.9f));
                }
                if (ui.PointerHeld && PieceAt(ui.Hover, p, out _))
                    for (int g = 0; g < 8; g++)
                        c.Disc(ui.Hover.x + (g * 7 % 9 - 4) * 2f, ui.Hover.y + 10 + g * 3f, 1.2f, new Color(1f, 1f, 0.9f, 0.8f));
                Meter(c, x0, pieceY + 100, len, 8, coverage.Mean(0.8f), new Color(0.95f, 0.95f, 0.8f));
                break;
            case Work.Fold:
                if (!setting)
                {
                    // The far end lifted and brought back over the bar, like turning a page.
                    float mid = x0 + len * 0.5f, end = x0 + len - foldDrag * len;
                    Color metal = Color.Lerp(CraftArt.MetalColor(Rules, p), CraftArt.Heat(p.MeanTemperature, out float g2), g2);
                    c.Rect(Mathf.Min(mid, end), cy - 30, Mathf.Max(mid, end), cy + 30, new Color(metal.r, metal.g, metal.b, 0.85f));
                    c.Line(end, cy - 34, end, cy + 34, Mark, 2f);
                    c.Line(mid, pieceY - 4, mid, pieceY + 94, new Color(1f, 1f, 1f, 0.35f), 1f);
                }
                else
                    DrawSetting(c, p, x0, len * 0.5f, cy, true);
                break;
            case Work.Weld:
                if (partner == null)
                {
                    // The other hot bars on the anvil: click one to stack it on this one.
                    int n = 0;
                    foreach (CraftPiece q in Ws.Pieces)
                    {
                        if (q == p || q.kind != CraftPieceKind.Metal || q.location != CraftLocation.Air) continue;
                        var at = new Rect(20, 232 + n * 44, 170, 40);
                        if (at.yMax > c.Height) break;
                        c.Rect(at.xMin, at.yMin, at.xMax, at.yMax, new Color(0f, 0f, 0f, 0.4f));
                        float qm = Fit(q, 160, 30);
                        CraftArt.DrawTop(c, Rules, q, new Rect(at.xMin + 5, at.yMin + 5, (float)(q.Length / qm), 30), qm);
                        if (at.Contains(ui.Hover)) c.Frame(at.xMin, at.yMin, at.xMax, at.yMax, Mark, 2f);
                        partners.Add((q.id, at));
                        n++;
                    }
                }
                else
                {
                    CraftPiece q = Ws.Piece(partner);
                    if (q != null)
                    {
                        Color qc = Color.Lerp(CraftArt.MetalColor(Rules, q), CraftArt.Heat(q.MeanTemperature, out float g3), g3);
                        c.Rect(x0 + 4, cy - 26, x0 + len - 4, cy + 26, new Color(qc.r, qc.g, qc.b, 0.75f));
                    }
                    DrawSetting(c, p, x0, len, cy, false);
                }
                break;
        }
    }

    /// <summary>The seam being set: a zone per stretch filling with each good blow, the welding heat, the hammer.</summary>
    private void DrawSetting(CraftPixels c, CraftPiece p, float from, float length, float cy, bool fold)
    {
        if (fold) c.Rect(from + length, cy - 46, from + length * 2f, cy + 46, new Color(0f, 0f, 0f, 0.55f)); // folded under
        float zw = length / zones.Length;
        for (int z = 0; z < zones.Length; z++)
        {
            float fill = Mathf.Clamp01(zones[z]);
            c.Rect(from + z * zw + 1, pieceY + 100, from + (z + 1) * zw - 1, pieceY + 110, new Color(0f, 0f, 0f, 0.5f));
            c.Rect(from + z * zw + 1, pieceY + 100, from + z * zw + 1 + (zw - 2) * fill, pieceY + 110, fill >= 1f ? new Color(0.45f, 0.95f, 0.5f) : Mark);
        }
        float q = (float)CraftForging.WeldQuality(Rules, p);
        Meter(c, 20, 386, 170, 8, q, q > 0.75f ? new Color(1f, 0.95f, 0.6f) : q > 0.4f ? new Color(1f, 0.6f, 0.25f) : new Color(0.5f, 0.3f, 0.2f));
        Vector2 h = ui.Hover;
        if (h.x >= from && h.x <= from + length)
        {
            float charge = (float)hammer.Charge;
            c.Rect(h.x - 16, cy + 50 + 30 * charge, h.x + 16, cy + 66 + 30 * charge, new Color(0.4f, 0.4f, 0.44f));
        }
    }

    private float SegX(CraftPiece p, int i)
    {
        double pos = 0;
        for (int k = 0; k < i && k < p.segments.Count; k++) pos += p.segments[k].length;
        return x0 + (float)(pos / mm);
    }

    private bool PieceAt(Vector2 pos, CraftPiece p, out int seg)
    {
        seg = -1;
        if (p == null || pos.x < x0 || pos.x > x0 + len || pos.y < pieceY - 20 || pos.y > pieceY + 110)
            return false;
        seg = Mathf.Clamp(SegmentAtPixel(p, pos.x, x0, mm), 0, p.segments.Count - 1);
        return true;
    }

    // ------------------------------------------------------------------ the pointer
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        if (phase == CraftPointer.Down && bellowsArea.Contains(pos))
        {
            float s = (float)pump.Pump(Hands);
            puffShown = 1f;
            Send(new CraftRequest(CraftRequestKind.Bellows) { a = s });
            Station.GetComponentInChildren<CraftStationVisuals>()?.Puff();
            return;
        }
        if (phase == CraftPointer.Down && heap.Contains(pos) && !QuickBench)
        {
            shovel = 0f;
            return;
        }
        CraftPiece p = Selected;
        if (!IsMetal(p))
            return;
        if (work != Work.None)
        {
            if (right && phase == CraftPointer.Down) { Stop("Put down."); return; }
            WorkPointer(phase, pos, p);
            return;
        }
        if (p.location != CraftLocation.Fire && pos.y > 200)
        {
            int seg = SegmentAtPixel(p, pos.x, x0, mm);
            if (phase == CraftPointer.Down) { dragStart = seg; spanFrom = spanTo = seg; }
            else if (phase == CraftPointer.Drag && dragStart >= 0) { spanFrom = dragStart; spanTo = seg; }
            else if (phase == CraftPointer.Up) dragStart = -1;
            return;
        }
        if (phase == CraftPointer.Down && hearth.Contains(pos))
        {
            if (p.location == CraftLocation.Fire) Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Air });
            else PutIn();
        }
    }

    private void WorkPointer(CraftPointer phase, Vector2 pos, CraftPiece p)
    {
        switch (work)
        {
            case Work.Flux:
                if (phase == CraftPointer.Up) FinishFlux(p);
                return;
            case Work.Fold when !setting:
                if (phase == CraftPointer.Down && PieceAt(pos, p, out _) && pos.x > x0 + len * 0.6f) foldFrom = pos.x;
                else if (phase == CraftPointer.Drag && foldFrom >= 0f) foldDrag = Mathf.Clamp01((x0 + len - pos.x) / Mathf.Max(1f, len)); // the end follows the hand
                else if (phase == CraftPointer.Up && foldFrom >= 0f)
                {
                    foldFrom = -1f;
                    if (foldDrag >= 0.85f) { foldDrag = 1f; setting = true; said = "Folded: now set the weld along the seam with the hammer, while it is at welding heat."; }
                    else foldDrag = 0f; // not brought right over: it springs back
                }
                return;
            case Work.Weld when partner == null:
                if (phase == CraftPointer.Down)
                    foreach ((string id, Rect at) in partners)
                        if (at.Contains(pos)) { partner = id; said = "Stacked: set the weld along it with the hammer, while both are at welding heat."; return; }
                return;
            default:
            {
                // Setting the weld: hold to raise the hammer, release to strike the stretch under the pointer.
                float length = work == Work.Fold ? len * 0.5f : len;
                if (phase == CraftPointer.Down && pos.x >= x0 && pos.x <= x0 + length) hammer.Raise();
                else if (phase == CraftPointer.Up && hammer.Raised)
                {
                    CraftBlow b = hammer.Release(Mathf.Clamp01((pos.x - x0) / Mathf.Max(1f, length)), 0, Hands);
                    float heat = (float)CraftForging.WeldQuality(Rules, p);
                    float worth = (float)b.force * (heat > 0.3f ? Mathf.Clamp01(heat / 0.75f) : 0.15f);
                    int z = Mathf.Clamp((int)(b.position * zones.Length), 0, zones.Length - 1);
                    zones[z] += worth * 0.6f;
                    if (z > 0) zones[z - 1] += worth * 0.2f;
                    if (z < zones.Length - 1) zones[z + 1] += worth * 0.2f;
                    blows++;
                    said = b.slipped ? b.Words + " Your arm is tired." : heat < 0.4f ? "It is not at welding heat: the blows do not take." : b.force < 0.4 ? "Too light a blow to set it." : "The seam closes under the blow.";
                    bool all = true;
                    foreach (float v in zones) all &= v >= 1f;
                    if (all) FinishWeld(p);
                }
                return;
            }
        }
    }

    public override void Refresh()
    {
        RefreshCounts(fuel, fuels);
        if (damper != null) damper.SetValueWithoutNotify((float)Ws.Forge.damper);
        if (info == null)
            return;
        CraftPiece p = Selected;
        string how;
        switch (work)
        {
            case Work.Flux: how = "<b>Sprinkling flux</b>: hold the button and move along the hot bar - cover all of it. Release when done; right-click to stop."; break;
            case Work.Fold:
                how = setting
                    ? $"<b>Setting the fold</b>: hold and release on the folded bar to strike - every stretch must close (the bars below). Welding heat: the meter at the bottom left (bright = ready). {blows} blows. Right-click: stop without welding."
                    : "<b>Folding</b>: grab the far (right) end of the bar and drag it back over to the middle.";
                break;
            case Work.Weld:
                how = partner == null ? "<b>Welding on a bar</b>: click the other hot bar (right) to stack it on this one." : $"<b>Setting the weld</b>: strike all along the stacked bars while they are at welding heat. {blows} blows. Right-click: stop.";
                break;
            default:
                how = p != null && p.kind == CraftPieceKind.Metal && p.flux < 0.3 && p.MaxTemperature > 900 ? "Hot enough to flux: sprinkle flux on it before folding or welding." : "";
                break;
        }
        info.text = (how + " " + said).Trim();
    }
}

/// <summary>
/// The anvil: the bar seen from the side on the anvil and from above. Press and hold on the bar to raise the hammer
/// (the longer, the harder - until the arm tires and the blow wavers), move the pointer above or below the bar's line
/// to tilt the face, release to strike where you aim. Blows snatched too fast fall short. The tool decides what a blow
/// does: the flat face spreads, the cross peen draws it long, the straight peen widens it, the fuller grooves it, the
/// horn bends it, the hardy cuts it, the drift opens an eye, the flatter smooths, the dishing stake cups a plate.
/// </summary>
public sealed class CraftAnvilMode : CraftMode
{
    private readonly CraftHammer hammer = new CraftHammer();
    private CraftStrikeTool tool = CraftStrikeTool.Flat;
    private CraftStrikeFace face = CraftStrikeFace.Flat;
    private readonly List<Button> toolButtons = new List<Button>();
    private Button faceButton;
    private TextMeshProUGUI blowText;
    private float x0, mm, lineY, aim, tilt;
    private float flipShown = -1f, lastDrawAt;
    private string lastBlow = "";
    // Work that takes several blows before it is done: cutting with the chisel, making rivets, bending a buckle.
    private enum Job { None, Chisel, Rivets, Buckle }
    private Job job;
    private float jobDone, jobAt;
    private static readonly CraftStrikeTool[] ToolOrder =
    {
        CraftStrikeTool.Flat, CraftStrikeTool.PeenAcross, CraftStrikeTool.PeenAlong, CraftStrikeTool.Fuller, CraftStrikeTool.Horn,
        CraftStrikeTool.Hardy, CraftStrikeTool.Drift, CraftStrikeTool.Flatter, CraftStrikeTool.Dishing, CraftStrikeTool.Straighten,
    };

    public override string Title => "Anvil";
    public override bool Available(CraftStation s) => s.Has(CraftTools.Anvil);
    public override string Hint => "Hold on the bar to raise the hammer, release to strike. Above or below the line tilts the blow (a bevel). Strike while it glows." + ByHandHint("take up the hammer at the anvil");
    public override CraftHandsOnMode HandsOnMode => CraftHandsOnMode.Hammer;

    protected override void FillHandsOn(ref CraftHandsOnSetup setup)
    {
        setup.tool = tool;
        setup.face = face;
    }

    public override void Build(RectTransform controls)
    {
        toolButtons.Clear();
        RectTransform r1 = Row(controls, 32f, 4f);
        RectTransform r2 = Row(controls, 32f, 4f);
        for (int i = 0; i < ToolOrder.Length; i++)
        {
            CraftStrikeTool t = ToolOrder[i];
            Button b = Btn(i < 5 ? r1 : r2, CraftForging.ToolName(t), () => { tool = t; ShowSelection(); }, 0f, UITheme.ButtonStyle.Tab);
            toolButtons.Add(b);
        }
        RectTransform r3 = Row(controls);
        faceButton = Btn(r3, "On its face", () => { face = face == CraftStrikeFace.Flat ? CraftStrikeFace.Edge : CraftStrikeFace.Flat; ShowSelection(); }, 140f);
        Btn(r3, "Turn over", () => Send(new CraftRequest(CraftRequestKind.Turn, ui.SelectedId)), 110f);
        Btn(r3, QuickBench ? "Cut here (chisel)" : "Chisel…", () => StartJob(Job.Chisel), QuickBench ? 150f : 90f);
        Btn(r3, QuickBench ? "Make rivets" : "Rivets…", () => StartJob(Job.Rivets), QuickBench ? 110f : 90f);
        Btn(r3, QuickBench ? "Bend a buckle" : "Buckle…", () => StartJob(Job.Buckle), QuickBench ? 120f : 90f);
        RectTransform r4 = Row(controls);
        HandsOnButton(r4, "Take up the hammer");
        blowText = InfoRow(controls, 48f); // what happened: its own wrapping line, inside the window
        ShowSelection();
    }

    private void ShowSelection()
    {
        for (int i = 0; i < toolButtons.Count; i++) UITheme.SetSelected(toolButtons[i], ToolOrder[i] == tool);
        TMP_Text t = faceButton != null ? faceButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (t != null) t.text = face == CraftStrikeFace.Flat ? "On its face" : "On its edge";
    }

    public override void Tick(float dt) => hammer.Tick(dt);

    public override void Leave()
    {
        hammer.Lower();
        job = Job.None;
    }

    /// <summary>A job of several blows (a test bench does it at once).</summary>
    private void StartJob(Job j)
    {
        CraftPiece p = Selected;
        if (p == null) { ui.Message("Pick a piece first."); return; }
        if (QuickBench)
        {
            if (j == Job.Chisel) Send(new CraftRequest(CraftRequestKind.Split, p.id) { a = aim });
            else Send(new CraftRequest(CraftRequestKind.FormFastener, p.id) { i0 = (int)(j == Job.Rivets ? CraftFastenerKind.Rivet : CraftFastenerKind.Buckle) });
            return;
        }
        job = job == j ? Job.None : j;
        jobDone = 0f;
        jobAt = aim;
        lastBlow = job == Job.Chisel ? "Set the chisel where it should cut (it stays where you first strike) and drive it through with firm blows."
            : job == Job.Rivets ? "Cut and head the rivets: firm blows on the end of the rod." : job == Job.Buckle ? "Bend the buckle round the horn: firm blows." : "";
    }

    /// <summary>A blow on a job: firm blows count fully, light ones a little; done after about four.</summary>
    private void JobBlow(CraftPiece p, CraftBlow b)
    {
        if (jobDone <= 0f && job == Job.Chisel) jobAt = (float)b.position;
        jobDone += Mathf.Clamp01((float)b.force * 1.4f) * 0.28f;
        if (jobDone < 1f)
        {
            lastBlow = b.slipped ? $"{b.Words} ({jobDone * 100:0}%)" : $"{(b.force > 0.6 ? "A firm blow" : "A light blow")}: {(job == Job.Chisel ? "the chisel bites deeper" : "it takes shape")} ({jobDone * 100:0}%).";
            return;
        }
        Job j = job;
        job = Job.None;
        bool ok = j == Job.Chisel
            ? Send(new CraftRequest(CraftRequestKind.Split, p.id) { a = jobAt })
            : Send(new CraftRequest(CraftRequestKind.FormFastener, p.id) { i0 = (int)(j == Job.Rivets ? CraftFastenerKind.Rivet : CraftFastenerKind.Buckle) });
        lastBlow = ok ? (j == Job.Chisel ? "The chisel goes through: it is cut in two." : j == Job.Rivets ? "The rivets are made." : "The buckle is bent.") : "";
    }

    public override void Draw(CraftPixels c)
    {
        // The anvil: horn on the left, the face, the hardy hole.
        c.Polygon(new List<Vector2> { new Vector2(60, 150), new Vector2(170, 130), new Vector2(170, 160) }, Iron);
        c.Rect(170, 110, 640, 160, Iron);
        c.Rect(250, 30, 560, 110, new Color(0.17f, 0.17f, 0.19f));
        c.Rect(600, 150, 616, 160, new Color(0.05f, 0.05f, 0.06f));
        lineY = 230;
        CraftPiece p = Selected;
        if (!IsMetal(p))
        {
            return;
        }
        // Room for the design's length too, so the target shows how far it still has to be drawn out.
        CraftShapeTarget shape = CraftArt.TargetShape(Rules, p, out _);
        mm = Mathf.Max(Fit(p, 560, 110), shape != null ? shape.lengthMax / 560f : 0f);
        float len = (float)(p.Length / mm), span = Mathf.Max(len, shape != null ? shape.lengthMax / mm : 0f);
        x0 = 170 + (470 - span) * 0.5f;
        // Turning it over rolls it about its length: the section turns, and from above the edge goes to the other side.
        float now = Time.unscaledTime, target = p.faceUp == 1 ? 180f : 0f;
        flipShown = flipShown < 0f ? target : Mathf.MoveTowards(flipShown, target, (now - lastDrawAt) * 600f);
        lastDrawAt = now;
        float flip = Mathf.Cos(flipShown * Mathf.Deg2Rad);
        CraftArt.DrawSide(c, Rules, p, new Rect(x0, 160, len, 40), mm, 3f);
        CraftArt.DrawTop(c, Rules, p, new Rect(x0, lineY + 40, len, 100), mm, null, flip < 0f);
        CraftArt.DrawTargetProfile(c, Rules, p, x0, 160, mm, true, 3f);
        CraftArt.DrawTargetProfile(c, Rules, p, x0, lineY + 90, mm, false);
        // The section where the hammer would fall: the face that is up takes a tilted blow's bevel.
        int seg = Mathf.Clamp(SegmentAtPixel(p, Mathf.Clamp(ui.Hover.x, x0, x0 + len), x0, mm), 0, p.segments.Count - 1);
        var box = new Rect(12, 8, 220, 96);
        MetalSegment sg = p.segments[seg];
        bool hasTarget = CraftArt.TargetAt(Rules, p, seg, out float tw, out float tt, out float ta, out bool edged);
        float scale = CraftArt.SectionScale(sg, box, hasTarget ? tw : 0f, hasTarget ? tt : 0f);
        Vector2 o = CraftArt.DrawSection(c, sg, box, CraftArt.MetalColor(Rules, p), scale, flip);
        if (hasTarget) CraftArt.DrawTargetSection(c, o, scale, tw, tt, ta, edged, p.edges >= 2, flip);
        // The face that is up: a bright line along it.
        float up = (float)sg.thickness * 0.5f * scale * Mathf.Sign(flip == 0f ? 1f : flip);
        c.Line(o.x - (float)sg.width * 0.5f * scale, o.y + Mathf.Abs(up) + 3f, o.x + (float)sg.width * 0.5f * scale, o.y + Mathf.Abs(up) + 3f, Mark, 1.5f);
        if (p.location != CraftLocation.Air)
            c.Rect(0, 0, c.Width, c.Height, new Color(0f, 0f, 0f, 0.45f));
        // The hammer: aim, tilt and charge.
        Vector2 h = ui.Hover;
        if (h.x >= 0)
        {
            float ax = Mathf.Clamp(h.x, x0, x0 + len);
            float t = Mathf.Clamp((h.y - (lineY + 90)) / 60f, -1f, 1f);
            c.Line(ax, 140, ax, 380, new Color(1f, 1f, 1f, 0.15f), 1f);
            float charge = (float)hammer.Charge;
            c.Rect(ax - 18, 330 + 30 * charge, ax + 18, 350 + 30 * charge, new Color(0.4f, 0.4f, 0.44f));
            c.Line(ax - 18, 340 + 30 * charge - t * 8, ax + 18, 340 + 30 * charge + t * 8, Mark, 2f);
            Meter(c, 20, 370, 140, 10, charge / 1.0f, charge > 1.0f ? new Color(1f, 0.5f, 0.3f) : new Color(1f, 0.8f, 0.4f));
        }
        if (job != Job.None)
        {
            if (job == Job.Chisel && jobDone > 0f)
            {
                float jx = x0 + jobAt * len;
                c.Line(jx, 150, jx, 380, new Color(1f, 0.6f, 0.4f, 0.8f), 2f);
            }
            Meter(c, 20, 352, 140, 10, jobDone, new Color(0.6f, 0.85f, 1f));
        }
    }

    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Selected;
        if (!IsMetal(p))
            return;
        float len = (float)(p.Length / mm);
        aim = Mathf.Clamp01((pos.x - x0) / Mathf.Max(1f, len));
        tilt = Mathf.Clamp((pos.y - (lineY + 90)) / 60f, -1f, 1f);
        if (phase == CraftPointer.Down && !right && pos.x >= x0 - 10 && pos.x <= x0 + len + 10 && pos.y > 140 && pos.y < 380)
            hammer.Raise();
        else if (phase == CraftPointer.Up && hammer.Raised && job != Job.None)
            JobBlow(p, hammer.Release(job == Job.Chisel && jobDone > 0f ? jobAt : aim, 0, Hands));
        else if (phase == CraftPointer.Up && hammer.Raised)
        {
            CraftBlow blow = hammer.Release(aim, tilt, Hands);
            if (Send(new CraftRequest(CraftRequestKind.Strike, p.id) { a = (float)blow.position, i0 = (int)tool, i1 = (int)face, b = (float)blow.force, c = (float)blow.tilt }))
            {
                lastBlow = blow.Words;
                foreach (CraftEvent e in Ws.RecentEvents)
                    if (e.Kind == CraftEventKind.Struck && e.Piece == p.id) lastBlow += " " + CraftLook.StrikeSound(e.Id);
            }
        }
    }

    public override void Refresh()
    {
        CraftPiece p = Selected;
        string face = p != null && IsMetal(p) ? $"<b>{(p.faceUp == 0 ? "Left" : "Right")} face up</b> (the gold line): a tilted blow bevels it. " : "";
        string shape = "";
        if (p != null && IsMetal(p) && CraftArt.TargetShape(Rules, p, out _) is CraftShapeTarget t)
            shape = p.Length < t.lengthMin * 0.97 ? "Shorter than it should be: draw it out to the green ticks. "
                : p.Length > t.lengthMax * 1.03 ? "Longer than it should be. "
                : "Long enough: now bring it to the green outline. ";
        if (blowText != null) blowText.text = face + shape + lastBlow;
    }
}

/// <summary>
/// Quenching and tempering, with the hands: drag the hot work from the bench into the tank (edge first, point first or
/// flat) and keep it moving - up and down, side to side or in circles; held still, a vapour jacket slows the quench and
/// warps it - then drag it up out of the tank. Drag it into the oven to temper (hold on the firebox to kindle the
/// oven, hold again to rake the fire out; hold on the oven door to take it out). Drag it onto the ash box and rake the
/// ash over it to anneal slowly - the better it is buried, the slower it cools; hold on the ashes to dig it out. Paint
/// clay along the spine before hardening: thicker where you go over it more; the edge stays bare and hardens.
/// A test station has quick buttons for all of it.
/// </summary>
public sealed class CraftQuenchMode : CraftMode
{
    private enum Hold { None, Firebox, OvenDoor, DigOut }

    private Slider depth, oven, clay;
    private TMP_Dropdown entry;
    private Button clayButton;
    private TextMeshProUGUI info;
    private Rect tank, ovenBox, firebox, ashBox, bench;
    private readonly Queue<Vector2> moves = new Queue<Vector2>();
    private CraftQuenchMotion sent = CraftQuenchMotion.Still;
    private float motionTimer;
    private int clayFrom = -1, clayTo = -1;
    private float x0, mm, len;
    private bool dragging, painting, burying, lifting;
    private Vector2 dragAt;
    private readonly CraftCoverage clayCoat = new CraftCoverage();
    private readonly CraftShake rake = new CraftShake();
    private float buried;
    private Hold hold;
    private float holdTime;
    private string said = "";

    public override string Title => "Quench";
    public override bool Available(CraftStation s) => s.Has(CraftTools.QuenchTank) || s.Has(CraftTools.Oven) || s.Has(CraftTools.Ashes);
    public override string Hint => "Drag the piece into the tank, the oven or the ashes; keep it moving in the tank, drag it up to lift it out. Hold on the oven's firebox to light it." + ByHandHint("quench it by hand");
    public override CraftHandsOnMode HandsOnMode => Station.Has(CraftTools.QuenchTank) ? CraftHandsOnMode.Quench : CraftHandsOnMode.None;

    protected override void FillHandsOn(ref CraftHandsOnSetup setup)
    {
        setup.depth = depth != null ? depth.value : 1f;
        setup.entryAngle = EntryAngle;
    }

    private float EntryAngle => entry == null || entry.value == 0 ? 0f : entry.value == 1 ? 0.1f : 0.9f;

    public override void Build(RectTransform controls)
    {
        RectTransform r1 = Row(controls);
        entry = Drop(r1, new[] { "Edge first", "Point first", "Flat" }, 0, 140f);
        depth = Slide(r1, "Depth", 0.2f, 1f, 1f);
        if (QuickBench)
        {
            Btn(r1, "Plunge", Plunge, 100f, UITheme.ButtonStyle.Primary);
            Btn(r1, "Lift out", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Air }), 100f);
        }
        if (Station.Has(CraftTools.QuenchTank)) HandsOnButton(r1, "Quench by hand", 160f);
        RectTransform r2 = Row(controls);
        oven = Slide(r2, "Oven °C", 100f, 650f, Mathf.Max(150f, (float)Ws.Oven.setpoint), true);
        oven.onValueChanged.AddListener(v => { if (Ws.Oven.setpoint > 0) Send(new CraftRequest(CraftRequestKind.Oven) { a = v }); }); // a lit oven follows its vent
        if (QuickBench)
        {
            Btn(r2, "Light the oven", () => Send(new CraftRequest(CraftRequestKind.Oven) { a = oven.value }), 130f);
            Btn(r2, "Into the oven", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Oven }), 120f);
            Btn(r2, "Oven off", () => Send(new CraftRequest(CraftRequestKind.Oven) { a = 0 }), 90f, UITheme.ButtonStyle.Ghost);
        }
        RectTransform r3 = Row(controls);
        clay = Slide(r3, "Clay mm", 0.5f, 5f, 2f);
        if (QuickBench)
        {
            Btn(r3, "Clay it", () =>
            {
                CraftPiece p = Selected;
                if (p == null) return;
                int a = clayFrom >= 0 ? Mathf.Min(clayFrom, clayTo) : 0, b = clayFrom >= 0 ? Mathf.Max(clayFrom, clayTo) : p.segments.Count - 1;
                Send(new CraftRequest(CraftRequestKind.Clay, p.id) { i0 = a, i1 = b, a = clay.value });
            }, 100f);
            Btn(r3, "Bury in ashes", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Ashes }), 130f);
            Btn(r3, "Out of the ashes", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Air }), 150f, UITheme.ButtonStyle.Ghost);
        }
        else
            clayButton = Btn(r3, "Paint clay…", ToggleClay, 150f);
        info = InfoRow(controls);
    }

    private void Plunge()
    {
        CraftPiece p = Selected;
        if (p == null) { ui.Message("Pick a piece first."); return; }
        sent = CraftQuenchMotion.Still;
        Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Quench, a = depth.value, b = EntryAngle });
    }

    // ------------------------------------------------------------------ clay
    private void ToggleClay()
    {
        CraftPiece p = Selected;
        if (!painting)
        {
            if (p == null || p.kind != CraftPieceKind.Metal) { ui.Message("Pick a blade first."); return; }
            if (p.location != CraftLocation.Air) { ui.Message("Clay goes on with the piece on the bench."); return; }
            painting = true;
            clayCoat.Reset(p.segments.Count);
            for (int i = 0; i < p.segments.Count; i++) clayCoat.Set(i, (float)p.segments[i].clay);
            SetClayLabel("Done claying");
            return;
        }
        painting = false;
        SetClayLabel("Paint clay…");
        if (p == null || clayCoat.Count != p.segments.Count) return;
        if (clayCoat.Mean(0.3f) < 0.01f) { said = "No clay went on."; return; }
        Send(new CraftRequest(CraftRequestKind.Clay, p.id) { i0 = 0, i1 = p.segments.Count - 1, a = clay.value, points = clayCoat.ToList() });
        said = "The clay is on: let it dry, then heat it and quench - the bare edge hardens, the clayed spine stays tough.";
    }

    private void SetClayLabel(string text)
    {
        TMP_Text t = clayButton != null ? clayButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (t != null) t.text = text;
    }

    public override void Leave()
    {
        if (painting) ToggleClay();
        dragging = burying = lifting = false;
        hold = Hold.None;
    }

    // ------------------------------------------------------------------ drawing
    public override void Draw(CraftPixels c)
    {
        CraftQuenchant m = Rules.Quenchant(Ws.Quench.medium);
        tank = new Rect(40, 20, 380, 200);
        c.Rect(tank.xMin - 8, tank.yMin - 8, tank.xMax + 8, tank.yMax, new Color(0.3f, 0.22f, 0.15f));
        Color medium = m != null && m.id.Contains("oil") ? new Color(0.45f, 0.35f, 0.12f, 0.9f) : m != null && m.id.Contains("blood") ? new Color(0.5f, 0.05f, 0.05f, 0.9f) : Water;
        c.Rect(tank.xMin, tank.yMin, tank.xMax, tank.yMax - 20, medium);
        ovenBox = new Rect(460, 20, 220, 150);
        firebox = new Rect(472, 26, 196, 22);
        c.Rect(ovenBox.xMin, ovenBox.yMin, ovenBox.xMax, ovenBox.yMax, Iron);
        float warm = Mathf.Clamp01((float)((Ws.Oven.temperature - Ws.Ambient) / 600.0));
        c.Rect(ovenBox.xMin + 15, ovenBox.yMin + 56, ovenBox.xMax - 15, ovenBox.yMax - 15, Color.Lerp(new Color(0.08f, 0.08f, 0.09f), new Color(0.45f, 0.18f, 0.08f), warm));
        bool lit = Ws.Oven.setpoint > 0;
        c.Rect(firebox.xMin, firebox.yMin, firebox.xMax, firebox.yMax, lit ? new Color(0.9f, 0.45f, 0.12f) : new Color(0.12f, 0.1f, 0.09f));
        if (lit)
            for (int i = 0; i < 8; i++)
                c.Line(firebox.xMin + 12 + i * 24, firebox.yMax, firebox.xMin + 14 + i * 24 + 4 * Mathf.Sin(Time.unscaledTime * 6 + i), firebox.yMax + 10 + 6 * Mathf.Sin(Time.unscaledTime * 5 + i), new Color(1f, 0.6f, 0.2f, 0.7f), 4f);
        ashBox = new Rect(460, 190, 220, 50);
        c.Rect(ashBox.xMin, ashBox.yMin, ashBox.xMax, ashBox.yMax, new Color(0.5f, 0.48f, 0.45f));
        bench = new Rect(20, 262, 680, 120);
        if (hold != Hold.None)
        {
            Rect r = hold == Hold.Firebox ? firebox : hold == Hold.OvenDoor ? ovenBox : ashBox;
            Meter(c, r.xMin, r.yMax + 3, r.width, 6, holdTime / HoldSeconds(hold), Mark);
        }
        CraftPiece p = Selected;
        if (!IsMetal(p))
            return;
        mm = Fit(p, 340, 60);
        len = (float)(p.Length / mm);
        bool onBench = p.location == CraftLocation.Air && !burying;
        x0 = onBench && !dragging ? bench.center.x - len * 0.5f : tank.center.x - len * 0.5f;
        float y = 300;
        if (dragging) { x0 = dragAt.x - len * 0.5f; y = dragAt.y - 35; }
        else if (p.location == CraftLocation.Quench) { y = lifting ? Mathf.Max(tank.yMin + 60, dragAt.y - 35) : tank.yMin + 60 + Wobble(); }
        else if (p.location == CraftLocation.Oven) y = -999;
        else if (p.location == CraftLocation.Ashes || burying) { x0 = ashBox.center.x - Mathf.Min(len, 200) * 0.5f; y = ashBox.yMin + 4; }
        if (y > -999)
        {
            float shownLen = (p.location == CraftLocation.Ashes || burying) ? Mathf.Min(len, 200) : len;
            CraftArt.DrawTop(c, Rules, p, new Rect(x0, y, shownLen, 70), mm * len / shownLen);
        }
        if (painting && onBench)
            for (int i = 0; i < p.segments.Count; i++)
            {
                float a = x0 + (float)(SegStart(p, i) / mm), b = x0 + (float)(SegStart(p, i + 1) / mm);
                float t = Mathf.Clamp01(clayCoat[i] / Mathf.Max(0.5f, clay.value));
                if (t > 0.01f) c.Rect(a, y + 8, b, y + 8 + 50 * t, new Color(0.62f, 0.52f, 0.4f, 0.55f + 0.4f * t));
            }
        // Ash raked over it.
        if (burying || p.location == CraftLocation.Ashes)
        {
            float cover = p.location == CraftLocation.Ashes ? (float)Mathf.Clamp01((float)p.depth) : buried;
            for (int i = 0; i < 40; i++)
                if ((i * 0.025f) < cover)
                    c.Disc(ashBox.xMin + 8 + (i * 47 % 204), ashBox.yMin + 6 + (i * 13 % 36), 6, new Color(0.58f, 0.56f, 0.53f));
        }
        if (p.location == CraftLocation.Quench)
        {
            double hot = p.MaxTemperature;
            for (int i = 0; i < (int)Math.Min(30, hot / 30); i++)
                c.Disc(x0 + (i * 47 % Mathf.Max(1, (int)len)), tank.yMax - 30 + (i * 13 % 20), 3 + i % 4, new Color(1f, 1f, 1f, 0.35f));
        }
        if (QuickBench && clayFrom >= 0 && p.location == CraftLocation.Air)
            c.Rect(x0 + (float)(SegStart(p, Mathf.Min(clayFrom, clayTo)) / mm), 300 - 40, x0 + (float)(SegStart(p, Mathf.Max(clayFrom, clayTo) + 1) / mm), 300 - 32, new Color(0.62f, 0.52f, 0.4f));
    }

    private static double SegStart(CraftPiece p, int i)
    {
        double pos = 0;
        for (int k = 0; k < i && k < p.segments.Count; k++) pos += p.segments[k].length;
        return pos;
    }

    private float Wobble() => sent == CraftQuenchMotion.UpDown ? 10f * Mathf.Sin(Time.unscaledTime * 8f) : 0f;

    private static float HoldSeconds(Hold h) => h == Hold.Firebox ? 1.5f : h == Hold.DigOut ? 1f : 0.6f;

    // ------------------------------------------------------------------ the pointer
    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Selected;
        if (phase == CraftPointer.Down && firebox.Contains(pos) && !QuickBench) { hold = Hold.Firebox; holdTime = 0f; return; }
        if (p == null)
            return;
        if (phase == CraftPointer.Up) hold = Hold.None;
        // In the tank: keep it moving; drag it up out of the tank to lift it out.
        if (p.location == CraftLocation.Quench)
        {
            if (phase == CraftPointer.Down && tank.Contains(pos)) lifting = true;
            if (lifting && (phase == CraftPointer.Drag || phase == CraftPointer.Down))
            {
                dragAt = pos;
                moves.Enqueue(pos);
                while (moves.Count > 20) moves.Dequeue();
            }
            if (phase == CraftPointer.Up && lifting)
            {
                lifting = false;
                if (pos.y > tank.yMax + 10 && !QuickBench)
                    Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Air });
            }
            return;
        }
        if (QuickBench)
        {
            // The test bench: clicks do it at once.
            if (phase == CraftPointer.Down && tank.Contains(pos)) { Plunge(); return; }
            if (phase == CraftPointer.Down && ovenBox.Contains(pos)) { Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Oven }); return; }
            if (phase == CraftPointer.Down && ashBox.Contains(pos)) { Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Ashes }); return; }
            if (IsMetal(p) && p.location == CraftLocation.Air && pos.y > 260 && pos.y < 380)
            {
                int seg = SegmentAtPixel(p, pos.x, x0, mm);
                if (phase == CraftPointer.Down) clayFrom = clayTo = seg;
                else if (phase == CraftPointer.Drag && clayFrom >= 0) clayTo = seg;
            }
            return;
        }
        if (p.location == CraftLocation.Oven && phase == CraftPointer.Down && ovenBox.Contains(pos)) { hold = Hold.OvenDoor; holdTime = 0f; return; }
        if (p.location == CraftLocation.Ashes && phase == CraftPointer.Down && ashBox.Contains(pos)) { hold = Hold.DigOut; holdTime = 0f; return; }
        if (!IsMetal(p) || p.location != CraftLocation.Air)
            return;
        // Raking ash over it.
        if (burying)
        {
            if (phase == CraftPointer.Down || phase == CraftPointer.Drag)
            {
                if (ashBox.Contains(pos)) rake.Add(pos.x, Time.unscaledTime);
                buried = Mathf.Clamp01(rake.Shakes / 8f);
            }
            if (phase == CraftPointer.Up && buried > 0.05f)
            {
                Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Ashes, a = Mathf.Max(0.05f, buried) });
                said = buried >= 1f ? "Buried deep in the ashes: it will cool very slowly (annealed soft)." : "Partly buried: it cools faster than it would deep in the ashes.";
                burying = false;
            }
            if (right) burying = false;
            return;
        }
        // Painting clay on.
        bool overPiece = pos.x >= x0 && pos.x <= x0 + len && pos.y >= 290 && pos.y <= 380;
        if (painting)
        {
            if ((phase == CraftPointer.Down || phase == CraftPointer.Drag) && overPiece)
            {
                int seg = Mathf.Clamp(SegmentAtPixel(p, pos.x, x0, mm), 0, p.segments.Count - 1);
                if (right) clayCoat.Wipe(seg, 0.4f);
                else clayCoat.Paint(seg, clay.value * 0.08f, clay.value);
            }
            return;
        }
        // Dragging the piece from the bench to the tank, the oven or the ashes.
        if (phase == CraftPointer.Down && overPiece) { dragging = true; dragAt = pos; return; }
        if (!dragging)
            return;
        if (phase == CraftPointer.Drag) dragAt = pos;
        if (phase == CraftPointer.Up)
        {
            dragging = false;
            if (tank.Contains(pos)) Plunge();
            else if (ovenBox.Contains(pos)) Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Oven });
            else if (ashBox.Contains(pos)) { burying = true; buried = 0f; rake.Clear(); said = "Rake the ash over it: drag back and forth over the ash box."; }
        }
    }

    /// <summary>The motion of the hand in the tank, from the last moves: up and down, side to side, round, or still; the holds.</summary>
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
                        Send(new CraftRequest(CraftRequestKind.Oven) { a = lit ? 0f : oven.value });
                        said = lit ? "The fire is raked out of the oven." : $"The oven is kindled: it warms toward {oven.value:0} °C.";
                        break;
                    case Hold.OvenDoor:
                    case Hold.DigOut:
                        if (p != null) Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Air });
                        break;
                }
            }
        }
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
        if (moves.Count >= 4 && ui.PointerHeld)
        {
            Vector2[] v = moves.ToArray();
            float dx = 0, dy = 0, turn = 0;
            for (int i = 1; i < v.Length; i++)
            {
                Vector2 d = v[i] - v[i - 1];
                dx += Mathf.Abs(d.x);
                dy += Mathf.Abs(d.y);
                if (i > 1) turn += Vector3.Cross(v[i - 1] - v[i - 2], d).z;
            }
            if (dx + dy > 20)
                m = Mathf.Abs(turn) > (dx + dy) * 6 ? CraftQuenchMotion.Swirl : dy > dx * 1.5f ? CraftQuenchMotion.UpDown : dx > dy * 1.5f ? CraftQuenchMotion.SideToSide : CraftQuenchMotion.Swirl;
        }
        moves.Clear();
        if (m != sent)
        {
            sent = m;
            Send(new CraftRequest(CraftRequestKind.Agitate, p.id) { i0 = (int)m });
        }
    }

    public override void Refresh()
    {
        if (info == null)
            return;
        CraftPiece p = Selected;
        string how = "";
        if (painting) how = $"<b>Painting clay</b>: hold and stroke along the piece (thicker where you go over it more, up to {clay.value:0.#} mm; right-drag wipes). Leave the edge bare. Press Done claying when finished.";
        else if (burying) how = $"<b>Burying</b>: drag back and forth over the ash box to rake ash over it ({buried * 100:0}% covered), then let go.";
        else if (p != null && p.location == CraftLocation.Quench) how = $"In the tank: keep it moving - up and down, side to side or round ({sent}). Drag it up out of the tank to lift it out.";
        else if (p != null && p.location == CraftLocation.Oven) how = $"Tempering in the oven ({Ws.Oven.temperature:0} °C). Hold on the oven door to take it out.";
        else if (p != null && p.location == CraftLocation.Ashes) how = "Cooling slowly in the ashes. Hold on the ashes to dig it out.";
        else if (!QuickBench) how = $"Drag the piece into the tank, the oven or the ash box. Hold on the oven's firebox to {(Ws.Oven.setpoint > 0 ? "rake its fire out" : "kindle it")}.";
        info.text = (how + " " + said).Trim();
    }
}
