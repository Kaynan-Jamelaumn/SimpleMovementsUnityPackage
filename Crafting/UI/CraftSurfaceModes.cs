using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Grinding, filing, descaling and polishing. The blade lies under the wheel seen from above; above it, magnified, the
/// cross-section where the pointer is - what the metal really looks like there, the design's target shape as a dashed
/// green outline, and the gold line where the wheel bites for the side chosen (the angle held written by it). Pick a
/// side (the pictures on the right, or the list), hold on the blade where the wheel should bite - move along it to
/// grind a stretch, stay to take more off; higher on the blade presses harder (the PRESS gauge). The section changes as
/// you work, what was just ground shines, and the strip along the blade judges each stretch as the item will be judged:
/// yellow still to grind, green at the design, orange off it (the bevels met at another angle, or a little narrow),
/// red ground too far, blue where the heat drew the temper. The wheel heats where it works, a thin edge most: the HEAT
/// gauge's red is where a hardened edge's temper runs (and the end where the wheel cannot hold it) - dip it in the tank
/// first. A file skates on hardened steel. A polish or a brush works the whole surface: the strip and the panel on the
/// right show its scratches against each grit, and the scale. Coatings go on last.
/// </summary>
public sealed class CraftGrindMode : CraftMode
{
    /// <summary>A dip in the tank lasts until the piece is cooler than this (°C), or <see cref="DipSeconds"/> at most.</summary>
    public const float DipCool = 100f, DipSeconds = 4f;
    /// <summary>How hard the hand presses at the bottom and at the top of the blade drawn (× the pressure set): the window and the 3D wheel alike.</summary>
    public const float PressLow = 0.7f, PressHigh = 1.3f;
    /// <summary>What the wheel did goes to the station in batches of this much of its time (s) while the blade is held to it.</summary>
    private const float SendEvery = 0.2f;
    /// <summary>The heat gauge's top (°C): a little over what the wheel can hold.</summary>
    private const float HeatTop = 450f;

    private TMP_Dropdown tool, side, coat;
    private Slider angle, pressure;
    private RectTransform angleGroup, coatRow;
    private TextMeshProUGUI toolNote, info;
    private Button dipButton;
    private TMP_Text dipLabel;
    private readonly List<CraftAbrasive> tools = new List<CraftAbrasive>();
    private readonly List<string> coats = new List<string>();
    private readonly CraftStroke stroke = new CraftStroke();
    // What the controls hold (kept between openings of the area, and from the 3D work place).
    private string toolId;
    private int sideIndex;
    private float angleValue = 15f, pressureValue = 0.5f;
    private CraftPieceKind toolsFor = CraftPieceKind.Assembly;

    private float x0, mm, len, sendTimer, lastPress;
    private float[] fresh = new float[0];
    private string freshFor;
    private int hoverSeg;
    private Rect topView, sectionBox, heatBar, pressBar;
    private readonly Rect[] sideIcons = new Rect[7];
    // The press under way (the piece held to the wheel) and why the work stopped. A press that picks a side never grinds,
    // however it wobbles or wherever it is let go.
    private bool grinding;
    private string grindId, stopWhy;
    // Was the reason the work stopped this area's own check (too hot, in the tank...)? It goes once that passes again;
    // the station's refusals stay until the next press.
    private bool stopLocal;
    // The dip: the piece in the tank and how long it has been in; asked and not yet answered; the hands left meanwhile
    // (a network client's dip answered after the area closed: lifted out at once).
    private string dipId;
    private float dipTime;
    private bool dipAsked, dipLeft;
    // Read a few times a second: the lowest heat at which a hardened edge of the piece starts to lose its temper.
    private double temperRuns = double.PositiveInfinity;

    private static readonly CraftSurfaceSide[] Sides =
        { CraftSurfaceSide.LeftBevel, CraftSurfaceSide.RightBevel, CraftSurfaceSide.Faces, CraftSurfaceSide.EdgeLine, CraftSurfaceSide.Spine, CraftSurfaceSide.Tang, CraftSurfaceSide.Eye };
    private static readonly string[] SideNames = { "Left bevel", "Right bevel", "Faces", "Edge line", "Spine", "Tang", "Eye" };
    private static readonly string[] IconNames = { "1 LEFT", "2 RIGHT", "3 FACES", "4 EDGE", "5 SPINE", "6 TANG", "7 EYE" };
    private static readonly string[] SideHelp =
    {
        "the top face along the edge, held at the angle: the first bevel",
        "the face underneath along the edge, held at the angle: the second bevel",
        "both broad faces, flat on the wheel: thinner (the bevels keep their angle)",
        "straight into the edge: back to a flat - out go chips, the honing and the burr",
        "the back of the blade: it gets narrower",
        "the tang: narrower and thinner, to fit the grip",
        "inside the eye or slot: only a file reaches there",
    };

    // The strip's colours (and its legend).
    private static readonly Color StripToGrind = new Color(1f, 0.88f, 0.35f, 0.95f), StripDone = Good,
        StripOff = new Color(1f, 0.55f, 0.15f, 0.95f), StripTooFar = Harm, StripSoft = new Color(0.35f, 0.5f, 1f, 0.95f),
        StripScale = new Color(0.2f, 0.19f, 0.18f, 0.95f);

    public override string Title => "Grind";
    public override CraftDiscipline Discipline => Selected != null && Selected.kind == CraftPieceKind.Wood ? CraftDiscipline.Woodwork : CraftDiscipline.Smithing;
    // Metal surface work: a wheel, polishing, or files where metal is worked (not the files of a leather bench).
    public override bool Available(CraftStation s) =>
        s.Has(CraftTools.Grinder) || s.Has(CraftTools.Polishing) || s.Has(CraftTools.Files) && (s.Has(CraftTools.Anvil) || s.Has(CraftTools.Forge));
    public override string Hint =>
        "Hold on the blade where the wheel should bite: move along to grind a stretch, stay to take more off; higher presses harder. Mouse wheel: the angle. Right-click lifts it off." +
        ByHandHint("hold it to the wheel by hand");
    public override CraftHandsOnMode HandsOnMode => CraftHandsOnMode.Grind;
    /// <summary>A blade in its hilt or a head on its haft is ground in place (its parts are listed under the assembly).</summary>
    public override bool AcceptsFitted(CraftPiece part) => IsMetal(part);

    private CraftSurfaceSide CurrentSide => Sides[Mathf.Clamp(sideIndex, 0, Sides.Length - 1)];
    private CraftAbrasive CurrentTool => tools.Count > 0 && tool != null ? tools[Mathf.Clamp(tool.value, 0, tools.Count - 1)] : null;
    /// <summary>Does the tool in hand work a side at an angle (a wheel, a file) - or the whole surface (a polish, a brush)?</summary>
    private bool Shaping => CurrentTool == null || CurrentTool.kind == CraftAbrasiveKind.Wheel || CurrentTool.kind == CraftAbrasiveKind.File;
    private bool BevelSide => CurrentSide == CraftSurfaceSide.LeftBevel || CurrentSide == CraftSurfaceSide.RightBevel;

    protected override void FillHandsOn(ref CraftHandsOnSetup setup)
    {
        setup.abrasive = CurrentTool?.id;
        setup.side = CurrentSide;
        setup.angle = angleValue;
        setup.pressure = pressureValue;
    }

    /// <summary>The wheel, side and angle the hands ended with at the 3D wheel come back to the controls.</summary>
    public override void TakeBackHandsOn(in CraftHandsOnSetup setup)
    {
        string id = setup.abrasive;
        int t = tools.FindIndex(a => a.id == id);
        if (t >= 0)
        {
            toolId = id;
            tool?.SetValueWithoutNotify(t);
        }
        int s = Array.IndexOf(Sides, setup.side);
        if (s >= 0)
        {
            sideIndex = s;
            side?.SetValueWithoutNotify(s);
        }
        if (setup.angle > 0f)
        {
            angleValue = Mathf.Clamp(Mathf.Round(setup.angle), (float)CraftSurface.StoneAngleMin, (float)CraftSurface.MaxBevelAngle);
            SetSlider(angle, angleValue);
        }
        ShowTool();
    }

    public override void Build(RectTransform controls)
    {
        RectTransform r1 = Row(controls);
        tool = Drop(r1, new List<string> { "-" }, 0, 210f);
        tool.onValueChanged.AddListener(_ => { toolId = CurrentTool?.id; stopWhy = null; ShowTool(); });
        side = Drop(r1, SideNames, sideIndex, 140f);
        side.onValueChanged.AddListener(v => { sideIndex = v; stopWhy = null; ShowTool(); });
        angleGroup = UITheme.Row("Angle", r1, 34f, 8f);
        InventoryUIFactory.Size(angleGroup, -1f, 34f, 1f);
        angle = Slide(angleGroup, "Angle", (float)CraftSurface.StoneAngleMin, (float)CraftSurface.MaxBevelAngle, angleValue, true, 44f, v => $"{v:0}° a side");
        angle.onValueChanged.AddListener(v => angleValue = v);
        toolNote = Text(r1, "", 12f);
        RectTransform r2 = Row(controls);
        pressure = Slide(r2, "Pressure", 0.05f, 1f, pressureValue, false, 64f, v => v.ToString("0.00"));
        pressure.onValueChanged.AddListener(v => pressureValue = v);
        dipButton = Btn(r2, "Dip to cool", Dip, 120f);
        dipLabel = dipButton.GetComponentInChildren<TMP_Text>(true);
        dipButton.gameObject.SetActive(Station.Has(CraftTools.QuenchTank)); // no tank, no dip
        HandsOnButton(r2, "At the wheel by hand", 200f);
        RectTransform r3 = coatRow = Row(controls); // metal only: wood is oiled at the finishing bench
        coats.Clear();
        foreach (CraftSupplyDefinition s in Rules.Supplies)
            if (s.kind == CraftSupplyKind.Oil || s.kind == CraftSupplyKind.Wax) coats.Add(s.id);
        coat = CountedDrop(r3, coats, 230f, "No oil or wax");
        Btn(r3, "Coat it", CoatIt, 100f);
        Text(r3, "Coat it last: grinding, brushing and heat take it off.", 12f);
        RectTransform r4 = Row(controls, 52f);
        info = Text(r4, "", 13f);
        InventoryUIFactory.Size(info, -1f, 52f, 1f);
        FitInside(info);
        toolsFor = CraftPieceKind.Assembly; // the list is built for the piece picked
        RebuildTools(Selected);
        ShowTool();
    }

    // ------------------------------------------------------------------ the tools
    /// <summary>
    /// The tools that work the piece picked (wheels, metal files, polishes and brushes for metal; the rasp and the knife
    /// for wood - each refuses the other), wheels first; the one in hand stays if it is still there, else the first
    /// wheel, or the first file.
    /// </summary>
    private void RebuildTools(CraftPiece p)
    {
        CraftPieceKind kind = p != null && p.kind == CraftPieceKind.Wood ? CraftPieceKind.Wood : CraftPieceKind.Metal;
        if (tool == null || kind == toolsFor && tools.Count > 0)
            return;
        toolsFor = kind;
        tools.Clear();
        foreach (CraftAbrasiveKind k in new[] { CraftAbrasiveKind.Wheel, CraftAbrasiveKind.File, CraftAbrasiveKind.Polish, CraftAbrasiveKind.Brush })
            foreach (CraftAbrasive a in Rules.Abrasives)
            {
                if (a.kind != k || !Here(a)) continue;
                if (kind == CraftPieceKind.Wood ? !a.wood : a.wood) continue;
                tools.Add(a);
            }
        var names = new List<string>();
        foreach (CraftAbrasive a in tools) names.Add($"{Verb(a)}: {a.Name}");
        if (names.Count == 0) names.Add(kind == CraftPieceKind.Wood ? "No rasp or knife here" : "No wheel or file here");
        int pick = tools.FindIndex(a => a.id == toolId);
        if (pick < 0) pick = tools.FindIndex(a => a.kind == CraftAbrasiveKind.Wheel);
        if (pick < 0) pick = tools.FindIndex(a => a.kind == CraftAbrasiveKind.File);
        pick = Mathf.Max(0, pick);
        tool.ClearOptions();
        tool.AddOptions(names);
        tool.SetValueWithoutNotify(pick);
        tool.RefreshShownValue();
        if (pick < tools.Count) toolId = tools[pick].id;
    }

    private bool Here(CraftAbrasive a) => a.kind switch
    {
        CraftAbrasiveKind.Wheel => Station.Has(CraftTools.Grinder),
        CraftAbrasiveKind.Polish => Station.Has(CraftTools.Polishing),
        CraftAbrasiveKind.File => Station.Has(CraftTools.Files),
        CraftAbrasiveKind.Brush => Station.Has(CraftTools.Files),
        _ => false,
    };

    private static string Verb(CraftAbrasive a) => a.wood ? "Shape" : a.kind switch
    {
        CraftAbrasiveKind.Wheel => "Grind",
        CraftAbrasiveKind.File => "File",
        CraftAbrasiveKind.Polish => "Polish",
        _ => "Descale",
    };

    /// <summary>The controls for the tool in hand: a side and an angle for the wheel and the file; neither for a polish or a brush (they work the whole surface).</summary>
    private void ShowTool()
    {
        if (side == null)
            return;
        bool shaping = Shaping;
        side.gameObject.SetActive(shaping);
        angleGroup.gameObject.SetActive(shaping);
        if (angle != null) angle.interactable = BevelSide;
        toolNote.gameObject.SetActive(!shaping);
        CraftAbrasive a = CurrentTool;
        toolNote.text = a == null ? "" : a.kind == CraftAbrasiveKind.Polish
            ? "Works all the surface under it; each grit clears the last one's scratches."
            : "Brushes off the scale under it; worked on past it, it scratches the metal.";
    }

    // ------------------------------------------------------------------ the dip and the coat
    private void Dip()
    {
        if (dipId != null)
        {
            LiftOut(); // the button reads "Lift it out" while it is in
            return;
        }
        CraftPiece p = Selected;
        if (!IsMetal(p) || dipAsked)
            return;
        string why = DipRefused(p);
        if (why != null)
        {
            ui.Message(why, true);
            return;
        }
        StopGrinding(true); // what the wheel did goes first
        stopWhy = null;
        string id = p.id;
        dipAsked = true;
        dipLeft = false;
        Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Quench, a = 1f }, (ok, reason) =>
        {
            dipAsked = false;
            bool left = dipLeft;
            dipLeft = false;
            // In (or perhaps in: no answer leaves it unknown) - it comes out again once cool, or after a few seconds; at
            // once when the hands left meanwhile (the answer came after the area closed), as Leave lifts a dip out.
            if (ok || reason == CraftAnswerWaits.NoAnswer)
            {
                if (left)
                    LiftOutNow(id);
                else
                {
                    dipId = id;
                    dipTime = 0f;
                }
            }
        });
    }

    /// <summary>
    /// Why the piece cannot be dipped (null: it can): only a piece in the hands, not fitted, at a station with a tank - and
    /// not while it can still harden (a dip would quench it: that is the Quench area's work).
    /// </summary>
    private string DipRefused(CraftPiece p)
    {
        if (!Station.Has(CraftTools.QuenchTank)) return "There is no water here to dip it in.";
        if (p.kind == CraftPieceKind.Wood) return "Wood is not cooled in the tank.";
        if (p.location == CraftLocation.Assembled) return "It is fitted: it cannot go in the tank - press lighter and let it cool.";
        if (p.location == CraftLocation.Fire) return "It is in the fire: quench it at the tank (the Quench area), not here.";
        if (p.location != CraftLocation.Air) return "Take it out first.";
        if (CraftSurface.DipQuenches(Ws, p)) return CraftSurface.DipWouldQuench("at the tank (the Quench area)", Station.Has(CraftTools.Ashes));
        return null;
    }

    /// <summary>
    /// How the hot piece is cooled, in the advice's words: dipped - unless it is fitted (<paramref name="fitted"/>: what to
    /// do instead), there is no tank here, or it can still harden (a dip would quench it).
    /// </summary>
    private string CoolIt(CraftPiece p, string fitted)
    {
        if (p.location == CraftLocation.Assembled) return $"it is fitted and cannot go in the tank: {fitted}";
        if (!Station.Has(CraftTools.QuenchTank)) return "let it cool";
        if (CraftSurface.DipQuenches(Ws, p)) return "let it cool in the air (it is still hot enough to harden: a dip would quench it)";
        return "dip it";
    }

    /// <summary>The dipped piece comes out of the tank (it is cool, the time is up, or the hands leave it).</summary>
    private void LiftOut()
    {
        string id = dipId;
        dipId = null;
        if (id == null || Station == null || Ws == null)
            return;
        CraftPiece p = Ws.Piece(id);
        if (p != null && p.location == CraftLocation.Quench)
            Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Air });
    }

    /// <summary>
    /// A dip answered after the hands left it: out of the tank at once. This copy of the workshop may not show it in the
    /// tank yet (the answer came unknown), so it goes out unless it shows it somewhere else than the hands or the tank.
    /// </summary>
    private void LiftOutNow(string id)
    {
        if (id == null || Station == null || Ws == null)
            return;
        CraftPiece p = Ws.Piece(id);
        if (p != null && (p.location == CraftLocation.Quench || p.location == CraftLocation.Air))
            Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Air });
    }

    private void CoatIt()
    {
        CraftPiece p = Selected;
        if (!IsMetal(p) || coats.Count == 0 || coat == null)
            return;
        Send(new CraftRequest(CraftRequestKind.Coat, p.id) { id = coats[Mathf.Clamp(coat.value, 0, coats.Count - 1)] });
    }

    // ------------------------------------------------------------------ the hands on the wheel
    public override void Tick(float dt)
    {
        if (dipId != null)
        {
            dipTime += dt;
            CraftPiece d = Ws.Piece(dipId);
            if (d == null || d.location != CraftLocation.Quench && dipTime > 0.5f)
                dipId = null; // gone, or taken out some other way
            else if (d.location == CraftLocation.Quench && (d.MaxTemperature < DipCool || dipTime >= DipSeconds))
                LiftOut();
        }
        if (grinding)
        {
            CraftPiece p = Ws.Piece(grindId);
            if (p == null || ui.SelectedId != grindId)
                StopGrinding(false);
            else if (!ui.PointerHeld)
                StopGrinding(true);
            else if (OnBlade(ui.Hover))
            {
                // One sample a frame while the blade is held to the wheel: holding still keeps grinding there.
                Sample(p, ui.Hover, Time.unscaledTime);
                sendTimer += dt;
                if (sendTimer >= SendEvery) Flush();
            }
            else if (stroke.Samples > 0)
                Flush(); // off the wheel: what it did goes; back on, it bites again
        }
        for (int i = 0; i < fresh.Length; i++)
            fresh[i] = Mathf.Max(0f, fresh[i] - dt * 0.7f);
        if (dipLabel != null)
        {
            string label = dipId != null ? "Lift it out" : "Dip to cool";
            if (dipLabel.text != label) dipLabel.text = label;
        }
        LiveDraw = grinding || dipId != null || IsMetal(Selected) && OnBlade(ui.Hover);
    }

    private bool OnBlade(Vector2 pos) => len > 0f && pos.x >= x0 - 6f && pos.x <= x0 + len + 6f && pos.y >= topView.yMin - 8f && pos.y <= topView.yMax + 8f;

    /// <summary>How hard the hand presses at height <paramref name="y"/> on the blade drawn: harder toward the top, where the wheel is.</summary>
    private float PressAt(float y) => pressureValue * Mathf.Lerp(PressLow, PressHigh, Mathf.Clamp01((y - topView.yMin) / Mathf.Max(1f, topView.height)));

    private void Sample(CraftPiece p, Vector2 pos, float time)
    {
        lastPress = PressAt(pos.y);
        stroke.Add(time, PositionAtPixel(p, pos.x, x0, mm), 0.5, angleValue, lastPress, Hands);
    }

    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        switch (phase)
        {
            case CraftPointer.Cancel:
                // Lifted off the wheel: what it ground so far is ground; the press is over.
                if (grinding) StopGrinding(true);
                return;
            case CraftPointer.Scroll:
                // The wheel turns the angle held, a degree a notch.
                if (Shaping && BevelSide && angle != null)
                {
                    angleValue = Mathf.Clamp(angleValue + Mathf.Sign(scroll), (float)CraftSurface.StoneAngleMin, (float)CraftSurface.MaxBevelAngle);
                    SetSlider(angle, angleValue);
                }
                return;
            case CraftPointer.Down:
            {
                if (Shaping)
                    for (int i = 0; i < sideIcons.Length; i++)
                        if (sideIcons[i].Contains(pos))
                        {
                            sideIndex = i;
                            side?.SetValueWithoutNotify(i);
                            stopWhy = null;
                            ShowTool();
                            return; // this press only picks the side: its wobble and release do nothing
                        }
                CraftPiece p = Selected;
                if (!IsMetal(p) || !OnBlade(pos))
                    return;
                if (!CanWork(p, out string why))
                {
                    stopWhy = why;
                    stopLocal = true;
                    return;
                }
                stopWhy = null;
                grinding = true;
                grindId = p.id;
                stroke.Clear();
                sendTimer = 0f;
                Sample(p, pos, ui.PointerTime);
                return;
            }
            case CraftPointer.Up:
                if (grinding) StopGrinding(true);
                return;
        }
        // Drag and Move add nothing: the wheel is sampled by time (Tick), wherever the blade is held.
    }

    /// <summary>The blade comes off the wheel: what it did is sent (<paramref name="send"/>), or dropped.</summary>
    private void StopGrinding(bool send)
    {
        if (send && stroke.Samples > 0 && grindId != null)
        {
            sendTimer = Mathf.Max(sendTimer, 1f / 60f); // a touch is a frame of the wheel at least
            Flush();
        }
        stroke.Clear();
        sendTimer = 0f;
        grinding = false;
    }

    /// <summary>Why the tool in hand cannot work the piece now (checked here, so that nothing is sent five times a second to be refused).</summary>
    private bool CanWork(CraftPiece p, out string why)
    {
        why = null;
        CraftAbrasive a = CurrentTool;
        if (a == null)
            why = p.kind == CraftPieceKind.Wood ? "There is no rasp or knife here for wood." : "There is no wheel, file, polish or brush here for metal.";
        else if (p.location == CraftLocation.Quench)
            why = "It is cooling in the tank.";
        else if (p.location != CraftLocation.Air && p.location != CraftLocation.Assembled)
            why = $"Take it out first: it is {CraftStationUI.LocationText(p)}.";
        else if (a.kind == CraftAbrasiveKind.Wheel && p.MaxTemperature > CraftSurface.WheelTooHot)
        {
            string cool = CoolIt(p, "let it cool");
            why = cool == "dip it" ? "It is far too hot to hold on the wheel: dip it, or let it cool." : $"It is far too hot to hold on the wheel - {cool}.";
        }
        else if (a.kind == CraftAbrasiveKind.File && p.MaxTemperature > CraftSurface.FileTooHot)
            why = "It is too hot to file: let it cool.";
        else if (a.kind == CraftAbrasiveKind.Wheel && CurrentSide == CraftSurfaceSide.Eye)
            why = "A wheel cannot reach inside an eye: use a file.";
        return why == null;
    }

    /// <summary>Sends what the tool did since the last send: the stretch it covered and how long it dwelt on each segment there.</summary>
    private void Flush()
    {
        float window = sendTimer;
        sendTimer = 0f;
        CraftPiece p = Ws?.Piece(grindId);
        CraftAbrasive a = CurrentTool;
        if (!IsMetal(p) || p.segments.Count == 0 || stroke.Samples < 1 || a == null)
        {
            stroke.Clear();
            return;
        }
        if (!CanWork(p, out string why))
        {
            stroke.Clear();
            grinding = false;
            stopWhy = why;
            stopLocal = true;
            return;
        }
        // The segments under the stroke by their real lengths, and the time each had under the wheel: the wheel's time
        // shared out over the stretch it went along (all of it on one stretch held still).
        double dwell = stroke.Dwell(p, Math.Max(1f / 60f, window), out int from, out int to);
        float press = (float)stroke.Pressure, held = (float)stroke.Angle;
        stroke.Clear(); // out of the buffer before it goes: never sent twice
        CraftSurfaceSide s = CurrentSide;
        var r = new CraftRequest(CraftRequestKind.Grind, p.id) { i0 = from, i1 = to, i2 = (int)s, id = a.id, a = held, b = press, c = (float)dwell };
        switch (a.kind)
        {
            case CraftAbrasiveKind.File: r.kind = CraftRequestKind.File; break;
            case CraftAbrasiveKind.Brush: r = new CraftRequest(CraftRequestKind.Descale, p.id) { i0 = from, i1 = to, id = a.id, a = press, b = (float)dwell }; break;
            case CraftAbrasiveKind.Polish: r = new CraftRequest(CraftRequestKind.Polish, p.id) { i0 = from, i1 = to, id = a.id, a = press, b = (float)dwell }; break;
        }
        string id = p.id;
        Send(r, (ok, reason) =>
        {
            if (ok)
                Glint(id, from, to, a, s);
            else if (reason != CraftAnswerWaits.NoAnswer && grindId == id)
            {
                // Refused (too hot, it skates...): the hands stop until the next press, rather than asking again and again.
                if (grinding) StopGrinding(false);
                stopWhy = reason;
                stopLocal = false;
            }
        });
    }

    /// <summary>What the station took the work on shines a moment: the stretch the tool really cut (not where it skated).</summary>
    private void Glint(string id, int from, int to, CraftAbrasive a, CraftSurfaceSide s)
    {
        CraftPiece p = Ws?.Piece(id);
        if (p == null || id != ui.SelectedId)
            return;
        if (fresh.Length != p.segments.Count || freshFor != id)
        {
            fresh = new float[p.segments.Count];
            freshFor = id;
        }
        for (int i = Mathf.Max(0, from); i <= Mathf.Min(fresh.Length - 1, to); i++)
            if (a.kind == CraftAbrasiveKind.Polish || a.kind == CraftAbrasiveKind.Brush || CraftSurface.WorkedHardness(Rules, p, p.segments[i], s) <= a.maxHardness)
                fresh[i] = 1f;
    }

    // ------------------------------------------------------------------ the selected piece changes, the area closes
    public override void Leave()
    {
        // What the wheel did goes to the piece it was done on; a piece in the tank is lifted out as the hands leave it - and
        // one whose dip is still to be answered, once it is.
        StopGrinding(true);
        LiftOut();
        if (dipAsked) dipLeft = true;
        stopWhy = null;
    }

    public override void SelectionChanged(string to)
    {
        fresh = new float[0];
        freshFor = null;
        hoverSeg = 0;
        stopWhy = null;
        RebuildTools(Selected);
        ShowTool();
    }

    // ------------------------------------------------------------------ drawing
    public override void Draw(CraftPixels c)
    {
        sectionBox = new Rect(12, 236, 326, 156);
        heatBar = new Rect(348, 252, 12, 120);
        pressBar = new Rect(664, 246, 12, 52);
        CraftPiece p = Selected;
        CraftAbrasive a = CurrentTool;
        if (Shaping) DrawSideIcons(c, p);
        else DrawSurfacePanel(c, p, a);
        if (!IsMetal(p) || p.segments.Count == 0)
        {
            len = 0f;
            return;
        }
        if (fresh.Length != p.segments.Count || freshFor != p.id)
        {
            fresh = new float[p.segments.Count];
            freshFor = p.id;
        }
        mm = Fit(p, 660, 140);
        len = (float)(p.Length / mm);
        x0 = (c.Width - len) * 0.5f;
        topView = new Rect(x0, 26, len, 150);
        float[] starts = CraftArt.DrawTop(c, Rules, p, topView, mm, null, false, ui.Dark);
        // The side view, as thick as it is but kept to its band (a thick bar is drawn thinner, not over the pictures) - a
        // dished plate's rise too: the thickness takes half the band then, the dish what is left of it.
        double thickest = 0, deepest = 0;
        foreach (MetalSegment s in p.segments) { thickest = Math.Max(thickest, s.thickness); deepest = Math.Max(deepest, s.dish); }
        const float band = 26f;
        float sideScale = Mathf.Min(3f, (deepest > 0.05 ? band * 0.5f : band) * mm / Mathf.Max(0.1f, (float)thickest));
        float rise = Mathf.Max(0f, band - Mathf.Max(1f, (float)thickest / mm * sideScale));
        float dishScale = deepest > 0.05 ? Mathf.Min(1f, rise * mm / (float)deepest) : 1f;
        CraftArt.DrawSide(c, Rules, p, new Rect(x0, 198, len, band), mm, sideScale, ui.Dark, dishScale);
        float cy = topView.center.y;
        bool anyTarget = false;
        double gritDone = CraftSurface.GritDone(a, NextFiner(a)); // a polish has done what it can: the next grit takes it on
        for (int i = 0; i < p.segments.Count; i++)
        {
            MetalSegment s = p.segments[i];
            float x1 = starts[i], x2 = starts[i + 1], w = (float)s.width / mm;
            if (fresh[i] > 0.01f)
                c.Rect(x1, cy - w * 0.5f, x2, cy + w * 0.5f, new Color(1f, 0.97f, 0.85f, 0.45f * fresh[i]));
            bool target = CraftArt.TargetAt(Rules, p, i, out float tw, out float tt, out float ta, out bool edged);
            anyTarget |= target;
            if (target && i % 2 == 0)
            {
                // The design's width, dotted in green (inside the blade's view).
                float hw = Mathf.Min(tw * 0.5f / mm, topView.height * 0.5f - 1f);
                c.Rect(x1, cy + hw - 0.6f, x2, cy + hw + 0.6f, CraftArt.TargetColor);
                c.Rect(x1, cy - hw - 0.6f, x2, cy - hw + 0.6f, CraftArt.TargetColor);
            }
            c.Rect(x1 + 0.5f, 182, x2 - 0.5f, 192, Shaping ? StripColor(p, s, target, tw, tt, ta, edged) : SurfaceColor(s, a, gritDone));
            if (Shaping && target && ShortToGrind(s, tw, tt, ta, edged))
                c.Rect(x1 + 0.5f, 189, x2 - 0.5f, 192, StripOff); // short of the design (the hammer left it so), its edge still to grind
            if (Shaping && SoftEdge(s))
                c.Rect(x1 + 0.5f, 182, x2 - 0.5f, 186, StripSoft); // the heat drew this edge's temper
        }
        // The segment under the pointer: its section, the target, where the tool works.
        Vector2 h = ui.Hover;
        if (h.x >= x0 && h.x <= x0 + len && h.y >= 18 && h.y <= 230)
            hoverSeg = SegmentAtPixel(p, h.x, x0, mm);
        hoverSeg = Mathf.Clamp(hoverSeg, 0, p.segments.Count - 1);
        c.Frame(starts[hoverSeg], topView.yMin - 2, starts[hoverSeg + 1], topView.yMax + 2, new Color(1f, 1f, 1f, 0.35f), 1f);
        MetalSegment seg = p.segments[hoverSeg];
        bool hasTarget = CraftArt.TargetAt(Rules, p, hoverSeg, out float ttw, out float ttt, out float tta, out bool tedged);
        float scale = CraftArt.SectionScale(seg, sectionBox, hasTarget ? ttw : 0f, hasTarget ? ttt : 0f);
        Vector2 o = CraftArt.DrawSection(c, seg, sectionBox, CraftArt.MetalColor(Rules, p), scale, 1f, p.kind == CraftPieceKind.Wood, p.edges);
        if (hasTarget) CraftArt.DrawTargetSection(c, o, scale, ttw, ttt, tta, tedged, p.IntendedEdges(Rules, p.edges) >= 2); // the target's own edges
        // While hot, the apex glows the colour its oxide takes at that heat (straw, purple, blue).
        Color film = CraftMetallurgy.TemperColor(seg.edge.temp);
        if (film.a > 0f && p.kind == CraftPieceKind.Metal)
            c.Halo(o.x + (float)seg.width * 0.5f * scale, o.y, 10f, film, 0.8f);
        bool biting = grinding && stopWhy == null && OnBlade(h);
        if (Shaping)
        {
            Vector2 end = DrawContact(c, o, scale, seg, CurrentSide, angleValue, biting, sectionBox);
            if (BevelSide)
                CraftArt.Label(c, Mathf.Clamp(end.x - 34f, sectionBox.xMin + 4f, sectionBox.xMax - 40f), Mathf.Clamp(end.y + 3f, sectionBox.yMin + 4f, sectionBox.yMax - 14f),
                    $"{angleValue:0}°", biting ? Mark : new Color(1f, 0.85f, 0.5f, 0.8f));
        }
        else
        {
            // A polish or a brush works the whole surface: the pad over the section.
            float hw = (float)seg.width * 0.5f * scale, ht = (float)seg.thickness * 0.5f * scale;
            c.Frame(o.x - hw - 5f, o.y - ht - 5f, o.x + hw + 5f, o.y + ht + 5f, biting ? new Color(1f, 0.9f, 0.45f, 1f) : Mark, biting ? 2.5f : 1.5f);
        }
        DrawHeat(c, p);
        DrawPress(c, h);
        // The wheel (or the file) where the pointer is, against the side it works - the edge for the bevels and the edge
        // line (the top of the blade drawn), the spine for the spine and the tang, flat across the blade for the faces, in
        // the eye for the eye; a polish or a brush is a pad flat on the blade - with sparks while it bites, all in the
        // blade's view.
        if (OnBlade(h))
        {
            bool wheel = a != null && a.kind == CraftAbrasiveKind.Wheel;
            float wx = Mathf.Clamp(h.x, x0, x0 + len);
            float hw = (float)p.segments[SegmentAtPixel(p, wx, x0, mm)].width * 0.5f / mm;
            float edgeY = Mathf.Min(topView.yMax - 4f, cy + hw), spineY = Mathf.Max(topView.yMin + 4f, cy - hw);
            CraftSurfaceSide worked = CurrentSide;
            Color steel = new Color(0.62f, 0.6f, 0.56f, 0.85f), file = new Color(0.5f, 0.5f, 0.55f, 0.85f);
            float hitY;
            if (!Shaping || worked == CraftSurfaceSide.Faces)
            {
                // flat on the blade: the wheel's face or the file across it, the pad of a polish or a brush
                Color pad = !Shaping ? new Color(0.78f, 0.74f, 0.62f, 0.45f) : new Color(steel.r, steel.g, steel.b, 0.45f);
                c.Rect(wx - 18f, Mathf.Max(topView.yMin, spineY - 3f), wx + 18f, Mathf.Min(topView.yMax, edgeY + 3f), pad);
                c.Frame(wx - 18f, Mathf.Max(topView.yMin, spineY - 3f), wx + 18f, Mathf.Min(topView.yMax, edgeY + 3f), Shaping ? steel : file, 1f);
                hitY = cy;
            }
            else if (worked == CraftSurfaceSide.Eye)
            {
                c.Rect(wx - 2f, Mathf.Max(topView.yMin, cy - 12f), wx + 2f, Mathf.Min(topView.yMax, cy + 12f), file); // the file in the eye
                hitY = cy;
            }
            else if (worked == CraftSurfaceSide.Spine || worked == CraftSurfaceSide.Tang)
            {
                if (wheel) c.Ring(wx, Mathf.Max(spineY - 19f, topView.yMin + 21f), 20, 4, steel);
                else c.Rect(wx - 30, Mathf.Max(spineY - 8f, topView.yMin), wx + 30, spineY - 1f, file);
                hitY = spineY;
            }
            else
            {
                if (wheel) c.Ring(wx, Mathf.Min(edgeY + 19f, topView.yMax - 21f), 20, 4, steel);
                else c.Rect(wx - 30, edgeY + 1f, wx + 30, Mathf.Min(edgeY + 8f, topView.yMax), file);
                hitY = edgeY;
            }
            if (biting && wheel && p.kind == CraftPieceKind.Metal)
                for (int i = 0; i < 14; i++)
                {
                    float t = Time.unscaledTime * 9f + i * 1.7f;
                    float sx = Mathf.Min(wx + 26 + (i * 13 % 50), c.Width - 2f), sy = Mathf.Clamp(hitY - 6f + Mathf.Sin(t) * 14f, topView.yMin, topView.yMax);
                    c.Line(wx, hitY, sx, sy, new Color(1f, 0.85f, 0.4f, 0.75f), 1f);
                }
        }
        DrawLegend(c, a, anyTarget, p.kind == CraftPieceKind.Metal);
        if (p.location == CraftLocation.Quench)
            c.Rect(0, 0, c.Width, 232, new Color(Water.r, Water.g, Water.b, 0.35f)); // in the tank, cooling
        else if (p.location != CraftLocation.Air && p.location != CraftLocation.Assembled)
            c.Rect(0, 0, c.Width, c.Height, new Color(0f, 0f, 0f, 0.45f));
    }

    /// <summary>The strip along the blade: the stretch judged against the design as the evaluator judges it (<see cref="CraftSurface.Judge"/>).</summary>
    private static Color StripColor(CraftPiece p, MetalSegment s, bool target, float tw, float tt, float ta, bool edged)
    {
        if (!target)
            return s.Beveled ? new Color(0.75f, 0.75f, 0.8f, 0.8f) : new Color(0.3f, 0.3f, 0.33f, 0.8f);
        CraftSurface.Judgement j = CraftSurface.Judge(s, tw, tt, ta, edged);
        switch (j.progress)
        {
            case CraftSurface.Progress.Done: return StripDone;
            case CraftSurface.Progress.Off: return StripOff;
            case CraftSurface.Progress.TooFar: return StripTooFar;
            default: return Color.Lerp(new Color(0.45f, 0.38f, 0.2f, 0.95f), StripToGrind, (float)j.done);
        }
    }

    /// <summary>A stretch narrower or thinner than the design whose edge is still to grind (<see cref="CraftSurface.Judgement.under"/>): its strip is marked along the top.</summary>
    private static bool ShortToGrind(MetalSegment s, float tw, float tt, float ta, bool edged)
    {
        CraftSurface.Judgement j = CraftSurface.Judge(s, tw, tt, ta, edged);
        return j.under && j.progress == CraftSurface.Progress.ToGrind;
    }

    /// <summary>
    /// The strip for a polish or a brush: scale dark; the scratches against this grit - green when it has done what it
    /// can (<paramref name="done"/>, <see cref="CraftSurface.GritDone"/>: the next finer grit takes them on at its pace),
    /// yellow while it works them, red when they are deeper than it reaches.
    /// </summary>
    private static Color SurfaceColor(MetalSegment s, CraftAbrasive a, double done)
    {
        if (s.scale > 0.005)
            return StripScale;
        if (a == null || a.kind == CraftAbrasiveKind.Brush)
            return StripDone;
        if (s.rough > a.finish * CraftSurface.GritReach)
            return StripTooFar;
        return s.rough <= done ? StripDone : StripToGrind;
    }

    /// <summary>The next finer polishing grit here after <paramref name="a"/> (null: none finer here).</summary>
    private CraftAbrasive NextFiner(CraftAbrasive a)
    {
        if (a == null || a.kind != CraftAbrasiveKind.Polish)
            return null;
        CraftAbrasive next = null;
        foreach (CraftAbrasive g in Rules.Abrasives)
            if (g.kind == CraftAbrasiveKind.Polish && Here(g) && g.finish < a.finish && (next == null || g.finish > next.finish))
                next = g;
        return next;
    }

    /// <summary>Is this stretch's edge hardened and drawn soft by heat (more tempered than a blade wants)?</summary>
    private static bool SoftEdge(MetalSegment s) => s.edge.mart > 0.3 && CraftMetallurgy.TemperFraction(s.edge.temperP) > CraftSurface.DrawnSoft;

    /// <summary>The legend of the strip, under the blade (a temper drawn only for metal).</summary>
    private void DrawLegend(CraftPixels c, CraftAbrasive a, bool judged, bool metal)
    {
        float x = 12f, y = 5f;
        x = LegendItem(c, x, y, "STRIP:", null);
        if (Shaping && !judged)
            LegendItem(c, x, y, "NO DESIGN CHOSEN FOR IT: NOTHING TO JUDGE BY", null); // grey: lighter where beveled
        else if (Shaping)
        {
            x = LegendItem(c, x, y, "TO GRIND", StripToGrind);
            x = LegendItem(c, x, y, "AT DESIGN", StripDone);
            x = LegendItem(c, x, y, "OFF", StripOff);
            x = LegendItem(c, x, y, "TOO FAR", StripTooFar);
            x = LegendMark(c, x, y, "UNDERSIZE");
            if (metal) LegendItem(c, x, y, "TEMPER DRAWN", StripSoft);
        }
        else if (a != null && a.kind == CraftAbrasiveKind.Polish)
        {
            x = LegendItem(c, x, y, "SCALE", StripScale);
            x = LegendItem(c, x, y, "WORKING", StripToGrind);
            x = LegendItem(c, x, y, "DONE FOR THIS GRIT", StripDone);
            LegendItem(c, x, y, "TOO DEEP", StripTooFar);
        }
        else
        {
            x = LegendItem(c, x, y, "SCALE", StripScale);
            LegendItem(c, x, y, "CLEAN", StripDone);
        }
    }

    /// <summary>The undersize mark's key: a stretch still to grind with the orange line along its top, as the strip draws it.</summary>
    private static float LegendMark(CraftPixels c, float x, float y, string text)
    {
        c.Rect(x, y, x + 9f, y + 9f, StripToGrind);
        c.Rect(x, y + 6f, x + 9f, y + 9f, StripOff);
        x += 13f;
        CraftArt.Label(c, x, y, text, CraftArt.LabelInk);
        return x + CraftArt.LabelWidth(text) + 12f;
    }

    private static float LegendItem(CraftPixels c, float x, float y, string text, Color? swatch)
    {
        if (swatch.HasValue)
        {
            c.Rect(x, y, x + 9f, y + 9f, swatch.Value);
            x += 13f;
        }
        CraftArt.Label(c, x, y, text, CraftArt.LabelInk);
        return x + CraftArt.LabelWidth(text) + 12f;
    }

    /// <summary>
    /// The HEAT gauge beside the section: the hottest the piece is; green where the work is safe, red from where a
    /// hardened edge's temper runs, and the end where the wheel cannot hold it. The fill takes the colour bright steel
    /// takes at that heat. Degrees only with a thermometer.
    /// </summary>
    private void DrawHeat(CraftPixels c, CraftPiece p)
    {
        if (p.kind != CraftPieceKind.Metal)
            return;
        float t = (float)p.MaxTemperature, top = HeatTop;
        float hold = (float)CraftSurface.WheelTooHot / top;
        float runs = double.IsInfinity(temperRuns) ? hold : Mathf.Clamp((float)temperRuns / top, 0f, hold);
        Color fill = CraftMetallurgy.TemperColor(t);
        if (fill.a <= 0f) fill = new Color(0.8f, 0.8f, 0.84f, 1f);
        Meter(c, heatBar.x, heatBar.y, heatBar.width, heatBar.height, t / top, fill,
            (0f, runs, GoodBand), (runs, hold, HarmBand), (hold, 1f, new Color(0.35f, 0.08f, 0.06f, 1f)));
        CraftArt.Label(c, heatBar.center.x - CraftArt.LabelWidth("HEAT") * 0.5f, heatBar.yMax + 6f, "HEAT", CraftArt.LabelInk);
        if (Station.HasInstrument(CraftInstruments.Thermometer))
        {
            // a heat in °C (never to be taken for the bevel's angle), between the section and the sides' pictures - a long
            // one a row lower, under the blade's side view
            string deg = $"{t:0}°C";
            float dw = CraftArt.LabelWidth(deg), dx = Mathf.Max(sectionBox.xMax + 2f, heatBar.center.x - dw * 0.5f);
            CraftArt.Label(c, dx, dx + dw > 382f ? heatBar.yMin - 26f : heatBar.yMin - 14f, deg, CraftArt.LabelInk);
        }
    }

    /// <summary>The PRESS gauge: how hard the hand presses now (or would, where the pointer is) - the pressure set, more toward the top of the blade.</summary>
    private void DrawPress(CraftPixels c, Vector2 h)
    {
        float value = grinding ? lastPress : OnBlade(h) ? PressAt(h.y) : pressureValue;
        Meter(c, pressBar.x, pressBar.y, pressBar.width, pressBar.height, value, grinding ? Mark : new Color(0.75f, 0.7f, 0.55f, 1f));
        CraftArt.Label(c, pressBar.center.x - CraftArt.LabelWidth("PRESS") * 0.5f, pressBar.yMax + 5f, "PRESS", CraftArt.LabelInk);
    }

    /// <summary>The gold line where the tool meets the metal in the section, for the side chosen, kept inside <paramref name="box"/>; returns its far end.</summary>
    private static Vector2 DrawContact(CraftPixels c, Vector2 o, float scale, MetalSegment s, CraftSurfaceSide side, float angle, bool biting, Rect box)
    {
        float hw = (float)s.width * 0.5f * scale, ht = (float)s.thickness * 0.5f * scale;
        float mid = (ht - (float)s.bevelDepthL * scale + (-ht + (float)s.bevelDepthR * scale)) * 0.5f;
        Color gold = biting ? new Color(1f, 0.9f, 0.45f, 1f) : Mark;
        float thick = biting ? 3.5f : 2.5f;
        float ca = Mathf.Cos(angle * Mathf.Deg2Rad), sa = Mathf.Max(1e-3f, Mathf.Sin(angle * Mathf.Deg2Rad));
        float ax = o.x + hw + 2f, ay = o.y + mid;
        // As long as it fits in the box, whatever the angle.
        float L = Mathf.Min(150f, hw * 1.4f + 30f);
        L = Mathf.Min(L, (ax - box.xMin - 4f) / Mathf.Max(1e-3f, ca));
        Vector2 end = new Vector2(ax, ay);
        switch (side)
        {
            case CraftSurfaceSide.LeftBevel:
                L = Mathf.Max(8f, Mathf.Min(L, (box.yMax - 4f - (ay + 2f)) / sa));
                end = new Vector2(ax - L * ca, ay + 2f + L * sa);
                c.Line(ax, ay + 2f, end.x, end.y, gold, thick);
                break;
            case CraftSurfaceSide.RightBevel:
                L = Mathf.Max(8f, Mathf.Min(L, (ay - 2f - box.yMin - 4f) / sa));
                end = new Vector2(ax - L * ca, ay - 2f - L * sa);
                c.Line(ax, ay - 2f, end.x, end.y, gold, thick);
                break;
            case CraftSurfaceSide.Faces:
                c.Line(o.x - hw, o.y + ht + 3, o.x + hw, o.y + ht + 3, gold, thick);
                c.Line(o.x - hw, o.y - ht - 3, o.x + hw, o.y - ht - 3, gold, thick);
                break;
            case CraftSurfaceSide.EdgeLine:
                c.Line(o.x + hw + 3, o.y - ht - 6, o.x + hw + 3, o.y + ht + 6, gold, thick);
                break;
            case CraftSurfaceSide.Spine:
                c.Line(o.x - hw - 3, o.y - ht - 6, o.x - hw - 3, o.y + ht + 6, gold, thick);
                break;
            case CraftSurfaceSide.Tang:
                c.Line(o.x - hw - 3, o.y - ht - 6, o.x - hw - 3, o.y + ht + 6, gold, thick * 0.7f);
                c.Line(o.x + hw + 3, o.y - ht - 6, o.x + hw + 3, o.y + ht + 6, gold, thick * 0.7f);
                c.Line(o.x - hw, o.y + ht + 3, o.x + hw, o.y + ht + 3, gold, thick * 0.7f);
                break;
            default:
                c.Ring(o.x, o.y, Mathf.Max(4f, Mathf.Min(hw, ht) * 0.6f), 2f, gold);
                break;
        }
        return end;
    }

    /// <summary>The seven sides as small sections, the gold line on each where the tool works, its number and name; click one to choose it.</summary>
    private void DrawSideIcons(CraftPixels c, CraftPiece p)
    {
        int edges = p != null && p.edges >= 2 ? 2 : 1;
        var dummy = new MetalSegment { width = 30, thickness = 6, bevelDepthL = 2.2, bevelWidthL = 8, bevelDepthR = 2.2, bevelWidthR = 8, apex = 30 };
        for (int i = 0; i < 7; i++)
        {
            float x = 384 + (i % 4) * 82, y = i < 4 ? 316 : 240;
            sideIcons[i] = new Rect(x, y, 76, 72);
            bool on = Sides[i] == CurrentSide;
            var pic = new Rect(x, y + 14, 76, 58);
            float scale = CraftArt.SectionScale(dummy, pic) * 0.8f;
            Vector2 o = CraftArt.DrawSection(c, dummy, pic, new Color(0.6f, 0.6f, 0.64f), scale, 1f, false, edges);
            DrawContact(c, o, scale, dummy, Sides[i], 18f, false, pic);
            c.Frame(x, y, x + 76, y + 72, on ? Mark : new Color(1f, 1f, 1f, 0.15f), on ? 2.5f : 1f);
            // Its number (the 3D keys) and name, under it.
            CraftArt.Label(c, x + 38 - CraftArt.LabelWidth(IconNames[i]) * 0.5f, y + 3, IconNames[i], on ? Mark : CraftArt.LabelInk);
        }
    }

    /// <summary>
    /// With a polish or a brush the panel on the right shows the surface where the pointer is: its scratches on a scale
    /// from rough (left) to a mirror (right) with each grit's finish ticked, green where the grit in hand works at its
    /// pace and the white mark where the surface is; for a brush, the scale left on it.
    /// </summary>
    private void DrawSurfacePanel(CraftPixels c, CraftPiece p, CraftAbrasive a)
    {
        for (int i = 0; i < sideIcons.Length; i++) sideIcons[i] = Rect.zero;
        var panel = new Rect(384, 240, 264, 148);
        c.Rect(panel.xMin, panel.yMin, panel.xMax, panel.yMax, new Color(0f, 0f, 0f, 0.45f));
        c.Frame(panel.xMin, panel.yMin, panel.xMax, panel.yMax, new Color(1f, 1f, 1f, 0.15f), 1f);
        MetalSegment s = IsMetal(p) && p.segments.Count > 0 ? p.segments[Mathf.Clamp(hoverSeg, 0, p.segments.Count - 1)] : null;
        if (a != null && a.kind == CraftAbrasiveKind.Brush)
        {
            CraftArt.Label(c, panel.xMin + 10, panel.yMax - 20, "SCALE HERE", CraftArt.LabelInk);
            float v = s != null ? Mathf.Clamp01((float)s.scale / 0.1f) : 0f;
            Meter(c, panel.xMin + 10, panel.yMin + 70, panel.width - 20, 16, v, new Color(0.55f, 0.5f, 0.45f, 1f));
            CraftArt.Label(c, panel.xMin + 10, panel.yMin + 52, "NONE", CraftArt.LabelInk);
            CraftArt.Label(c, panel.xMax - 10 - CraftArt.LabelWidth("THICK"), panel.yMin + 52, "THICK", CraftArt.LabelInk);
            CraftArt.Label(c, panel.xMin + 10, panel.yMin + 14, v > 0.05f ? "BRUSH ON" : "CLEAN", v > 0.05f ? StripToGrind : StripDone);
            return;
        }
        CraftArt.Label(c, panel.xMin + 10, panel.yMax - 20, "SCRATCHES HERE", CraftArt.LabelInk);
        var bar = new Rect(panel.xMin + 10, panel.yMin + 70, panel.width - 20, 14);
        float X(double rough) => bar.xMin + bar.width * Mathf.Clamp01((float)((Math.Log10(60) - Math.Log10(Math.Max(0.1, rough))) / (Math.Log10(60) - Math.Log10(0.1))));
        c.Rect(bar.xMin, bar.yMin, bar.xMax, bar.yMax, new Color(0.18f, 0.18f, 0.2f, 1f));
        if (a != null)
            c.Rect(X(a.finish * CraftSurface.GritReach), bar.yMin, X(a.finish), bar.yMax, GoodBand); // what this grit works at its pace
        foreach (CraftAbrasive g in Rules.Abrasives)
        {
            if (g.kind != CraftAbrasiveKind.Polish && g.kind != CraftAbrasiveKind.Wheel || !Here(g)) continue;
            float gx = X(g.finish);
            bool mine = a != null && g.id == a.id;
            c.Rect(gx - 0.75f, bar.yMin - 4, gx + 0.75f, bar.yMax + 4, mine ? Mark : new Color(1f, 1f, 1f, 0.45f));
            if (!mine) continue;
            string label = a.Name.ToUpperInvariant().Replace("POLISHING ", "");
            CraftArt.Label(c, Mathf.Clamp(gx - CraftArt.LabelWidth(label) * 0.5f, panel.xMin + 4, panel.xMax - 4 - CraftArt.LabelWidth(label)), bar.yMax + 8, label, Mark);
        }
        CraftArt.Label(c, bar.xMin, bar.yMin - 16, "ROUGH", CraftArt.LabelInk);
        CraftArt.Label(c, bar.xMax - CraftArt.LabelWidth("MIRROR"), bar.yMin - 16, "MIRROR", CraftArt.LabelInk);
        if (s != null)
        {
            float sx = X(s.rough);
            c.Rect(sx - 1.5f, bar.yMin - 6, sx + 1.5f, bar.yMax + 6, Color.white);
        }
        // The key.
        c.Rect(panel.xMin + 10, panel.yMin + 13, panel.xMin + 19, panel.yMin + 22, GoodBand);
        CraftArt.Label(c, panel.xMin + 23, panel.yMin + 12, "THIS GRIT", CraftArt.LabelInk);
        c.Rect(panel.xMin + 124, panel.yMin + 13, panel.xMin + 127, panel.yMin + 22, Color.white);
        CraftArt.Label(c, panel.xMin + 132, panel.yMin + 12, "THE SURFACE", CraftArt.LabelInk);
    }

    // ------------------------------------------------------------------ what it says
    public override void Refresh()
    {
        if (info == null)
            return;
        CraftPiece p = Selected;
        RebuildTools(p);
        RefreshCounts(coat, coats);
        CraftAbrasive a = CurrentTool;
        // Wood is neither dipped nor coated here (the finishing bench oils it): no tank button, no coat row for it.
        bool wood = p != null && p.kind == CraftPieceKind.Wood;
        if (coatRow != null && coatRow.gameObject.activeSelf == wood) coatRow.gameObject.SetActive(!wood);
        bool dipHere = Station.Has(CraftTools.QuenchTank) && (!wood || dipId != null);
        if (dipButton != null && dipButton.gameObject.activeSelf != dipHere) dipButton.gameObject.SetActive(dipHere);
        if (dipButton != null) dipButton.interactable = dipId != null || IsMetal(p) && p.kind == CraftPieceKind.Metal && !dipAsked;
        // This area's own reason for stopping (too hot, in the tank) goes once it no longer holds.
        if (stopWhy != null && stopLocal && IsMetal(p) && CanWork(p, out _))
            stopWhy = null;
        temperRuns = double.PositiveInfinity;
        if (p != null && p.kind == CraftPieceKind.Metal)
            foreach (MetalSegment s in p.segments)
                temperRuns = Math.Min(temperRuns, CraftSurface.TemperRunsAt(Rules, s.edge));
        var sb = new StringBuilder();
        if (Shaping)
        {
            int si = Mathf.Clamp(sideIndex, 0, Sides.Length - 1);
            sb.Append($"<b>{SideNames[si]}</b>: {SideHelp[si]}");
            if (si <= 1 && p != null && p.edges >= 2) sb.Append(" - on both edges at once");
            sb.Append(". ");
        }
        else if (a != null)
            sb.Append($"<b>{a.Name}</b>: {(a.kind == CraftAbrasiveKind.Polish ? "brings the surface toward its finish" : "takes the scale off where you hold it")}. ");
        if (!IsMetal(p) || p.segments.Count == 0)
        {
            info.text = sb.Append("Pick a blade (or a fitted part, under its assembly) from the pieces on the left.").ToString();
            return;
        }
        int i = Mathf.Clamp(hoverSeg, 0, p.segments.Count - 1);
        MetalSegment g = p.segments[i];
        bool calipers = Station.HasInstrument(CraftInstruments.Calipers), loupe = Station.HasInstrument(CraftInstruments.Loupe);
        bool target = CraftArt.TargetAt(Rules, p, i, out float tw, out float tt, out float ta, out bool edged);
        sb.Append($"\nHere ({i + 1}/{p.segments.Count}): ");
        if (Shaping)
            sb.Append(ShapeWords(g, target, tw, tt, ta, edged, calipers));
        else
            sb.Append(SurfaceWords(g, a, loupe));
        sb.Append('\n').Append(Advice(p, g, a, target, tw, tt, ta, edged));
        info.text = sb.ToString();
    }

    /// <summary>The stretch's shape in words - in numbers only with calipers - against what the design wants there.</summary>
    private static string ShapeWords(MetalSegment s, bool target, float tw, float tt, float ta, bool edged, bool calipers)
    {
        var sb = new StringBuilder();
        string design = target ? $" (design: {tw:0.#} × {tt:0.#} mm{(edged ? $", {ta * 0.5f:0}° a side" : "")})" : "";
        if (calipers)
        {
            sb.Append($"{s.width:0.#} × {s.thickness:0.#} mm");
            if (s.Beveled) sb.Append($", bevels {s.bevelWidthL:0.#}/{s.bevelWidthR:0.#} mm at {s.AngleL:0}°/{s.AngleR:0}°, edge {s.EdgeThickness:0.##} mm");
            else sb.Append(", no bevel yet");
            return sb.Append(design).Append('.').ToString();
        }
        if (!s.Beveled) sb.Append(edged ? "no bevel yet" : "flat");
        else if (s.EdgeThickness <= CraftSurface.EdgeMet) sb.Append("the bevels meet");
        else if (CraftSurface.HasEdge(s)) sb.Append("bevels down, a flat still along the edge");
        else sb.Append("bevels started");
        if (s.Beveled && edged && target)
        {
            double want = ta * 0.5, mean = (s.AngleL + s.AngleR) * 0.5;
            sb.Append(Math.Abs(mean - want) <= 2 ? ", at the design's angle" : mean > want ? ", steeper than the design" : ", shallower than the design");
        }
        if (target)
        {
            CraftSurface.Judgement j = CraftSurface.Judge(s, tw, tt, ta, edged);
            string width = j.widthOff > 0.04 ? "wider" : j.widthOff < -0.04 ? "narrower" : "", thick = j.thicknessOff > 0.04 ? "thicker" : j.thicknessOff < -0.04 ? "thinner" : "";
            sb.Append(width.Length + thick.Length == 0 ? "; the design's width and thickness"
                : $"; {(width.Length > 0 && thick.Length > 0 ? width + " and " + thick : width + thick)} than the design");
        }
        return sb.Append(design).Append('.').ToString();
    }

    /// <summary>The surface's scratches in words (in µm only with a loupe), against the grit in hand.</summary>
    private string SurfaceWords(MetalSegment s, CraftAbrasive a, bool loupe)
    {
        if (s.scale > 0.05) return "thick black scale" + (a != null && a.kind == CraftAbrasiveKind.Polish ? ": brush or grind it off first." : ".");
        if (s.scale > 0.005) return "a dark film of scale" + (a != null && a.kind == CraftAbrasiveKind.Polish ? ": brush it off first." : ".");
        string words = CraftSurface.FinishWords(s.rough); // the look's words too
        string sb = loupe ? $"{words} (~{s.rough:0.#} µm)" : words;
        if (a == null || a.kind != CraftAbrasiveKind.Polish)
            return sb + ", clean.";
        if (s.rough > a.finish * CraftSurface.GritReach)
            return $"{sb}: too deep for {a.Name.ToLowerInvariant()} - go back to {Coarser(s.rough)} first.";
        CraftAbrasive next = NextFiner(a);
        if (s.rough <= CraftSurface.GritDone(a, next))
            return next != null ? $"{sb}: {a.Name.ToLowerInvariant()} has done what it can - go on to the {next.Name.ToLowerInvariant()}."
                : $"{sb}: {a.Name.ToLowerInvariant()} has done what it can - no finer grit here.";
        return $"{sb}: {a.Name.ToLowerInvariant()} is bringing it down.";
    }

    /// <summary>The finest grit or wheel here that still reaches scratches this deep.</summary>
    private string Coarser(double rough)
    {
        CraftAbrasive best = null;
        foreach (CraftAbrasive g in Rules.Abrasives)
            if ((g.kind == CraftAbrasiveKind.Polish || g.kind == CraftAbrasiveKind.Wheel) && Here(g) && rough <= g.finish * CraftSurface.GritReach && (best == null || g.finish < best.finish))
                best = g;
        return best != null ? "the " + best.Name.ToLowerInvariant() : "a coarser grit";
    }

    /// <summary>"narrower", "thinner" or "narrower and thinner": how a stretch falls short of the design (<see cref="CraftSurface.Judgement.under"/>).</summary>
    private static string Short(CraftSurface.Judgement j)
    {
        bool narrow = j.widthOff < -0.04, thin = j.thicknessOff < -0.04;
        return narrow && thin ? "narrower and thinner" : narrow ? "narrower" : thin ? "thinner" : "narrower or thinner";
    }

    /// <summary>The line of what matters now: why the work stopped, the tank, the heat, a fitted part, then the next step.</summary>
    private string Advice(CraftPiece p, MetalSegment g, CraftAbrasive a, bool target, float tw, float tt, float ta, bool edged)
    {
        if (!string.IsNullOrEmpty(stopWhy))
            return $"<color=#FF8A70>{stopWhy}</color>";
        if (dipId != null)
            return "Cooling in the tank... it comes out by itself (or Lift it out).";
        if (p.location != CraftLocation.Air && p.location != CraftLocation.Assembled)
            return $"It is {CraftStationUI.LocationText(p)}: take it out to work it.";
        bool fitted = p.location == CraftLocation.Assembled;
        double hot = p.MaxTemperature;
        if (p.kind == CraftPieceKind.Metal && !double.IsInfinity(temperRuns) && hot > temperRuns - 60)
            return $"<color=#FF8A70>Hot: past the red on the HEAT gauge its hardened edge loses its temper (it goes blue and soft) - {CoolIt(p, "stop and let it cool")}.</color>";
        if (p.kind == CraftPieceKind.Metal && hot > 250)
            return $"<color=#FFB070>Hot: the wheel cannot hold it past the HEAT gauge's dark end - {CoolIt(p, "press lighter and let it cool")}.</color>";
        bool wooden = p.kind == CraftPieceKind.Wood;
        string pre = !fitted ? "" : wooden ? "Fitted: worked in place. " : "Fitted: worked in place (it cannot go in the tank - press lighter). ";
        if (!Shaping)
        {
            if (a != null && a.kind == CraftAbrasiveKind.Polish && p.kind == CraftPieceKind.Metal && g.scale <= 0.005 && g.rough < 1.5 && p.Profile(Rules).Steel)
                return pre + "Polished bright: to etch it, lower it into a vat of acid at the tannery.";
            return pre + "Work it grit by grit: the coarse wheel, the fine wheel, then the emeries and the rouge.";
        }
        if (a != null && a.kind == CraftAbrasiveKind.File && p.kind == CraftPieceKind.Metal && CraftSurface.WorkedHardness(Rules, p, g, CurrentSide) > a.maxHardness)
            return pre + "The file will skate here: it is harder than the file (file before hardening, or use the wheel).";
        if (target && edged)
        {
            // (ta is the angle the design's section carries here: the design's own, or steeper toward a point)
            CraftSurface.Judgement j = CraftSurface.Judge(g, tw, tt, ta, edged, p.IntendedEdges(Rules, p.edges));
            bool met = g.EdgeThickness <= CraftSurface.EdgeMet, centred = j.doneL >= 0.8 && j.doneR >= 0.8;
            if (j.progress == CraftSurface.Progress.Off && met && (!j.angleOkL || !j.angleOkR))
                return pre + $"The bevels met at about {(g.AngleL + g.AngleR) * 0.5:0}° a side; the design wants {j.want:0}: grind both at {j.want:0}°, a little at a time, to bring them round.";
            if (j.progress == CraftSurface.Progress.ToGrind && met && centred && j.thicknessOff > 0.04)
                return pre + "Thicker than the design here: grind the faces to thin it (the bevels keep their angle), a little at a time.";
            if (j.progress == CraftSurface.Progress.ToGrind && met && centred && j.widthOff > 0.04)
                return pre + "Wider than the design here: grind both bevels on, in turn, at the same angle - the edge goes back.";
            if (j.progress == CraftSurface.Progress.ToGrind && met)
                return pre + $"The apex is off-centre: grind the {(j.doneL < j.doneR ? "left" : "right")} bevel to bring it to the middle.";
            if (j.progress == CraftSurface.Progress.ToGrind)
                return pre + $"Grind the two bevels in turn, a little at a time, at {j.want:0}° a side (the slider), until they meet in the middle."
                    + (j.under ? $" It is {Short(j)} than the design here (the hammer left it so: the strip's orange line) - grinding cannot put that back, but the edge is wanted all the same." : "");
            if (j.progress == CraftSurface.Progress.TooFar)
                return pre + $"{CraftText.Capital(Short(j))} than the design here, by more than it allows: grinding cannot put it back - leave it.";
            if (j.progress == CraftSurface.Progress.Off)
                return pre + $"The bevels meet here, but it is a little {Short(j)} than the design: grinding cannot put that back - leave it.";
            if (j.progress == CraftSurface.Progress.Done)
                return pre + (Station.Has(CraftTools.Whetstone) ? "Ground here: once the strip is green along the edge, hone it on the whetstone (Sharpen)."
                    : "Ground here: once the strip is green along the edge, hone it on a whetstone (not at this station).");
        }
        else if (target)
        {
            CraftSurface.Judgement j = CraftSurface.Judge(g, tw, tt, ta, edged);
            if (j.progress == CraftSurface.Progress.ToGrind)
                return pre + "Wider or thicker than the design here: the faces make it thinner, the spine narrower.";
            if (j.progress == CraftSurface.Progress.Off || j.progress == CraftSurface.Progress.TooFar)
                return pre + $"{CraftText.Capital(Short(j))} than the design here: grinding cannot put it back - leave it.";
        }
        else if (!p.segments.Exists(x => x.Beveled) && p.kind == CraftPieceKind.Metal)
            return pre + "To make an edge, grind the two bevels in turn, a little at a time."
                + (string.IsNullOrEmpty(p.intentDesign) && Station.Has(CraftTools.Anvil) ? " Say what it is for at the anvil (For): the strip then judges each stretch against its design." : "");
        return pre + (wooden ? "Once it is shaped, sand and oil it at the finishing bench." : "Coat it last, once it is ground, sharpened and polished.");
    }
}

/// <summary>
/// The whetstone. Above: the section where the pointer is (the bevels, the apex, the burr curling over) with the
/// design's edge as a dashed green outline, and a gauge of the angle you hold (gold) against the bevel's (grey) and the
/// design's (green). Below: the stone - drag strokes across it; the pointer's height over the stone is the angle you
/// hold (5° at the bottom, 35° at the top), the steadier the keener. A stroke counts once it has run a fifth of the stone
/// and turns back, and each one goes to the station as it is made. The stone only reaches the apex held at the bevel's
/// angle or steeper; held lower it rubs the bevel's shoulder. The strip along the edge goes from red (dull: it glints)
/// to green (keen); the white line is a burr; the two thin bars over the blade are how far each face has been honed
/// (L blue, R pink). Hone one face with edge-leading strokes (pushed toward the edge: the arrow on the stone says which
/// way) until its bar fills and a burr rolls over, then the other; take the burr off with light trailing strokes (the
/// PRESS gauge's green) or the strop - drawn spine first: edge leading, the edge cuts its leather; then a finer stone.
/// Held well above the bevel and the design's angle (the green line), the stone hones a steeper edge than the item
/// wants, and the line under the controls says so.
/// </summary>
public sealed class CraftSharpenMode : CraftMode
{
    private TMP_Dropdown stone;
    private Slider pressure;
    private Button leftButton, rightButton;
    private TextMeshProUGUI strokeText, info;
    private bool leftFace = true;
    private string stoneId;
    private float pressureValue = 0.45f;
    private readonly List<CraftAbrasive> stones = new List<CraftAbrasive>();
    private readonly CraftStroke stroke = new CraftStroke();
    private Rect stoneArea, sectionBox, gauge, pressBar;
    private float x0, mm, len, heldAngle = 15f, lastAngle = -1f, lastWobble = -1f;
    private bool lastLeading;
    private int from = -1, to = -1, hoverSeg;
    // The gesture under way, decided where it began: a stroke on the stone, or the stretch dragged on the blade.
    private enum Gesture { None, Stroke, Span }
    private Gesture gesture;
    private string strokeId;
    private int sentPasses, spanStart, spanFrom0, spanTo0;
    // Strokes the station took on each face of the piece picked, and the last one's feel.
    private int honedL, honedR;
    private string lastFeel = "";
    // The sharpness strip: red blunt, this between, green keen.
    private static readonly Color Dullish = new Color(1f, 0.85f, 0.35f);

    public override string Title => "Sharpen";
    public override bool Available(CraftStation s) => s.Has(CraftTools.Whetstone);
    public override string Hint =>
        "Drag strokes across the stone (bottom): the pointer's height is the angle you hold; drag on the blade to pick a stretch. Wheel: pressure. Right-click drops a stroke." +
        ByHandHint("sharpen it by hand");
    public override CraftHandsOnMode HandsOnMode => CraftHandsOnMode.Sharpen;
    /// <summary>A fitted blade or head is sharpened in place (its parts are listed under the assembly).</summary>
    public override bool AcceptsFitted(CraftPiece part) => part != null && part.kind == CraftPieceKind.Metal;

    private CraftAbrasive CurrentStone => stones.Count > 0 && stone != null ? stones[Mathf.Clamp(stone.value, 0, stones.Count - 1)] : null;

    protected override void FillHandsOn(ref CraftHandsOnSetup setup)
    {
        setup.abrasive = CurrentStone?.id;
        setup.side = leftFace ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel;
        setup.pressure = pressureValue;
        // The stretch of edge framed here is the one honed by hand too.
        setup.span = from >= 0 && to >= 0;
        setup.from = Mathf.Min(from, to);
        setup.to = Mathf.Max(from, to);
    }

    /// <summary>The stone, the face and the press the hands ended with at the 3D stone come back to the controls.</summary>
    public override void TakeBackHandsOn(in CraftHandsOnSetup setup)
    {
        string id = setup.abrasive;
        int s = stones.FindIndex(a => a.id == id);
        if (s >= 0)
        {
            stoneId = id;
            stone?.SetValueWithoutNotify(s);
        }
        leftFace = setup.side != CraftSurfaceSide.RightBevel;
        if (setup.pressure > 0f)
        {
            pressureValue = Mathf.Clamp(setup.pressure, 0.05f, 1f);
            SetSlider(pressure, pressureValue);
        }
        ShowFace();
    }

    public override void Build(RectTransform controls)
    {
        stones.Clear();
        var names = new List<string>();
        foreach (CraftAbrasive a in Rules.AbrasivesOf(CraftAbrasiveKind.Whetstone)) { stones.Add(a); names.Add(a.Name); }
        RectTransform r1 = Row(controls);
        stone = Drop(r1, names.Count > 0 ? names : new List<string> { "No stone" }, Mathf.Max(0, stones.FindIndex(a => a.id == stoneId)), 190f);
        stone.onValueChanged.AddListener(_ => stoneId = CurrentStone?.id);
        stoneId = CurrentStone?.id;
        leftButton = Btn(r1, "Left face", () => { leftFace = true; ShowFace(); }, 104f, UITheme.ButtonStyle.Tab);
        rightButton = Btn(r1, "Right face", () => { leftFace = false; ShowFace(); }, 104f, UITheme.ButtonStyle.Tab);
        pressure = Slide(r1, "Pressure", 0.05f, 1f, pressureValue, false, 64f, v => v.ToString("0.00"));
        pressure.onValueChanged.AddListener(v => pressureValue = v);
        RectTransform r2 = Row(controls);
        Btn(r2, "Whole edge", () => { from = to = -1; }, 120f, UITheme.ButtonStyle.Ghost);
        HandsOnButton(r2, "On the stone by hand", 200f);
        strokeText = Text(r2, "", 12f);
        RectTransform r3 = Row(controls, 52f);
        info = Text(r3, "", 13f);
        InventoryUIFactory.Size(info, -1f, 52f, 1f);
        FitInside(info);
        ShowFace();
    }

    private void ShowFace()
    {
        UITheme.SetSelected(leftButton, leftFace);
        UITheme.SetSelected(rightButton, !leftFace);
    }

    /// <summary>The stretch of edge worked: the one dragged on the blade, else every stretch with a bevel (the whole piece if none).</summary>
    private void EdgeSpan(CraftPiece p, out int a, out int b)
    {
        int n = p.segments.Count;
        if (from >= 0 && to >= 0) { a = Mathf.Clamp(Mathf.Min(from, to), 0, n - 1); b = Mathf.Clamp(Mathf.Max(from, to), 0, n - 1); return; }
        a = n;
        b = -1;
        for (int i = 0; i < n; i++)
            if (p.segments[i].Beveled) { a = Mathf.Min(a, i); b = Mathf.Max(b, i); }
        if (b < 0) { a = 0; b = n - 1; }
    }

    // ------------------------------------------------------------------ the strokes
    private bool OnBlade(Vector2 pos) => len > 0f && pos.x >= x0 && pos.x <= x0 + len && pos.y > 92f && pos.y < 236f;

    /// <summary>The angle held at height <paramref name="y"/> over the stone (5° at its bottom, 35° at its top).</summary>
    private float AngleAt(float y) => Mathf.Lerp((float)CraftSurface.StoneAngleMin, (float)CraftSurface.StoneAngleMax, Mathf.Clamp01((y - stoneArea.yMin) / Mathf.Max(1f, stoneArea.height)));

    private float YOf(double angle) => stoneArea.yMin + stoneArea.height * Mathf.Clamp01((float)((angle - CraftSurface.StoneAngleMin) / (CraftSurface.StoneAngleMax - CraftSurface.StoneAngleMin)));

    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece p = Selected;
        switch (phase)
        {
            case CraftPointer.Cancel:
                // The blade comes off the stone: the stroke under way is dropped (the passes already made were taken).
                if (gesture == Gesture.Stroke) stroke.Clear();
                else if (gesture == Gesture.Span) { from = spanFrom0; to = spanTo0; }
                gesture = Gesture.None;
                return;
            case CraftPointer.Scroll:
                // The wheel: how hard the hand presses, a twentieth a notch.
                pressureValue = Mathf.Clamp(pressureValue + 0.05f * Mathf.Sign(scroll), 0.05f, 1f);
                SetSlider(pressure, pressureValue);
                return;
            case CraftPointer.Down:
                gesture = Gesture.None;
                if (p == null || p.kind != CraftPieceKind.Metal || p.segments.Count == 0)
                    return;
                if (stoneArea.Contains(pos))
                {
                    gesture = Gesture.Stroke;
                    strokeId = p.id;
                    stroke.Clear();
                    sentPasses = 0;
                    AddSample(pos);
                }
                else if (OnBlade(pos))
                {
                    // Dragging on the blade picks the stretch of edge to work.
                    gesture = Gesture.Span;
                    spanFrom0 = from;
                    spanTo0 = to;
                    spanStart = SegmentAtPixel(p, pos.x, x0, mm);
                    from = to = spanStart;
                }
                return;
            case CraftPointer.Drag:
                if (gesture == Gesture.Stroke)
                {
                    AddSample(pos);
                    SendPasses(false);
                }
                else if (gesture == Gesture.Span && p != null && p.segments.Count > 0)
                    to = SegmentAtPixel(p, Mathf.Clamp(pos.x, x0, x0 + len), x0, mm);
                return;
            case CraftPointer.Up:
                if (gesture == Gesture.Stroke)
                {
                    SendPasses(true);
                    stroke.Clear();
                }
                gesture = Gesture.None;
                return;
        }
    }

    /// <summary>A sample of the stroke: off the stone it is held at the stone's nearest edge (above it, the steepest angle).</summary>
    private void AddSample(Vector2 pos)
    {
        float px = Mathf.Clamp(pos.x, stoneArea.xMin, stoneArea.xMax), py = Mathf.Clamp(pos.y, stoneArea.yMin, stoneArea.yMax);
        float along = (px - stoneArea.xMin) / Mathf.Max(1f, stoneArea.width);
        heldAngle = AngleAt(py);
        // Pushing the blade away (to the right for the left face, to the left for the right) leads with the edge.
        stroke.Add(ui.PointerTime, along, leftFace ? along : 1f - along, heldAngle, pressureValue, Hands);
    }

    /// <summary>Each pass goes to the station as it is made (the run under way too, when the stroke ends); a wiggle is no pass and sends nothing.</summary>
    private void SendPasses(bool ending)
    {
        CraftPiece p = Station != null && Ws != null ? Ws.Piece(strokeId) : null;
        while (sentPasses < stroke.PassRuns.Count)
            SendPass(p, stroke.PassRuns[sentPasses++]);
        if (ending && stroke.Current(out CraftPass last))
            SendPass(p, last);
    }

    private void SendPass(CraftPiece p, CraftPass pass)
    {
        lastAngle = (float)pass.angle;
        lastWobble = (float)pass.wobble;
        lastLeading = pass.leading;
        lastFeel = $"{pass.angle:0}°, {(pass.wobble > 3 ? "rocking" : pass.wobble > 1.2 ? "unsteady" : "steady")}, edge {(pass.leading ? "leading" : "trailing")}";
        CraftAbrasive s = CurrentStone;
        if (p == null || s == null)
            return;
        EdgeSpan(p, out int a, out int b);
        if (Blocked(p, a, b) != null)
            return; // the info row says why (nothing is sent to be refused)
        string id = p.id;
        bool left = leftFace;
        Send(new CraftRequest(CraftRequestKind.Sharpen, id)
        {
            i0 = a, i1 = b, i2 = (int)(left ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel), id = s.id,
            a = (float)pass.angle, b = (float)pass.pressure, count = 1, c = (float)pass.wobble, flag = pass.leading,
        }, (ok, reason) =>
        {
            if (!ok || id != ui.SelectedId)
                return;
            if (left) honedL++;
            else honedR++;
            ShowStrokes();
        });
        ShowStrokes();
    }

    /// <summary>Why the stone cannot work the stretch now (null: it can) - said in the info row, not sent to be refused.</summary>
    private string Blocked(CraftPiece p, int a, int b)
    {
        if (p.location == CraftLocation.Quench) return "It is cooling in the tank.";
        if (p.location != CraftLocation.Air && p.location != CraftLocation.Assembled) return $"Take it out first: it is {CraftStationUI.LocationText(p)}.";
        if (p.MaxTemperature > CraftSurface.StoneTooHot) return "It is too hot for the stone: let it cool first.";
        if (!CraftSurface.HasEdge(p, a, b)) return $"There is no edge here yet: grind or forge the bevels first{GrindWhere()}.";
        return null;
    }

    /// <summary>Where the bevels are ground: the Grind area, when this station has one.</summary>
    private string GrindWhere()
    {
        CraftGrindMode grind = ui.Area<CraftGrindMode>();
        return grind != null && grind.Available(Station) ? " (Grind area)" : " (on a wheel, or with files)";
    }

    private void ShowStrokes()
    {
        if (strokeText == null)
            return;
        strokeText.text = honedL + honedR == 0 && lastFeel.Length == 0 ? ""
            : $"Strokes L {honedL} · R {honedR}{(lastFeel.Length > 0 ? " · last: " + lastFeel : "")}";
    }

    public override void Tick(float dt)
    {
        LiveDraw = gesture != Gesture.None || stoneArea.Contains(ui.Hover);
    }

    // ------------------------------------------------------------------ the selected piece changes, the area closes
    public override void Leave()
    {
        // A stroke still on the stone ends as it is: its passes go to the piece they were made on.
        if (gesture == Gesture.Stroke)
            SendPasses(true);
        stroke.Clear();
        gesture = Gesture.None;
    }

    public override void SelectionChanged(string to)
    {
        // The stretch picked and the strokes counted belong to the piece they were picked on.
        from = this.to = -1;
        hoverSeg = 0;
        honedL = honedR = 0;
        lastFeel = "";
        lastAngle = lastWobble = -1f;
        lastLeading = false;
        ShowStrokes();
    }

    // ------------------------------------------------------------------ drawing
    public override void Draw(CraftPixels c)
    {
        CraftPiece p = Selected;
        stoneArea = new Rect(140, 10, 440, 78);
        sectionBox = new Rect(12, 238, 330, 154);
        gauge = new Rect(356, 238, 352, 154);
        pressBar = new Rect(598, 12, 12, 74);
        CraftAbrasive s = CurrentStone;
        bool metal = p != null && p.kind == CraftPieceKind.Metal && p.segments.Count > 0;
        Color stoneColor = CraftSurface.IsStrop(s) ? new Color(0.45f, 0.3f, 0.18f) : Color.Lerp(new Color(0.35f, 0.33f, 0.3f), new Color(0.75f, 0.72f, 0.68f), s != null ? Mathf.Clamp01(1f - s.finish / 20f) : 0.5f);
        c.Rect(stoneArea.xMin - 6, stoneArea.yMin - 4, stoneArea.xMax + 6, stoneArea.yMax + 4, Wood);
        c.Rect(stoneArea.xMin, stoneArea.yMin, stoneArea.xMax, stoneArea.yMax, stoneColor);
        // The height marks on the stone: 5° at the bottom, 35° at the top, every 5°.
        for (int k = 0; k <= 6; k++)
        {
            float y = stoneArea.yMin + stoneArea.height * k / 6f;
            c.Line(stoneArea.xMin, y, stoneArea.xMin + 12, y, new Color(1f, 1f, 1f, 0.35f), 1f);
        }
        CraftArt.Label(c, stoneArea.xMin - 10 - CraftArt.LabelWidth("5°"), stoneArea.yMin - 3, "5°", CraftArt.LabelInk);
        CraftArt.Label(c, stoneArea.xMin - 10 - CraftArt.LabelWidth("35°"), stoneArea.yMax - 8, "35°", CraftArt.LabelInk);
        CraftArt.Label(c, stoneArea.xMin - 10 - CraftArt.LabelWidth("20°"), YOf(20) - 5, "20°", CraftArt.LabelDim);
        // Which way leads with the edge for the face chosen: pushed toward the edge (right for the left face, left for the
        // right one); pulled back, it trails.
        string way = leftFace ? "< TRAILING   EDGE LEADING >" : "< EDGE LEADING   TRAILING >";
        float wayW = CraftArt.LabelWidth(way), wayX = stoneArea.center.x - wayW * 0.5f;
        c.Rect(wayX - 3f, stoneArea.yMin + 1f, wayX + wayW + 3f, stoneArea.yMin + 14f, new Color(0f, 0f, 0f, 0.35f));
        CraftArt.Label(c, wayX, stoneArea.yMin + 3f, way, CraftArt.LabelInk);
        DrawPress(c);
        if (!metal)
        {
            len = 0f;
            return;
        }
        mm = Fit(p, 660, 110);
        len = (float)(p.Length / mm);
        x0 = (c.Width - len) * 0.5f;
        float[] starts = CraftArt.DrawTop(c, Rules, p, new Rect(x0, 112, len, 104), mm, null, false, ui.Dark);
        EdgeSpan(p, out int a, out int b);
        // Which bar is which face: L on the left of the upper (blue) one, R on the right of the lower (pink) one.
        CraftArt.Label(c, x0 - 12, 99, "L", new Color(0.55f, 0.75f, 1f, 1f));
        CraftArt.Label(c, x0 + len + 5, 92, "R", new Color(1f, 0.6f, 0.85f, 1f));
        for (int i = 0; i < p.segments.Count; i++)
        {
            MetalSegment g = p.segments[i];
            float sx = starts[i], ex = starts[i + 1];
            bool inSpan = i >= a && i <= b;
            float keen = (float)CraftEvaluator.Sharpness(g);
            // Sharpness strip along the edge, by the words' ladder: red blunt, yellow as it gets keen, green razor-keen.
            float k1 = (float)CraftSurface.Keen, k2 = (float)CraftSurface.RazorKeen;
            Color col = keen >= k2 ? Good : keen >= k1 ? Color.Lerp(Dullish, Good, (keen - k1) / (k2 - k1)) : Color.Lerp(Harm, Dullish, keen / k1);
            c.Rect(sx + 0.5f, 222, ex - 0.5f, 232, inSpan ? col : new Color(col.r, col.g, col.b, 0.3f));
            if (g.burr > CraftSurface.BurrShows) c.Rect(sx + 0.5f, 232, ex - 0.5f, 235, new Color(0.92f, 0.92f, 0.98f, 0.9f));
            // How far each face has been honed.
            c.Rect(sx + 0.5f, 102, sx + 0.5f + (ex - sx - 1f) * Mathf.Clamp01((float)g.honeL), 106, new Color(0.55f, 0.75f, 1f, inSpan ? 0.95f : 0.35f));
            c.Rect(sx + 0.5f, 95, sx + 0.5f + (ex - sx - 1f) * Mathf.Clamp01((float)g.honeR), 99, new Color(1f, 0.6f, 0.85f, inSpan ? 0.95f : 0.35f));
        }
        if (from >= 0 && to >= 0)
            c.Frame(starts[a], 108, starts[b + 1], 220, Mark, 1.5f);
        // The section where the pointer is over the blade (else the middle of the stretch worked).
        Vector2 h = ui.Hover;
        hoverSeg = OnBlade(h) ? Mathf.Clamp(SegmentAtPixel(p, h.x, x0, mm), 0, p.segments.Count - 1) : Mathf.Clamp((a + b) / 2, 0, p.segments.Count - 1);
        MetalSegment seg = p.segments[hoverSeg];
        bool hasTarget = CraftArt.TargetAt(Rules, p, hoverSeg, out float tw, out float tt, out float ta, out bool edged);
        // Magnify the edge: the section's own width is drawn only near the apex.
        var edgeOnly = new MetalSegment
        {
            width = Math.Max(Math.Max(seg.bevelWidthL, seg.bevelWidthR) * 1.6, 4), thickness = seg.thickness, bevelDepthL = seg.bevelDepthL, bevelDepthR = seg.bevelDepthR,
            bevelWidthL = seg.bevelWidthL, bevelWidthR = seg.bevelWidthR, apex = seg.apex, burr = seg.burr, chip = seg.chip, honeL = seg.honeL, honeR = seg.honeR,
        };
        float scale = CraftArt.SectionScale(edgeOnly, sectionBox);
        Vector2 o = CraftArt.DrawSection(c, edgeOnly, sectionBox, CraftArt.MetalColor(Rules, p), scale);
        if (hasTarget && edged)
            CraftArt.DrawTargetSection(c, o, scale, (float)edgeOnly.width, (float)seg.thickness, ta, true, false);
        double bevel = leftFace ? seg.AngleL : seg.AngleR;
        DrawGauge(c, hasTarget && edged ? ta * 0.5f : -1f, (float)bevel);
        // On the stone's height marks: the design's angle (green) and this face's bevel (grey) - hold it there or a little above.
        if (hasTarget && edged) c.Rect(stoneArea.xMin - 4, YOf(ta * 0.5f) - 1, stoneArea.xMin + 18, YOf(ta * 0.5f) + 1, CraftArt.TargetColor);
        if (bevel > 0) c.Rect(stoneArea.xMin - 4, YOf(bevel) - 1, stoneArea.xMin + 18, YOf(bevel) + 1, new Color(0.75f, 0.75f, 0.78f, 0.9f));
        // The blade on the stone while stroking: its spine and edge at the angle held.
        if (stoneArea.Contains(h) || gesture == Gesture.Stroke)
        {
            float bx = Mathf.Clamp(h.x, stoneArea.xMin, stoneArea.xMax), by = Mathf.Clamp(h.y, stoneArea.yMin, stoneArea.yMax);
            if (h.x < 0f) { bx = stoneArea.center.x; by = YOf(heldAngle); }
            if (gesture != Gesture.Stroke) heldAngle = AngleAt(by);
            // The blade rises from the stone at the angle held, its spine away from the edge that leads - kept inside the
            // stone's wooden frame, clear of the PRESS gauge, the angle marks and the honing bars.
            float dir = leftFace ? 1f : -1f, dx = -dir * Mathf.Cos(heldAngle * Mathf.Deg2Rad), dy = Mathf.Sin(heldAngle * Mathf.Deg2Rad);
            float sy = by - 4f, room = 70f;
            float frameL = stoneArea.xMin - 6f, frameR = stoneArea.xMax + 6f, frameT = stoneArea.yMax + 4f;
            if (dx < -1e-4f) room = Mathf.Min(room, (bx - frameL) / -dx);
            if (dx > 1e-4f) room = Mathf.Min(room, (frameR - bx) / dx);
            if (dy > 1e-4f) room = Mathf.Min(room, (frameT - sy) / dy);
            room = Mathf.Max(6f, room);
            c.Line(bx + dx * room, sy + dy * room, bx, sy, Mark, 3f);
            c.Disc(bx, sy, 2.5f, Color.white);
        }
    }

    /// <summary>The PRESS gauge by the stone: the pressure set; green where a trailing stroke takes the burr off (brighter the lighter), red where a leading one chips a hard edge.</summary>
    private void DrawPress(CraftPixels c)
    {
        Meter(c, pressBar.x, pressBar.y, pressBar.width, pressBar.height, pressureValue, Mark,
            (0f, (float)CraftSurface.BurrOffFull, Color.Lerp(GoodBand, Good, 0.35f)), ((float)CraftSurface.BurrOffFull, (float)CraftSurface.BurrOffBelow, GoodBand),
            (0.7f, 1f, HarmBand));
        // Its name in the middle, its bands' words beside them: light at the bottom, chipping at the top.
        CraftArt.Label(c, pressBar.xMax + 8, pressBar.yMin + pressBar.height * 0.4f, "PRESS", CraftArt.LabelInk);
        CraftArt.Label(c, pressBar.xMax + 8, pressBar.yMin + 2, "LIGHT", Good);
        CraftArt.Label(c, pressBar.xMax + 8, pressBar.yMin + 4 + pressBar.height * 0.7f, "CHIPS", Harm);
    }

    /// <summary>The angle you hold (gold) against the bevel's (grey) and the design's (green) - the stone flat, the blade leaning on it.</summary>
    private void DrawGauge(CraftPixels c, float targetAngle, float bevelAngle)
    {
        c.Rect(gauge.xMin, gauge.yMin, gauge.xMax, gauge.yMax, new Color(0f, 0f, 0f, 0.45f));
        float bx = gauge.xMin + 40, by = gauge.yMin + 28;
        // As long as the steepest angle stays inside the gauge.
        float L = Mathf.Min(gauge.width - 70, (gauge.yMax - 8 - by) / Mathf.Sin((float)CraftSurface.StoneAngleMax * Mathf.Deg2Rad));
        c.Rect(bx - 20, by - 14, bx + L + 10, by, new Color(0.45f, 0.43f, 0.4f));
        for (int k = 5; k <= 35; k += 5)
        {
            float r = k * Mathf.Deg2Rad;
            c.Line(bx + (L - 8) * Mathf.Cos(r), by + (L - 8) * Mathf.Sin(r), bx + L * Mathf.Cos(r), by + L * Mathf.Sin(r), new Color(1f, 1f, 1f, 0.3f), 1f);
        }
        float r5 = (float)CraftSurface.StoneAngleMin * Mathf.Deg2Rad, r35 = (float)CraftSurface.StoneAngleMax * Mathf.Deg2Rad;
        CraftArt.Label(c, bx + L * Mathf.Cos(r5) - 18, by + L * Mathf.Sin(r5) + 4, "5°", CraftArt.LabelInk);
        CraftArt.Label(c, bx + L * Mathf.Cos(r35) + 4, by + L * Mathf.Sin(r35) - 8, "35°", CraftArt.LabelInk);
        if (bevelAngle > 0f)
        {
            float r = Mathf.Min(bevelAngle, 40f) * Mathf.Deg2Rad;
            c.Line(bx, by, bx + L * Mathf.Cos(r), by + L * Mathf.Sin(r), new Color(0.75f, 0.75f, 0.78f, 0.9f), 2f);
        }
        if (targetAngle > 0f)
        {
            float r = targetAngle * Mathf.Deg2Rad;
            c.Line(bx, by, bx + L * Mathf.Cos(r), by + L * Mathf.Sin(r), CraftArt.TargetColor, 2f);
        }
        float shown = stoneArea.Contains(ui.Hover) || gesture == Gesture.Stroke ? heldAngle : lastAngle > 0f ? lastAngle : heldAngle;
        float hr = shown * Mathf.Deg2Rad;
        c.Line(bx, by, bx + L * Mathf.Cos(hr), by + L * Mathf.Sin(hr), Mark, 4f);
        // The legend: the gauge's lines, and the strip's colours.
        float lx = gauge.xMax - 92, ly = gauge.yMax - 18;
        GaugeKey(c, lx, ly, "HELD", Mark);
        GaugeKey(c, lx, ly - 15, "BEVEL", new Color(0.75f, 0.75f, 0.78f, 0.9f));
        GaugeKey(c, lx, ly - 30, "DESIGN", CraftArt.TargetColor);
        // the strip's keys under their caption (y up: the caption first, above them)
        CraftArt.Label(c, lx, ly - 51, "STRIP:", CraftArt.LabelDim);
        GaugeKey(c, lx, ly - 66, "DULL", Harm);
        GaugeKey(c, lx, ly - 81, "KEEN", Good);
        GaugeKey(c, lx, ly - 96, "BURR", new Color(0.92f, 0.92f, 0.98f));
    }

    private static void GaugeKey(CraftPixels c, float x, float y, string text, Color col)
    {
        c.Rect(x, y + 1, x + 9, y + 9, col);
        CraftArt.Label(c, x + 13, y, text, CraftArt.LabelInk);
    }

    // ------------------------------------------------------------------ what it says
    /// <summary>Where the edge stands and what to do next, by the station's own thresholds.</summary>
    public override void Refresh()
    {
        if (info == null)
            return;
        CraftPiece p = Selected;
        if (p == null || p.kind != CraftPieceKind.Metal || p.segments.Count == 0)
        {
            info.text = "Pick a blade (or a fitted part, under its assembly) from the pieces on the left. Strokes on the stone (bottom) sharpen the stretch of edge framed in gold (drag on the blade to choose it).";
            return;
        }
        EdgeSpan(p, out int a, out int b);
        double hl = 0, hr = 0, burr = 0, keen = 0, apex = 0, floor = 0, flat = 0, soft = 0, want = 0, micro = 0;
        int n = 0, wanted = 0, honed = 0;
        CraftAbrasive s = CurrentStone;
        double wobble = Math.Max(lastWobble > 0f ? lastWobble : 0, 11.5 * CraftRequestGuard.FloorOf(Hands.skill));
        bool edge = CraftSurface.HasEdge(p, a, b);
        for (int i = a; i <= b && i < p.segments.Count; i++)
        {
            MetalSegment g = p.segments[i];
            if (edge && !CraftSurface.HasEdge(g)) continue; // the stretches the stone hones
            hl += Math.Min(1, g.honeL); hr += Math.Min(1, g.honeR); burr = Math.Max(burr, g.burr);
            keen += CraftEvaluator.Sharpness(g); apex += g.apex; flat = Math.Max(flat, g.EdgeThickness);
            if (s != null)
            {
                floor += CraftSurface.SharpenFloor(Rules, p, g, s, wobble);
                soft += CraftSurface.SoftFactor(CraftMetallurgy.Hardness(Rules, p.Profile(Rules), g, g.edge, Math.Max(0.2, g.EdgeThickness)));
            }
            // the angle a side the design wants here (as its section carries it), and the angle the stone has honed
            if (CraftArt.TargetAt(Rules, p, i, out _, out _, out float ta, out bool edged) && edged) { want += ta * 0.5; wanted++; }
            if (g.microAngle > 0) { micro += g.microAngle; honed++; }
            n++;
        }
        n = Math.Max(1, n);
        hl /= n; hr /= n; keen /= n; apex /= n; floor /= n; soft /= n;
        want = wanted > 0 ? want / wanted : 0;
        micro = honed > 0 ? micro / honed : 0;
        bool loupe = Station.HasInstrument(CraftInstruments.Loupe), calipers = Station.HasInstrument(CraftInstruments.Calipers);
        bool strop = CraftSurface.IsStrop(s);
        var sb = new StringBuilder();
        sb.Append($"<color=#8CB8FF>Left face {hl * 100:0}%</color> · <color=#FF99D9>right face {hr * 100:0}%</color> · the edge {CraftSurface.KeenWords(keen)}{(loupe ? $" (apex ~{apex:0.#} µm)" : "")}");
        if (calipers && micro > 0) sb.Append($" · honed at {micro:0}°{(want > 0 ? $" (the design {2 * want:0}°)" : "")}");
        if (burr > CraftSurface.BurrShows) sb.Append(" · a burr");
        sb.Append(p.location == CraftLocation.Assembled ? " · fitted: sharpened in place.\n" : ".\n");
        double thisFace = leftFace ? hl : hr, otherFace = leftFace ? hr : hl;
        string other = leftFace ? "right" : "left", mine = leftFace ? "left" : "right";
        // Edge leading is a push toward the edge (to the right for the left face, the stone's arrow); trailing a pull back.
        string lead = leftFace ? "push it to the right" : "push it to the left", trail = leftFace ? "pull it to the left" : "pull it to the right";
        CraftSurface.HoneStep step = CraftSurface.NextHone(thisFace, otherFace, burr, flat, edge, apex, s != null ? floor : apex);
        string why = Blocked(p, a, b);
        if (why != null && step != CraftSurface.HoneStep.NoEdge)
            sb.Append($"<color=#FF8A70>{why}</color>");
        else
            switch (step)
            {
                case CraftSurface.HoneStep.NoEdge:
                    sb.Append($"<b>There is no edge here yet</b>: grind the bevels first{GrindWhere()} - a stone only hones an edge that is there.");
                    break;
                case CraftSurface.HoneStep.WearFlat:
                    sb.Append($"A flat ({(calipers ? $"{flat:0.##} mm" : "you can see it")}) still shows along the edge: the bevels have not met here - {(strop ? "the strop does not wear it away" : "the stone wears it away only slowly")}, so better grind them until they meet (the grind strip goes green){GrindWhere()}.");
                    break;
                case CraftSurface.HoneStep.WearLastFlat:
                    sb.Append(strop
                        ? $"The bevels have met, but a hair of flat ({(calipers ? $"{flat:0.##} mm" : "you can see it")}) still shows: the strop does not wear it away - a few strokes on a stone first."
                        : $"The bevels have met, but a hair of flat ({(calipers ? $"{flat:0.##} mm" : "you can see it")}) still shows: stroke the <b>{mine} face</b> and the {other}, edge leading ({lead} for this one) - the stone wears it away first, then the faces hone (their bars fill).");
                    break;
                case CraftSurface.HoneStep.HoneFace:
                    sb.Append(strop
                        ? $"On the strop, draw the <b>{mine} face</b> spine first ({trail}) until its bar fills - edge leading, the edge cuts into the leather."
                        : $"Stroke the <b>{mine} face</b> at a steady angle, edge leading ({lead} across the stone), until its bar fills - a burr starts to curl over (the white line).");
                    break;
                case CraftSurface.HoneStep.TurnFace:
                    sb.Append($"{(burr > CraftSurface.BurrShows ? "A burr has rolled over" : "This face is honed")}: now the <b>{other} face</b>, the same way.");
                    break;
                case CraftSurface.HoneStep.TakeBurrOff:
                    sb.Append(strop
                        ? $"Take the burr off on the strop: each face drawn spine first ({trail} for the {mine} face)."
                        : $"Take the burr off: <b>light trailing strokes</b> ({trail}, toward the spine; pressure under {CraftSurface.BurrOffBelow:0.0}, the lighter the faster - the PRESS gauge's green) or the strop, spine first.");
                    break;
                case CraftSurface.HoneStep.KeepHoning:
                    sb.Append(strop
                        ? "Both faces honed: a few more strokes on each face, spine first, bring it to the strop's best."
                        : "Both faces honed: a few more steady strokes on each face bring it to this stone's best (light trailing strokes raise no burr).");
                    break;
                case CraftSurface.HoneStep.StoneTooCoarse:
                    sb.Append("This stone is coarser than the edge already is: it would only coarsen it - go to a finer stone or the strop.");
                    break;
                default:
                    if (soft > 1.2) sb.Append("As keen as this metal holds: a soft metal does not keep a finer edge (a hardened steel does).");
                    else if (wobble > 1.5) sb.Append("As keen as these strokes make it: steadier strokes go finer.");
                    else sb.Append(s != null && FinerStone(s) ? "As keen as this stone makes it: go to a <b>finer stone</b>, then the strop (spine first)." : "Finished: as keen as it gets.");
                    break;
            }
        // The angle: below the bevel the stone only rubs its shoulder; well above the bevel and the design's angle it hones
        // a steeper edge than the item wants. Edge leading, the strop's leather is cut.
        double bevel = 0;
        for (int i = a; i <= b && i < p.segments.Count; i++) bevel = Math.Max(bevel, leftFace ? p.segments[i].AngleL : p.segments[i].AngleR);
        double steepest = Math.Max(want, bevel);
        if (strop && lastLeading && lastAngle > 0f)
            sb.Append($"\n<color=#FFB070>Edge leading, the edge cuts into the strop's leather and dulls: draw it spine first ({trail}).</color>");
        else if (bevel - 1 > CraftSurface.StoneAngleMax)
            sb.Append($"\n<color=#FFB070>This bevel is steeper than the stone can be held: grind it lower on the wheel{GrindWhere()}.</color>");
        else if (lastAngle > 0f && bevel > 0 && CraftSurface.Contact(lastAngle, bevel) < 1)
            sb.Append($"\n<color=#FFB070>Hold it steeper: at about {lastAngle:0}° the stone only rubs the bevel's shoulder - hold it at the grey line on the gauge or a little above{(calipers ? $" (this bevel is {bevel:0}°)" : "")}.</color>");
        else if (lastAngle > 0f && steepest > 0 && lastAngle > steepest + 3)
            sb.Append($"\n<color=#FFB070>Hold it lower: at about {lastAngle:0}° the stone hones the edge steeper than {(want >= bevel ? $"the design's {want:0}° a side (the green line)" : $"its bevel{(calipers ? $" ({bevel:0}°)" : "")} (the grey line)")} - a steeper edge cuts worse.</color>");
        info.text = sb.ToString();
    }

    /// <summary>Has the station a stone finer than <paramref name="s"/>?</summary>
    private bool FinerStone(CraftAbrasive s)
    {
        foreach (CraftAbrasive a in stones)
            if (a.apex < s.apex - 1e-6) return true;
        return false;
    }
}
