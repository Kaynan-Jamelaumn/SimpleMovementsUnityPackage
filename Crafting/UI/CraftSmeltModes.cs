using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The ore bench: the lump and the fragments it has broken into, each drawn by its size and how much ore shows in it.
/// Hold on a fragment and release to crush it (harder blows break it smaller - and powder it); right-click a fragment
/// to throw it out with the waste rock. Pan it to wash the dirt and light rock away (steady circles in the pan wash
/// cleanly, a jerky hand loses ore with the mud), and sieve off the fines. What is bound in the mineral stays.
/// </summary>
public sealed class CraftOreMode : CraftMode
{
    private readonly CraftHammer hammer = new CraftHammer { fullCharge = 0.7f };
    private Slider flow, mesh;
    private List<Vector3> placed = new List<Vector3>();
    private int target = -1;
    private Rect pan;
    private readonly CraftStroke swirl = new CraftStroke();
    private float panStart;

    public override string Title => "Ore";
    public override bool Available(CraftStation s) => s.Has(CraftTools.OreBench);
    public override string Hint => "Hold and release on a fragment to crush it; right-click to throw it out. Swirl in the pan to wash it.";

    public override void Build(RectTransform controls)
    {
        RectTransform r1 = Row(controls);
        flow = Slide(r1, "Water", 0.1f, 1f, 0.5f);
        Btn(r1, "Rinse", () => Send(new CraftRequest(CraftRequestKind.Wash, ui.SelectedId) { a = flow.value, b = 6f, c = 0.5f }), 100f);
        RectTransform r2 = Row(controls);
        mesh = Slide(r2, "Sieve mesh", 0.3f, 3f, 1f);
        Btn(r2, "Sieve", () => Send(new CraftRequest(CraftRequestKind.Sieve, ui.SelectedId) { a = mesh.value }), 100f);
    }

    public override void Tick(float dt) => hammer.Tick(dt);
    public override void Leave() => hammer.Lower();

    public override void Draw(CraftPixels c)
    {
        CraftPiece p = Selected;
        pan = new Rect(560, 30, 140, 140);
        c.Disc(pan.center.x, pan.center.y, 66, new Color(0.35f, 0.3f, 0.25f));
        c.Disc(pan.center.x, pan.center.y, 58, Water);
        if (p == null || p.kind != CraftPieceKind.Ore)
        {
            placed.Clear();
            return;
        }
        placed = CraftArt.DrawOre(c, Rules, p, new Rect(20, 20, 520, 360));
        Vector2 h = ui.Hover;
        target = -1;
        for (int i = 0; i < placed.Count; i++)
            if (Vector2.Distance(h, new Vector2(placed[i].x, placed[i].y)) <= placed[i].z + 2) { target = i; break; }
        if (target >= 0)
        {
            Vector3 f = placed[target];
            c.Ring(f.x, f.y, f.z + 3, 2, Mark);
            if (hammer.Raised) Meter(c, f.x - 30, f.y + f.z + 8, 60, 8, (float)hammer.Charge, new Color(1f, 0.8f, 0.4f));
        }
    }

    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Selected;
        if (p == null || p.kind != CraftPieceKind.Ore)
            return;
        if (pan.Contains(pos) || swirl.Active && phase != CraftPointer.Down)
        {
            if (phase == CraftPointer.Down) { swirl.Clear(); panStart = Time.unscaledTime; }
            if (phase == CraftPointer.Down || phase == CraftPointer.Drag)
                swirl.Add(Time.unscaledTime, (pos.x - pan.xMin) / pan.width, (pos.y - pan.yMin) / pan.height, Mathf.Atan2(pos.y - pan.center.y, pos.x - pan.center.x) * Mathf.Rad2Deg, flow.value, Hands);
            if (phase == CraftPointer.Up && swirl.Samples > 4)
            {
                double seconds = Time.unscaledTime - panStart;
                double technique = Mathf.Clamp01(1f - (float)swirl.Wobble / 140f) * Mathf.Clamp01(swirl.Samples / 30f);
                Send(new CraftRequest(CraftRequestKind.Wash, p.id) { a = flow.value, b = (float)Math.Min(30, seconds * 2), c = (float)technique });
                swirl.Clear();
            }
            return;
        }
        if (target < 0)
            return;
        if (right && phase == CraftPointer.Down)
        {
            Send(new CraftRequest(CraftRequestKind.DiscardFragment, p.id) { i0 = target });
            return;
        }
        if (phase == CraftPointer.Down)
            hammer.Raise();
        else if (phase == CraftPointer.Up && hammer.Raised)
        {
            CraftBlow b = hammer.Release(0.5, 0, Hands);
            Send(new CraftRequest(CraftRequestKind.Crush, p.id) { i0 = target, a = (float)(b.force * 1.5) });
        }
    }
}

/// <summary>
/// The bloomery furnace: charcoal and ore in layers, a draft from the bellows, flux to take the rock into slag. The
/// shaft is drawn in section: the charge, the glow, the slag pooling, the bloom growing at the bottom (or liquid metal,
/// if it runs). Keep it hot but not too hot, tap the slag when it chokes the draft, pull the bloom while it is soft.
/// </summary>
public sealed class CraftFurnaceMode : CraftMode
{
    private readonly CraftBellowsPump pump = new CraftBellowsPump();
    private TMP_Dropdown fuel, flux;
    private Slider damper;
    private readonly List<string> fuels = new List<string>(), fluxes = new List<string>();
    private Rect bellowsArea;

    public override string Title => "Furnace";
    public override bool Available(CraftStation s) => s.Has(CraftTools.Furnace);
    public override string Hint => "Charge cleaned ore, feed charcoal, pump the bellows steadily; flux, tap the slag, pull the bloom.";

    public override void Build(RectTransform controls)
    {
        fuels.Clear();
        var fn = new List<string>();
        foreach (CraftFuel f in Rules.Fuels) { fuels.Add(f.id); fn.Add(f.Name); }
        fluxes.Clear();
        var xn = new List<string>();
        foreach (CraftSupplyDefinition s in Rules.Supplies)
            if (s.kind == CraftSupplyKind.Flux || s.kind == CraftSupplyKind.Cupel) { fluxes.Add(s.id); xn.Add(s.Name); }
        RectTransform r1 = Row(controls);
        Btn(r1, "Charge the piece", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Furnace }), 150f, UITheme.ButtonStyle.Primary);
        fuel = Drop(r1, fn, 0, 160f);
        Btn(r1, "Add fuel", () => { if (fuels.Count > 0) Send(new CraftRequest(CraftRequestKind.AddFuel) { id = fuels[fuel.value], count = 2, flag = true }); }, 100f);
        damper = Slide(r1, "Air vent", 0f, 1f, (float)Ws.Furnace.fire.damper, false, 64f);
        damper.onValueChanged.AddListener(v => Send(new CraftRequest(CraftRequestKind.Damper) { a = v, flag = true }));
        RectTransform r2 = Row(controls);
        flux = Drop(r2, xn.Count > 0 ? xn : new List<string> { "-" }, 0, 160f);
        Btn(r2, "Add flux", () => { if (fluxes.Count > 0) Send(new CraftRequest(CraftRequestKind.Flux) { id = fluxes[flux.value], count = 1, i0 = 0 }); }, 100f);
        Btn(r2, "Tap the slag", () => Send(new CraftRequest(CraftRequestKind.TapSlag) { a = 1f }), 120f);
        Btn(r2, "Pull the bloom", () => Send(new CraftRequest(CraftRequestKind.ExtractBloom)), 130f, UITheme.ButtonStyle.Primary);
        RectTransform r3 = Row(controls);
        Text(r3, "Liquid metal (copper, lead, silver, cast iron) is run off in the Crucible area: choose a mould and pour.", 13f);
    }

    public override void Tick(float dt) => pump.Tick(dt);

    public override void Draw(CraftPixels c)
    {
        CraftFurnaceState f = Ws.Furnace;
        Rect shaft = new Rect(240, 20, 240, 360);
        c.Rect(shaft.xMin - 30, shaft.yMin, shaft.xMax + 30, shaft.yMax, new Color(0.45f, 0.3f, 0.22f));
        c.Rect(shaft.xMin, shaft.yMin + 10, shaft.xMax, shaft.yMax - 10, new Color(0.08f, 0.07f, 0.07f));
        Color glow = CraftArt.Heat(f.fire.temperature, out float k);
        c.Rect(shaft.xMin, shaft.yMin + 40, shaft.xMax, shaft.yMin + 200, new Color(glow.r, glow.g, glow.b, k * 0.8f));
        // The charge: ore lumps in the shaft.
        int n = 0;
        foreach (CraftPiece p in Ws.Pieces)
            if (p.location == CraftLocation.Furnace)
            {
                CraftOreDefinition o = p.kind == CraftPieceKind.Ore ? Rules.Ore(p.ore.ore) : null;
                Color col = o != null ? Color.Lerp(o.color, glow, k * 0.6f) : CraftArt.MetalColor(Rules, p);
                c.Disc(shaft.xMin + 40 + n % 4 * 50, shaft.yMax - 60 - n / 4 * 40, 16, col);
                n++;
            }
        // Slag and the bloom (or a pool).
        float slag = Mathf.Clamp01((float)(f.slag / Math.Max(1, Rules.Settings.slagCapacity)));
        c.Rect(shaft.xMin, shaft.yMin + 10, shaft.xMax, shaft.yMin + 10 + slag * 60, new Color(0.25f, 0.18f, 0.12f));
        if (f.BloomMass > 1)
            c.Disc(shaft.center.x, shaft.yMin + 40, Mathf.Clamp(Mathf.Sqrt((float)f.BloomMass) * 0.9f, 6f, 60f), Color.Lerp(new Color(0.35f, 0.33f, 0.32f), glow, k));
        if (f.PoolMass > 5)
            c.Rect(shaft.xMin + 20, shaft.yMin + 12, shaft.xMax - 20, shaft.yMin + 22, Color.Lerp(new Color(1f, 0.6f, 0.2f), Color.white, 0.2f));
        // The bellows.
        bellowsArea = new Rect(40, 60, 150, 90);
        c.Polygon(new List<Vector2> { new Vector2(50, 105), new Vector2(170, 75), new Vector2(170, 135) }, new Color(0.45f, 0.3f, 0.18f));
        c.Rect(170, 100, shaft.xMin - 30, 110, Iron);
        Meter(c, 50, 50, 120, 8, (float)pump.Strength, new Color(0.6f, 0.8f, 1f));
        Meter(c, 520, 40, 160, 10, slag, new Color(0.7f, 0.45f, 0.25f));
    }

    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        if (phase == CraftPointer.Down && bellowsArea.Contains(pos))
            Send(new CraftRequest(CraftRequestKind.Bellows) { a = (float)pump.Pump(Hands), flag = true });
    }

    public override void Refresh()
    {
        if (damper != null) damper.SetValueWithoutNotify((float)Ws.Furnace.fire.damper);
    }
}

/// <summary>
/// The crucible: melt metals together (proportions, not names, make the alloy), cover the melt against the air, stir
/// it even, skim the dross, then pour: press on the crucible and lift the pointer to tilt it - a steady, unbroken stream
/// fills the mould soundly; too slow it freezes short, too fast it traps air, a broken stream leaves a cold shut.
/// </summary>
public sealed class CraftCrucibleMode : CraftMode
{
    private TMP_Dropdown mold, cover, source;
    private readonly List<string> molds = new List<string>(), covers = new List<string>();
    private CraftPourControl pour;
    private float tiltStartY, tilt;
    private Rect crucible, mould;

    public override string Title => "Crucible";
    public override bool Available(CraftStation s) => s.Has(CraftTools.Crucible) || s.Has(CraftTools.Furnace);
    public override string Hint => "Put metal in, heat it in the fire, cover, stir, skim. Press on the crucible and lift to tilt it; keep the stream steady.";

    public override void Build(RectTransform controls)
    {
        molds.Clear();
        var mn = new List<string>();
        foreach (CraftMold m in Rules.Molds) { molds.Add(m.id); mn.Add(m.Name); }
        covers.Clear();
        var cn = new List<string>();
        foreach (CraftSupplyDefinition s in Rules.Supplies)
            if (s.kind == CraftSupplyKind.Flux) { covers.Add(s.id); cn.Add(s.Name); }
        RectTransform r1 = Row(controls);
        Btn(r1, "Into the crucible", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Melt }), 160f, UITheme.ButtonStyle.Primary);
        cover = Drop(r1, cn.Count > 0 ? cn : new List<string> { "-" }, 0, 150f);
        Btn(r1, "Cover it", () => { if (covers.Count > 0) Send(new CraftRequest(CraftRequestKind.Flux) { id = covers[cover.value], count = 1, i0 = 1 }); }, 100f);
        Btn(r1, "Stir", () => Send(new CraftRequest(CraftRequestKind.StirMelt) { a = 1f }), 70f);
        Btn(r1, "Skim", () => Send(new CraftRequest(CraftRequestKind.Skim) { a = 0.8f }), 70f);
        RectTransform r2 = Row(controls);
        Text(r2, "Mould", 13f, 50f);
        mold = Drop(r2, mn, 0, 200f);
        var sources = new List<string>();
        if (Station.Has(CraftTools.Crucible)) sources.Add("from the crucible");
        if (Station.Has(CraftTools.Furnace)) sources.Add("from the furnace");
        source = Drop(r2, sources, 0, 180f);
    }

    private bool FromFurnace => source != null && source.options.Count > 0 && source.options[source.value].text.Contains("furnace");

    public override void Tick(float dt)
    {
        if (pour == null)
            return;
        if (!ui.PointerHeld)
        {
            Finish();
            return;
        }
        Vector2 h = ui.Hover;
        if (h.y >= 0) tilt = Mathf.Clamp01((h.y - tiltStartY) / 160f);
        pour.Tick(dt, tilt, Hands);
        if (pour.Poured >= 1)
            Finish();
    }

    private void Finish()
    {
        if (pour == null)
            return;
        CraftPourControl p = pour;
        pour = null;
        tilt = 0f;
        if (!p.Started)
            return;
        string m = molds.Count > 0 ? molds[Mathf.Clamp(mold.value, 0, molds.Count - 1)] : null;
        // A mould left part-filled is a short casting: the steadiness and breaks are what the hands did.
        float rate = (float)p.MeanRate * (float)Math.Max(0.3, p.Poured);
        Send(FromFurnace
            ? new CraftRequest(CraftRequestKind.TakeMelt) { id = m, a = rate, b = (float)p.Steadiness }
            : new CraftRequest(CraftRequestKind.Pour) { id = m, a = rate, b = (float)p.Steadiness, i0 = p.Interruptions });
    }

    public override void Leave()
    {
        pour = null;
        tilt = 0f;
    }

    public override void Draw(CraftPixels c)
    {
        CraftCrucibleState cs = Ws.Crucible;
        crucible = new Rect(160, 120, 200, 200);
        float angle = tilt * 70f;
        Vector2 pivot = new Vector2(crucible.xMax, crucible.yMax);
        Vector2 Rot(float x, float y)
        {
            float a = -angle * Mathf.Deg2Rad, dx = x - pivot.x, dy = y - pivot.y;
            return new Vector2(pivot.x + dx * Mathf.Cos(a) - dy * Mathf.Sin(a), pivot.y + dx * Mathf.Sin(a) + dy * Mathf.Cos(a));
        }
        var body = new List<Vector2> { Rot(crucible.xMin, crucible.yMax), Rot(crucible.xMin + 30, crucible.yMin), Rot(crucible.xMax - 30, crucible.yMin), Rot(crucible.xMax, crucible.yMax) };
        c.Polygon(body, new Color(0.42f, 0.36f, 0.32f));
        double mass = FromFurnace ? Ws.Furnace.PoolMass : cs.Mass;
        double temperature = FromFurnace ? Ws.Furnace.fire.temperature : cs.temperature;
        Color glow = CraftArt.Heat(temperature, out float k);
        if (mass > 1)
        {
            float level = Mathf.Clamp01((float)(mass / 2500.0)) * 140f + 20f;
            var melt = new List<Vector2> { Rot(crucible.xMin + 34, crucible.yMin + 10 + level), Rot(crucible.xMin + 34, crucible.yMin + 10), Rot(crucible.xMax - 34, crucible.yMin + 10), Rot(crucible.xMax - 34, crucible.yMin + 10 + level) };
            c.Polygon(melt, Color.Lerp(new Color(0.4f, 0.38f, 0.36f), glow, k));
            if (cs.dross > 5 && !FromFurnace) c.Rect(crucible.xMin + 40, crucible.yMin + 10 + level - 6, crucible.xMax - 40, crucible.yMin + 10 + level, new Color(0.3f, 0.28f, 0.25f, 0.9f));
            if (cs.cover > 0.3 && !FromFurnace) c.Rect(crucible.xMin + 40, crucible.yMin + 10 + level, crucible.xMax - 40, crucible.yMin + 16 + level, new Color(0.7f, 0.75f, 0.8f, 0.6f));
        }
        foreach (CraftPiece p in Ws.Pieces)
            if (p.location == CraftLocation.Melt)
                c.Rect(crucible.xMin + 60, crucible.yMin + 30, crucible.xMax - 60, crucible.yMin + 60, CraftArt.MetalColor(Rules, p));
        // The mould.
        mould = new Rect(460, 40, 220, 70);
        c.Rect(mould.xMin, mould.yMin, mould.xMax, mould.yMax, new Color(0.3f, 0.25f, 0.2f));
        c.Rect(mould.xMin + 10, mould.yMin + 20, mould.xMax - 10, mould.yMax - 10, new Color(0.1f, 0.08f, 0.07f));
        if (pour != null)
        {
            c.Rect(mould.xMin + 10, mould.yMin + 20, mould.xMin + 10 + (mould.width - 20) * (float)pour.Poured, mould.yMax - 10, Color.Lerp(glow, Color.white, 0.2f));
            if (pour.Rate > 0.04)
                c.Line(pivot.x, pivot.y, mould.xMin + 40, mould.yMax - 15, glow, 2f + 10f * (float)pour.Rate);
            Meter(c, 460, 140, 220, 10, (float)pour.Steadiness, new Color(0.5f, 0.85f, 0.6f));
        }
    }

    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        if (phase == CraftPointer.Down && crucible.Contains(pos))
        {
            pour = new CraftPourControl();
            tiltStartY = pos.y;
            tilt = 0f;
        }
    }
}
