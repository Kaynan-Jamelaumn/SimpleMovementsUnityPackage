using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The ore bench: the lump and the fragments it has broken into, each drawn by its size and how much ore shows in it.
/// Hold on a fragment and release to crush it (harder blows break it smaller - and powder it); right-click a fragment
/// to throw it out with the waste rock. Pan it to wash the dirt and light rock away (steady circles in the pan wash
/// cleanly, a jerky hand loses ore with the mud), and shake the sieve to sort off the fines - a good, even shaking lets
/// everything smaller than the mesh through, a few lazy shakes only the smallest. What is bound in the mineral stays.
/// <para>Or clean one stone at a time with a brush, dry: pick a brush and scrub back and forth over a fragment. The
/// soft brush takes loose dirt, the stiff one caked clay, the wire brush knocks off the weathered rind of rock (and a
/// little ore with it). Dirt shows as brown specks, the rind as a grey ring. Slower than washing, but nothing is lost to
/// the water. By hand (3D) the stone is turned in the fingers and brushed all round.</para>
/// </summary>
public sealed class CraftOreMode : CraftMode
{
    private enum Tool { Hammer, Soft, Stiff, Wire }
    private static readonly string[] ToolNames = { "Hammer", "Soft brush", "Stiff brush", "Wire brush" };
    private readonly CraftHammer hammer = new CraftHammer { fullCharge = 0.7f };
    private Tool tool = Tool.Hammer;
    private readonly List<Button> toolButtons = new List<Button>();
    // Scrubbing a stone: which one, how much of it the brush went over (eight sectors round it), for how long.
    private readonly CraftCoverage scrub = new CraftCoverage();
    private int scrubbing = -1;
    private float scrubStart, scrubPhase;
    private Vector2 lastScrub;
    private Slider flow, mesh;
    private TextMeshProUGUI info;
    private List<Vector3> placed = new List<Vector3>();
    private int target = -1;
    private Rect pan, sieve;
    private readonly CraftStroke swirl = new CraftStroke();
    private readonly CraftShake shake = new CraftShake();
    private float panStart, sieveShift;
    private bool shaking;
    private string said = "";

    public override string Title => "Ore";
    public override bool Available(CraftStation s) => s.Has(CraftTools.OreBench);
    public override string Hint => "Hammer: hold and release on a fragment to crush it; right-click to throw it out. Brushes: scrub back and forth over a stone. Swirl in the pan to wash; shake the sieve to sort the fines." + ByHandHint("brush a stone in your hand");
    public override CraftHandsOnMode HandsOnMode => CraftHandsOnMode.Brush;

    protected override void FillHandsOn(ref CraftHandsOnSetup setup)
    {
        setup.brush = tool == Tool.Hammer ? CraftOreBrush.Stiff : (CraftOreBrush)((int)tool - 1);
        setup.fragment = Mathf.Max(0, target);
    }

    private CraftOreBrush Brush => (CraftOreBrush)((int)tool - 1);

    public override void Build(RectTransform controls)
    {
        RectTransform r0 = Row(controls, 30f, 4f);
        toolButtons.Clear();
        for (int i = 0; i < ToolNames.Length; i++)
        {
            Tool t = (Tool)i;
            toolButtons.Add(Btn(r0, ToolNames[i], () => { tool = t; scrubbing = -1; ShowTool(); }, 0f, UITheme.ButtonStyle.Tab, 28f));
        }
        HandsOnButton(r0, "Brush by hand", 150f);
        ShowTool();
        RectTransform r1 = Row(controls);
        flow = Slide(r1, "Water", 0.1f, 1f, 0.5f);
        if (QuickBench)
            Btn(r1, "Rinse", () => Send(new CraftRequest(CraftRequestKind.Wash, ui.SelectedId) { a = flow.value, b = 6f, c = 0.5f }), 100f);
        RectTransform r2 = Row(controls);
        mesh = Slide(r2, "Sieve mesh", 0.3f, 3f, 1f);
        if (QuickBench)
            Btn(r2, "Sieve", () => Send(new CraftRequest(CraftRequestKind.Sieve, ui.SelectedId) { a = mesh.value, b = 1f }), 100f);
        info = InfoRow(controls);
    }

    private void ShowTool()
    {
        for (int i = 0; i < toolButtons.Count; i++) UITheme.SetSelected(toolButtons[i], (Tool)i == tool);
    }

    public override void Tick(float dt) => hammer.Tick(dt);
    public override void Leave()
    {
        scrubbing = -1;
        hammer.Lower();
        shaking = false;
    }

    public override void Draw(CraftPixels c)
    {
        CraftPiece p = Selected;
        pan = new Rect(560, 30, 140, 140);
        c.Disc(pan.center.x, pan.center.y, 66, new Color(0.35f, 0.3f, 0.25f));
        c.Disc(pan.center.x, pan.center.y, 58, Water);
        // The sieve: a frame and its mesh (finer mesh, closer wires), jiggling while it is shaken.
        sieve = new Rect(560 + sieveShift, 200, 140, 120);
        c.Rect(sieve.xMin, sieve.yMin, sieve.xMax, sieve.yMax, Wood);
        c.Rect(sieve.xMin + 8, sieve.yMin + 8, sieve.xMax - 8, sieve.yMax - 8, new Color(0.08f, 0.07f, 0.06f));
        float gap = Mathf.Lerp(6f, 22f, Mathf.InverseLerp(0.3f, 3f, mesh.value));
        for (float x = sieve.xMin + 8; x < sieve.xMax - 8; x += gap) c.Line(x, sieve.yMin + 8, x, sieve.yMax - 8, new Color(0.6f, 0.6f, 0.62f, 0.8f), 1f);
        for (float y = sieve.yMin + 8; y < sieve.yMax - 8; y += gap) c.Line(sieve.xMin + 8, y, sieve.xMax - 8, y, new Color(0.6f, 0.6f, 0.62f, 0.8f), 1f);
        if (shaking)
        {
            Meter(c, sieve.xMin, sieve.yMax + 4, sieve.width, 6, Mathf.Clamp01(shake.Shakes / 8f), Mark);
            for (int i = 0; i < Mathf.Min(20, shake.Shakes * 3); i++)
                c.Disc(sieve.xMin + 14 + (i * 31 % 112), sieve.yMin - 4 - (i * 7 % 14), 1.5f, new Color(0.55f, 0.45f, 0.35f));
        }
        if (p == null || p.kind != CraftPieceKind.Ore)
        {
            placed.Clear();
            return;
        }
        placed = CraftArt.DrawOre(c, Rules, p, new Rect(20, 20, 520, 360));
        // What the brushes take off: a grey ring of weathered rind, brown specks of dirt (as much as each stone holds).
        for (int i = 0; i < placed.Count && i < p.ore.fragments.Count; i++)
        {
            OreFragment f = p.ore.fragments[i];
            Vector3 q = placed[i];
            float rind = (float)(f.gangue * CraftOres.Rind(f.size));
            if (rind > 0.02f) c.Ring(q.x, q.y, q.z - 1f, Mathf.Clamp(rind * 45f, 1.5f, q.z * 0.4f), new Color(0.46f, 0.43f, 0.38f, 0.95f));
            int specks = Mathf.Min(70, Mathf.RoundToInt((float)f.dirt * 300f * Mathf.Clamp01(q.z / 30f + 0.25f)));
            for (int k = 0; k < specks; k++)
            {
                float a = CraftMath.Hash(i * 97 + k, 3) % 628 / 100f, r = q.z * 0.85f * Mathf.Sqrt(CraftMath.Hash(i * 31 + k, 5) % 100 / 100f);
                int sector = Mathf.FloorToInt(Mathf.Repeat(a, 2f * Mathf.PI) / (2f * Mathf.PI) * 8f) % 8;
                if (i == scrubbing && scrub[sector] > CraftMath.Hash(k, 11) % 100 / 100f) continue; // scrubbed off just now
                c.Disc(q.x + Mathf.Cos(a) * r, q.y + Mathf.Sin(a) * r, Mathf.Clamp(q.z * 0.06f, 1.2f, 2.6f), new Color(0.3f, 0.21f, 0.12f, 0.95f));
            }
        }
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
        if (scrubbing >= 0 && scrubbing < placed.Count)
        {
            Vector3 f = placed[scrubbing];
            Meter(c, f.x - 30, f.y + f.z + 8, 60, 6, scrub.Mean(), new Color(0.6f, 0.85f, 1f));
        }
        // The brush in the hand.
        if (tool != Tool.Hammer && h.x >= 0)
        {
            float wob = scrubbing >= 0 ? Mathf.Sin(scrubPhase) * 3f : 0f;
            Color bristle = tool == Tool.Soft ? new Color(0.78f, 0.7f, 0.55f) : tool == Tool.Stiff ? new Color(0.25f, 0.2f, 0.15f) : new Color(0.65f, 0.65f, 0.68f);
            c.Rect(h.x - 14 + wob, h.y + 2, h.x + 14 + wob, h.y + 10, new Color(0.45f, 0.3f, 0.17f));
            for (int k = 0; k < 8; k++) c.Line(h.x - 12 + k * 3.4f + wob, h.y + 2, h.x - 12 + k * 3.4f + wob * 1.5f, h.y - 5, bristle, 1f);
            c.Line(h.x + 14 + wob, h.y + 6, h.x + 30 + wob, h.y + 16, new Color(0.45f, 0.3f, 0.17f), 3f);
        }
    }

    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Selected;
        if (p == null || p.kind != CraftPieceKind.Ore)
            return;
        // A brush: scrub back and forth over a stone; what it went over is sent when the hand comes up.
        if (tool != Tool.Hammer && (scrubbing >= 0 || phase == CraftPointer.Down && target >= 0))
        {
            if (phase == CraftPointer.Down)
            {
                scrubbing = target;
                scrub.Reset(8);
                scrubStart = Time.unscaledTime;
                lastScrub = pos;
            }
            else if (phase == CraftPointer.Drag && scrubbing < placed.Count)
            {
                Vector3 f = placed[scrubbing];
                float moved = Vector2.Distance(pos, lastScrub);
                lastScrub = pos;
                if (Vector2.Distance(pos, new Vector2(f.x, f.y)) <= f.z + 4f && moved > 0.5f)
                {
                    int sector = Mathf.FloorToInt(Mathf.Repeat(Mathf.Atan2(pos.y - f.y, pos.x - f.x), 2f * Mathf.PI) / (2f * Mathf.PI) * 8f) % 8;
                    // A tired arm scrubs less hard; bigger stones take longer to go over.
                    scrub.Paint(sector, moved / Mathf.Max(8f, f.z * 6f) * (0.45f + 0.55f * (float)Hands.Strength));
                    scrubPhase += moved * 0.3f;
                    if (Mathf.Repeat(scrubPhase, 6f) < 0.3f) Hands.Move(tool == Tool.Wire ? 0.35 : 0.2);
                }
            }
            else if (phase == CraftPointer.Up)
            {
                float cover = scrub.Mean();
                int frag = scrubbing;
                scrubbing = -1;
                if (cover > 0.01f && Send(new CraftRequest(CraftRequestKind.BrushOre, p.id) { i0 = frag, i1 = (int)Brush, a = cover, b = Mathf.Max(0.05f, Time.unscaledTime - scrubStart) }))
                {
                    CraftOreDefinition o = Rules.Ore(p.ore.ore);
                    bool clay = o != null && o.gangue == CraftGangue.Clay;
                    said = Brush == CraftOreBrush.Soft ? (clay ? "The soft brush barely shifts the caked clay: the stiff brush would." : "Loose dirt brushed off.")
                        : Brush == CraftOreBrush.Stiff ? "Dirt and clay scrubbed off." : "The wire brush knocks the weathered rind off - and scratches a little ore away.";
                    said = $"{cover * 100:0}% of the stone gone over. {said}";
                }
            }
            return;
        }
        // The sieve: drag back and forth on it.
        if (!QuickBench && (sieve.Contains(pos) && phase == CraftPointer.Down || shaking))
        {
            if (phase == CraftPointer.Down) { shaking = true; shake.Clear(); }
            if (phase == CraftPointer.Down || phase == CraftPointer.Drag)
            {
                shake.Add(pos.x, Time.unscaledTime);
                sieveShift = Mathf.Clamp((pos.x - 630f) * 0.15f, -10f, 10f);
            }
            if (phase == CraftPointer.Up)
            {
                shaking = false;
                sieveShift = 0f;
                if (shake.Shakes >= 2)
                {
                    float thorough = Mathf.Clamp01(shake.Shakes / 8f) * 0.6f + 0.4f * shake.Rhythm;
                    Send(new CraftRequest(CraftRequestKind.Sieve, p.id) { a = mesh.value, b = thorough });
                    said = $"{shake.Shakes} shakes{(thorough > 0.8f ? ", even and thorough" : thorough > 0.5f ? "" : ": only the smallest fell through")}.";
                }
            }
            return;
        }
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
                said = technique > 0.7 ? "Steady circles: the mud washes off cleanly." : "A jerky swirl: some ore went out with the mud.";
                swirl.Clear();
            }
            return;
        }
        if (target < 0 || tool != Tool.Hammer)
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

    public override void Refresh()
    {
        if (info == null) return;
        string how = shaking ? $"<b>Shaking the sieve</b>: keep dragging back and forth at an even pace ({shake.Shakes} shakes)."
            : scrubbing >= 0 ? $"<b>Scrubbing</b> with the {CraftOres.BrushName(Brush)}: back and forth over the stone, all round it ({scrub.Mean() * 100:0}% gone over)."
            : tool != Tool.Hammer ? $"<b>{ToolNames[(int)tool]}</b>: press on a stone and scrub back and forth. Brown specks are dirt, a grey ring the weathered rind. Soft: loose dirt. Stiff: caked clay. Wire: the rind (and a little ore)."
            : "Crush the big lumps, swirl them in the pan (top right) to wash them, then shake them through the sieve - or brush the stones clean one by one.";
        info.text = (how + " " + said).Trim();
    }
}

/// <summary>
/// The bloomery furnace: charcoal and ore in layers, a draft from the bellows, flux to take the rock into slag. The
/// shaft is drawn in section: the charge, the glow, the slag pooling, the bloom growing at the bottom (or liquid metal,
/// if it runs). Keep it hot but not too hot, tap the slag when it chokes the draft, pull the bloom while it is soft.
/// <para>With the hands: drag the ore from the bench up into the mouth of the shaft to charge it, hold on the charcoal
/// heap to shovel fuel in, hold over the mouth to scoop flux in, hold on the tap hole to let the slag run, drag the
/// bloom out of the bottom of the shaft. A test station has quick buttons.</para>
/// </summary>
public sealed class CraftFurnaceMode : CraftMode
{
    private enum Hold { None, Fuel, Flux, Tap }

    private readonly CraftBellowsPump pump = new CraftBellowsPump();
    private TMP_Dropdown fuel, flux;
    private Slider damper;
    private TextMeshProUGUI info;
    private readonly List<string> fuels = new List<string>(), fluxes = new List<string>();
    private Rect bellowsArea, shaft, mouth, heap, tapHole, bench, bloomAt;
    private Hold hold;
    private float holdTime;
    private bool dragging, pulling;
    private Vector2 dragAt;
    private string said = "";

    public override string Title => "Furnace";
    public override bool Available(CraftStation s) => s.Has(CraftTools.Furnace);
    public override string Hint => "Charge cleaned ore (drag it into the mouth), shovel charcoal (hold on the heap), pump the bellows steadily; flux, tap the slag, pull the bloom.";

    public override void Build(RectTransform controls)
    {
        fluxes.Clear();
        var xn = new List<string>();
        foreach (CraftSupplyDefinition s in Rules.Supplies)
            if (s.kind == CraftSupplyKind.Flux || s.kind == CraftSupplyKind.Cupel) { fluxes.Add(s.id); xn.Add(s.Name); }
        RectTransform r1 = Row(controls);
        if (QuickBench)
            Btn(r1, "Charge the piece", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Furnace }), 150f, UITheme.ButtonStyle.Primary);
        fuel = FuelDrop(r1, fuels, 160f);
        if (QuickBench)
            Btn(r1, "Add fuel", () => { AddFuel(); }, 100f);
        damper = Slide(r1, "Air vent", 0f, 1f, (float)Ws.Furnace.fire.damper, false, 64f);
        damper.onValueChanged.AddListener(v => Send(new CraftRequest(CraftRequestKind.Damper) { a = v, flag = true }));
        RectTransform r2 = Row(controls);
        flux = Drop(r2, xn.Count > 0 ? xn : new List<string> { "-" }, 0, 160f);
        if (QuickBench)
        {
            Btn(r2, "Add flux", AddFlux, 100f);
            Btn(r2, "Tap the slag", () => Send(new CraftRequest(CraftRequestKind.TapSlag) { a = 1f }), 120f);
            Btn(r2, "Pull the bloom", () => Send(new CraftRequest(CraftRequestKind.ExtractBloom)), 130f, UITheme.ButtonStyle.Primary);
        }
        RectTransform r3 = Row(controls);
        Text(r3, "Liquid metal (copper, lead, silver, cast iron) is run off in the Crucible area: choose a mould and pour.", 13f);
        info = InfoRow(controls, 40f);
    }

    private bool AddFuel()
    {
        if (fuels.Count == 0) return false;
        string id = fuels[Mathf.Clamp(fuel.value, 0, fuels.Count - 1)];
        if (Have(id) == 0) { ui.Message($"You have no {Rules.Fuel(id)?.Name ?? id}: pick a fuel you have (any wood burns as firewood)."); return false; }
        return Send(new CraftRequest(CraftRequestKind.AddFuel) { id = id, count = 2, flag = true });
    }

    private void AddFlux()
    {
        if (fluxes.Count > 0) Send(new CraftRequest(CraftRequestKind.Flux) { id = fluxes[Mathf.Clamp(flux.value, 0, fluxes.Count - 1)], count = 1, i0 = 0 });
    }

    private static float Seconds(Hold h) => h == Hold.Fuel ? 0.8f : h == Hold.Flux ? 1f : 1.2f;

    public override void Tick(float dt)
    {
        pump.Tick(dt);
        if (hold == Hold.None)
            return;
        Rect r = hold == Hold.Fuel ? heap : hold == Hold.Flux ? mouth : tapHole;
        if (!ui.PointerHeld || !r.Contains(ui.Hover)) { hold = Hold.None; return; }
        if ((holdTime += dt) < Seconds(hold))
            return;
        holdTime = 0f;
        switch (hold)
        {
            case Hold.Fuel: if (AddFuel()) said = $"Two shovels of {Rules.Fuel(fuels[Mathf.Clamp(fuel.value, 0, fuels.Count - 1)])?.Name.ToLowerInvariant()} in."; else hold = Hold.None; break; // keeps shovelling while held
            case Hold.Flux: AddFlux(); said = "A scoop of flux in."; hold = Hold.None; break;
            default:
                if (Send(new CraftRequest(CraftRequestKind.TapSlag) { a = 1f })) said = "The slag runs out of the tap hole, glowing.";
                hold = Hold.None;
                break;
        }
    }

    public override void Leave()
    {
        hold = Hold.None;
        dragging = pulling = false;
    }

    public override void Draw(CraftPixels c)
    {
        CraftFurnaceState f = Ws.Furnace;
        shaft = new Rect(240, 20, 240, 360);
        mouth = new Rect(shaft.xMin - 10, shaft.yMax - 40, shaft.width + 20, 50);
        tapHole = new Rect(shaft.xMax + 30, shaft.yMin + 10, 36, 30);
        heap = new Rect(20, 200, 160, 70);
        bench = new Rect(520, 200, 180, 80);
        c.Rect(shaft.xMin - 30, shaft.yMin, shaft.xMax + 30, shaft.yMax, new Color(0.45f, 0.3f, 0.22f));
        c.Rect(shaft.xMin, shaft.yMin + 10, shaft.xMax, shaft.yMax - 10, new Color(0.08f, 0.07f, 0.07f));
        Color glow = CraftArt.Heat(f.fire.temperature, out float k);
        c.Rect(shaft.xMin, shaft.yMin + 40, shaft.xMax, shaft.yMin + 200, new Color(glow.r, glow.g, glow.b, k * 0.8f));
        int n = 0;
        foreach (CraftPiece p in Ws.Pieces)
            if (p.location == CraftLocation.Furnace)
            {
                CraftOreDefinition o = p.kind == CraftPieceKind.Ore ? Rules.Ore(p.ore.ore) : null;
                Color col = o != null ? Color.Lerp(o.color, glow, k * 0.6f) : CraftArt.MetalColor(Rules, p);
                c.Disc(shaft.xMin + 40 + n % 4 * 50, shaft.yMax - 60 - n / 4 * 40, 16, col);
                n++;
            }
        float slag = Mathf.Clamp01((float)(f.slag / Math.Max(1, Rules.Settings.slagCapacity)));
        c.Rect(shaft.xMin, shaft.yMin + 10, shaft.xMax, shaft.yMin + 10 + slag * 60, new Color(0.25f, 0.18f, 0.12f));
        bloomAt = new Rect(shaft.center.x - 50, shaft.yMin + 10, 100, 70);
        if (f.BloomMass > 1 && !pulling)
            c.Disc(shaft.center.x, shaft.yMin + 40, Mathf.Clamp(Mathf.Sqrt((float)f.BloomMass) * 0.9f, 6f, 60f), Color.Lerp(new Color(0.35f, 0.33f, 0.32f), glow, k));
        if (pulling)
            c.Disc(dragAt.x, dragAt.y, Mathf.Clamp(Mathf.Sqrt((float)f.BloomMass) * 0.9f, 6f, 40f), Color.Lerp(new Color(0.35f, 0.33f, 0.32f), glow, k));
        if (f.PoolMass > 5)
            c.Rect(shaft.xMin + 20, shaft.yMin + 12, shaft.xMax - 20, shaft.yMin + 22, Color.Lerp(new Color(1f, 0.6f, 0.2f), Color.white, 0.2f));
        // The tap hole (plugged with clay), the charcoal heap, the bench with the ore.
        c.Rect(shaft.xMax, shaft.yMin + 18, tapHole.xMin, shaft.yMin + 30, new Color(0.2f, 0.12f, 0.08f));
        c.Disc(tapHole.center.x, tapHole.center.y, 12, slag > 0.2f ? new Color(0.7f, 0.35f, 0.15f) : new Color(0.5f, 0.42f, 0.32f));
        for (int i = 0; i < 22; i++)
            c.Disc(heap.xMin + 12 + (i * 29 % 136), heap.yMin + 8 + (i * 17 % 40), 7 + i % 3, new Color(0.12f, 0.11f, 0.1f));
        c.Rect(bench.xMin, bench.yMin, bench.xMax, bench.yMin + 6, Wood);
        CraftPiece sel = Selected;
        if (sel != null && sel.location == CraftLocation.Air && sel.kind == CraftPieceKind.Ore)
        {
            Vector2 at = dragging ? dragAt : bench.center;
            CraftOreDefinition o = Rules.Ore(sel.ore.ore);
            c.Disc(at.x, at.y, 22, o != null ? o.color : new Color(0.5f, 0.4f, 0.35f));
        }
        if (hold != Hold.None)
        {
            Rect r = hold == Hold.Fuel ? heap : hold == Hold.Flux ? mouth : tapHole;
            Meter(c, r.xMin, r.yMax + 3, r.width, 6, holdTime / Seconds(hold), Mark);
        }
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
        {
            Send(new CraftRequest(CraftRequestKind.Bellows) { a = (float)pump.Pump(Hands), flag = true });
            return;
        }
        if (QuickBench)
            return;
        CraftPiece sel = Selected;
        if (phase == CraftPointer.Down)
        {
            if (heap.Contains(pos)) { hold = Hold.Fuel; holdTime = 0f; return; }
            if (tapHole.Contains(pos)) { hold = Hold.Tap; holdTime = 0f; return; }
            if (mouth.Contains(pos)) { hold = Hold.Flux; holdTime = 0f; return; }
            if (bench.Contains(pos) && sel != null && sel.kind == CraftPieceKind.Ore && sel.location == CraftLocation.Air) { dragging = true; dragAt = pos; return; }
            if (bloomAt.Contains(pos) && Ws.Furnace.BloomMass > 1) { pulling = true; dragAt = pos; return; }
            return;
        }
        if (phase == CraftPointer.Drag) { dragAt = pos; return; }
        if (phase != CraftPointer.Up)
            return;
        hold = Hold.None;
        if (dragging)
        {
            dragging = false;
            if (mouth.Contains(pos) && sel != null && Send(new CraftRequest(CraftRequestKind.Move, sel.id) { location = CraftLocation.Furnace }))
                said = "Charged into the shaft, on a layer of charcoal.";
        }
        if (pulling)
        {
            pulling = false;
            if (!shaft.Contains(pos) && Send(new CraftRequest(CraftRequestKind.ExtractBloom)))
                said = "The bloom comes out, a glowing spongy lump: work it at the anvil while it is soft.";
        }
    }

    public override void Refresh()
    {
        RefreshCounts(fuel, fuels);
        if (damper != null) damper.SetValueWithoutNotify((float)Ws.Furnace.fire.damper);
        if (info == null) return;
        string how = QuickBench ? "" : "Drag ore from the bench into the mouth (top); hold on the heap to shovel charcoal, on the mouth to add flux, on the tap hole (right) to run the slag; drag the bloom out of the bottom.";
        info.text = (how + " " + said).Trim();
    }
}

/// <summary>
/// The crucible: melt metals together (proportions, not names, make the alloy), cover the melt against the air, stir
/// it even, skim the dross, then pour: press on the crucible and lift the pointer to tilt it - a steady, unbroken stream
/// fills the mould soundly; too slow it freezes short, too fast it traps air, a broken stream leaves a cold shut.
/// <para>Every step is done with the hands - choose the tool: the <b>tongs</b> (drag a piece from the bench into the
/// crucible, or a piece that has not melted yet back out), the <b>cover</b> (sprinkle it over the whole melt), the
/// <b>stirring rod</b> (steady circles in the melt), the <b>skimmer</b> (drag slowly from the middle out over the rim)
/// and the <b>pour</b>.</para>
/// </summary>
public sealed class CraftCrucibleMode : CraftMode
{
    private enum Tool { Tongs, Cover, Stir, Skim, Pour }

    private TMP_Dropdown mold, cover, source;
    private TextMeshProUGUI info;
    private readonly List<string> molds = new List<string>(), covers = new List<string>();
    private readonly List<Button> toolButtons = new List<Button>();
    private Tool tool = Tool.Pour;
    private CraftPourControl pour;
    private float tiltStartY, tilt;
    // Pouring by the window is the harder way: the crucible is carried over the mould by hand (the pointer's side to
    // side), the stream arcs further the faster it runs and the hands drift, and it must land in the mould's mouth.
    private float startX, shift, drift, landX, pourClock;
    // The pouring cup's hole: the stream has to land within it.
    private const float MouthRadius = 12f;
    private Vector2 mouth;
    private readonly List<Vector2> splashes = new List<Vector2>();
    private CraftMold shownMold;
    private Rect crucible, mould, bench;
    private readonly CraftCoverage sprinkle = new CraftCoverage();
    private readonly CraftSwirl stir = new CraftSwirl();
    private bool dragging, liftingOut, skimming, sprinkling;
    private string lifted;
    private Vector2 dragAt;
    private float skimStart;
    private string said = "";
    private static readonly string[] ToolNames = { "Tongs", "Cover", "Stirring rod", "Skimmer", "Pour" };

    public override string Title => "Crucible";
    public override bool Available(CraftStation s) => s.Has(CraftTools.Crucible) || s.Has(CraftTools.Furnace);
    public override string Hint => "Choose a tool: tongs to put metal in (or lift it out), cover, stir, skim; then pour - press on the crucible and lift to tilt it, keep the stream steady." + ByHandHint("pour by hand");
    public override CraftHandsOnMode HandsOnMode => CraftHandsOnMode.Pour;

    protected override void FillHandsOn(ref CraftHandsOnSetup setup)
    {
        setup.mold = molds.Count > 0 ? molds[Mathf.Clamp(mold.value, 0, molds.Count - 1)] : null;
        setup.fromFurnace = FromFurnace;
    }

    public override void Build(RectTransform controls)
    {
        molds.Clear();
        var mn = new List<string>();
        foreach (CraftMold m in Rules.AllMolds()) { molds.Add(m.id); mn.Add(m.Name); } // the stock ones, then one for each design's part
        covers.Clear();
        var cn = new List<string>();
        foreach (CraftSupplyDefinition s in Rules.Supplies)
            if (s.kind == CraftSupplyKind.Flux) { covers.Add(s.id); cn.Add(s.Name); }
        RectTransform r0 = Row(controls, 30f, 4f);
        toolButtons.Clear();
        for (int i = 0; i < ToolNames.Length; i++)
        {
            Tool t = (Tool)i;
            toolButtons.Add(Btn(r0, ToolNames[i], () => { tool = t; ShowTool(); }, 0f, UITheme.ButtonStyle.Tab, 28f));
        }
        RectTransform r1 = Row(controls);
        Text(r1, "Cover", 13f, 46f);
        cover = Drop(r1, cn.Count > 0 ? cn : new List<string> { "-" }, 0, 150f);
        if (QuickBench)
        {
            Btn(r1, "Into the crucible", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Melt }), 150f, UITheme.ButtonStyle.Primary);
            Btn(r1, "Lift out", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Air }), 80f);
            Btn(r1, "Cover it", () => { if (covers.Count > 0) Send(new CraftRequest(CraftRequestKind.Flux) { id = covers[cover.value], count = 1, i0 = 1, a = 1f }); }, 90f);
            Btn(r1, "Stir", () => Send(new CraftRequest(CraftRequestKind.StirMelt) { a = 1f }), 60f);
            Btn(r1, "Skim", () => Send(new CraftRequest(CraftRequestKind.Skim) { a = 0.8f }), 60f);
        }
        RectTransform r2 = Row(controls);
        Text(r2, "Mould", 13f, 50f);
        mold = Drop(r2, mn, 0, 200f);
        var sources = new List<string>();
        if (Station.Has(CraftTools.Crucible)) sources.Add("from the crucible");
        if (Station.Has(CraftTools.Furnace)) sources.Add("from the furnace");
        source = Drop(r2, sources, 0, 180f);
        HandsOnButton(r2, "Pour by hand", 150f);
        info = InfoRow(controls, 40f);
        ShowTool();
    }

    private void ShowTool()
    {
        for (int i = 0; i < toolButtons.Count; i++) UITheme.SetSelected(toolButtons[i], (Tool)i == tool);
        stir.Clear();
        sprinkling = skimming = dragging = liftingOut = false;
    }

    private bool FromFurnace => source != null && source.options.Count > 0 && source.options[source.value].text.Contains("furnace");

    public override void Tick(float dt)
    {
        if (tool == Tool.Cover && sprinkling && ui.PointerHeld && crucible.Contains(ui.Hover))
            sprinkle.Paint(Mathf.Clamp((int)((ui.Hover.x - crucible.xMin) / crucible.width * 8), 0, 7), dt * 1.6f);
        if (pour == null)
            return;
        if (!ui.PointerHeld)
        {
            Finish();
            return;
        }
        Vector2 h = ui.Hover;
        if (h.y >= 0) tilt = Mathf.Clamp01((h.y - tiltStartY) / 160f);
        if (h.x >= 0) shift = Mathf.Clamp(h.x - startX, -60f, 260f);
        // The hands drift slowly (a new waver every third of a second): tired or unpractised hands drift further.
        pourClock += dt;
        float wander = (float)Hands.Tremble((int)(pourClock * 3f) + 51) * 650f;
        drift = Mathf.Lerp(drift, wander, Mathf.Min(1f, dt * 3f));
        landX = crucible.xMax + 18f + 95f * (float)pour.Rate + drift;
        bool onTarget = Mathf.Abs(landX - mouth.x) <= MouthRadius;
        double before = pour.Spilled + pour.Overflow;
        pour.Tick(dt, tilt, Hands, 4, onTarget);
        if (pour.Spilled + pour.Overflow > before + 0.004 && splashes.Count < 60)
            splashes.Add(new Vector2(landX + UnityEngine.Random.Range(-14f, 14f), mouth.y + UnityEngine.Random.Range(-30f, 30f)));
        // Full: tip it back. It is done once the stream stops (keep pouring and it runs over the top).
        if (pour.Full && pour.Rate < 0.04)
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
        // A mould left part-filled is a short casting: the steadiness and breaks are what the hands did; what missed the
        // mouth is lost on the floor, what ran over the top leaves flash.
        float rate = (float)p.MeanRate * (float)Math.Max(0.3, p.Poured);
        float spilt = (float)p.SpillShare, over = (float)Math.Min(1, p.Overflow * 2);
        int share = Mathf.Clamp(Mathf.RoundToInt((float)p.Poured * 1000f), 1, 1000);
        if (Send(FromFurnace
            ? new CraftRequest(CraftRequestKind.TakeMelt) { id = m, a = rate, b = (float)p.Steadiness, c = spilt, d = over, i1 = share }
            : new CraftRequest(CraftRequestKind.Pour) { id = m, a = rate, b = (float)p.Steadiness, i0 = p.Interruptions, c = spilt, d = over, i1 = share }))
            said = (p.Poured < 0.95 ? "The mould is not full: a short casting. " : "") + (spilt > 0.1f ? $"A good deal splashed on the sand ({spilt * 100:0}% as much again as went in). " : "")
                + (over > 0.05f ? "It ran over the top: file the flash off. " : "") + (p.Interruptions > 0 ? "The stream broke: a cold shut. " : "")
                + (p.Poured >= 0.95 && spilt <= 0.1f && over <= 0.05f && p.Interruptions == 0 ? "A clean pour, right into the mouth." : "");
    }

    public override void Leave()
    {
        pour = null;
        tilt = shift = 0f;
        ShowTool();
    }

    public override void Draw(CraftPixels c)
    {
        CraftCrucibleState cs = Ws.Crucible;
        crucible = new Rect(160 + (pour != null ? shift : 0f), 120, 200, 200); // carried over the mould while pouring
        bench = new Rect(430, 250, 270, 120);
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
        float level = Mathf.Clamp01((float)(mass / 2500.0)) * 140f + 20f;
        if (mass > 1)
        {
            var melt = new List<Vector2> { Rot(crucible.xMin + 34, crucible.yMin + 10 + level), Rot(crucible.xMin + 34, crucible.yMin + 10), Rot(crucible.xMax - 34, crucible.yMin + 10), Rot(crucible.xMax - 34, crucible.yMin + 10 + level) };
            c.Polygon(melt, Color.Lerp(new Color(0.4f, 0.38f, 0.36f), glow, k));
            if (cs.dross > 5 && !FromFurnace) c.Rect(crucible.xMin + 40, crucible.yMin + 10 + level - 6, crucible.xMax - 40, crucible.yMin + 10 + level, new Color(0.3f, 0.28f, 0.25f, 0.9f));
            if (cs.cover > 0.3 && !FromFurnace) c.Rect(crucible.xMin + 40, crucible.yMin + 10 + level, crucible.xMax - 40, crucible.yMin + 16 + level, new Color(0.7f, 0.75f, 0.8f, 0.6f));
        }
        // Pieces in it that have not melted yet.
        foreach (CraftPiece p in Ws.Pieces)
            if (p.location == CraftLocation.Melt && p.id != lifted)
                c.Rect(crucible.xMin + 60, crucible.yMin + 30, crucible.xMax - 60, crucible.yMin + 60, Color.Lerp(CraftArt.MetalColor(Rules, p), CraftArt.Heat(p.MaxTemperature, out float g), g));
        // The bench, with the selected piece on it (the tongs carry it).
        if (pour == null) c.Rect(bench.xMin, bench.yMin, bench.xMax, bench.yMin + 8, Wood); // out of the way while pouring
        CraftPiece sel = Selected;
        if (sel != null && sel.location == CraftLocation.Air && (sel.kind == CraftPieceKind.Metal || sel.kind == CraftPieceKind.Ore || sel.kind == CraftPieceKind.Fasteners) && sel.segments.Count > 0)
        {
            float mm = Fit(sel, 240, 50);
            float len = (float)(sel.Length / mm);
            Vector2 at = dragging ? dragAt - new Vector2(len * 0.5f, 25f) : new Vector2(bench.center.x - len * 0.5f, bench.yMin + 12);
            CraftArt.DrawTop(c, Rules, sel, new Rect(at.x, at.y, len, 50), mm);
        }
        if (liftingOut) c.Rect(dragAt.x - 30, dragAt.y - 12, dragAt.x + 30, dragAt.y + 12, Iron);
        // The tool in the hand.
        Vector2 h = ui.Hover;
        if (crucible.Contains(h))
        {
            switch (tool)
            {
                case Tool.Stir: c.Line(h.x, h.y, h.x + 30, h.y + 90, new Color(0.5f, 0.45f, 0.4f), 4f); break;
                case Tool.Skim: c.Line(h.x - 20, h.y, h.x + 20, h.y, Iron, 5f); c.Line(h.x + 20, h.y, h.x + 60, h.y + 70, Iron, 3f); break;
                case Tool.Cover:
                    if (ui.PointerHeld) for (int i = 0; i < 10; i++) c.Disc(h.x + (i * 7 % 11 - 5) * 2f, h.y - i * 4f, 1.4f, new Color(0.95f, 0.95f, 0.9f));
                    break;
            }
        }
        if (tool == Tool.Cover && sprinkling)
            Meter(c, crucible.xMin, crucible.yMin - 12, crucible.width, 6, sprinkle.Mean(), new Color(0.9f, 0.92f, 0.95f));
        if (tool == Tool.Stir && stir.Active)
            Meter(c, crucible.xMin, crucible.yMin - 12, crucible.width, 6, (float)cs.mixed, new Color(0.6f, 0.85f, 1f));
        DrawMould(c, glow, pivot);
    }

    /// <summary>
    /// The mould in its sand box, seen from above: its cavity has the outline of what it casts, with the mouth (the
    /// sprue) at the left end; the metal runs in there and creeps along it. Splashes on the sand are what missed.
    /// </summary>
    private void DrawMould(CraftPixels c, Color glow, Vector2 lip)
    {
        mould = new Rect(450, 24, 250, 120);
        c.Rect(mould.xMin, mould.yMin, mould.xMax, mould.yMax, new Color(0.5f, 0.42f, 0.3f));
        string id = molds.Count > 0 && mold != null ? molds[Mathf.Clamp(mold.value, 0, molds.Count - 1)] : null;
        shownMold = id != null ? Rules.Mold(id) : null;
        mouth = new Vector2(mould.xMin + 24f, mould.center.y);
        // The pouring cup: a raised clay funnel on the sprue - the mouth the stream must go into. A pulsing ring and a
        // guide line straight up from it show where to bring the stream (green while it lands in the cup).
        bool aimed = pour != null && pour.Rate > 0.04 && Mathf.Abs(landX - mouth.x) <= MouthRadius;
        Color mark = aimed ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.82f, 0.3f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        c.Disc(mouth.x, mouth.y, 20f, new Color(0.8f, 0.66f, 0.48f));
        c.Ring(mouth.x, mouth.y, 20f, 2f, new Color(0.55f, 0.43f, 0.3f));
        c.Disc(mouth.x, mouth.y, MouthRadius, new Color(0.06f, 0.05f, 0.04f));
        c.Ring(mouth.x, mouth.y, 24f + 3f * pulse, 2f, mark);
        for (float y = mould.yMax + 36f; y < 392f; y += 14f)
            c.Line(mouth.x, y, mouth.x, y + 7f, new Color(mark.r, mark.g, mark.b, 0.45f), 1.5f);
        c.Line(mouth.x - 9f, mould.yMax + 50f, mouth.x, mould.yMax + 38f, mark, 2.5f); // an arrow down onto the cup
        c.Line(mouth.x + 9f, mould.yMax + 50f, mouth.x, mould.yMax + 38f, mark, 2.5f);
        float full = pour != null ? (float)pour.Poured : 0f;
        Color hot = Color.Lerp(glow, Color.white, 0.2f);
        if (shownMold != null)
        {
            CraftShapeTarget shape = shownMold.profile != null && shownMold.profile.Count >= 2 ? new CraftShapeTarget { profile = shownMold.profile } : null;
            float widest = shownMold.width;
            if (shape != null) foreach (CraftProfilePoint q in shownMold.profile) widest = Mathf.Max(widest, q.width);
            float x0 = mouth.x + 22f, room = mould.xMax - 10f - x0;
            float k = Mathf.Min(room / Mathf.Max(1f, shownMold.length), (mould.height - 24f) / Mathf.Max(1f, widest)); // px per mm
            float len = shownMold.length * k;
            c.Rect(mouth.x, mouth.y - 3f, x0, mouth.y + 3f, new Color(0.08f, 0.06f, 0.05f)); // the runner
            if (full > 0f) c.Rect(mouth.x, mouth.y - 2f, x0, mouth.y + 2f, hot);
            const int n = 40;
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n, w = shownMold.width;
                if (shape != null) shape.At(t, out w, out _);
                float hw = Mathf.Max(1.5f, w * k * 0.5f), xa = x0 + len * i / n, xb = x0 + len * (i + 1) / n + 0.6f;
                c.Rect(xa, mouth.y - hw, xb, mouth.y + hw, new Color(0.1f, 0.08f, 0.07f));
                if (full * n > i) c.Rect(xa, mouth.y - hw + 0.8f, xb, mouth.y + hw - 0.8f, hot);
            }
            if (shownMold.eyeAt >= 0f && shownMold.eyeDiameter > 0f)
                c.Disc(x0 + len * shownMold.eyeAt, mouth.y, shownMold.eyeDiameter * k * 0.5f, new Color(0.5f, 0.42f, 0.3f)); // the core that makes the eye
            if (pour != null && pour.Overflow > 0.01)
                c.Rect(x0 - 4f, mouth.y - Mathf.Min(50f, widest * k * 0.5f + 4f + (float)pour.Overflow * 60f), x0 + len + 4f, mouth.y - widest * k * 0.5f - 1f, new Color(hot.r, hot.g, hot.b, 0.5f));
        }
        foreach (Vector2 sp in splashes) c.Disc(sp.x, sp.y, 2.5f, new Color(glow.r * 0.6f + 0.2f, glow.g * 0.5f + 0.15f, glow.b * 0.4f + 0.1f));
        if (pour != null)
        {
            if (pour.Rate > 0.04)
            {
                // The stream arcs from the lip down to where it lands.
                // It leaves the lip running outward and falls: an arc, bending down.
                Vector2 to = new Vector2(landX, mouth.y), mid = new Vector2(lip.x + (landX - lip.x) * 0.7f, lip.y - (lip.y - mouth.y) * 0.25f);
                float wdt = 2f + 8f * (float)pour.Rate;
                c.Line(lip.x, lip.y, mid.x, mid.y, glow, wdt);
                c.Line(mid.x, mid.y, to.x, to.y, glow, wdt);
                bool on = Mathf.Abs(landX - mouth.x) <= MouthRadius;
                c.Ring(landX, mouth.y, 6f, 1.5f, on ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.35f, 0.25f));
            }
            Meter(c, mould.xMin, mould.yMax + 8, mould.width, 8, full, full >= 1f ? new Color(1f, 0.4f, 0.3f) : new Color(1f, 0.8f, 0.4f));
            Meter(c, mould.xMin, mould.yMax + 20, mould.width, 8, (float)pour.Steadiness, new Color(0.5f, 0.85f, 0.6f));
        }
        else if (tool == Tool.Pour)
        {
            // Before tipping: where a moderate stream from the lip would land now (carry the crucible right to the cup).
            float x = crucible.xMax + 18f + 95f * 0.35f;
            for (float y = mouth.y + 10f; y < lip.y; y += 12f) c.Line(x, y, x, y + 5f, new Color(1f, 0.8f, 0.4f, 0.35f), 1.5f);
            c.Ring(x, mouth.y, 6f, 1.5f, new Color(1f, 0.8f, 0.4f, 0.6f));
        }
    }

    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece sel = Selected;
        switch (tool)
        {
            case Tool.Pour:
                if (phase == CraftPointer.Down && crucible.Contains(pos))
                {
                    pour = new CraftPourControl();
                    tiltStartY = pos.y;
                    startX = pos.x;
                    tilt = shift = drift = pourClock = 0f;
                    splashes.Clear();
                    said = "";
                }
                return;
            case Tool.Tongs:
                if (phase == CraftPointer.Down)
                {
                    if (bench.Contains(pos) && sel != null && sel.location == CraftLocation.Air) { dragging = true; dragAt = pos; }
                    else if (crucible.Contains(pos))
                    {
                        // A piece that has not melted yet: lift it out (the selected one if it is in, else the first).
                        CraftPiece inside = sel != null && sel.location == CraftLocation.Melt ? sel : null;
                        if (inside == null)
                            foreach (CraftPiece x in Ws.Pieces)
                                if (x.location == CraftLocation.Melt) { inside = x; break; }
                        if (inside != null) { liftingOut = true; lifted = inside.id; dragAt = pos; }
                    }
                }
                else if (phase == CraftPointer.Drag) dragAt = pos;
                else if (phase == CraftPointer.Up)
                {
                    if (dragging && crucible.Contains(pos) && sel != null)
                    {
                        Send(new CraftRequest(CraftRequestKind.Move, sel.id) { location = CraftLocation.Melt });
                        said = "Into the crucible: it melts when the crucible is hot enough for it.";
                    }
                    if (liftingOut && !crucible.Contains(pos) && lifted != null)
                    {
                        Send(new CraftRequest(CraftRequestKind.Move, lifted) { location = CraftLocation.Air });
                        said = "Lifted out with the tongs.";
                    }
                    dragging = liftingOut = false;
                    lifted = null;
                }
                return;
            case Tool.Cover:
                if (phase == CraftPointer.Down && crucible.Contains(pos)) { sprinkling = true; sprinkle.Reset(8); }
                else if (phase == CraftPointer.Up && sprinkling)
                {
                    sprinkling = false;
                    float cov = sprinkle.Mean();
                    if (cov > 0.05f && covers.Count > 0)
                    {
                        Send(new CraftRequest(CraftRequestKind.Flux) { id = covers[Mathf.Clamp(cover.value, 0, covers.Count - 1)], count = 1, i0 = 1, a = cov });
                        said = cov > 0.85f ? "A whole cover over the melt: the air is kept off it." : $"Only {cov * 100:0}% covered: the bare melt takes up air and dross.";
                    }
                }
                return;
            case Tool.Stir:
                if (phase == CraftPointer.Down && crucible.Contains(pos)) stir.Begin(crucible.center);
                if ((phase == CraftPointer.Drag || phase == CraftPointer.Down) && stir.Active)
                {
                    stir.Add(pos);
                    if (stir.Turns >= 2f)
                    {
                        float strength = 0.4f + 0.6f * stir.Steadiness;
                        stir.Spend(2f);
                        if (Send(new CraftRequest(CraftRequestKind.StirMelt) { a = strength }))
                            said = strength > 0.8f ? "Wide, steady circles: it mixes evenly." : "An uneven stir: it mixes slowly.";
                    }
                }
                if (phase == CraftPointer.Up) stir.Clear();
                return;
            case Tool.Skim:
                if (phase == CraftPointer.Down && crucible.Contains(pos)) { skimming = true; skimStart = Time.unscaledTime; }
                else if (phase == CraftPointer.Up && skimming)
                {
                    skimming = false;
                    if (!crucible.Contains(pos))
                    {
                        float care = Mathf.Clamp01(0.3f + 0.7f * Mathf.Clamp01((Time.unscaledTime - skimStart) / 0.9f));
                        if (Send(new CraftRequest(CraftRequestKind.Skim) { a = care }))
                            said = care > 0.8f ? "A slow, careful skim: the dross comes off with little metal." : "A hasty skim: some metal went with the dross.";
                    }
                }
                return;
        }
    }

    public override void Refresh()
    {
        if (info == null) return;
        string how;
        switch (tool)
        {
            case Tool.Tongs: how = "<b>Tongs</b>: drag the piece from the bench into the crucible; drag a piece that has not melted yet out of it to lift it out."; break;
            case Tool.Cover: how = "<b>Cover</b>: hold and sweep over the crucible to sprinkle the cover across the whole melt."; break;
            case Tool.Stir: how = "<b>Stirring rod</b>: hold and draw steady circles in the crucible."; break;
            case Tool.Skim: how = "<b>Skimmer</b>: press in the middle of the melt and drag slowly out over the rim."; break;
            default:
                how = pour == null ? "<b>Pour</b>: press on the crucible; lift the pointer to tip it and carry it right until the stream comes down the dotted line into the clay pouring cup (the ring turns green). Tip it back as soon as it is full."
                    : pour.Full ? (pour.Rate > 0.04 ? "<color=#ff8a70>Full: tip it back now - it is running over!</color>" : "Full.")
                    : pour.Rate < 0.04 ? "Tip it further." : Mathf.Abs(landX - mouth.x) <= MouthRadius ? "Into the mouth: keep it there, steady." : "<color=#ff8a70>Missing the mouth: it splashes on the sand!</color>";
                break;
        }
        info.text = (how + " " + said).Trim();
    }
}
