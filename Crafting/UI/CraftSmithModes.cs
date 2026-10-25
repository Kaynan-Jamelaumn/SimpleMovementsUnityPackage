using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The forge: feed the fire, work the bellows (each pump a puff; a steady rhythm builds the draft, frantic pumping only
/// tires the arm), open or choke the air, and put the work in the fire - all of it, or only the part dragged on - deep
/// in the coals or near the edge. The coals glow with the fire's heat; the metal glows with its own.
/// </summary>
public sealed class CraftForgeMode : CraftMode
{
    private readonly CraftBellowsPump pump = new CraftBellowsPump();
    private Slider depth, damper;
    private TMP_Dropdown fuel;
    private readonly List<string> fuels = new List<string>();
    private int spanFrom = -1, spanTo = -1, dragStart = -1;
    private float x0, mm;
    private Rect bellowsArea, hearth;
    private float puffShown;

    public override string Title => "Forge";
    public override bool Available(CraftStation s) => s.Has(CraftTools.Forge);
    public override string Hint => "Click the bellows in a steady rhythm. Drag along the piece to choose what goes in the fire, then click the coals.";

    public override void Build(RectTransform controls)
    {
        RectTransform r1 = Row(controls);
        fuels.Clear();
        var names = new List<string>();
        foreach (CraftFuel f in Rules.Fuels) { fuels.Add(f.id); names.Add(f.Name); }
        fuel = Drop(r1, names, 0, 180f);
        Btn(r1, "Add fuel", () => AddFuel(1), 110f);
        Btn(r1, "+3", () => AddFuel(3), 50f);
        damper = Slide(r1, "Air vent", 0f, 1f, (float)Ws.Forge.damper);
        damper.onValueChanged.AddListener(v => Send(new CraftRequest(CraftRequestKind.Damper) { a = v }));
        RectTransform r2 = Row(controls);
        depth = Slide(r2, "Depth", 0.2f, 1f, 0.7f);
        Btn(r2, "Into the fire", PutIn, 130f, UITheme.ButtonStyle.Primary);
        Btn(r2, "Out", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Air }), 70f);
        Btn(r2, "Whole piece", () => { spanFrom = spanTo = -1; }, 110f, UITheme.ButtonStyle.Ghost);
        RectTransform r3 = Row(controls);
        Btn(r3, "Flux it (borax)", () => Send(new CraftRequest(CraftRequestKind.Flux, ui.SelectedId) { id = Rules.FirstSupply(CraftSupplyKind.Flux)?.id ?? "borax", i0 = 2, count = 1 }), 150f);
        Btn(r3, "Fold and weld", () => Send(new CraftRequest(CraftRequestKind.Fold, ui.SelectedId)), 140f);
        Btn(r3, "Weld to…", WeldToOther, 100f);
        Btn(r3, "Into the ashes", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Ashes }), 140f);
    }

    private void AddFuel(int n)
    {
        if (fuels.Count == 0) return;
        Send(new CraftRequest(CraftRequestKind.AddFuel) { id = fuels[Mathf.Clamp(fuel.value, 0, fuels.Count - 1)], count = n });
    }

    private void PutIn()
    {
        CraftPiece p = Selected;
        if (p == null) { ui.Message("Pick a piece first."); return; }
        var r = new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Fire, a = depth.value };
        if (spanFrom >= 0) { r.i0 = Mathf.Min(spanFrom, spanTo); r.i1 = Mathf.Max(spanFrom, spanTo); }
        Send(r);
    }

    private void WeldToOther()
    {
        CraftPiece p = Selected;
        if (p == null) return;
        foreach (CraftPiece q in Ws.Pieces)
            if (q != p && q.kind == CraftPieceKind.Metal && q.location == CraftLocation.Air && q.MaxTemperature > 700)
            {
                Send(new CraftRequest(CraftRequestKind.Weld, p.id) { other = q.id });
                return;
            }
        ui.Message("Another hot piece must be on the anvil to weld it to.");
    }

    public override void Tick(float dt)
    {
        pump.Tick(dt);
        puffShown = Mathf.Max(0f, puffShown - dt * 3f);
    }

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
        // The selected piece, above the fire (sunk in the coals when it is in).
        CraftPiece p = Selected;
        if (IsMetal(p))
        {
            mm = Fit(p, 560, 90);
            x0 = 200 + (470 - (float)(p.Length / mm)) * 0.5f;
            float y = p.location == CraftLocation.Fire ? hearth.yMin + 30 : 260;
            CraftArt.DrawTop(c, Rules, p, new Rect(x0, y, (float)(p.Length / mm), 90), mm);
            if (spanFrom >= 0 && p.location != CraftLocation.Fire)
            {
                float a = SegX(p, Mathf.Min(spanFrom, spanTo)), b = SegX(p, Mathf.Max(spanFrom, spanTo) + 1);
                c.Frame(a, y - 50, b, y + 50, Mark, 2f);
            }
            if (p.location == CraftLocation.Fire)
            {
                float a = SegX(p, Mathf.Clamp(p.from, 0, p.segments.Count - 1)), b = SegX(p, Mathf.Clamp(p.to, 0, p.segments.Count - 1) + 1);
                c.Rect(a, hearth.yMin + 20, b, hearth.yMin + 26, new Color(glow.r, glow.g, glow.b, 0.6f));
            }
        }
    }

    private float SegX(CraftPiece p, int i)
    {
        double pos = 0;
        for (int k = 0; k < i && k < p.segments.Count; k++) pos += p.segments[k].length;
        return x0 + (float)(pos / mm);
    }

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
        CraftPiece p = Selected;
        if (!IsMetal(p))
            return;
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

    public override void Refresh()
    {
        if (damper != null) damper.SetValueWithoutNotify((float)Ws.Forge.damper);
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
    private string lastBlow = "";
    private static readonly CraftStrikeTool[] ToolOrder =
    {
        CraftStrikeTool.Flat, CraftStrikeTool.PeenAcross, CraftStrikeTool.PeenAlong, CraftStrikeTool.Fuller, CraftStrikeTool.Horn,
        CraftStrikeTool.Hardy, CraftStrikeTool.Drift, CraftStrikeTool.Flatter, CraftStrikeTool.Dishing, CraftStrikeTool.Straighten,
    };

    public override string Title => "Anvil";
    public override bool Available(CraftStation s) => s.Has(CraftTools.Anvil);
    public override string Hint => "Hold on the bar to raise the hammer, release to strike. Above or below the line tilts the blow (a bevel). Strike while it glows.";

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
        Btn(r3, "Cut here (chisel)", () => Send(new CraftRequest(CraftRequestKind.Split, ui.SelectedId) { a = aim }), 150f);
        Btn(r3, "Make rivets", () => Send(new CraftRequest(CraftRequestKind.FormFastener, ui.SelectedId) { i0 = (int)CraftFastenerKind.Rivet }), 110f);
        Btn(r3, "Bend a buckle", () => Send(new CraftRequest(CraftRequestKind.FormFastener, ui.SelectedId) { i0 = (int)CraftFastenerKind.Buckle }), 120f);
        RectTransform r4 = Row(controls);
        blowText = Text(r4, "");
        ShowSelection();
    }

    private void ShowSelection()
    {
        for (int i = 0; i < toolButtons.Count; i++) UITheme.SetSelected(toolButtons[i], ToolOrder[i] == tool);
        TMP_Text t = faceButton != null ? faceButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (t != null) t.text = face == CraftStrikeFace.Flat ? "On its face" : "On its edge";
    }

    public override void Tick(float dt) => hammer.Tick(dt);

    public override void Leave() => hammer.Lower();

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
        mm = Fit(p, 560, 110);
        float len = (float)(p.Length / mm);
        x0 = 170 + (470 - len) * 0.5f;
        CraftArt.DrawSide(c, Rules, p, new Rect(x0, 160, len, 40), mm, 3f);
        CraftArt.DrawTop(c, Rules, p, new Rect(x0, lineY + 40, len, 100), mm);
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
        else if (phase == CraftPointer.Up && hammer.Raised)
        {
            CraftBlow blow = hammer.Release(aim, tilt, Hands);
            if (Send(new CraftRequest(CraftRequestKind.Strike, p.id) { a = (float)blow.position, i0 = (int)tool, i1 = (int)face, b = (float)blow.force, c = (float)blow.tilt }))
            {
                lastBlow = $"{(blow.force > 0.9 ? "A heavy" : blow.force > 0.5 ? "A firm" : "A light")} blow{(blow.rushed > 0.3 ? ", rushed" : "")}{(blow.miss > 0.03 ? ", off its mark" : "")}.";
                foreach (CraftEvent e in Ws.RecentEvents)
                    if (e.Kind == CraftEventKind.Struck && e.Piece == p.id) lastBlow += " " + CraftLook.StrikeSound(e.Id);
            }
        }
    }

    public override void Refresh()
    {
        if (blowText != null) blowText.text = lastBlow;
    }
}

/// <summary>
/// Quenching and tempering: plunge the hot work into the tank (edge first, point first or flat), keep it moving - drag
/// up and down, side to side or in circles in the tank; held still, a vapour jacket slows the quench and warps it -
/// and lift it out. The oven tempers at a set heat; buried in the ashes it anneals slowly; clay along the spine keeps
/// it softer there.
/// </summary>
public sealed class CraftQuenchMode : CraftMode
{
    private Slider depth, oven, clay;
    private TMP_Dropdown entry;
    private Rect tank, ovenBox, ashBox;
    private readonly Queue<Vector2> moves = new Queue<Vector2>();
    private CraftQuenchMotion sent = CraftQuenchMotion.Still;
    private float motionTimer;
    private int clayFrom = -1, clayTo = -1;
    private float x0, mm;

    public override string Title => "Quench";
    public override bool Available(CraftStation s) => s.Has(CraftTools.QuenchTank) || s.Has(CraftTools.Oven) || s.Has(CraftTools.Ashes);
    public override string Hint => "Click the tank to plunge it, drag inside to keep it moving, Lift out. Drag on the piece to choose where clay goes.";

    public override void Build(RectTransform controls)
    {
        RectTransform r1 = Row(controls);
        entry = Drop(r1, new[] { "Edge first", "Point first", "Flat" }, 0, 140f);
        depth = Slide(r1, "Depth", 0.2f, 1f, 1f);
        Btn(r1, "Plunge", Plunge, 100f, UITheme.ButtonStyle.Primary);
        Btn(r1, "Lift out", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Air }), 100f);
        RectTransform r2 = Row(controls);
        oven = Slide(r2, "Oven °C", 100f, 650f, Mathf.Max(150f, (float)Ws.Oven.setpoint), true);
        Btn(r2, "Light the oven", () => Send(new CraftRequest(CraftRequestKind.Oven) { a = oven.value }), 130f);
        Btn(r2, "Into the oven", () => Send(new CraftRequest(CraftRequestKind.Move, ui.SelectedId) { location = CraftLocation.Oven }), 120f);
        Btn(r2, "Oven off", () => Send(new CraftRequest(CraftRequestKind.Oven) { a = 0 }), 90f, UITheme.ButtonStyle.Ghost);
        RectTransform r3 = Row(controls);
        clay = Slide(r3, "Clay mm", 0.5f, 5f, 2f);
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

    private void Plunge()
    {
        CraftPiece p = Selected;
        if (p == null) { ui.Message("Pick a piece first."); return; }
        float angle = entry.value == 0 ? 0f : entry.value == 1 ? 0.1f : 0.9f;
        sent = CraftQuenchMotion.Still;
        Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Quench, a = depth.value, b = angle });
    }

    public override void Draw(CraftPixels c)
    {
        CraftQuenchant m = Rules.Quenchant(Ws.Quench.medium);
        tank = new Rect(40, 20, 380, 200);
        c.Rect(tank.xMin - 8, tank.yMin - 8, tank.xMax + 8, tank.yMax, new Color(0.3f, 0.22f, 0.15f));
        Color medium = m != null && m.id.Contains("oil") ? new Color(0.45f, 0.35f, 0.12f, 0.9f) : m != null && m.id.Contains("blood") ? new Color(0.5f, 0.05f, 0.05f, 0.9f) : Water;
        c.Rect(tank.xMin, tank.yMin, tank.xMax, tank.yMax - 20, medium);
        ovenBox = new Rect(460, 20, 220, 150);
        c.Rect(ovenBox.xMin, ovenBox.yMin, ovenBox.xMax, ovenBox.yMax, Iron);
        float warm = Mathf.Clamp01((float)((Ws.Oven.temperature - Ws.Ambient) / 600.0));
        c.Rect(ovenBox.xMin + 15, ovenBox.yMin + 15, ovenBox.xMax - 15, ovenBox.yMax - 15, Color.Lerp(new Color(0.08f, 0.08f, 0.09f), new Color(0.45f, 0.18f, 0.08f), warm));
        ashBox = new Rect(460, 190, 220, 50);
        c.Rect(ashBox.xMin, ashBox.yMin, ashBox.xMax, ashBox.yMax, new Color(0.5f, 0.48f, 0.45f));
        CraftPiece p = Selected;
        if (!IsMetal(p))
            return;
        mm = Fit(p, 340, 60);
        float len = (float)(p.Length / mm);
        x0 = tank.center.x - len * 0.5f;
        float y = p.location == CraftLocation.Quench ? tank.yMin + 60 + Wobble() : p.location == CraftLocation.Oven ? -999 : 300;
        if (y > -999)
            CraftArt.DrawTop(c, Rules, p, new Rect(x0, y, len, 70), mm);
        if (p.location == CraftLocation.Quench)
        {
            double hot = p.MaxTemperature;
            for (int i = 0; i < (int)Math.Min(30, hot / 30); i++)
                c.Disc(x0 + (i * 47 % Mathf.Max(1, (int)len)), tank.yMax - 30 + (i * 13 % 20), 3 + i % 4, new Color(1f, 1f, 1f, 0.35f));
        }
        if (clayFrom >= 0 && p.location == CraftLocation.Air)
        {
            double a = 0, b = 0;
            for (int i = 0; i < p.segments.Count; i++)
            {
                if (i < Mathf.Min(clayFrom, clayTo)) a += p.segments[i].length;
                if (i <= Mathf.Max(clayFrom, clayTo)) b += p.segments[i].length;
            }
            c.Rect(x0 + (float)(a / mm), 300 - 40, x0 + (float)(b / mm), 300 - 32, new Color(0.62f, 0.52f, 0.4f));
        }
    }

    private float Wobble() => sent == CraftQuenchMotion.UpDown ? 10f * Mathf.Sin(Time.unscaledTime * 8f) : 0f;

    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Selected;
        if (p == null)
            return;
        if (p.location == CraftLocation.Quench && tank.Contains(pos))
        {
            if (phase == CraftPointer.Drag || phase == CraftPointer.Down)
            {
                moves.Enqueue(pos);
                while (moves.Count > 20) moves.Dequeue();
            }
            return;
        }
        if (phase == CraftPointer.Down && p.location != CraftLocation.Quench && tank.Contains(pos))
        {
            Plunge();
            return;
        }
        if (phase == CraftPointer.Down && ovenBox.Contains(pos))
        {
            Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Oven });
            return;
        }
        if (phase == CraftPointer.Down && ashBox.Contains(pos))
        {
            Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Ashes });
            return;
        }
        if (IsMetal(p) && p.location == CraftLocation.Air && pos.y > 260 && pos.y < 380)
        {
            int seg = SegmentAtPixel(p, pos.x, x0, mm);
            if (phase == CraftPointer.Down) clayFrom = clayTo = seg;
            else if (phase == CraftPointer.Drag && clayFrom >= 0) clayTo = seg;
        }
    }

    /// <summary>The motion of the hand in the tank, from the last moves: up and down, side to side, round, or still.</summary>
    public override void Tick(float dt)
    {
        CraftPiece p = Selected;
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
}
