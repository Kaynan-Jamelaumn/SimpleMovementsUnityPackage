using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>What the player works with their own hands at a crafting station, in 3D.</summary>
public enum CraftHandsOnMode { None, Hammer, Bellows, Grind, Sharpen, Quench, Pour, Carve, Brush }

/// <summary>What the window had chosen when a hands-on work place was taken up (the tool, the stone, the mould...).</summary>
public struct CraftHandsOnSetup
{
    public string piece;
    public CraftStrikeTool tool;
    public CraftStrikeFace face;
    public string abrasive;
    public CraftSurfaceSide side;
    /// <summary>
    /// The wheel's angle; the hand's pressure (the wheel's, the whetstone's, the carving knife's setting); how deep it
    /// goes into the fire or the tank; the entry into the tank (0 upright, point first; 0.9 flat).
    /// </summary>
    public float angle, pressure, depth, entryAngle;
    public string mold, fuel;
    public bool fromFurnace;
    /// <summary>The ore bench: the stone to brush first and the brush in hand.</summary>
    public int fragment;
    public CraftOreBrush brush;
    /// <summary>
    /// A stretch of the piece chosen in the window - the forge's span (what goes into the fire), the whetstone's framed
    /// stretch of edge: segments <see cref="from"/> to <see cref="to"/> when <see cref="span"/> is set, the whole piece
    /// (the whole edge) otherwise.
    /// </summary>
    public bool span;
    public int from, to;
    /// <summary>
    /// The carving cut: 0 shave the width, 1 shave the thickness, 2 round it, 3 bore the tang slot - the 3D knife's cuts.
    /// Any other (the window's saw, 4) starts the knife shaving the width, and comes back as it was unless a cut is
    /// chosen by hand.
    /// </summary>
    public int cut;
    /// <summary>The anvil's horn: the far end is bent down (false: up), as the window's toggle says.</summary>
    public bool bendDown;
}

/// <summary>
/// A crafting station worked with the hands, in 3D, instead of on the window's drawing: the window steps aside, the
/// view moves to the work place - at the station's own point for it (<see cref="CraftStationVisuals.HandsOnPoint"/>),
/// seen from the side the player stands on, the station's own model of it and the player's body stepping aside - and
/// the mouse becomes the hand (what it carries moves as it moves on the screen). Every key is an input action of the station map
/// (<see cref="StationControls"/>): rebindable, shown in the hints as bound for the device in use, and meaning the same
/// at every work place (the defaults, keyboard and mouse / gamepad):
/// <list type="bullet">
/// <item><b>Work</b> (left button / right trigger): the hand - hold it to raise the hammer, press the blade to the
/// wheel, draw a stroke, tip the crucible, scrub, cut.</item>
/// <item><b>Put Down</b> (right-click or Q / B): the tool down - what the hands did is sent - and the window comes back;
/// Escape goes back to the game. The work goes on as it is.</item>
/// <item><b>Next Tool</b> (Tab / right shoulder): the next tool in hand - the hammer's, the wheel or file, the stone,
/// the brush, the knife's cut.</item>
/// <item><b>The number keys</b> and <b>Previous / Next Choice</b> (, and . / the d-pad's left and right): the work
/// place's list of choices - the hammer's tools, the wheel's side, the face on the stone, the entry into the tank, the
/// stone on the ore bench, the knife's cut.</item>
/// <item><b>Less / More</b> ([ and ] / the d-pad's down and up) and the wheel: adjust what is in hand - the air vent,
/// the angle at the wheel, the press on the stone, how deep it goes into the tank, the knife's setting, the stone
/// turned in the fingers, the way the horn bends.</item>
/// <item><b>Quick</b> (Space / A), <b>Turn Over</b> (T / Y), <b>Fire</b> (R / X), <b>Face or edge</b> and
/// <b>Feed the fire</b> (F / left shoulder): the work place's own actions.</item>
/// </list>
/// The work places:
/// <list type="bullet">
/// <item><b>Anvil</b>: the piece lies on the anvil as it really is (its shape segment by segment - the eye, the
/// fuller, a cut, the cracks that show - glowing at each segment's heat), as the window's top view draws it. Move along
/// it to aim; hold Work to raise the hammer (the longer, the harder - the Blow meter full at a full blow, red once the
/// arm wavers) - it comes up only as soon after a blow as the window's hammer does (a press while one falls waits for it);
/// move well up or down while holding to tilt the face (a little drift is no tilt; past what strikes flat the Tilt meter
/// turns amber and the bevel it forges is shaded on the bar); release to strike. Over the horn, Less / More choose which
/// way the far end bends. The hot chisel stays where it was set. A weld still open: its heat under the hammer on the
/// second meter. Face or edge, Turn Over, Fire: into the fire and out (in the fire, its heat in words).</item>
/// <item><b>Bellows</b>: hold Work and pull down to squeeze them, up to let them fill (or Quick); a steady rhythm builds
/// the draft - the meter is the draft the fire gets through its vent, the words say the rhythm. Fire, Feed the fire,
/// Turn Over (the piece in the coals), Less / More: the air vent.</item>
/// <item><b>Wheel</b> (or file, brush, polish): hold Work to press the blade to the wheel - sideways along the blade,
/// higher presses harder (0.7 to 1.3 × the pressure set in the window); Less / More: the angle (5-35°); the choices: the
/// side; Quick dips it in the tank until it is cool (and lifts it out) - not a fitted part, wood, or a piece still hot
/// enough to harden (a dip would quench it).</item>
/// <item><b>Whetstone</b>: hold Work and draw the blade across the stone; the hand's height is the angle - each stroke
/// that is a real pass goes as it is made, a wiggle is none. Next Tool: the stone; Turn Over or the choices: the face;
/// Less / More: how hard it is pressed.</item>
/// <item><b>Quench</b>: hold Work to plunge the hot piece (or take hold of one already in the tank) and keep it moving -
/// up and down along it or round, not side to side; release to lift it out. The choices: upright, point first, or flat;
/// Less / More: how much of it goes in.</item>
/// <item><b>Crucible</b>: carry the crucible sideways until the stream will land in the cup, hold Work and raise the
/// hand to tip it; keep the stream steady and unbroken, and tip it back (let go) as soon as the mould is full - kept
/// tipped it runs over. Checked before the hands start (a mould chosen, the melt melted, metal to pour); a melt too
/// small for the mould is not stopped - it casts short, the Mould meter's red tick showing where it runs out.</item>
/// <item><b>Ore bench</b>: a stone of the lump on the bench as it is - ore in veins, a weathered rind, dirt and caked
/// clay. Hold Work and scrub over it (each going-over is sent as it ends); hold Quick and move to turn it in the hand,
/// Less / More spin it, Turn Over turns it over; Next Tool: the brush; the choices: the stone.</item>
/// <item><b>Carving bench</b>: hold Work and draw the knife along the blank (where it starts and stops is the stretch
/// cut; straight is clean - the knife shows how far the hand is off the stroke's line, the Straight meter how straight
/// it runs); Less / More: the knife's setting (how deep it bites); boring: the brace and bit stand at the end nearer the
/// hand - hold Work and circle the hand, round and even, wherever it is: each circle turns the bit a stretch deeper (the
/// Steady meter: how evenly). The design's outline lies over the wood, and its shape is told under the knife.</item>
/// </list>
/// What the hands do reaches the workshop as the window's requests do (<see cref="CraftStation.Request"/>), measured by
/// the same hand models (<see cref="CraftHammer"/>, <see cref="CraftStroke"/>, <see cref="CraftPourControl"/>,
/// <see cref="CraftBellowsPump"/>, <see cref="CraftHandTrail"/>, <see cref="CraftBrushCover"/>) and checked by the same
/// rules before anything is sent, so it works the same offline and in a network session, where the server checks it.
/// Every refusal - and every note on work granted only in part - shows in the message line over the panel
/// (<see cref="Say"/>), and what a request did is read from its answer (on a network client once the copy of the workshop
/// shows it).
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Crafting/Craft Hands On")]
public class CraftHandsOn : MonoBehaviour
{
    [Header("View")]
    [SerializeField, Range(20f, 90f)] private float fieldOfView = 48f;
    [Tooltip("How far from the work place the view stands (metres).")]
    [SerializeField, Range(0.3f, 3f)] private float viewDistance = 0.85f;

    /// <summary>The hammer's tools, in the order the number keys and the choices pick them (the hot chisel last).</summary>
    private static readonly CraftStrikeTool[] ToolOrder =
    {
        CraftStrikeTool.Flat, CraftStrikeTool.PeenAcross, CraftStrikeTool.PeenAlong, CraftStrikeTool.Fuller, CraftStrikeTool.Horn,
        CraftStrikeTool.Hardy, CraftStrikeTool.Drift, CraftStrikeTool.Flatter, CraftStrikeTool.Dishing, CraftStrikeTool.Straighten,
        CraftStrikeTool.Chisel,
    };
    private static readonly CraftSurfaceSide[] Sides =
        { CraftSurfaceSide.LeftBevel, CraftSurfaceSide.RightBevel, CraftSurfaceSide.Faces, CraftSurfaceSide.EdgeLine, CraftSurfaceSide.Spine, CraftSurfaceSide.Tang, CraftSurfaceSide.Eye };
    private static readonly string[] SideNames = { "left bevel", "right bevel", "faces", "edge line", "spine", "tang", "eye" };
    /// <summary>The wheel's work goes to the station in batches of this much of its time (s) while the blade is held to it - as the window's.</summary>
    private const float GrindEvery = 0.2f;
    /// <summary>The 3D requests are numbered from here on a network client, apart from the window's (<see cref="CraftAnswerWaits"/>).</summary>
    private const int NumberFrom = 1 << 30;

    private CraftStation station;
    private CraftStationVisuals visuals;
    private CombatEntity player;
    private CraftHands hands;
    private CraftHandsOnMode mode, worked;
    private CraftHandsOnSetup setup;
    private Action<bool> onEnd;

    // Requests and their answers: a network client numbers its requests and waits for the station's answers; where the
    // rules run the events a request raises are caught for its answer.
    private readonly CraftAnswerWaits waits = new CraftAnswerWaits(NumberFrom);
    private CraftStation listening;
    private List<CraftEvent> capture;
    private static readonly CraftEvent[] NoEvents = new CraftEvent[0];
    private IReadOnlyList<CraftEvent> answerEvents = NoEvents;
    private bool relaying;

    // The view and the HUD. The game's own camera draws nothing meanwhile (it stays, with its listener).
    private Camera cam, mainCam;
    private int mainMask;
    private bool cursorWasVisible;
    private CursorLockMode cursorWasLocked;
    private Canvas hud;
    private TextMeshProUGUI hudTitle, hudFeel, hudHint, hudLegend, hudMessage;
    /// <summary>The dark band behind the message line, as wide as its words (shown only while there are any).</summary>
    private Image hudMessageBack;
    private HudMeter meter, meter2, arms;
    private float shake, hintTimer, messageUntil;
    /// <summary>How tired the arm was last said to be (0 fresh, 1 tiring, 2 heavy, 3 spent), as the window counts it.</summary>
    private int armLevel;

    // The work place: a frame at the tool's place (x along the station - the view's left is +x - y up, z toward the
    // player), the floor's height in it, what of the station and of the player is hidden meanwhile, the materials it made.
    private Transform root;
    private float floorY;
    private readonly List<Renderer> hiddenModels = new List<Renderer>(), hiddenPlayer = new List<Renderer>();
    private readonly List<Material> ownMaterials = new List<Material>();
    private Transform pieceObject;
    private MeshFilter pieceFilter;
    private MeshRenderer pieceRenderer;
    private uint pieceShape;
    private bool pieceBuilt;
    private float pieceTimer;
    private float pieceLength = 0.2f, pieceWidth = 0.03f;

    // Pointer.
    private Vector2 pressAt;
    private bool held, wasHeld, waitRelease;

    // Anvil.
    private readonly CraftHammer hammer = new CraftHammer();
    private Transform hammerObject, bevelMark;
    private float aim = 0.5f, tilt, swing = -1f, flipShown, chiselAt = -1f;
    private float slipShown;
    private CraftBlow pendingBlow;
    private float pendingAt;
    private bool pendingRaise;
    private string lastBlow = "";

    // Bellows.
    private readonly CraftBellowsPump pump = new CraftBellowsPump();
    private float airPending;
    private Transform bellowsTop;
    private float opening = 1f, ventSet = -1f, ventAt;
    private bool squeezing;

    // Wheel and stone.
    private readonly List<CraftAbrasive> abrasives = new List<CraftAbrasive>();
    private int abrasive;
    private readonly CraftStroke stroke = new CraftStroke();
    private float sendTimer;
    private Transform wheelObject;
    private float wheelTurn, sparkClock;
    private bool leftFace = true;
    // The wheel: a press under way and why the work stopped; the dip in the tank.
    private bool grinding, dipAsked;
    // The work place was put down while a dip was still to be answered (a network client): lifted out once it is.
    private bool dipLeft;
    private string grindStop, dipId;
    // Was the wheel's stop reason this place's own check (too hot, in the tank)? It goes once that passes again, as the window's.
    private bool grindStopLocal;
    private float dipTime;
    // The whetstone: the stroke under way, the passes of it sent, the strokes counted per face, the last pass.
    private bool stroking;
    private int passesSent, honedL, honedR;
    private string honeStop, lastFeel = "";
    private float lastAngle = -1f, lastWobble = -1f;
    private bool lastLeading;
    private readonly List<CraftPass> passBuffer = new List<CraftPass>();

    // Quench: the hand that holds the piece in the tank (plunged here, or taken hold of where the window left it).
    private readonly CraftHandTrail tankHand = new CraftHandTrail(0.6, 2);
    private CraftQuenchMotion sentMotion = CraftQuenchMotion.Still;
    private float motionTimer, quenchDepth, tankLength = 0.6f;
    private bool plunged, ownPlunge;
    /// <summary>Lifts out of the tank sent and not yet answered (a network client): meanwhile the piece is out as far as the hand goes.</summary>
    private int liftsAsked;
    private Transform tongsObject;

    // Carving.
    private static readonly string[] CutNames = { "shave the width", "shave the thickness", "round it", "bore the tang slot" };
    private int carveCut;
    /// <summary>A cut was chosen by hand (the window's own cut comes back otherwise: <see cref="Setup"/>).</summary>
    private bool cutPicked, carving, boring;
    private float carveFrom, carveTo, carveStart, chipClock;
    /// <summary>The hand's way across the screen in the knife stroke under way: how straight it kept to its own line.</summary>
    private readonly List<Vector2> carvePath = new List<Vector2>();
    private int boreEnd;
    /// <summary>Turns of the bit sent and not yet answered: the next reaches past them (they have not bored their stretch yet).</summary>
    private int boreAhead;
    private Transform knifeObject, bitObject;
    /// <summary>How straight the last knife stroke was (as sent; -1 before the first), and how far round the bit has turned (degrees, as drawn).</summary>
    private float lastStraight = -1f, bitSpin, bitFrom;
    // The design's shape laid over the wood: its outline as the blank lies, and as it lies on its side.
    private Transform ghostWidth, ghostThick;
    private Material ghostMaterial;

    // Ore bench: the stone in 3D, turned in the hand, and the brush.
    private static readonly string[] BrushNames = { "soft brush", "stiff brush", "wire brush" };
    private readonly CraftOreRock rock = new CraftOreRock();
    private Transform rockObject, brushObject;
    private MeshFilter rockFilter;
    private Material rockMaterial;
    private CraftOreBrush oreBrush;
    private Quaternion rockTurn = Quaternion.identity, rockShown = Quaternion.identity;
    private Vector2 lastPointer;
    private readonly CraftHandTrail scrubHand = new CraftHandTrail(0.15, 1);
    private float brushSeconds, brushTired, scrubPhase, dustClock;
    private int brushLayout;
    /// <summary>The stone in the hand as the workshop had it when it was put on the bench (what each brush's going-over is counted on).</summary>
    private OreFragment stoneAtBench;
    /// <summary>The circles the hand draws turning the bit, counted round their own middle wherever they are drawn.</summary>
    private readonly CraftHandCircles boreCircles = new CraftHandCircles();
    // Shavings and dust flying (a few kept for the next, not made and destroyed each time).
    private readonly List<(Transform t, Vector3 v, float life)> chips = new List<(Transform, Vector3, float)>();
    private readonly Stack<Transform> chipPool = new Stack<Transform>();

    // Crucible.
    private CraftPourControl pour;
    private bool tippingBack;
    private double pourNeed;
    private float fillSeconds = 4f, lastPourAt = -99f;
    /// <summary>The station's pace of pours (the guard: one each two seconds): a pour started sooner would be refused when it ends.</summary>
    private const float PourEvery = 2.05f;
    private Transform pourSpace, crucibleObject, streamUpper, streamLower;
    // The mould's cavity in the shape of what it casts, and the metal filling it cell by cell from the mouth.
    private readonly List<Transform> fillCells = new List<Transform>();
    private float pourTilt, shownFill;
    // Aiming: the hand carries the crucible left and right (the pointer's x); the stream must land in the pouring cup.
    private const float PourBaseX = -0.09f, CupRadius = 0.016f;
    private float pourAim, pourDrift, pourClock, pourMouthX, pourLandX, pourReach;
    private Transform aimOn, aimOff;

    public static CraftHandsOn For(CraftStation s)
    {
        if (s == null)
            return null;
        CraftHandsOn h = s.GetComponent<CraftHandsOn>();
        return h != null ? h : s.gameObject.AddComponent<CraftHandsOn>();
    }

    public bool Active => mode != CraftHandsOnMode.None;
    public CraftHandsOnMode Mode => mode;

    /// <summary>
    /// Is <paramref name="kind"/> the work of the 3D work place <paramref name="at"/> - the work that claims the full pace
    /// of the hands there (<see cref="CraftStation.WorkingByHand(CombatEntity, CraftRequestKind)"/>): the wheel's, the
    /// file's, the brush's and the polish's at the wheel, the knife's at the carving bench, the ore brush's at the bench.
    /// </summary>
    public static bool WorkedAt(CraftHandsOnMode at, CraftRequestKind kind)
    {
        switch (at)
        {
            case CraftHandsOnMode.Grind: return kind == CraftRequestKind.Grind || kind == CraftRequestKind.File || kind == CraftRequestKind.Descale || kind == CraftRequestKind.Polish;
            case CraftHandsOnMode.Carve: return kind == CraftRequestKind.Carve;
            case CraftHandsOnMode.Brush: return kind == CraftRequestKind.BrushOre;
            default: return false;
        }
    }

    /// <summary>
    /// The choices of the hands at the work place, kept current - the tool and face (and the horn's way), the stone and
    /// the face it hones, the side and angle, the press, the entry into the tank and how deep, the brush and the stone in
    /// hand, the carving cut and the knife's setting - starting from what the window chose. Still readable once the tool
    /// is put down: the window takes it back (<see cref="CraftMode.TakeBackHandsOn"/>).
    /// </summary>
    public CraftHandsOnSetup Setup
    {
        get
        {
            CraftHandsOnSetup s = setup;
            if ((worked == CraftHandsOnMode.Grind || worked == CraftHandsOnMode.Sharpen) && abrasives.Count > 0)
                s.abrasive = abrasives[Mathf.Clamp(abrasive, 0, abrasives.Count - 1)].id;
            if (worked == CraftHandsOnMode.Sharpen)
                s.side = leftFace ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel;
            if (worked == CraftHandsOnMode.Carve && cutPicked)
                s.cut = carveCut; // chosen by hand; otherwise the window's own cut (its saw too) comes back as it was
            if (worked == CraftHandsOnMode.Brush)
            {
                s.brush = oreBrush;
                if (rock.Fragment >= 0) s.fragment = rock.Fragment;
            }
            return s;
        }
    }

    private CraftWorkshop Ws => station != null ? station.Workshop : null;
    private CraftPiece Piece => Ws?.Piece(setup.piece);
    private CraftAbrasive Abrasive => abrasives.Count > 0 ? abrasives[Mathf.Clamp(abrasive, 0, abrasives.Count - 1)] : null;
    /// <summary>The pressure set (the wheel's, the stone's, the knife's): as the window left it, or as the hand turned it here.</summary>
    private float Pressure(float otherwise) => setup.pressure > 0f ? setup.pressure : otherwise;

    // ------------------------------------------------------------------ starting and stopping
    /// <summary>
    /// Can <paramref name="what"/> be taken up at <paramref name="s"/> with <paramref name="with"/> now? The checks
    /// <see cref="Begin"/> makes, without taking anything up - so the window asks before it puts its own work down
    /// (<see cref="CraftMode.TakeUpByHand"/>). False with the reason, in the workshop's words.
    /// </summary>
    public static bool CanTakeUp(CraftStation s, CraftHandsOnMode what, in CraftHandsOnSetup with, out string reason)
    {
        reason = "";
        CraftWorkshop ws = s != null ? s.Workshop : null;
        if (ws == null || what == CraftHandsOnMode.None)
        {
            reason = "There is nothing to work here by hand.";
            return false;
        }
        CraftPiece p = ws.Piece(with.piece);
        bool shaped = p != null && (p.kind == CraftPieceKind.Metal || p.kind == CraftPieceKind.Wood) && p.segments.Count > 0;
        bool metal = p != null && p.kind == CraftPieceKind.Metal && p.segments.Count > 0;
        switch (what)
        {
            case CraftHandsOnMode.Hammer:
                if (!s.Has(CraftTools.Anvil)) reason = "There is no anvil here.";
                else if (!shaped) reason = "Pick a piece of metal to forge.";
                break;
            case CraftHandsOnMode.Bellows:
                if (!s.Has(CraftTools.Forge)) reason = "There is no forge here.";
                break;
            case CraftHandsOnMode.Grind:
                if (!shaped) reason = "Pick a blade to work.";
                else if (Abrasives(s, what, p, null) == 0) reason = p.kind == CraftPieceKind.Wood ? "There is no rasp or knife here for wood." : "There is no wheel or file here.";
                break;
            case CraftHandsOnMode.Sharpen:
                if (!metal) reason = "Pick a blade to sharpen.";
                else if (Abrasives(s, what, p, null) == 0) reason = "There are no whetstones here.";
                break;
            case CraftHandsOnMode.Quench:
                if (!s.Has(CraftTools.QuenchTank)) reason = "There is no quench tank here.";
                else if (!metal) reason = "Pick a hot piece of metal to quench.";
                break;
            case CraftHandsOnMode.Carve:
                if (!s.Has(CraftTools.Files)) reason = "There is no carving knife here.";
                else if (p == null || p.kind != CraftPieceKind.Wood || p.segments.Count == 0) reason = "Pick a wooden blank to carve.";
                break;
            case CraftHandsOnMode.Brush:
                if (!s.Has(CraftTools.OreBench)) reason = "There are no brushes here.";
                else if (p == null || p.kind != CraftPieceKind.Ore || p.ore.fragments.Count == 0) reason = "Pick a lump of ore to clean.";
                else OnTheBench(p, out reason);
                break;
            case CraftHandsOnMode.Pour:
                if (string.IsNullOrEmpty(with.mold) || s.Rules.Mold(with.mold) == null) reason = "Choose a mould first.";
                else ws.CanPour(with.fromFurnace, out reason); // melted, metal to pour: told before the hands start, not after (a melt too small for the mould pours short)
                break;
        }
        return string.IsNullOrEmpty(reason);
    }

    /// <summary>The ore bench's own check: on the bench, and cool enough to hold (the workshop's words).</summary>
    private static bool OnTheBench(CraftPiece p, out string reason)
    {
        reason = p.location == CraftLocation.Air ? p.ore.temperature > 120 ? "It is too hot to handle: let it cool on the bench." : ""
            : p.location == CraftLocation.Furnace ? "It is part of the furnace's charge now: it smelts there."
            : p.location == CraftLocation.Melt ? "It is in the crucible: lift it out with the tongs first." : "Put it on the bench first.";
        return reason.Length == 0;
    }

    /// <summary>
    /// The tools of a work place for <paramref name="p"/> (into <paramref name="into"/> when given): the whetstones; or the
    /// wheels, files, polishes and brushes that work its kind (a rasp and a knife for wood), wheels first - as the window
    /// lists them. Returns how many.
    /// </summary>
    private static int Abrasives(CraftStation s, CraftHandsOnMode what, CraftPiece p, List<CraftAbrasive> into)
    {
        into?.Clear();
        int n = 0;
        bool wood = p != null && p.kind == CraftPieceKind.Wood;
        CraftAbrasiveKind[] order = what == CraftHandsOnMode.Sharpen
            ? new[] { CraftAbrasiveKind.Whetstone }
            : new[] { CraftAbrasiveKind.Wheel, CraftAbrasiveKind.File, CraftAbrasiveKind.Polish, CraftAbrasiveKind.Brush };
        foreach (CraftAbrasiveKind k in order)
            foreach (CraftAbrasive a in s.Rules.Abrasives)
            {
                if (a.kind != k) continue;
                bool here = k switch
                {
                    CraftAbrasiveKind.Wheel => s.Has(CraftTools.Grinder),
                    CraftAbrasiveKind.Polish => s.Has(CraftTools.Polishing),
                    CraftAbrasiveKind.Whetstone => s.Has(CraftTools.Whetstone),
                    _ => s.Has(CraftTools.Files),
                };
                if (!here || k != CraftAbrasiveKind.Whetstone && (wood ? !a.wood : a.wood)) continue;
                into?.Add(a);
                n++;
            }
        return n;
    }

    /// <summary>
    /// Takes up <paramref name="what"/> for <paramref name="who"/> with the window's <paramref name="hand"/> (their
    /// tremble and fatigue carry over). <paramref name="ended"/> is called when it is put down (true = back to the
    /// window). False (and why) when it cannot be used now (<see cref="CanTakeUp"/>).
    /// </summary>
    public bool Begin(CraftHandsOnMode what, CombatEntity who, CraftHands hand, CraftHandsOnSetup with, Action<bool> ended, out string reason)
    {
        reason = "";
        station = GetComponent<CraftStation>();
        if (station == null || who == null || what == CraftHandsOnMode.None || station.Workshop == null)
            return false;
        if (!CanTakeUp(station, what, with, out reason))
            return false;
        if (Active)
            End(false, false);
        setup = with;
        CraftPiece p = station.Workshop.Piece(with.piece);
        if (what == CraftHandsOnMode.Grind || what == CraftHandsOnMode.Sharpen)
        {
            Abrasives(station, what, p, abrasives);
            abrasive = abrasives.FindIndex(a => a.id == with.abrasive);
            if (abrasive < 0) abrasive = Mathf.Max(0, abrasives.FindIndex(a => a.kind == CraftAbrasiveKind.Wheel));
        }
        visuals = station.GetComponentInChildren<CraftStationVisuals>();
        player = who;
        hands = hand ?? new CraftHands(1);
        armLevel = CraftHands.Level(hands.fatigue); // said only when it gets worse here
        mode = worked = what;
        Listen(station);
        // The station knows the hands are at a 3D work place from now: its work claims the full pace (d = 1).
        Send(new CraftRequest(CraftRequestKind.HandsOn) { flag = true, i0 = (int)what });
        onEnd = ended;
        held = wasHeld = false;
        // A button already down (on the window's slider, a gamepad trigger) is not a press here until it is let go.
        waitRelease = StationControls.Held(StationControl.Work);
        // the cut chosen in the window; one the knife does not make here (the window's saw) starts by shaving, never boring
        carveCut = with.cut >= 0 && with.cut < CutNames.Length ? with.cut : 0;
        cutPicked = carving = boring = false;
        lastStraight = -1f;
        bitSpin = 0f;
        swing = -1f;
        pendingRaise = false;
        chiselAt = -1f;
        airPending = 0f;
        lastBlow = lastFeel = "";
        grinding = dipAsked = stroking = false;
        grindStop = honeStop = dipId = null;
        passesSent = honedL = honedR = 0;
        lastAngle = lastWobble = -1f;
        lastLeading = false;
        stroke.Clear();
        tankHand.Clear();
        scrubHand.Clear();
        sentMotion = CraftQuenchMotion.Still;
        plunged = ownPlunge = false;
        liftsAsked = 0;
        pour = null;
        tippingBack = false;
        pourTilt = shownFill = 0f;
        aimOn = aimOff = null;
        opening = 1f;
        ventSet = -1f;
        pieceBuilt = false;
        leftFace = with.side != CraftSurfaceSide.RightBevel;
        flipShown = p != null && p.faceUp == 1 ? 180f : 0f;

        BuildPlace();
        // The view: a camera of its own over the work place, gliding there from the player's. The game's camera stays (it
        // is still Camera.main, with its listener) but draws nothing meanwhile: the scene is drawn once.
        Camera main = Camera.main;
        var go = new GameObject("Craft Hands-On View");
        cam = go.AddComponent<Camera>();
        if (main != null)
        {
            cam.CopyFrom(main);
            cam.depth = main.depth + 5f;
            go.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            mainCam = main;
            mainMask = main.cullingMask;
            main.cullingMask = 0;
        }
        cam.fieldOfView = fieldOfView;
        cam.nearClipPlane = 0.02f;
        TargetView(out Vector3 vp, out Quaternion vr);
        if (main == null) go.transform.SetPositionAndRotation(vp, vr);
        cursorWasVisible = Cursor.visible;
        cursorWasLocked = Cursor.lockState;
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = false; // what the hand carries shows where it is (the crucible and its ring at the pour), at every place
        StationControls.BeginHandsOn(this); // the gameplay keys go off while the hands are at work
        BuildHud();
        hintTimer = 0f;
        return true;
    }

    /// <summary>
    /// Puts the tool down: back to the window (<paramref name="backToWindow"/>) or to the game. What the hands were doing
    /// is finished first, as the window finishes it when the hands leave its drawing: the grind's last batch and a dip
    /// lifted out, the whetstone's stroke under way, the knife's stroke, the pour (what ran into the mould is cast), the
    /// going-over of the brush, a piece held in the tank lifted out (one hanging there as the window left it stays).
    /// Their answers show in the window's message row.
    /// </summary>
    public void End(bool backToWindow, bool notify = true)
    {
        if (!Active)
            return;
        CraftHandsOnMode was = mode;
        mode = CraftHandsOnMode.None; // from here what is said goes to the window's message row
        switch (was)
        {
            case CraftHandsOnMode.Hammer:
                // A blow already falling lands; a hammer held up is lowered without striking.
                if (swing >= 0f) { swing = -1f; Strike(Piece, pendingBlow); }
                hammer.Lower();
                pendingRaise = false;
                break;
            case CraftHandsOnMode.Grind: StopGrinding(true); LiftDip(); if (dipAsked) dipLeft = true; break;
            case CraftHandsOnMode.Sharpen: EndStroke(true); break;
            case CraftHandsOnMode.Quench: if (plunged) LiftOut(); break;
            case CraftHandsOnMode.Pour: FinishPour(); break;
            case CraftHandsOnMode.Brush: FlushBrush(); break;
            case CraftHandsOnMode.Carve: FinishKnife(); boring = false; boreCircles.Clear(); break;
        }
        Send(new CraftRequest(CraftRequestKind.HandsOn) { flag = false, i0 = (int)was }); // the tool is down: the window's pace again
        StationControls.EndHandsOn(this);
        if (pieceFilter != null && pieceFilter.sharedMesh != null) Destroy(pieceFilter.sharedMesh); // built here, not shared
        pieceFilter = null;
        DestroyMesh(ghostWidth);
        DestroyMesh(ghostThick);
        ghostWidth = ghostThick = null;
        foreach (Material m in ownMaterials)
            if (m != null) Destroy(m);
        ownMaterials.Clear();
        ghostMaterial = null;
        chips.Clear(); // the shavings go with the work place
        chipPool.Clear();
        fillCells.Clear();
        pourSpace = crucibleObject = streamUpper = streamLower = aimOn = aimOff = null;
        tongsObject = null;
        // What stepped aside for the work place comes back: the station's own model of it, the player's body, the game's view.
        CraftStationVisuals.ShowAgain(hiddenModels);
        CraftStationVisuals.ShowAgain(hiddenPlayer);
        if (mainCam != null) mainCam.cullingMask = mainMask;
        mainCam = null;
        rockObject = brushObject = null;
        rockFilter = null;
        if (rock.Mesh != null) Destroy(rock.Mesh);
        if (rockMaterial != null) Destroy(rockMaterial);
        rockMaterial = null;
        knifeObject = bitObject = bevelMark = null;
        if (root != null) Destroy(root.gameObject);
        root = null;
        pieceObject = null;
        if (cam != null) Destroy(cam.gameObject);
        cam = null;
        if (hud != null) Destroy(hud.gameObject);
        hud = null;
        hudMessage = null;
        hudMessageBack = null;
        meter = meter2 = arms = null;
        Cursor.lockState = backToWindow ? CursorLockMode.None : cursorWasLocked;
        Cursor.visible = backToWindow || cursorWasVisible;
        Action<bool> callback = onEnd;
        onEnd = null;
        if (notify) callback?.Invoke(backToWindow);
    }

    private void OnDisable()
    {
        End(false);
        waits.DropAll(); // the station goes: nothing more will be answered here
        StopListening();
    }

    // ------------------------------------------------------------------ requests, answers, messages
    /// <summary>
    /// Sends a request from the work place, as the window's Send does: false when it was refused at once (where the rules
    /// run, or a check on this machine), true when it was done or - a network client - sent. <paramref name="answered"/>
    /// (ok, reason) runs exactly once with the real outcome: at once where the rules run; on a network client when the
    /// station's answer has come and the copy of the workshop shows what it did ((false, <see cref="CraftAnswerWaits.NoAnswer"/>)
    /// after 10 s: the outcome unknown) - inside it <see cref="answerEvents"/> holds the events the request's own
    /// operations raised. A refusal's reason, or a note on work granted only in part, shows in the message line.
    /// </summary>
    private bool Send(CraftRequest r, Action<bool, string> answered = null)
    {
        if (station == null || player == null)
        {
            answered?.Invoke(false, "");
            return false;
        }
        Listen(station);
        string reason;
        if (!station.RulesHere && station.Net != null)
        {
            // A setting (the air vent) goes with the client's settings, only its latest value; everything else is numbered
            // in this work place's own range, so its refusal comes back here even when nobody waits for the outcome.
            bool setting = CraftRequest.IsSetting(r.kind);
            r.seq = setting ? 0 : waits.Expect((ok, why) => Answer(answered, ok, why, answerEvents), Time.unscaledTimeAsDouble);
            if (!station.Request(player, r, out reason))
            {
                waits.Forget(r.seq);
                Say(reason, true);
                Answer(answered, false, reason ?? "", null);
                return false;
            }
            Say(reason); // cut short before it went (a request too big to carry)
            if (setting) Answer(answered, true, "", null);
            return true;
        }
        // Where the rules run: done here and now, the events it raised caught for its answer.
        List<CraftEvent> outer = capture, mine = new List<CraftEvent>();
        capture = mine;
        bool done;
        try { done = station.Request(player, r, out reason); }
        finally { capture = outer; }
        Say(reason, !done);
        Answer(answered, done, reason ?? "", mine);
        return done;
    }

    private void Answer(Action<bool, string> answered, bool ok, string reason, IReadOnlyList<CraftEvent> events)
    {
        if (answered == null)
            return;
        IReadOnlyList<CraftEvent> outer = answerEvents;
        answerEvents = events ?? NoEvents;
        try { answered(ok, reason); }
        catch (Exception e) { Debug.LogException(e); }
        finally { answerEvents = outer; }
    }

    /// <summary>Follows <paramref name="s"/>'s answers, events and messages (until the tool is down and nothing is waited for).</summary>
    private void Listen(CraftStation s)
    {
        if (listening == s)
            return;
        StopListening();
        listening = s;
        if (s == null)
            return;
        s.Answered += OnAnswered;
        s.WorkshopEvent += OnEvent;
        s.LocalMessage += OnLocalMessage;
    }

    private void StopListening()
    {
        if (listening == null)
            return;
        listening.Answered -= OnAnswered;
        listening.WorkshopEvent -= OnEvent;
        listening.LocalMessage -= OnLocalMessage;
        listening = null;
    }

    /// <summary>A network client: the station answered one of the requests sent from here (its copy shows what it did).</summary>
    private void OnAnswered(int seq, bool ok, string reason)
    {
        if (!waits.Waiting(seq))
            return; // the window's, or given up on
        Say(reason, !ok);
        IReadOnlyList<CraftEvent> outer = answerEvents;
        answerEvents = listening != null ? listening.AnswerEvents : NoEvents;
        try { waits.Answer(seq, ok, reason); }
        finally { answerEvents = outer; }
    }

    private void OnEvent(CraftEvent e)
    {
        capture?.Add(e); // raised by the request being run here: its answer's
        Tell(e);
    }

    /// <summary>
    /// What the workshop says happens while the hands work, in the message line - to the piece in hand (a crack, it burns,
    /// the oil flares, the temper runs, it slumps...), and at the fire or the crucible to the fire and the melt - the bad
    /// news in red, with a jolt. What the work place shows by itself or its answers tell is not said again: a blow's ring
    /// (in the anvil's line), the sparks off the wheel, a stroke on the stone, the pour's verdict.
    /// </summary>
    private void Tell(CraftEvent e)
    {
        if (!Active || string.IsNullOrEmpty(e.Text))
            return;
        bool mine = !string.IsNullOrEmpty(e.Piece) && e.Piece == setup.piece;
        bool place = string.IsNullOrEmpty(e.Piece) && (mode == CraftHandsOnMode.Pour
            ? e.Id != "spilt" && (e.Kind == CraftEventKind.Note || e.Kind == CraftEventKind.Melted || e.Kind == CraftEventKind.SlagTapped || e.Kind == CraftEventKind.FireOut && e.Id == "furnace")
            : (mode == CraftHandsOnMode.Bellows || mode == CraftHandsOnMode.Hammer) && e.Kind == CraftEventKind.FireOut && e.Id == "forge");
        if (!mine && !place)
            return;
        switch (e.Kind)
        {
            // (The window logs the workshop's events itself: they are only shown here.)
            case CraftEventKind.Cracked:
            case CraftEventKind.Burning:
            case CraftEventKind.QuenchFlare:
            case CraftEventKind.Melted:
                Say(e.Text, true, false);
                shake = Mathf.Max(shake, 0.6f);
                break;
            case CraftEventKind.Note:
            case CraftEventKind.Defect: // a flaw showing for the first time (scale hammered in, rot, shrinking...)
                bool bad = e.Kind == CraftEventKind.Defect || e.Id == "temper_drawn" || e.Id == "seam" || e.Id == "warp" || e.Id == "blotch";
                Say(e.Text, bad, false);
                if (bad) shake = Mathf.Max(shake, 0.4f);
                break;
            case CraftEventKind.Quenched:
            case CraftEventKind.Hardened:
            case CraftEventKind.Tempered:
            case CraftEventKind.ScaleFlaked:
            case CraftEventKind.MagnetLost:
            case CraftEventKind.Dried:
            case CraftEventKind.FireOut:
            case CraftEventKind.SlagTapped:
                Say(e.Text, false, false);
                break;
        }
    }

    /// <summary>A message for this machine's player from the station (told, or from the server): the message line shows it while the hands work.</summary>
    private void OnLocalMessage(string text)
    {
        if (!relaying && Active)
            Say(text, false, false); // the window logs the station's messages itself
    }

    /// <summary>
    /// The message line over the panel - a refusal's reason (<paramref name="refused"/>, in red), a note on work granted
    /// only in part, what a request did: the newest shows at once and stays as long as the window keeps its messages
    /// (<see cref="CraftWindowRules.MessageSeconds"/>), and goes into the window's "What happened" too (a refusal dimmed:
    /// <see cref="CraftStation.Log"/>), as every message of the window does. Once the tool is down, the window's message
    /// row (and its log) has it instead. Part of the HUD the presentation lays out: the row is <c>hudMessage</c>.
    /// </summary>
    public void Say(string text, bool refused = false) => Say(text, refused, true);

    /// <summary><see cref="Say(string, bool)"/>; <paramref name="log"/> false for what the window logs by itself (the workshop's events, the station's messages) or need not keep (a setting turned).</summary>
    private void Say(string text, bool refused, bool log)
    {
        if (string.IsNullOrEmpty(text))
            return;
        if (!Active || hudMessage == null)
        {
            if (station == null || relaying)
                return;
            relaying = true;
            try { station.ShowLocalMessage(text); }
            finally { relaying = false; }
            return;
        }
        hudMessage.text = refused ? $"<color=#FF9A80>{text}</color>" : text;
        messageUntil = Time.unscaledTime + CraftWindowRules.MessageSeconds(text);
        FitMessageBacking();
        if (log && station != null) station.Log(text, refused);
    }

    // ------------------------------------------------------------------ every frame
    private void Update()
    {
        // Answers still waited for (the tool may be down already): given up on after a while, as the window does.
        if (waits.Count > 0 && waits.Expire(Time.unscaledTimeAsDouble) > 0)
            Say(CraftAnswerWaits.NoAnswer, true);
        if (!Active)
        {
            if (waits.Count == 0) StopListening();
            return;
        }
        float dt = Time.unscaledDeltaTime;
        if (station == null || player == null || !player.isActiveAndEnabled || Ws == null)
        {
            End(false);
            return;
        }
        if (StationControls.Pressed(StationControl.PutDown))
        {
            End(true);
            return;
        }
        Vector2 pointer = StationControls.Pointer;
        wasHeld = held;
        bool down = StationControls.Held(StationControl.Work);
        if (waitRelease && !down) waitRelease = false;
        held = down && !waitRelease;
        if (held && !wasHeld) pressAt = pointer;
        hands.Rest(dt);
        switch (mode)
        {
            case CraftHandsOnMode.Hammer: UpdateHammer(dt, pointer); break;
            case CraftHandsOnMode.Bellows: UpdateBellows(dt, pointer); break;
            case CraftHandsOnMode.Grind: UpdateGrind(dt, pointer); break;
            case CraftHandsOnMode.Sharpen: UpdateSharpen(dt, pointer); break;
            case CraftHandsOnMode.Quench: UpdateQuench(dt, pointer); break;
            case CraftHandsOnMode.Pour: UpdatePour(dt, pointer); break;
            case CraftHandsOnMode.Brush: UpdateBrush(dt, pointer); break;
            case CraftHandsOnMode.Carve: UpdateCarve(dt, pointer); break;
        }
        if (!Active)
            return;
        ShowArm();
        if (hudMessage != null && messageUntil > 0f && Time.unscaledTime > messageUntil)
        {
            hudMessage.text = "";
            messageUntil = 0f;
            FitMessageBacking();
        }
        hintTimer -= dt;
        if (hintTimer <= 0f && hudHint != null)
        {
            hintTimer = 0.5f; // the keys follow a rebind or a switch to the gamepad
            hudHint.text = Hint();
            if (hudLegend != null) hudLegend.text = Legend();
        }
        pieceTimer -= dt;
        if (pieceTimer <= 0f)
        {
            pieceTimer = 0.12f;
            RefreshPiece();
        }
        // The view glides to its place; a blow shakes it a little.
        TargetView(out Vector3 p, out Quaternion r);
        float k = 1f - Mathf.Exp(-dt * 5f);
        shake = Mathf.Max(0f, shake - dt * 6f);
        Vector3 jolt = shake > 0f ? UnityEngine.Random.insideUnitSphere * 0.006f * shake : Vector3.zero;
        cam.transform.SetPositionAndRotation(Vector3.Lerp(cam.transform.position, p, k) + jolt, Quaternion.Slerp(cam.transform.rotation, r, k));
    }

    /// <summary>
    /// The arm: its bar empties as it tires, coloured by how tired it is (green, gold, orange, red: the steps of
    /// <see cref="CraftHands.Level"/>, at which its label's word changes too); crossing into a worse state, the arm's
    /// words - the hammer's blows at the anvil, the hand's other work at the other places - go into the
    /// message line, as the window's arm does (it rests a little before they come again).
    /// </summary>
    private void ShowArm()
    {
        float f = (float)hands.fatigue;
        int now = CraftHands.Level(f), level = now;
        if (level < armLevel && f > CraftHands.LevelFrom(armLevel) - 0.1f) level = armLevel;
        if (level > armLevel) Say($"<color=#FFB070>{CraftHands.FeelingAt(level, mode == CraftHandsOnMode.Hammer)}</color>");
        armLevel = level;
        arms?.Set(now == 0 ? "Arms" : now == 1 ? "Arms: tiring" : now == 2 ? "Arms: heavy" : "Arms: spent",
            Mathf.Clamp01(1f - f), now == 0 ? UITheme.Good : now == 1 ? UITheme.Gold : now == 2 ? ArmHeavy : UITheme.Bad);
    }

    /// <summary>The arm's bar when it is heavy: between the tiring gold and the spent red.</summary>
    private static readonly Color ArmHeavy = new Color(1f, 0.6f, 0.32f, 1f);

    /// <summary>The choice the keys picked this frame among <paramref name="count"/> (a number key, or the previous / next one after <paramref name="current"/>); -1 when none.</summary>
    private static int PickedChoice(int count, int current)
    {
        if (count <= 0)
            return -1;
        int pick = StationControls.ChoicePressed(Mathf.Min(10, count));
        if (pick >= 0)
            return pick;
        int step = StationControls.ChoiceStep;
        return step != 0 ? ((Mathf.Clamp(current, 0, count - 1) + step) % count + count) % count : -1;
    }

    // ------------------------------------------------------------------ the anvil
    private void UpdateHammer(float dt, Vector2 pointer)
    {
        CraftPiece p = Piece;
        if (p == null)
        {
            End(true);
            return;
        }
        hammer.Tick(dt);
        // The tool: the number keys, the previous / next choice, Next Tool.
        int at = Array.IndexOf(ToolOrder, setup.tool);
        int choice = PickedChoice(ToolOrder.Length, at);
        if (choice < 0 && StationControls.Pressed(StationControl.NextTool)) choice = (Mathf.Max(0, at) + 1) % ToolOrder.Length;
        if (choice >= 0 && ToolOrder[choice] != setup.tool)
        {
            setup.tool = ToolOrder[choice];
            hintTimer = 0f;
        }
        // Less / More: over the horn, which way the far end bends (as the window's toggle).
        int adjust = StationControls.AdjustStep;
        if (adjust != 0 && setup.tool == CraftStrikeTool.Horn)
        {
            setup.bendDown = adjust < 0;
            hintTimer = 0f;
        }
        if (StationControls.Pressed(StationControl.FaceEdge)) { setup.face = setup.face == CraftStrikeFace.Flat ? CraftStrikeFace.Edge : CraftStrikeFace.Flat; hintTimer = 0f; }
        if (StationControls.Pressed(StationControl.TurnOver)) Send(new CraftRequest(CraftRequestKind.Turn, p.id));
        if (StationControls.Pressed(StationControl.Fire)) ToFireAndBack(p);
        bool onAnvil = p.location == CraftLocation.Air;
        if (PointOnPlane(pointer, 0f, out Vector3 local))
            aim = Mathf.Clamp01(0.5f - local.x / Mathf.Max(0.01f, pieceLength)); // its first segment on the view's left (+x)
        // The face's tilt: the hand moved up or down since it pressed - level within 2 % of the screen's height (a hand
        // holding the hammer up drifts a little; the window's 8 px of its 400), a full tilt 12 % beyond that.
        if (held && setup.tool != CraftStrikeTool.Horn)
            tilt = (float)CraftForging.TiltFrom(pointer.y - pressAt.y, Screen.height * 0.02f, Screen.height * 0.12f);
        else if (!hammer.Raised)
            tilt = Mathf.MoveTowards(tilt, 0f, dt * 3f);
        // A press raises the hammer - once it has come up from the last blow (the window's clock: a press sooner, or one
        // while the last blow is still falling, waits for it), and only over a piece it can work.
        float now = Time.unscaledTime;
        if (held && !wasHeld && !hammer.Raised && onAnvil)
        {
            string why = NoBlow(p);
            if (why != null) Say(why, true);
            else if (swing < 0f && SmithHammerClock.Ready(now)) hammer.Raise();
            else pendingRaise = true;
        }
        if (pendingRaise)
        {
            if (!held || !onAnvil)
            {
                pendingRaise = false;
                lastBlow = "The hammer had not come up yet: wait for it before the next blow.";
            }
            else if (swing < 0f && SmithHammerClock.Ready(now))
            {
                pendingRaise = false;
                hammer.Raise();
            }
        }
        if (hammer.Raised && !onAnvil)
            hammer.Lower(); // it went into the fire meanwhile
        if (hammer.Raised && !held)
        {
            // The chisel stays where it was set on the bar: every blow drives it there (aimed well away, it is set there instead).
            if (setup.tool == CraftStrikeTool.Chisel && (chiselAt < 0f || Mathf.Abs(aim - chiselAt) * (float)p.Length > 12f)) chiselAt = aim;
            pendingBlow = hammer.Release(setup.tool == CraftStrikeTool.Chisel ? chiselAt : aim, setup.tool == CraftStrikeTool.Horn ? 0 : tilt, hands);
            swing = 0f; // the hammer falls; the blow lands when it meets the work
            if (pendingBlow.slipped) slipShown = 1f;
        }
        if (swing >= 0f)
        {
            swing += dt / 0.07f;
            if (swing >= 1f)
            {
                swing = -1f;
                Strike(p, pendingBlow);
            }
        }
        // The hammer above the aim point: raised with the charge, tilted with the hand.
        if (hammerObject != null)
        {
            float charge = hammer.Raised ? (float)hammer.Charge : 0f;
            float rest = PieceTop(p) + 0.03f;
            // A tired arm cannot lift it as high.
            float lift = swing >= 0f ? Mathf.Lerp(0.05f + 0.3f * (float)pendingBlow.force, 0f, swing) : 0.06f + 0.3f * charge * (float)hands.Strength;
            float x = (0.5f - (setup.tool == CraftStrikeTool.Chisel && chiselAt >= 0f ? chiselAt : aim)) * pieceLength;
            hammerObject.localPosition = new Vector3(x, rest + lift, 0f);
            float waver = hammer.Raised && hammer.Held > hammer.fullCharge + hammer.holdBeforeWaver ? Mathf.Sin(Time.unscaledTime * 22f) * 3f : 0f;
            // A tired arm trembles while it holds the hammer up; a slipped blow glances off and twists the hammer aside.
            if (hammer.Raised && hands.fatigue > 0.5) waver += Mathf.Sin(Time.unscaledTime * 31f) * 4f * (float)(hands.fatigue - 0.5);
            slipShown = Mathf.Max(0f, slipShown - dt * 2.5f);
            // Cocked back as it rises; tilted for a bevel (past what still strikes flat), its face leans down toward the edge
            // of the face that is up (away from the hands with the left face up, toward them with the right) - the side the
            // blow bevels.
            float bevel = Bevelling ? (Mathf.Abs(tilt) - (float)CraftForging.FlatTilt) / (1f - (float)CraftForging.FlatTilt) : 0f;
            float lean = (Bevelling ? 6f + 24f * bevel : 0f) * (p.faceUp == 1 ? 1f : -1f);
            hammerObject.localRotation = Quaternion.Euler(lean + waver + lift * 120f, 0f, slipShown * 28f);
            hammerObject.gameObject.SetActive(onAnvil);
            // The bevel it will forge, shaded on the piece under it: the edge's side of the face up, wider the more it tilts.
            if (bevelMark != null)
            {
                bool show = onAnvil && Bevelling && (hammer.Raised || pendingRaise || swing >= 0f);
                bevelMark.gameObject.SetActive(show);
                if (show)
                {
                    float wide = Mathf.Max(0.003f, pieceWidth * 0.5f * (0.25f + 0.75f * bevel));
                    float face = Mathf.Max(0.01f, (float)(CraftForging.Contact(setup.tool) * 0.001));
                    bevelMark.localPosition = new Vector3(x, PieceTop(p) + 0.0012f, (p.faceUp == 1 ? 1f : -1f) * (pieceWidth * 0.5f - wide * 0.5f));
                    bevelMark.localScale = new Vector3(face, 0.0012f, wide);
                }
            }
        }
        if (pieceObject != null)
        {
            pieceObject.gameObject.SetActive(onAnvil);
            // Turning it over: it rolls over about its length (the edge goes to the other side, the other face comes up).
            flipShown = Mathf.MoveTowards(flipShown, p.faceUp == 1 ? 180f : 0f, dt * 540f);
            float roll = Mathf.Sin(flipShown * Mathf.Deg2Rad);
            pieceObject.localPosition = new Vector3(0f, PieceTop(p) * 0.5f + roll * pieceWidth * 0.6f, 0f);
            // As the window's top view shows it: its first segment on the left, the edge away from the hands with the left
            // face up (toward them with the right).
            pieceObject.localRotation = Quaternion.LookRotation(Vector3.up, -Vector3.right) * Quaternion.Euler(0f, flipShown, 0f);
        }
        // The blow as it would land, full at a full blow (green); red once it is held so long that the arm wavers and the
        // blow weakens - as the window's BLOW.
        float shown = hammer.Raised ? (float)hammer.Expected(hands) : swing >= 0f ? (float)pendingBlow.force : 0f;
        meter?.Set("Blow", Mathf.Clamp01(shown / (float)hammer.FullBlow), hammer.Wavers ? UITheme.Bad : UITheme.Gold, (0.95f, 1f, new Color(0.3f, 0.75f, 0.4f, 1f)));
        // A weld still open: the stretch under the hammer against the welding band (amber where it barely takes, green
        // where it sets well - the window's HEAT gauge), and in words its flux and how clean it is.
        double open = CraftForging.SeamOpen(p);
        string weld = "";
        if (open > 0 && onAnvil && p.kind == CraftPieceKind.Metal)
        {
            int under = Mathf.Clamp(p.SegmentAt(setup.tool == CraftStrikeTool.Chisel && chiselAt >= 0f ? chiselAt : aim), 0, p.segments.Count - 1);
            MetalSegment s = p.segments[under];
            double weldT = CraftForging.WeldingHeat(station.Rules, p.Profile(station.Rules)), lo = weldT - 450, hi = weldT + 50;
            CraftForging.WeldFactors(station.Rules, p, s, out double heat, out double flux, out double clean);
            float W(double t) => Mathf.Clamp01((float)((t - lo) / (hi - lo)));
            meter2?.Set("Weld heat", W(s.Temperature), heat > 0.5 ? UITheme.Good : UITheme.Gold,
                (W(weldT - 150), W(weldT - 75), new Color(0.55f, 0.42f, 0.15f, 1f)), (W(weldT - 75), 1f, new Color(0.2f, 0.55f, 0.25f, 1f)));
            weld = $" The seam is {(1 - open) * 100:0}% set: flat blows at a welding heat set it" +
                   (heat <= 0.01 ? " - here it is too cold to take" : "") + (flux < 0.3 ? " - no flux here: it will not take well" : "") + (clean < 0.6 ? " - scaled here: flux and heat it again" : "") + ".";
        }
        else if (TiltBevels && onAnvil)
        {
            // How far the face is tilted: within the grey it strikes flat; past it (amber) the blow forges a bevel.
            float t = Mathf.Abs(tilt);
            meter2?.Set("Tilt", t, Bevelling ? UITheme.Gold : new Color(0.7f, 0.7f, 0.74f, 1f),
                (0f, (float)CraftForging.FlatTilt, new Color(0.45f, 0.45f, 0.5f, 1f)), ((float)CraftForging.FlatTilt, 1f, new Color(0.55f, 0.42f, 0.15f, 1f)));
        }
        else
            meter2?.Hide();
        string raised = hammer.Wavers ? " <color=#FF9A80>Held too long: the arm wavers - let it fall.</color>"
            : Bevelling && (hammer.Raised || pendingRaise) ? $" <color=#FFB070>Tilted: this blow forges a bevel on the {(p.faceUp == 0 ? "left" : "right")} face, at its edge.</color>" : "";
        string horn = setup.tool == CraftStrikeTool.Horn ? $" · far end {(setup.bendDown ? "down" : "up")}" : "";
        hudTitle.text = $"Anvil · {CraftForging.ToolName(setup.tool)}{horn}{(setup.face == CraftStrikeFace.Edge ? " · on its edge" : "")} · {(p.faceUp == 0 ? "left" : "right")} face up";
        hudFeel.text = onAnvil ? $"{HeatWords(p)}. {(pendingRaise ? "The hammer is still coming up." : lastBlow)}{raised}{weld}"
            : p.location == CraftLocation.Fire ? $"It is in the fire: {InFireWords(p)}. {StationControls.K(StationControl.Fire)}: take it out.{SeamWords(p)}"
            : $"It is not on the anvil: it is {CraftStationUI.LocationText(p)}.";
    }

    /// <summary>Does the tool in hand forge a bevel when its face is tilted - the flat face, struck on the piece's face (the workshop's rule)?</summary>
    private bool TiltBevels => setup.tool == CraftStrikeTool.Flat && setup.face == CraftStrikeFace.Flat;

    /// <summary>Is the face tilted past what still strikes flat (<see cref="CraftForging.FlatTilt"/>) with a tool that bevels: this blow forges a bevel.</summary>
    private bool Bevelling => TiltBevels && Mathf.Abs(tilt) > (float)CraftForging.FlatTilt;

    /// <summary>
    /// The piece in the fire, in words: its heat (in °C with a thermometer) and what that heat is good for - not yet a
    /// forging heat, a forging heat, a welding heat, or hot enough to burn it - as the window's forge says it.
    /// </summary>
    private string InFireWords(CraftPiece p)
    {
        string heat = HeatWords(p);
        heat = char.ToLowerInvariant(heat[0]) + heat.Substring(1);
        if (p.kind != CraftPieceKind.Metal || p.segments.Count == 0)
            return heat;
        CraftAlloyProfile prof = p.Profile(station.Rules);
        double t = p.MaxTemperature, weld = CraftForging.WeldingHeat(station.Rules, prof);
        string what = t > prof.BurnTemperature ? "<color=#FF9A80>hot enough to burn it - take it out</color>"
            : t >= weld - 10 ? "a welding heat" : t >= prof.HotWorkMin ? "a forging heat" : "not yet a forging heat";
        return $"{heat} - {what}";
    }

    /// <summary>A weld's seam still open, in words (none when it is set): how much is set, and what sets the rest.</summary>
    private static string SeamWords(CraftPiece p)
    {
        double open = p.kind == CraftPieceKind.Metal ? CraftForging.SeamOpen(p) : 0;
        return open > 0 ? $" The seam is {(1 - open) * 100:0}% set: take it out at a welding heat and strike it shut." : "";
    }

    /// <summary>Why no blow can be struck on <paramref name="p"/> with the tool in hand (null: it can) - told before the hammer is raised, as the workshop would refuse it.</summary>
    private string NoBlow(CraftPiece p)
    {
        if (p.kind == CraftPieceKind.Wood && setup.tool != CraftStrikeTool.Straighten)
            return "Wood is shaped with a rasp and a knife, not the hammer.";
        return null;
    }

    private void Strike(CraftPiece p, CraftBlow blow)
    {
        if (p == null)
            return;
        CraftStrikeTool tool = setup.tool;
        // Over the horn the way chosen (the far end up or down, as it lies now - up, away from the hands, is the edge's
        // side with the left face up: a bend toward the edge, negative; the window's rule); otherwise the hand's tilt as it fell.
        double sent = tool == CraftStrikeTool.Horn ? (setup.bendDown ? 1 : -1) * (p.faceUp == 1 ? -1 : 1) : blow.tilt;
        double position = tool == CraftStrikeTool.Chisel && chiselAt >= 0f ? chiselAt : blow.position;
        SmithHammerClock.Struck(Time.unscaledTime); // the hammer comes up again from now, the window's and this one alike
        shake = Mathf.Clamp01((float)blow.force);
        string id = p.id;
        int seg = Mathf.Clamp(p.SegmentAt(position), 0, p.segments.Count - 1);
        string feel = CraftForging.BlowFeel(station.Rules, p, seg); // how that stretch takes the blow, as the window says it
        double seamBefore = CraftForging.SeamOpen(p);
        string words = blow.Words;
        lastBlow = "";
        Send(new CraftRequest(CraftRequestKind.Strike, id) { a = (float)position, i0 = (int)tool, i1 = (int)setup.face, b = (float)blow.force, c = (float)sent }, (ok, why) =>
        {
            if (!ok || setup.piece != id)
                return; // refused: the reason is in the message line
            // This blow's own sound, and what it did that can be seen.
            string sound = "", cut = "";
            foreach (CraftEvent e in answerEvents)
            {
                if (e.Piece != id) continue;
                if (e.Kind == CraftEventKind.Struck) sound = CraftLook.StrikeSound(e.Id);
                else if (e.Kind == CraftEventKind.Cut) cut = " It parts: cut in two.";
            }
            CraftPiece q = Ws?.Piece(id);
            if (cut.Length == 0 && q != null && CraftForging.Cuts(tool) && seg < q.segments.Count)
                cut = $" The cut is {Mathf.Clamp01((float)q.segments[seg].cut) * 100:0}% through.";
            if (cut.Length > 0 && tool == CraftStrikeTool.Chisel && cut.Contains("parts")) chiselAt = -1f;
            string seam = q != null && seamBefore > 0 && CraftForging.SeamOpen(q) < seamBefore - 0.005 ? $" The weld's seam closes ({(1 - CraftForging.SeamOpen(q)) * 100:0}% set)." : "";
            lastBlow = $"{words}{feel} {sound}{cut}{seam}".Trim();
        });
    }

    private void ToFireAndBack(CraftPiece p)
    {
        if (p.location == CraftLocation.Fire)
            Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Air });
        else if (!station.Has(CraftTools.Forge))
            Say("There is no forge here.", true);
        else
        {
            var r = new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Fire, a = setup.depth > 0f ? setup.depth : 0.7f };
            // the span chosen in the window, flagged as one (a span of only the first segment is not the whole piece)
            if (setup.span) { r.i0 = Mathf.Min(setup.from, setup.to); r.i1 = Mathf.Max(setup.from, setup.to); r.flag = true; }
            hammer.Lower();
            Send(r);
        }
    }

    // ------------------------------------------------------------------ the bellows
    /// <summary>The forge's air vent as the hand last set it (until the workshop shows it), else as the workshop has it.</summary>
    private float Vent => ventSet >= 0f ? ventSet : (float)Ws.Forge.damper;

    private void UpdateBellows(float dt, Vector2 pointer)
    {
        pump.Tick(dt);
        float late = BellowsClock.Flush(Time.unscaledTime, ref airPending); // the air of a quick pump, once the bellows have filled
        if (late > 0f) Send(new CraftRequest(CraftRequestKind.Bellows) { a = late });
        CraftPiece p = Piece;
        if (StationControls.Pressed(StationControl.Fire))
        {
            if (p != null) ToFireAndBack(p);
            else Say("Pick a piece in the window to put in the fire.", true);
        }
        if (StationControls.Pressed(StationControl.FeedFire))
        {
            if (string.IsNullOrEmpty(setup.fuel)) Say("Choose the fuel in the window first.", true);
            else Send(new CraftRequest(CraftRequestKind.AddFuel) { id = setup.fuel, count = 1 });
        }
        // Turn Over: the other face down in the coals (as the window's Turn it), so it heats evenly.
        if (StationControls.Pressed(StationControl.TurnOver) && p != null)
            Send(new CraftRequest(CraftRequestKind.Turn, p.id));
        // Less / More: the air vent, a tenth at a time (the workshop takes the latest).
        if (ventSet >= 0f && (Mathf.Abs((float)Ws.Forge.damper - ventSet) < 0.01f || Time.unscaledTime - ventAt > 1.5f))
            ventSet = -1f;
        int adjust = StationControls.AdjustStep;
        if (adjust != 0)
        {
            ventSet = Mathf.Clamp01(Mathf.Round((Vent + 0.1f * adjust) * 10f) / 10f);
            ventAt = Time.unscaledTime;
            Send(new CraftRequest(CraftRequestKind.Damper) { a = ventSet });
            hintTimer = 0f;
        }
        // A quick squeeze - once the bellows have filled again (as a full pump, never faster than they fill).
        if (StationControls.Pressed(StationControl.Quick) && !held && opening > 0.8f) { opening = 0.15f; Puff(); }
        if (held)
        {
            float target = Mathf.Clamp01(1f - (pressAt.y - pointer.y) / (Screen.height * 0.22f));
            opening = Mathf.MoveTowards(opening, target, dt * 6f);
            if (opening < 0.3f && !squeezing)
            {
                squeezing = true;
                Puff();
            }
            if (opening > 0.8f) squeezing = false;
        }
        else
        {
            opening = Mathf.MoveTowards(opening, 1f, dt * 1.5f); // the bellows fill again by themselves
            if (opening > 0.8f) squeezing = false;
        }
        if (bellowsTop != null)
            bellowsTop.localRotation = Quaternion.Euler(0f, 0f, -4f - 18f * opening);
        if (pieceObject != null)
        {
            // In the coals as the window draws it, the face up as it lies (turned over, the other one).
            pieceObject.gameObject.SetActive(p != null && p.location == CraftLocation.Fire);
            if (p != null) pieceObject.localRotation = Quaternion.LookRotation(Vector3.up, -Vector3.right) * Quaternion.Euler(0f, p.faceUp == 1 ? 180f : 0f, 0f);
        }
        CraftFireState f = Ws.Forge;
        // The draft the fire gets (its own through the vent, in grey; what the bellows' air adds above it) - not the pump's
        // own rhythm, which the words say - with the vent as the hand set it.
        CraftSimulationSettings settings = station.Rules.Settings;
        float own = (float)CraftThermal.DraftShare(settings, 0, Vent);
        meter?.Set("Draft", (float)CraftThermal.DraftShare(settings, f.air, Vent), UITheme.Gold, (0f, own, new Color(0.45f, 0.45f, 0.5f, 1f)));
        meter2?.Set("Air vent", Vent, new Color(0.55f, 0.7f, 0.95f, 1f));
        hudTitle.text = "The forge · the bellows";
        string fire = !f.lit ? "The fire is out" : f.level < 0.25 ? "The coals smoulder" : f.level < 0.6 ? "The fire burns steadily" : "The fire roars";
        string fuel = f.fuel < 1 ? ", the fuel is nearly gone" : f.fuel < 3 ? ", little fuel left" : "";
        // How the last pumps kept the rhythm (a moment after each), and a shut vent: the bellows' air does not reach the fire.
        string rhythm = pump.SinceLast > 2.5 ? "" : pump.Rhythm >= 1 ? "A steady rhythm. " : pump.Rhythm >= 0.8 ? "Slow: the air dies down between pumps. "
            : pump.Rhythm >= 0.75 ? "A little hurried: steadier pumps give more air. " : "<color=#FF9A80>Frantic: short, hurried pumps give little air and tire the arm.</color> ";
        string vent = Vent < 0.05f ? "<color=#FF9A80>The air vent is shut: no draft reaches the fire.</color> " : "";
        string piece = "";
        if (p != null && p.location == CraftLocation.Fire)
        {
            // As the window's forge says it: the face down in the coals running hotter, a weld's seam still open.
            bool downHotter = (p.faceBias > 0) == (p.faceUp == 0); // faceBias: face 1's lead; face 1 is down while face 0 is up
            string faces = Math.Abs(p.faceBias) > 0.15 && downHotter ? $" The face down in the coals runs hotter: turn it ({StationControls.K(StationControl.TurnOver)}) so it heats evenly." : "";
            piece = $"The piece is in the coals: {InFireWords(p)}.{faces}{SeamWords(p)}";
        }
        else if (p != null)
            piece = $"{StationControls.K(StationControl.Fire)}: put the piece in the coals.";
        hudFeel.text = $"{fire}{fuel}. {rhythm}{vent}{piece}";
    }

    private void Puff()
    {
        float s = (float)pump.Pump(hands);
        float air = BellowsClock.Pump(Time.unscaledTime, s, ref airPending); // a pump too soon goes with the next, as the window's
        if (air > 0f) Send(new CraftRequest(CraftRequestKind.Bellows) { a = air });
        visuals?.Puff();
    }

    // ------------------------------------------------------------------ the wheel, the file, the brush
    /// <summary>The angle held to the wheel: as set (5-35°, a bevel no steeper than the stone can be held to).</summary>
    private float GrindAngle => Mathf.Clamp(setup.angle > 0f ? setup.angle : 15f, (float)CraftSurface.StoneAngleMin, (float)CraftSurface.MaxBevelAngle);
    private bool Shaping => Abrasive == null || Abrasive.kind == CraftAbrasiveKind.Wheel || Abrasive.kind == CraftAbrasiveKind.File;
    private bool BevelSide => setup.side == CraftSurfaceSide.LeftBevel || setup.side == CraftSurfaceSide.RightBevel;

    private void UpdateGrind(float dt, Vector2 pointer)
    {
        CraftPiece p = Piece;
        if (p == null)
        {
            End(true);
            return;
        }
        // The tool: Next Tool. The side: the number keys, the previous / next choice. The angle: Less / More, the wheel.
        if (StationControls.Pressed(StationControl.NextTool) && abrasives.Count > 1)
        {
            StopGrinding(true);
            abrasive = (abrasive + 1) % abrasives.Count;
            grindStop = null;
            RebuildTool();
            hintTimer = 0f;
        }
        int side = PickedChoice(Sides.Length, Array.IndexOf(Sides, setup.side));
        if (side >= 0 && Sides[side] != setup.side)
        {
            StopGrinding(true);
            setup.side = Sides[side];
            grindStop = null;
            hintTimer = 0f;
        }
        int adjust = StationControls.AdjustStep;
        if (adjust != 0 && Shaping && BevelSide)
        {
            setup.angle = Mathf.Clamp(GrindAngle + adjust, (float)CraftSurface.StoneAngleMin, (float)CraftSurface.MaxBevelAngle);
            hintTimer = 0f;
        }
        if (StationControls.Pressed(StationControl.Quick)) Dip(p);
        TickDip(dt);
        CraftAbrasive a = Abrasive;
        float angle = GrindAngle;
        // Where along the blade the wheel bites - the hand carries the blade: moved right, it slides right and the wheel
        // bites nearer its first end (on the view's left) - and how hard the hand presses: the pressure set, × 0.7 at the
        // bottom of the screen to 1.3 at the top - the window's rule.
        float along = 1f - Mathf.Clamp01(pointer.x / Mathf.Max(1f, Screen.width));
        float press = Mathf.Clamp01(Pressure(0.5f) * Mathf.Lerp(CraftGrindMode.PressLow, CraftGrindMode.PressHigh, Mathf.Clamp01(pointer.y / Mathf.Max(1f, Screen.height))));
        if (held && !wasHeld)
        {
            // A press holds the blade to the wheel - when the tool can work it now (told here, not sent to be refused).
            if (CanWork(p, out string why)) { grinding = true; grindStop = null; stroke.Clear(); sendTimer = 0f; }
            else { grindStop = why; grindStopLocal = true; }
        }
        if (grinding && !held)
            StopGrinding(true);
        if (grinding)
        {
            // One sample a frame while the blade is held to the wheel (the arm tires by the time it works).
            stroke.Add(Time.unscaledTime, along, 0.5, angle, press, hands);
            sendTimer += dt;
            if (sendTimer >= GrindEvery) FlushGrind();
        }
        bool free = p.location == CraftLocation.Air || p.location == CraftLocation.Assembled;
        bool touching = grinding && free;
        bool wheel = a != null && a.kind == CraftAbrasiveKind.Wheel;
        wheelTurn += dt * (wheel ? 900f : 0f);
        if (wheelObject != null)
        {
            if (wheel)
                wheelObject.localRotation = Quaternion.Euler(wheelTurn, 0f, 90f);
            else
                wheelObject.localPosition = new Vector3(Mathf.Sin(Time.unscaledTime * (touching ? 9f : 0f)) * 0.05f, 0.15f, 0.02f);
        }
        // The blade slides along so the bitten segment is under the wheel; pressed in, tilted to the angle.
        if (pieceObject != null)
        {
            pieceObject.gameObject.SetActive(free); // dipped, it is in the tank (the line says so), not at the wheel
            // The side decides how the blade is presented (its first segment on the view's left, +x, the bitten point at
            // x = 0): a bevel edge-on and tilted to the angle so that the face it is on meets the rim, the edge line
            // straight in, a face flat to the rim, the spine first.
            float x = (along - 0.5f) * pieceLength;
            float gap = touching ? -0.002f - 0.004f * press : 0.03f;
            PresentToWheel(setup.side, angle, out Quaternion turn, out float reach);
            float z = wheel ? WheelFront + reach + gap : 0.02f;
            pieceObject.localPosition = new Vector3(x, wheel ? 0.12f : 0.135f, z);
            pieceObject.localRotation = turn;
        }
        // Sparks off the rim where the blade touches it, by the clock (as many a second at any frame rate), harder pressed more.
        if (touching && wheel && p.kind == CraftPieceKind.Metal && root != null)
        {
            sparkClock += dt;
            while (sparkClock >= 0.03f)
            {
                sparkClock -= 0.03f;
                visuals?.GrindSparks(root.TransformPoint(new Vector3(0f, 0.11f, WheelFront)), 2 + (int)(press * 5));
            }
        }
        meter?.Set("Press", press, UITheme.Gold, (Pressure(0.5f) * CraftGrindMode.PressLow, Pressure(0.5f) * CraftGrindMode.PressHigh, new Color(0.3f, 0.3f, 0.34f, 1f)));
        // The heat, as the window's HEAT gauge: red from where a hardened edge's temper runs, dark where the wheel cannot hold it.
        double runs = TemperRuns(p);
        if (p.kind == CraftPieceKind.Metal)
        {
            const float top = 450f;
            float hot = Mathf.Clamp01((float)p.MaxTemperature / top);
            if (double.IsInfinity(runs)) meter2?.Set("Heat", hot, new Color(1f, 0.6f, 0.3f, 1f), ((float)CraftSurface.WheelTooHot / top, 1f, new Color(0.15f, 0.12f, 0.12f, 1f)));
            else meter2?.Set("Heat", hot, new Color(1f, 0.6f, 0.3f, 1f), (Mathf.Clamp01((float)runs / top), 1f, new Color(0.65f, 0.22f, 0.18f, 1f)), ((float)CraftSurface.WheelTooHot / top, 1f, new Color(0.15f, 0.12f, 0.12f, 1f)));
        }
        else meter2?.Hide();
        string name = a == null ? "No tool" : a.Name;
        hudTitle.text = Shaping
            ? $"{name} · the {SideNames[Mathf.Max(0, Array.IndexOf(Sides, setup.side))]}{(BevelSide ? $" · {angle:0}°" : "")}"
            : $"{name} · all the surface under it";
        string state = p.location == CraftLocation.Quench
            ? (dipId != null ? "Cooling in the tank..." : $"It is in the tank: {StationControls.K(StationControl.Quick)} lifts it out.")
            : !free ? $"Take it out first: it is {CraftStationUI.LocationText(p)}." : HandHeat(p, runs);
        if (grindStop != null && grindStopLocal && !grinding && CanWork(p, out _)) grindStop = null; // the reason no longer holds
        hudFeel.text = $"{state}{(grindStop != null && p.location != CraftLocation.Quench ? $" <color=#FF9A80>{grindStop}</color>" : "")}{(touching && wheel ? " Sparks stream off the wheel." : "")}";
    }

    private const float WheelFront = 0.09f; // the wheel's rim nearest the player (its centre at z = -0.02, radius 0.11)

    /// <summary>How the blade is held to the wheel for <paramref name="side"/>, and how far it reaches from the rim.</summary>
    private void PresentToWheel(CraftSurfaceSide side, float angle, out Quaternion turn, out float reach)
    {
        // The edge toward the wheel (-z), the left face up, the first segment on the view's left (+x).
        Quaternion edgeIn = Quaternion.LookRotation(Vector3.up, -Vector3.right);
        Quaternion spineIn = edgeIn * Quaternion.Euler(0f, 180f, 0f); // rolled over: the spine toward the wheel, end for end the same
        float thick = Mathf.Max(0.002f, PieceTop(Piece));
        switch (side)
        {
            // The face whose bevel is ground leans toward the wheel by the angle: the left face (up) by turning its top toward -z.
            case CraftSurfaceSide.LeftBevel: turn = Quaternion.Euler(-angle, 0f, 0f) * edgeIn; reach = pieceWidth * 0.5f; break;
            case CraftSurfaceSide.RightBevel: turn = Quaternion.Euler(angle, 0f, 0f) * edgeIn; reach = pieceWidth * 0.5f; break;
            case CraftSurfaceSide.Faces: turn = Quaternion.Euler(-90f, 0f, 0f) * edgeIn; reach = thick * 0.5f; break;
            case CraftSurfaceSide.Spine: turn = spineIn; reach = pieceWidth * 0.5f; break;
            default: turn = edgeIn; reach = pieceWidth * 0.5f; break; // the edge line, the tang, the eye: straight in
        }
    }

    /// <summary>Why the tool in hand cannot work the piece now (checked here, so that nothing is sent five times a second to be refused) - the window's checks.</summary>
    private bool CanWork(CraftPiece p, out string why)
    {
        why = null;
        CraftAbrasive a = Abrasive;
        if (a == null)
            why = p.kind == CraftPieceKind.Wood ? "There is no rasp or knife here for wood." : "There is no wheel, file, polish or brush here for metal.";
        else if (p.location == CraftLocation.Quench)
            why = dipId != null ? "It is cooling in the tank." : $"It is in the tank: {StationControls.Key(StationControl.Quick)} lifts it out.";
        else if (p.location != CraftLocation.Air && p.location != CraftLocation.Assembled)
            why = $"Take it out first: it is {CraftStationUI.LocationText(p)}.";
        else if (a.kind == CraftAbrasiveKind.Wheel && p.MaxTemperature > CraftSurface.WheelTooHot)
        {
            string cool = CoolIt(p, "let it cool", false);
            why = cool == "dip it" ? "It is far too hot to hold on the wheel: dip it, or let it cool." : $"It is far too hot to hold on the wheel - {cool}.";
        }
        else if (a.kind == CraftAbrasiveKind.File && p.MaxTemperature > CraftSurface.FileTooHot)
            why = "It is too hot to file: let it cool.";
        else if (a.kind == CraftAbrasiveKind.Wheel && setup.side == CraftSurfaceSide.Eye)
            why = "A wheel cannot reach inside an eye: use a file.";
        return why == null;
    }

    /// <summary>The blade comes off the wheel: what it did is sent (<paramref name="send"/>), or dropped.</summary>
    private void StopGrinding(bool send)
    {
        if (send && grinding && stroke.Samples > 0)
        {
            sendTimer = Mathf.Max(sendTimer, 1f / 60f); // a touch is a frame of the wheel at least
            FlushGrind();
        }
        stroke.Clear();
        sendTimer = 0f;
        grinding = false;
    }

    /// <summary>Sends what the tool did since the last send: the stretch it covered (by the segments' real lengths) and how long it dwelt on each.</summary>
    private void FlushGrind()
    {
        float window = Mathf.Max(1f / 60f, sendTimer);
        sendTimer = 0f;
        CraftPiece p = Piece;
        CraftAbrasive a = Abrasive;
        if (p == null || p.segments.Count == 0 || stroke.Samples < 1 || a == null)
        {
            stroke.Clear();
            return;
        }
        if (!CanWork(p, out string why))
        {
            stroke.Clear();
            grinding = false;
            grindStop = why;
            grindStopLocal = true;
            return;
        }
        // The wheel's time shared out over the stretch it went along (all of it on one stretch held still), as the window's.
        double dwell = stroke.Dwell(p, window, out int from, out int to);
        float press = (float)stroke.Pressure, angle = (float)stroke.Angle;
        stroke.Clear(); // out of the buffer before it goes: never sent twice
        // d = 1: by hand, at the full pace of the hands (the window's careful work is slower).
        var r = new CraftRequest(CraftRequestKind.Grind, p.id) { i0 = from, i1 = to, i2 = (int)setup.side, id = a.id, a = angle, b = press, c = (float)dwell, d = 1f };
        switch (a.kind)
        {
            case CraftAbrasiveKind.File: r.kind = CraftRequestKind.File; break;
            case CraftAbrasiveKind.Brush: r = new CraftRequest(CraftRequestKind.Descale, p.id) { i0 = from, i1 = to, id = a.id, a = press, b = (float)dwell, d = 1f }; break;
            case CraftAbrasiveKind.Polish: r = new CraftRequest(CraftRequestKind.Polish, p.id) { i0 = from, i1 = to, id = a.id, a = press, b = (float)dwell, d = 1f }; break;
        }
        string id = p.id;
        Send(r, (ok, reason) =>
        {
            // Refused (too hot, it skates...): the hands stop until the next press, rather than asking again and again.
            if (!ok && reason != CraftAnswerWaits.NoAnswer && setup.piece == id)
            {
                if (grinding) StopGrinding(false);
                grindStop = reason;
                grindStopLocal = false;
            }
        });
    }

    /// <summary>Why the piece cannot be dipped (null: it can): only a piece in the hands, not fitted, at a station with a tank - the window's words.</summary>
    private string DipRefused(CraftPiece p)
    {
        if (!station.Has(CraftTools.QuenchTank)) return "There is no water here to dip it in.";
        if (p.kind == CraftPieceKind.Wood) return "Wood is not cooled in the tank.";
        if (p.location == CraftLocation.Assembled) return "It is fitted: it cannot go in the tank - press lighter and let it cool.";
        if (p.location == CraftLocation.Fire) return "It is in the fire: quench it at the tank, not here.";
        if (p.location != CraftLocation.Air) return "Take it out first.";
        if (CraftSurface.DipQuenches(Ws, p)) return CraftSurface.DipWouldQuench("at the tank", station.Has(CraftTools.Ashes));
        return null;
    }

    /// <summary>
    /// How the hot piece is cooled, in the window's words: dipped (<paramref name="key"/>: naming the key) - unless it is
    /// fitted (<paramref name="fitted"/>: what to do instead), there is no tank here, or it can still harden (a dip would
    /// quench it).
    /// </summary>
    private string CoolIt(CraftPiece p, string fitted, bool key)
    {
        if (p.location == CraftLocation.Assembled) return $"it is fitted and cannot go in the tank: {fitted}";
        if (!station.Has(CraftTools.QuenchTank)) return "let it cool";
        if (CraftSurface.DipQuenches(Ws, p)) return "let it cool in the air (it is still hot enough to harden: a dip would quench it)";
        return key ? $"dip it ({StationControls.K(StationControl.Quick)})" : "dip it";
    }

    /// <summary>
    /// Quick at the wheel: the blade into the tank to cool (it comes out once it is cooler than
    /// <see cref="CraftGrindMode.DipCool"/>, or after <see cref="CraftGrindMode.DipSeconds"/>); again, it comes out at once.
    /// A piece left in the tank another way comes out too.
    /// </summary>
    private void Dip(CraftPiece p)
    {
        if (dipId != null)
        {
            LiftDip();
            return;
        }
        if (dipAsked)
            return;
        if (p.location == CraftLocation.Quench)
        {
            Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Air });
            return;
        }
        string why = DipRefused(p);
        if (why != null)
        {
            Say(why, true);
            return;
        }
        StopGrinding(true); // what the wheel did goes first
        grindStop = null;
        string id = p.id;
        dipAsked = true;
        dipLeft = false;
        Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Quench, a = 1f }, (ok, reason) =>
        {
            dipAsked = false;
            bool left = dipLeft;
            dipLeft = false;
            // In (or perhaps in: no answer leaves it unknown) - it comes out again once cool, or after a few seconds; at
            // once when the tool was put down meanwhile, as End lifts a dip out.
            if (ok || reason == CraftAnswerWaits.NoAnswer)
            {
                if (left)
                {
                    CraftPiece q = Ws?.Piece(id);
                    if (q != null && (q.location == CraftLocation.Quench || q.location == CraftLocation.Air))
                        Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Air });
                }
                else
                {
                    dipId = id;
                    dipTime = 0f;
                }
            }
        });
    }

    private void TickDip(float dt)
    {
        if (dipId == null)
            return;
        dipTime += dt;
        CraftPiece d = Ws?.Piece(dipId);
        if (d == null || d.location != CraftLocation.Quench && dipTime > 0.5f)
            dipId = null; // gone, or taken out some other way
        else if (d.location == CraftLocation.Quench && (d.MaxTemperature < CraftGrindMode.DipCool || dipTime >= CraftGrindMode.DipSeconds))
            LiftDip();
    }

    /// <summary>The dipped piece comes out of the tank (it is cool, the time is up, or the hands leave it).</summary>
    private void LiftDip()
    {
        string id = dipId;
        dipId = null;
        if (id == null || Ws == null)
            return;
        CraftPiece p = Ws.Piece(id);
        if (p != null && p.location == CraftLocation.Quench)
            Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Air });
    }

    // ------------------------------------------------------------------ the whetstone
    /// <summary>The stretch of edge the stone works: the one framed in the window, else every bevelled segment.</summary>
    private void EdgeSpan(CraftPiece p, out int a, out int b)
    {
        int n = p.segments.Count;
        if (setup.span && n > 0)
        {
            a = Mathf.Clamp(Mathf.Min(setup.from, setup.to), 0, n - 1);
            b = Mathf.Clamp(Mathf.Max(setup.from, setup.to), 0, n - 1);
            return;
        }
        a = n;
        b = -1;
        for (int i = 0; i < n; i++)
            if (p.segments[i].Beveled) { a = Mathf.Min(a, i); b = Mathf.Max(b, i); }
        if (b < 0) { a = 0; b = n - 1; }
    }

    /// <summary>Why the stone cannot work the stretch now (null: it can) - told, not sent to be refused (the window's checks).</summary>
    private static string Blocked(CraftPiece p, int a, int b)
    {
        if (p.location == CraftLocation.Quench) return "It is cooling in the tank.";
        if (p.location != CraftLocation.Air && p.location != CraftLocation.Assembled) return $"Take it out first: it is {CraftStationUI.LocationText(p)}.";
        if (p.MaxTemperature > CraftSurface.StoneTooHot) return "It is too hot for the stone: let it cool first.";
        if (!CraftSurface.HasEdge(p, a, b)) return "There is no edge here yet: grind or forge the bevels first - a stone only hones an edge that is there.";
        return null;
    }

    private void UpdateSharpen(float dt, Vector2 pointer)
    {
        CraftPiece p = Piece;
        if (p == null)
        {
            End(true);
            return;
        }
        // The stone: Next Tool. The face: Turn Over, the number keys (1 left, 2 right), the previous / next choice.
        if (StationControls.Pressed(StationControl.NextTool) && abrasives.Count > 1)
        {
            EndStroke(true); // the stroke under way was made on the stone in hand
            abrasive = (abrasive + 1) % abrasives.Count;
            RebuildTool();
            hintTimer = 0f;
        }
        int face = PickedChoice(2, leftFace ? 0 : 1);
        if (StationControls.Pressed(StationControl.TurnOver)) face = leftFace ? 1 : 0;
        if (face >= 0 && (face == 0) != leftFace)
        {
            EndStroke(true); // on the face it was made on
            leftFace = face == 0;
            hintTimer = 0f;
        }
        // How hard it is pressed: Less / More, the wheel, a twentieth a step.
        int adjust = StationControls.AdjustStep;
        if (adjust != 0)
        {
            setup.pressure = Mathf.Clamp(Pressure(0.45f) + 0.05f * adjust, 0.05f, 1f);
            hintTimer = 0f;
        }
        CraftAbrasive s = Abrasive;
        float along = Mathf.Clamp01(pointer.x / Mathf.Max(1f, Screen.width));
        float angle = Mathf.Lerp((float)CraftSurface.StoneAngleMin, (float)CraftSurface.StoneAngleMax, Mathf.Clamp01(pointer.y / Mathf.Max(1f, Screen.height)));
        EdgeSpan(p, out int a, out int b);
        if (held && !wasHeld)
        {
            stroke.Clear();
            passesSent = 0;
            honeStop = Blocked(p, a, b);
            stroking = honeStop == null;
        }
        if (stroking && held)
        {
            // Pushing the blade away (to the right for the left face, to the left for the right) leads with the edge.
            stroke.Add(Time.unscaledTime, along, leftFace ? along : 1f - along, angle, Pressure(0.45f), hands);
            SendPasses(false); // each pass goes as it is made
        }
        else if (stroking && !held)
            EndStroke(true); // let go: the run under way too, if it is a pass
        bool free = p.location == CraftLocation.Air || p.location == CraftLocation.Assembled;
        if (pieceObject != null)
        {
            pieceObject.gameObject.SetActive(free);
            // Across the stone, its point away from the hands, the face honed lying on the stone with the spine raised by
            // the angle held, the edge on the stone pointing the way that leads it (the view's right for the left face, its
            // left for the right one); the hand carries it along the stone (right on the screen, right on the stone), and
            // the stretch it hones lies over the stone.
            float tip = angle * Mathf.Deg2Rad;
            Vector3 leftSide = leftFace ? new Vector3(Mathf.Sin(tip), -Mathf.Cos(tip), 0f) : new Vector3(Mathf.Sin(tip), Mathf.Cos(tip), 0f);
            Quaternion turn = Quaternion.LookRotation(leftSide, Vector3.back);
            Vector3 edgeWay = turn * Vector3.right;
            float hw = pieceWidth * 0.5f, x = (0.5f - along) * 0.22f;
            float over = (float)((p.PositionOf(a) + p.PositionOf(b)) * 0.5 - 0.5) * pieceLength;
            pieceObject.localPosition = new Vector3(x - edgeWay.x * hw, StoneTop - edgeWay.y * hw + 0.0015f, over);
            pieceObject.localRotation = turn;
        }
        // The angle held against the bevel's (grey) and the design's (green); how hard, against the burr's and the chipping bands.
        double bevel = 0;
        for (int i = a; i <= b && i < p.segments.Count; i++) bevel = Math.Max(bevel, leftFace ? p.segments[i].AngleL : p.segments[i].AngleR);
        float Span(double deg) => (float)((deg - CraftSurface.StoneAngleMin) / (CraftSurface.StoneAngleMax - CraftSurface.StoneAngleMin));
        var bands = new List<(float, float, Color)>();
        if (CraftArt.TargetAt(station.Rules, p, Mathf.Clamp((a + b) / 2, 0, p.segments.Count - 1), out _, out _, out float edgeAngle, out bool edged) && edged)
            bands.Add((Span(edgeAngle * 0.5 - 2), Span(edgeAngle * 0.5 + 2), new Color(0.3f, 0.75f, 0.4f, 1f))); // the design's angle a face
        if (bevel > 0) bands.Add((Span(bevel - 0.5), Span(bevel + 0.5), new Color(0.6f, 0.6f, 0.62f, 1f)));
        meter?.Set("Angle", Span(angle), UITheme.Gold, bands.ToArray());
        meter2?.Set("Press", Pressure(0.45f), UITheme.Gold,
            (0f, (float)CraftSurface.BurrOffFull, new Color(0.3f, 0.75f, 0.4f, 1f)), ((float)CraftSurface.BurrOffFull, (float)CraftSurface.BurrOffBelow, new Color(0.22f, 0.45f, 0.28f, 1f)),
            (0.7f, 1f, new Color(0.65f, 0.22f, 0.18f, 1f)));
        hudTitle.text = $"{(s != null ? s.Name : "No stone")} · the {(leftFace ? "left" : "right")} face · about {angle:0}° · press {Pressure(0.45f) * 100:0}%";
        string counted = honedL + honedR > 0 || lastFeel.Length > 0 ? $"Strokes L {honedL} · R {honedR}{(lastFeel.Length > 0 ? " · last: " + lastFeel : "")}. " : "";
        string why = Blocked(p, a, b) ?? (stroking ? null : honeStop);
        hudFeel.text = free || p.location == CraftLocation.Quench
            ? $"{counted}{(why != null ? $"<color=#FF9A80>{why}</color>" : HoneAdvice(p, a, b, bevel))}"
            : $"It is not in your hands: it is {CraftStationUI.LocationText(p)}.";
    }

    /// <summary>What to do next on the stone, by the station's own thresholds (<see cref="CraftSurface.NextHone"/>), in a line.</summary>
    private string HoneAdvice(CraftPiece p, int a, int b, double bevel)
    {
        CraftAbrasive s = Abrasive;
        double hl = 0, hr = 0, burr = 0, apex = 0, floor = 0, flat = 0;
        int n = 0;
        double wobble = Math.Max(lastWobble > 0f ? lastWobble : 0, 11.5 * CraftRequestGuard.FloorOf(hands.skill));
        bool edge = CraftSurface.HasEdge(p, a, b);
        for (int i = a; i <= b && i < p.segments.Count; i++)
        {
            MetalSegment g = p.segments[i];
            if (edge && !CraftSurface.HasEdge(g)) continue; // the stretches the stone hones
            hl += Math.Min(1, g.honeL); hr += Math.Min(1, g.honeR); burr = Math.Max(burr, g.burr);
            apex += g.apex; flat = Math.Max(flat, g.EdgeThickness);
            if (s != null) floor += CraftSurface.SharpenFloor(station.Rules, p, g, s, wobble);
            n++;
        }
        n = Math.Max(1, n);
        hl /= n; hr /= n; apex /= n; floor /= n;
        string edgeWords = EdgeWords(p, a, b);
        string mine = leftFace ? "left" : "right", other = leftFace ? "right" : "left";
        string turn = StationControls.K(StationControl.TurnOver);
        bool strop = CraftSurface.IsStrop(s);
        // Edge leading is a push toward the edge (to the right for the left face), trailing a pull back - as in the window.
        string lead = leftFace ? "push it to the right" : "push it to the left", trail = leftFace ? "pull it to the left" : "pull it to the right";
        string next = CraftSurface.NextHone(leftFace ? hl : hr, leftFace ? hr : hl, burr, flat, edge, apex, s != null ? floor : apex) switch
        {
            CraftSurface.HoneStep.NoEdge => "There is no edge here yet: grind the bevels first.",
            CraftSurface.HoneStep.WearFlat => $"A flat still shows along the edge: the bevels have not met here - {(strop ? "the strop does not wear it away" : "the stone wears it away only slowly")}, so better grind them until they meet.",
            CraftSurface.HoneStep.WearLastFlat => strop ? "The bevels have met, but a hair of flat still shows: the strop does not wear it away - a few strokes on a stone first."
                : $"The bevels have met, but a hair of flat still shows: stroke each face, edge leading ({lead} for this one) - the stone wears it away, then the faces hone.",
            CraftSurface.HoneStep.HoneFace => strop ? $"On the strop, draw the {mine} face spine first ({trail}): edge leading, the edge cuts the leather."
                : $"Stroke the {mine} face at a steady angle, edge leading ({lead}), until a burr curls over.",
            CraftSurface.HoneStep.TurnFace => $"This face is honed: now the {other} face ({turn}).",
            CraftSurface.HoneStep.TakeBurrOff => strop ? $"Take the burr off on the strop: each face drawn spine first ({trail} for the {mine} face)."
                : $"Take the burr off: light trailing strokes ({trail}; press under {CraftSurface.BurrOffBelow * 100:0}%, {StationControls.AdjustKeys}) or the strop, spine first.",
            CraftSurface.HoneStep.KeepHoning => "Both faces honed: a few more steady strokes on each bring it to this stone's best.",
            CraftSurface.HoneStep.StoneTooCoarse => $"This stone is coarser than the edge already is: a finer stone or the strop ({StationControls.K(StationControl.NextTool)}).",
            _ => wobble > 1.5 ? "As keen as these strokes make it: steadier strokes go finer." : "As keen as this stone makes it here.",
        };
        // The design's angle a side here (as its section carries it): held well above it and the bevel, the stone hones a
        // steeper edge than the item wants.
        double want = CraftArt.TargetAt(station.Rules, p, Mathf.Clamp((a + b) / 2, 0, p.segments.Count - 1), out _, out _, out float edgeAngle, out bool edged) && edged ? edgeAngle * 0.5 : 0;
        string steeper = strop && lastLeading && lastAngle > 0f ? $" Edge leading, the edge cuts into the strop's leather and dulls: draw it spine first ({trail})."
            : bevel - 1 > CraftSurface.StoneAngleMax ? " This bevel is steeper than the stone can be held: grind it lower."
            : lastAngle > 0f && bevel > 0 && CraftSurface.Contact(lastAngle, bevel) < 1 ? " Hold it steeper: the stone only rubs the bevel's shoulder (the grey mark)."
            : lastAngle > 0f && Math.Max(want, bevel) > 0 && lastAngle > Math.Max(want, bevel) + 3 ? $" Hold it lower: it hones the edge steeper than {(want >= bevel ? "the design's angle (the green mark)" : "its bevel (the grey mark)")} - a steeper edge cuts worse." : "";
        return $"{edgeWords} {next}{steeper}";
    }

    /// <summary>Each pass goes to the station as it is made (the run under way too, when the stroke ends); a wiggle is no pass and sends nothing.</summary>
    private void SendPasses(bool ending)
    {
        stroke.TakePasses(ref passesSent, ending, passBuffer);
        if (passBuffer.Count == 0)
            return;
        CraftPiece p = Piece;
        foreach (CraftPass pass in passBuffer)
            SendPass(p, pass);
    }

    private void SendPass(CraftPiece p, CraftPass pass)
    {
        lastAngle = (float)pass.angle;
        lastWobble = (float)pass.wobble;
        lastLeading = pass.leading;
        lastFeel = $"{pass.angle:0}°, {(pass.wobble > 3 ? "rocking" : pass.wobble > 1.2 ? "unsteady" : "steady")}, edge {(pass.leading ? "leading" : "trailing")}";
        CraftAbrasive s = Abrasive;
        if (p == null || s == null)
            return;
        EdgeSpan(p, out int a, out int b);
        string why = Blocked(p, a, b);
        if (why != null)
        {
            honeStop = why; // said, not sent to be refused
            return;
        }
        string id = p.id;
        bool left = leftFace;
        Send(new CraftRequest(CraftRequestKind.Sharpen, id)
        {
            i0 = a, i1 = b, i2 = (int)(left ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel), id = s.id,
            a = (float)pass.angle, b = (float)pass.pressure, count = 1, c = (float)pass.wobble, flag = pass.leading,
        }, (ok, reason) =>
        {
            if (!ok || setup.piece != id)
                return;
            if (left) honedL++;
            else honedR++;
        });
    }

    /// <summary>The blade comes off the stone: the stroke's passes go (<paramref name="send"/>) to the face and stone they were made on.</summary>
    private void EndStroke(bool send)
    {
        if (stroking && send)
            SendPasses(true);
        stroke.Clear();
        stroking = false;
        passesSent = 0;
    }

    /// <summary>The edge of the stretch worked in words, by the window's and the look's own ladder (<see cref="CraftSurface.KeenStep"/>), with its burr.</summary>
    private static string EdgeWords(CraftPiece p, int a, int b)
    {
        double keen = 0, burr = 0;
        int n = 0;
        for (int i = a; i <= b && i < p.segments.Count; i++)
        {
            keen += CraftEvaluator.Sharpness(p.segments[i]);
            burr = Math.Max(burr, p.segments[i].burr);
            n++;
        }
        keen /= Math.Max(1, n);
        string edge = CraftSurface.KeenStep(keen) switch
        {
            0 => "The edge is blunt: light glints all along it.",
            1 => "The edge cuts, but it is not keen: it still catches the light in places.",
            2 => "The edge is keen: it bites a fingernail.",
            _ => "The edge is razor-keen: it disappears into a line too fine to see.",
        };
        return edge + (burr > CraftSurface.BurrShows ? " A wire burr has rolled over." : "");
    }

    // ------------------------------------------------------------------ the quench tank
    /// <summary>How much of it goes into the tank (0.2-1): as the window set it, or as the hand set it here.</summary>
    private float TankDepth => Mathf.Clamp(setup.depth > 0f ? setup.depth : 1f, 0.2f, 1f);
    private bool FlatEntry => setup.entryAngle >= 0.5f;

    private void UpdateQuench(float dt, Vector2 pointer)
    {
        CraftPiece p = Piece;
        if (p == null)
        {
            End(true);
            return;
        }
        // The entry: upright, point first (the number key 1) or flat (2), or the previous / next choice; how deep: Less / More.
        int entry = PickedChoice(2, FlatEntry ? 1 : 0);
        if (entry >= 0 && (entry == 1) != FlatEntry)
        {
            setup.entryAngle = entry == 1 ? 0.9f : 0f;
            hintTimer = 0f;
        }
        // In the tank - unless the hand has just lifted it out and the copy of the workshop (a network client) does not show
        // it yet: then it is out, and a press plunges it again (after the lift, as the station takes them in turn).
        bool inTank = p.location == CraftLocation.Quench && liftsAsked == 0;
        int adjust = StationControls.AdjustStep;
        if (adjust != 0)
        {
            setup.depth = Mathf.Clamp(Mathf.Round((TankDepth + 0.1f * adjust) * 10f) / 10f, 0.2f, 1f);
            hintTimer = 0f;
            // Held in the tank, the hand lets it down deeper or draws it up: the tank is told how much of it is under now.
            if (plunged && (inTank || ownPlunge))
                Send(new CraftRequest(CraftRequestKind.Agitate, p.id) { i0 = (int)sentMotion, a = TankDepth });
        }
        if (held && !wasHeld && !plunged)
        {
            if (inTank)
            {
                // Already in the tank (the window put it there): the hand takes hold of it where it hangs.
                plunged = true;
                ownPlunge = false;
                sentMotion = p.motion;
                tankHand.Clear();
            }
            else if (p.location == CraftLocation.Air || p.location == CraftLocation.Fire || p.location == CraftLocation.Quench)
                Plunge(p);
            else
                Say($"Take it out first: it is {CraftStationUI.LocationText(p)}.", true);
        }
        // A piece taken hold of where it hung has left the tank (taken out some other way): the hand lets it go.
        if (plunged && !ownPlunge && !inTank)
        {
            plunged = false;
            sentMotion = CraftQuenchMotion.Still;
            tankHand.Clear();
            Say("It is out of the tank.");
        }
        if (plunged && !held)
            LiftOut();
        Vector2 hand = new Vector2((pointer.x / Mathf.Max(1f, Screen.width) - 0.5f) * 0.4f, (pointer.y / Mathf.Max(1f, Screen.height) - 0.5f) * 0.25f);
        if (plunged)
        {
            // How the hand moves it: judged over the last moment by distance and time (the window's rule), not by frames.
            tankHand.MinStep = Screen.height * 0.005f;
            tankHand.Add(Time.unscaledTime, pointer.x, pointer.y);
            motionTimer += dt;
            if (motionTimer >= 0.3f)
            {
                motionTimer = 0f;
                CraftQuenchMotion m = tankHand.Motion(Screen.height * 0.05f);
                if (m != sentMotion && (inTank || ownPlunge))
                {
                    sentMotion = m;
                    Send(new CraftRequest(CraftRequestKind.Agitate, p.id) { i0 = (int)m });
                }
            }
        }
        quenchDepth = Mathf.MoveTowards(quenchDepth, plunged || inTank ? 1f : 0f, dt * 5f);
        if (pieceObject != null)
        {
            // Held in the tongs by the tang: upright, point first (the tang up), or flat (its faces up and down, the tang on
            // the view's left), slanting in until as much of it is under the surface as is set, the far part first (as the
            // window draws it); moved as the hand moves it, inside the tank. The liquid lets what is under it show; the tongs
            // stay above it.
            bool inside = plunged || inTank;
            bool flat = inside ? p.entryAngle >= 0.5 : FlatEntry;
            float under = inTank && !ownPlunge ? Mathf.Clamp01((float)p.depth) : TankDepth;
            float half = pieceLength * 0.5f, bob = hand.y * 0.3f;
            float room = Mathf.Max(0f, tankLength * 0.5f - 0.03f - (flat ? half * Mathf.Cos(25f * Mathf.Deg2Rad) : pieceWidth));
            float x = Mathf.Clamp(-hand.x * 0.6f, -room, room); // right on the screen is -x here
            Quaternion turn;
            Vector3 down, at;
            if (flat)
            {
                // Slanted 25° along its length, the point down: the surface crosses it where the part under it begins.
                turn = Quaternion.Euler(0f, 0f, 25f) * Quaternion.LookRotation(Vector3.up, -Vector3.right);
                down = turn * Vector3.up; // from the tang toward the point
                float atSurface = 1f - under;
                at = new Vector3(x, Mathf.Lerp(0.06f + 0.42f * half, -down.y * pieceLength * (atSurface - 0.5f) - 0.02f * under, quenchDepth) + bob * quenchDepth, 0f);
            }
            else
            {
                turn = Quaternion.LookRotation(Vector3.forward, Vector3.down); // its flat toward the view, the point down
                down = Vector3.down;
                at = new Vector3(x, Mathf.Lerp(0.06f + half, half - under * pieceLength, quenchDepth) + bob * quenchDepth, 0f);
            }
            pieceObject.localPosition = at;
            pieceObject.localRotation = turn;
            bool shown = p.location == CraftLocation.Air || p.location == CraftLocation.Quench;
            pieceObject.gameObject.SetActive(shown);
            if (tongsObject != null)
            {
                tongsObject.gameObject.SetActive(shown);
                tongsObject.localPosition = at - down * half; // the tang's end
                tongsObject.localRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(0f, 0.85f, 0.5f));
            }
        }
        CraftQuenchant q = station.Rules.Quenchant(Ws.Quench.medium);
        CraftQuenchMotion shownMotion = plunged ? sentMotion : inTank ? p.motion : CraftQuenchMotion.Still;
        meter?.Set("Moving", plunged || inTank ? (shownMotion == CraftQuenchMotion.Still ? 0.08f : shownMotion == CraftQuenchMotion.SideToSide ? 0.5f : 0.95f) : 0f,
            shownMotion == CraftQuenchMotion.SideToSide ? UITheme.Bad : UITheme.Gold, (0.7f, 1f, new Color(0.3f, 0.75f, 0.4f, 1f)));
        meter2?.Set("Depth", TankDepth, new Color(0.55f, 0.7f, 0.95f, 1f));
        hudTitle.text = $"The tank · {q?.Name ?? Ws.Quench.medium} · {(FlatEntry ? "flat (it warps)" : "upright, point first")} · {(TankDepth >= 0.97f ? "all of it" : $"the far {TankDepth * 100:0}%")}";
        double hot = p.MaxTemperature;
        string motion = shownMotion switch
        {
            CraftQuenchMotion.UpDown => "Moving it up and down - good.",
            CraftQuenchMotion.Swirl => "Moving it round - good.",
            CraftQuenchMotion.SideToSide => "Side to side - it warps!",
            _ => "Held still: keep it moving.",
        };
        hudFeel.text = plunged
            ? $"{(hot > 400 ? "It hisses and boils the liquid round it." : hot > 120 ? "It bubbles, quieter now." : "It is cool.")} {motion}"
            : inTank ? $"It is in the tank, hanging still: hold {StationControls.K(StationControl.Work)} to take hold of it and keep it moving; let go to lift it out."
            : $"{HeatWords(p)}.{Harsh(CraftLook.HarshQuench(station.Rules, p, q, station.Quenchants))}";
    }

    /// <summary>The quench's warning (a harsh quenchant for a blade of hardening steel), coloured, after a space; "" when none is due.</summary>
    private static string Harsh(string words) => string.IsNullOrEmpty(words) ? "" : $" <color=#FFB070>{words}</color>";

    private void Plunge(CraftPiece p)
    {
        plunged = ownPlunge = true; // the hand follows at once; a refusal lets it go again
        sentMotion = CraftQuenchMotion.Still;
        tankHand.Clear();
        motionTimer = 0f;
        string id = p.id;
        Send(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Quench, a = TankDepth, b = setup.entryAngle }, (ok, why) =>
        {
            // In: the hiss (or the flare) is the workshop's own news, said as it comes (Tell).
            if (!ok && why != CraftAnswerWaits.NoAnswer && setup.piece == id)
                plunged = ownPlunge = false;
        });
    }

    /// <summary>The hand lifts the piece it holds out of the tank (one plunged here, even before the copy shows it in).</summary>
    private void LiftOut()
    {
        bool own = ownPlunge;
        plunged = ownPlunge = false;
        sentMotion = CraftQuenchMotion.Still;
        tankHand.Clear();
        CraftPiece p = Piece;
        if (p == null || !own && p.location != CraftLocation.Quench)
            return;
        liftsAsked++;
        Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Air }, (ok, why) => liftsAsked = Math.Max(0, liftsAsked - 1));
    }

    // ------------------------------------------------------------------ carving
    /// <summary>The knife's setting (0.1-1): how hard it is pressed into the wood - the window's Knife, or as the hand set it here.</summary>
    private float Knife => Mathf.Clamp(Pressure(0.5f), 0.1f, 1f);

    /// <summary>The depth (mm) the knife at <paramref name="press"/> takes in a stroke of <paramref name="seconds"/> by hand (the full pace) - the window's rule.</summary>
    private float BiteDepth(float press, float seconds) => press * 1.8f * Mathf.Clamp(seconds / 0.5f, 0.4f, 1.2f) * (0.6f + 0.4f * (float)hands.Strength);

    private void UpdateCarve(float dt, Vector2 pointer)
    {
        CraftPiece p = Piece;
        if (p == null)
        {
            End(true);
            return;
        }
        float along = Mathf.Clamp01(pointer.x / Mathf.Max(1f, Screen.width));
        // The cut: Next Tool, the number keys, the previous / next choice (a stroke under way is made first).
        int pick = PickedChoice(CutNames.Length, carveCut);
        if (pick < 0 && StationControls.Pressed(StationControl.NextTool)) pick = (carveCut + 1) % CutNames.Length;
        if (pick >= 0 && pick != carveCut)
        {
            FinishKnife();
            boring = false;
            boreCircles.Clear();
            carveCut = pick;
            cutPicked = true;
            hintTimer = 0f;
        }
        // The knife's setting: Less / More, the wheel.
        int adjust = StationControls.AdjustStep;
        if (adjust != 0)
        {
            setup.pressure = Mathf.Clamp(Knife + 0.05f * adjust, 0.1f, 1f);
            hintTimer = 0f;
            Say($"The knife: {Knife * 100:0}% - it bites about {BiteDepth(Knife, 0.5f):0.0} mm in a half-second stroke.", false, false);
        }
        bool bench = p.location == CraftLocation.Air;
        if (carveCut == 3)
        {
            // Boring: the bit stands at the end nearer the hand (the slot goes in from there), and each circle the hand
            // draws - wherever it draws it, from wherever it pressed - turns it once, a stretch deeper.
            if (!boring) boreEnd = along > 0.5f ? 1 : 0;
            if (held && !wasHeld)
            {
                if (!bench) Say("Take it onto the bench first.", true);
                else if (!CraftWoodwork.TangSlot(Ws, p, out _, out _))
                    Say(string.IsNullOrEmpty(p.intentDesign) ? "Choose what it is for in the window first: the slot is the size of the tang it goes on." : "This part goes on no tang: there is no slot to bore.", true);
                else
                {
                    boring = true;
                    boreCircles.MinStep = Mathf.Max(1f, Screen.height * 0.002f); // the window's 2 px of its 400 (kept by distance: any frame rate)
                    boreCircles.Begin(pointer.x, pointer.y);
                    bitFrom = bitSpin; // the brace goes on turning from where it stands
                }
            }
            if (held && boring)
            {
                boreCircles.Add(pointer.x, pointer.y);
                bitSpin = bitFrom + (float)(boreCircles.Wound * Mathf.Rad2Deg);
                if (boreCircles.Turns >= 1)
                {
                    boreCircles.Spend(1);
                    BoreTurn(p);
                }
            }
            if (!held && boring)
            {
                boring = false;
                boreCircles.Clear();
            }
        }
        else
        {
            // A knife stroke along the blank: where it starts and ends is the stretch cut; straight is clean.
            if (held && !wasHeld)
            {
                if (bench)
                {
                    carving = true;
                    carveFrom = carveTo = along;
                    carveStart = Time.unscaledTime;
                    carvePath.Clear();
                    carvePath.Add(pointer);
                }
                else Say("Take it onto the bench first.", true);
            }
            if (held && carving)
            {
                carveTo = along;
                if (carvePath.Count == 0 || carvePath.Count < 600 && (carvePath[carvePath.Count - 1] - pointer).sqrMagnitude >= 4f) carvePath.Add(pointer);
                chipClock += dt;
                while (chipClock >= 0.08f) { chipClock -= 0.08f; Chips(along, 1); }
            }
            if (!held && carving)
                FinishKnife();
        }
        // The knife over the blank where the hand is (its first end on the view's left, as the window draws it) and as far
        // off the stroke's own line as the hand is (straight, it runs down the middle), the chips flying; the blank on its
        // side to thin it, the design's outline over it (the width, or as it lies on its side the thickness). Boring, the
        // knife is put by and the bit stands at the end, turning as the hand circles.
        float top = carveCut == 1 ? pieceWidth : PieceTop(p), across = carveCut == 1 ? PieceTop(p) : pieceWidth;
        float tolerance = Mathf.Max(1f, Screen.height * StraightTolerance);
        float straight = carveCut == 3 ? (boring && boreCircles.Turned >= Mathf.PI ? (float)boreCircles.Steadiness : -1f)
            : carving ? (float)CraftWoodwork.StrokeSteady(carvePath, tolerance, hands.Unsteadiness) : lastStraight;
        if (knifeObject != null)
        {
            knifeObject.gameObject.SetActive(carveCut != 3);
            float off = 0f;
            if (carving)
            {
                CraftWoodwork.StrokeWander(carvePath, out double last);
                off = Mathf.Clamp((float)last / tolerance, -1.2f, 1.2f) * across * 0.5f; // up on the screen: the far side
            }
            knifeObject.localPosition = new Vector3((0.5f - along) * pieceLength, top + (held ? 0.004f : 0.03f), -off);
            knifeObject.localRotation = Quaternion.Euler(0f, carveCut == 1 ? 90f : 0f, held ? -18f : -35f);
        }
        if (bitObject != null)
        {
            bitObject.gameObject.SetActive(carveCut == 3 && bench);
            Vector3 outward = boreEnd == 1 ? Vector3.left : Vector3.right; // the first end on the view's left (+x)
            bitObject.localPosition = new Vector3((boreEnd == 1 ? -0.5f : 0.5f) * pieceLength, PieceTop(p) * 0.5f, 0f);
            bitObject.localRotation = Quaternion.LookRotation(outward, Vector3.up) * Quaternion.Euler(0f, 0f, bitSpin);
        }
        if (pieceObject != null)
        {
            pieceObject.gameObject.SetActive(bench);
            pieceObject.localPosition = new Vector3(0f, top * 0.5f, 0f);
            pieceObject.localRotation = Quaternion.LookRotation(Vector3.up, -Vector3.right) * Quaternion.Euler(0f, carveCut == 1 ? 90f : 0f, 0f);
            if (ghostWidth != null) ghostWidth.gameObject.SetActive(carveCut != 1);
            if (ghostThick != null) ghostThick.gameObject.SetActive(carveCut == 1);
        }
        for (int i = chips.Count - 1; i >= 0; i--)
        {
            (Transform t, Vector3 v, float life) = chips[i];
            life -= dt;
            if (t == null || life <= 0f) { Drop(t); chips.RemoveAt(i); continue; }
            v += Vector3.down * 2f * dt;
            t.localPosition += v * dt;
            t.localRotation *= Quaternion.Euler(400f * dt, 250f * dt, 0f);
            chips[i] = (t, v, life);
        }
        if (carveCut == 3)
            meter?.Set("Turn", boring ? (float)(boreCircles.Turns - Math.Floor(boreCircles.Turns)) : 0f, UITheme.Gold);
        else
        {
            // How deep the stroke bites (mm): the one under way, else a half-second stroke at this setting - red past what splits it.
            const float scale = (float)CraftWoodwork.CarveSplitDepth * 1.5f;
            float bite = BiteDepth(Knife, carving ? Time.unscaledTime - carveStart : 0.5f);
            meter?.Set("Bite", bite / scale, bite > CraftWoodwork.CarveSplitDepth ? UITheme.Bad : UITheme.Gold,
                ((float)CraftWoodwork.CarveSplitDepth / scale, 1f, new Color(0.65f, 0.22f, 0.18f, 1f)));
        }
        // How straight the stroke under way is (else the last one), or how evenly the bit is turned: red where the knife or
        // the bit tears the grain and splits it, green where it cuts clean - the very value the stroke or the turn is sent with.
        meter2?.Set(carveCut == 3 ? "Steady" : "Straight", Mathf.Max(0f, straight),
            straight < (float)CraftWoodwork.CarveSplitSteady ? UITheme.Bad : straight >= (float)CraftWoodwork.CleanStroke ? UITheme.Good : UITheme.Gold,
            (0f, (float)CraftWoodwork.CarveSplitSteady, new Color(0.65f, 0.22f, 0.18f, 1f)), ((float)CraftWoodwork.CleanStroke, 1f, new Color(0.3f, 0.75f, 0.4f, 1f)));
        hudTitle.text = $"Carving · {CutNames[carveCut]} · the knife {Knife * 100:0}%";
        string wander = straight >= 0f && straight < (float)CraftWoodwork.CarveSplitSteady && (carving || boring)
            ? (boring ? " <color=#FF9A80>Uneven circles: the bit tears the grain - turn it round and round, evenly.</color>"
                : " <color=#FF9A80>The hand wanders off the line: it will tear and split - keep it level.</color>") : "";
        hudFeel.text = !bench ? $"It is not on the bench: it is {CraftStationUI.LocationText(p)}." : (carveCut == 3 ? SlotWords(p) : TargetWords(p, along)) + wander;
    }

    /// <summary>How far (a share of the screen's height) a knife stroke may stray from its own line before it is no steady stroke at all (<see cref="CraftWoodwork.StrokeSteady"/>).</summary>
    private const float StraightTolerance = 0.04f;

    /// <summary>The knife's stroke as far as it went: sent to the piece it was drawn on (letting go, choosing another cut, putting the knife down).</summary>
    private void FinishKnife()
    {
        if (!carving)
            return;
        carving = false;
        CraftPiece p = Piece;
        if (p == null || p.location != CraftLocation.Air || Mathf.Abs(carveTo - carveFrom) <= 0.02f)
            return; // a dab, not a stroke
        int a = Mathf.Clamp(p.SegmentAt(Mathf.Min(carveFrom, carveTo)), 0, p.segments.Count - 1), b = Mathf.Clamp(p.SegmentAt(Mathf.Max(carveFrom, carveTo)), 0, p.segments.Count - 1);
        // Steady = straight: how far the hand strayed from the stroke's own line (either side of it: a stroke along the
        // blank however it lies on the screen is steady), and the hands' tremble. A slow stroke bites deeper than a flick;
        // a tired arm presses less; now and then the knife skids and digs in.
        float steady = (float)CraftWoodwork.StrokeSteady(carvePath, Mathf.Max(1f, Screen.height * StraightTolerance), hands.Unsteadiness);
        carvePath.Clear();
        float depth = BiteDepth(Knife, Time.unscaledTime - carveStart);
        bool slipped = hands.Slips(23);
        if (slipped) { steady *= 0.25f; depth *= 1.4f; shake = 0.4f; }
        lastStraight = steady; // what the meter shows of it, as it went
        hands.Move(Knife * 0.4f);
        var cut = carveCut == 0 ? CraftSurface.CarveCut.Width : carveCut == 1 ? CraftSurface.CarveCut.Thickness : CraftSurface.CarveCut.Round;
        string id = p.id;
        double splitBefore = p.wood.split;
        float bite = depth;
        Send(new CraftRequest(CraftRequestKind.Carve, id) { i0 = a, i1 = b, i2 = (int)cut, a = depth, b = steady, d = 1f }, (ok, why) =>
        {
            if (!ok)
                return;
            CraftPiece q = Ws?.Piece(id);
            bool split = q != null && q.wood.split > splitBefore + 1e-4;
            Say(slipped ? "The knife slipped and dug in: your arm is tired." + (split ? " It split along the grain." : "")
                : split ? (bite > CraftWoodwork.CarveSplitDepth ? $"Too deep a cut ({bite:0.0} mm): it split along the grain." : "A shaky stroke: the knife tore along the grain and split it.")
                : bite > CraftWoodwork.CarveSplitDepth * 0.8 ? $"A deep cut ({bite:0.0} mm), near what splits it: ease the knife."
                : steady >= (float)CraftWoodwork.CleanStroke ? $"A clean, steady shaving ({bite:0.0} mm)." : $"A wobbly cut ({bite:0.0} mm): it leaves the surface rough.", split || slipped);
        });
    }

    /// <summary>One turn of the bit: the stretches from the end it goes in at, a stretch deeper than it has gone - past the turns still on their way (<see cref="CraftWoodwork.BoreSpan"/>).</summary>
    private void BoreTurn(CraftPiece p)
    {
        if (!CraftWoodwork.BoreSpan(p, boreEnd, out int i0, out int i1, boreAhead))
            return;
        hands.Move(0.2);
        string id = p.id;
        int end = boreEnd;
        boreAhead++;
        Send(new CraftRequest(CraftRequestKind.Carve, id) { i0 = i0, i1 = i1, i2 = (int)CraftSurface.CarveCut.Bore, a = 1.2f * Knife + 0.3f, b = (float)boreCircles.Steadiness, d = 1f }, (ok, why) =>
        {
            boreAhead = Math.Max(0, boreAhead - 1);
            if (!ok)
                return;
            Chips(end == 1 ? 1f : 0f, 4);
            CraftPiece q = Ws?.Piece(id);
            if (q != null && CraftWoodwork.TangSlot(Ws, q, out double need, out bool measured))
            {
                double hole = CraftAssembly.Eye(q);
                Say(hole + 0.5 >= need ? $"The slot takes the {(measured ? "blade's tang" : "tang")} now ({hole:0} of {need:0} mm): bore no more." : $"The slot is {hole:0} mm; the tang needs {need:0} mm.");
            }
        });
    }

    /// <summary>What the design wants under the knife: its shape there (or its round size), and the length - against what the blank has.</summary>
    private string TargetWords(CraftPiece p, float along)
    {
        MetalSegment s = p.segments[Mathf.Clamp(p.SegmentAt(along), 0, p.segments.Count - 1)];
        string has = $"{s.width:0} × {s.thickness:0} mm";
        CraftShapeTarget shape = CraftArt.TargetShape(station.Rules, p, out float length);
        if (shape != null)
        {
            float t = length > 0f ? along * (float)p.Length / length : along;
            string len = $"{shape.lengthMin:0}-{shape.lengthMax:0} mm long (it is {p.Length:0})";
            if (t > 1f) return $"Past the end of the design here: it wants {len} - saw the rest off in the window.";
            shape.At(t, out float w, out float th);
            return $"Here it wants {w:0} × {th:0} mm (it is {has}); {len}.";
        }
        CraftComponentSpec spec = !string.IsNullOrEmpty(p.intentDesign) ? station.Rules.Design(p.intentDesign)?.Component(p.intentRole) : null;
        if (spec != null && spec.kind == CraftPieceKind.Wood)
            return $"It wants {spec.wood.diameterMin:0}-{spec.wood.diameterMax:0} mm thick and {spec.wood.lengthMin:0}-{spec.wood.lengthMax:0} mm long (here it is {has}, {p.Length:0} mm long).";
        return $"Here it is {has}, {p.Length:0} mm long. Choose what it is for in the window to see what it wants.";
    }

    /// <summary>The slot against the tang it takes (<see cref="CraftWoodwork.TangSlot"/>).</summary>
    private string SlotWords(CraftPiece p)
    {
        if (!CraftWoodwork.TangSlot(Ws, p, out double need, out bool measured))
            return string.IsNullOrEmpty(p.intentDesign) ? "Choose what it is for in the window first: the slot is the size of the tang it goes on." : "This part goes on no tang: there is no slot to bore.";
        double hole = CraftAssembly.Eye(p);
        return $"The slot: {hole:0} of the {need:0} mm the {(measured ? "blade's tang" : "tang")} needs. The bit stands at the end nearer the hand: hold {StationControls.K(StationControl.Work)} and circle the hand, round and even - each circle turns it a stretch deeper.";
    }

    private void Chips(float along, int count)
    {
        if (root == null) return;
        CraftPiece p = Piece;
        CraftWoodDefinition wd = p != null ? station.Rules.Wood(p.wood.wood) : null;
        Color c = wd != null ? Color.Lerp(wd.color, Color.white, 0.25f) : new Color(0.75f, 0.6f, 0.4f);
        float top = carveCut == 1 ? pieceWidth : PieceTop(p);
        for (int i = 0; i < count && chips.Count < 60; i++)
        {
            Transform t = Flake("Shaving", new Vector3((0.5f - along) * pieceLength, top + 0.01f, 0.01f), new Vector3(0.008f, 0.001f, 0.004f), c);
            chips.Add((t, new Vector3(UnityEngine.Random.Range(-0.15f, 0.15f), UnityEngine.Random.Range(0.1f, 0.35f), UnityEngine.Random.Range(0.05f, 0.25f)), 0.8f));
        }
    }

    /// <summary>A shaving or a crumb of dirt: one that has fallen before, taken up again, or a new one.</summary>
    private Transform Flake(string name, Vector3 at, Vector3 size, Color c)
    {
        Transform t = null;
        while (t == null && chipPool.Count > 0) t = chipPool.Pop();
        if (t == null)
            return Prim(PrimitiveType.Cube, name, at, size, c, false);
        t.name = name;
        t.localPosition = at;
        t.localRotation = Quaternion.identity;
        t.localScale = size;
        Renderer r = t.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = CraftModels.Plain(c, false, 0.2f);
        t.gameObject.SetActive(true);
        return t;
    }

    /// <summary>A shaving or a crumb that has fallen: put by for the next.</summary>
    private void Drop(Transform t)
    {
        if (t == null) return;
        t.gameObject.SetActive(false);
        chipPool.Push(t);
    }

    // ------------------------------------------------------------------ the ore bench
    private void UpdateBrush(float dt, Vector2 pointer)
    {
        CraftPiece p = Piece;
        if (p == null || p.kind != CraftPieceKind.Ore || p.ore.fragments.Count == 0)
        {
            End(true);
            return;
        }
        if (!OnTheBench(p, out string off))
        {
            rock.NewGoingOver(); // it would only be refused: the lump is not on the bench any more
            brushSeconds = 0f;
            Say(off, true);
            End(true);
            return;
        }
        // The lump changed under the stone (a blow, a stone picked out): the stones are looked at again - what was scrubbed
        // on the old layout would be refused as aimed at a stone that moved.
        if (CraftOres.Layout(p) != brushLayout || rock.Fragment < 0 || rock.Fragment >= p.ore.fragments.Count)
            ShowStone(Mathf.Clamp(rock.Fragment, 0, p.ore.fragments.Count - 1));
        // The brush: Next Tool. The stone: the number keys, the previous / next choice.
        if (StationControls.Pressed(StationControl.NextTool)) SetBrush((CraftOreBrush)(((int)oreBrush + 1) % BrushNames.Length));
        int stone = PickedChoice(p.ore.fragments.Count, rock.Fragment);
        if (stone >= 0 && stone != rock.Fragment)
        {
            FlushBrush();
            ShowStone(stone);
            hintTimer = 0f;
        }
        // Turning it in the hand: hold Quick and move; Less / More (the wheel) spin it; Turn Over rolls it upside down.
        Vector2 delta = pointer - lastPointer;
        lastPointer = pointer;
        bool turning = StationControls.Held(StationControl.Quick);
        if (turning && delta.sqrMagnitude > 0.01f)
            rockTurn = Quaternion.AngleAxis(-delta.x * 0.45f, Vector3.up) * Quaternion.AngleAxis(-delta.y * 0.45f, Vector3.right) * rockTurn; // its front follows the hand (the view's right is -x)
        int adjust = StationControls.AdjustStep;
        if (adjust != 0) rockTurn = Quaternion.AngleAxis(adjust * 20f, Vector3.up) * rockTurn;
        if (StationControls.Pressed(StationControl.TurnOver)) rockTurn = Quaternion.AngleAxis(180f, Vector3.right) * rockTurn;
        rockShown = Quaternion.Slerp(rockShown, rockTurn, 1f - Mathf.Exp(-dt * 10f));
        if (rockObject != null)
        {
            rockObject.localRotation = rockShown;
            rockObject.localPosition = new Vector3(0f, rock.Radius * 0.8f, 0f);
        }
        // The brush on the stone, where the pointer meets it.
        bool on = false;
        Vector3 from = Vector3.zero, dir = Vector3.forward, hit = Vector3.zero, normal = Vector3.up;
        if (cam != null && rockObject != null)
        {
            Ray ray = cam.ScreenPointToRay(pointer);
            from = rockObject.InverseTransformPoint(ray.origin);
            dir = rockObject.InverseTransformDirection(ray.direction);
            on = rock.Hit(from, dir, Reach, out hit, out normal);
        }
        bool scrubbing = held && on && !turning;
        // How briskly the hand scrubs: its speed over the last moment (the same at any frame rate), half the screen's
        // height a second at the most that counts.
        scrubHand.Add(Time.unscaledTime, pointer.x, pointer.y);
        float move = Mathf.Clamp01((float)scrubHand.Speed / Mathf.Max(1f, Screen.height * 0.5f));
        CraftOreDefinition o = station.Rules.Ore(p.ore.ore);
        if (scrubbing && move > 0.05f)
        {
            brushSeconds += dt;
            // Back and forth: the bristles go over what is under them - no faster than a brush can go over a stone (the
            // station counts no more), a tired arm scrubbing less hard.
            if (rock.GoingOver(oreBrush) < CraftRequestGuard.BrushCoverPerSecond * brushSeconds)
                rock.Brush(from, dir, BrushReach, 4f * dt * move * (0.45f + 0.55f * (float)hands.Strength), oreBrush);
            scrubPhase += dt * 30f;
            if ((brushTired += dt) >= 0.6f) { brushTired = 0f; hands.Move(oreBrush == CraftOreBrush.Wire ? 0.35 : 0.2); }
            dustClock += dt;
            while (dustClock >= 0.06f)
            {
                dustClock -= 0.06f;
                if (rock.Dirty || rock.Rinded) Dust(rockObject.localRotation * hit + rockObject.localPosition, rock.Dirty ? DirtDust(o) : new Color(0.55f, 0.52f, 0.48f));
            }
        }
        if (!held && wasHeld) FlushBrush(); // the hand lifted: that going-over goes
        if (brushObject != null && rockObject != null)
        {
            brushObject.gameObject.SetActive(true);
            if (on)
            {
                Vector3 at = rockObject.localRotation * (hit + normal * 0.012f) + rockObject.localPosition;
                Vector3 n = rockObject.localRotation * normal;
                brushObject.localPosition = at + (scrubbing ? Vector3.Cross(n, Vector3.up).normalized * Mathf.Sin(scrubPhase) * 0.006f : Vector3.zero);
                brushObject.localRotation = Quaternion.LookRotation(-n, Vector3.up) * Quaternion.Euler(0f, 0f, scrubbing ? Mathf.Sin(scrubPhase) * 8f : 0f);
            }
            else if (cam != null && root != null)
            {
                // Off the stone, the hand still shows: the brush hovers where the pointer is, level with the stone's middle
                // and facing it, so the stone is found again without a cursor (or with a gamepad's pointer).
                Ray ray = cam.ScreenPointToRay(pointer);
                var facing = new Plane(-cam.transform.forward, rockObject.position);
                if (facing.Raycast(ray, out float d))
                {
                    Vector3 at = root.InverseTransformPoint(ray.GetPoint(d));
                    Vector3 toStone = rockObject.localPosition - at;
                    brushObject.localPosition = at;
                    if (toStone.sqrMagnitude > 1e-6f) brushObject.localRotation = Quaternion.LookRotation(toStone, Vector3.up);
                }
            }
        }
        StepDust(dt);
        OreFragment f = p.ore.fragments[Mathf.Clamp(rock.Fragment, 0, p.ore.fragments.Count - 1)];
        // Clean: the station's own dirt share - the stone as it was put on the bench, with what each brush has gone over of it
        // since (the going-overs not yet answered too) taken off as the station takes it, the stone lighter by all it lost -
        // clean enough to smelt at 5 % dirt or less, the green.
        float dirt = (float)ShownDirt(o);
        meter?.Set("Clean", 1f - Mathf.Clamp01(dirt / 0.2f), UITheme.Good, (0.75f, 1f, new Color(0.3f, 0.75f, 0.4f, 1f)));
        meter2?.Set("Gone over", (float)rock.Covered(oreBrush), new Color(0.75f, 0.65f, 0.45f, 1f)); // by the brush in hand: at the end it takes no more
        hudTitle.text = $"Ore bench · {BrushNames[(int)oreBrush]} · stone {rock.Fragment + 1} of {p.ore.fragments.Count} ({f.size:0.#} cm)";
        hudFeel.text = StoneWords(o, dirt);
    }

    /// <summary>
    /// The stone's dirt as a share of what is left of it, as the station will have it once the brushes' going-overs are in:
    /// <see cref="CraftOres.BrushTo"/> on the stone as it was put on the bench, each brush as far over it as the hand has
    /// taken it (<see cref="CraftOreRock.Covered"/>).
    /// </summary>
    private double ShownDirt(CraftOreDefinition o)
    {
        if (stoneAtBench == null)
            return 0;
        OreFragment f = stoneAtBench.Clone();
        for (int b = 0; b < BrushNames.Length; b++)
            CraftOres.BrushTo(f, o, (CraftOreBrush)b, rock.Covered((CraftOreBrush)b));
        return f.dirt;
    }

    /// <summary>How far round the pointer the brush reaches over the stone (m): finding the stone under it.</summary>
    private const float Reach = 0.022f;

    /// <summary>
    /// How much of the stone as drawn the brush covers in one place: its own reach on a stone of the stone's real size - a
    /// pebble drawn larger than it is is covered in a stroke or two, a cobble takes many (the window's rule too: bigger
    /// stones take longer) - at most all the side facing it.
    /// </summary>
    private float BrushReach => Mathf.Min(Reach * rock.Magnified, rock.Radius * 2.5f);

    private void SetBrush(CraftOreBrush b)
    {
        if (b == oreBrush) return;
        FlushBrush(); // what the last brush went over goes as its own going-over
        oreBrush = b;
        BuildBrush();
        hintTimer = 0f;
    }

    /// <summary>Puts fragment <paramref name="index"/> of the lump on the bench, as the workshop has it (what each brush has already gone over too).</summary>
    private void ShowStone(int index)
    {
        CraftPiece p = Piece;
        brushSeconds = 0f;
        if (p == null || p.ore.fragments.Count == 0) return;
        index = Mathf.Clamp(index, 0, p.ore.fragments.Count - 1);
        rock.Build(station.Rules, p, index);
        stoneAtBench = p.ore.fragments[index].Clone();
        brushLayout = CraftOres.Layout(p);
        if (rockFilter != null) rockFilter.sharedMesh = rock.Mesh;
    }

    /// <summary>The brush in the hand: a wooden back, a handle, and bristles (horsehair, hog bristle or iron wire).</summary>
    private void BuildBrush()
    {
        if (root == null) return;
        if (brushObject != null) Destroy(brushObject.gameObject);
        brushObject = new GameObject("Brush").transform;
        brushObject.SetParent(root, false);
        Color bristle = oreBrush == CraftOreBrush.Soft ? new Color(0.78f, 0.7f, 0.55f) : oreBrush == CraftOreBrush.Stiff ? new Color(0.25f, 0.2f, 0.15f) : new Color(0.6f, 0.6f, 0.62f);
        Prim(PrimitiveType.Cube, "Back", new Vector3(0f, 0f, -0.016f), new Vector3(0.05f, 0.022f, 0.012f), new Color(0.45f, 0.3f, 0.17f), false, brushObject);
        Prim(PrimitiveType.Cube, "Bristles", new Vector3(0f, 0f, -0.004f), new Vector3(0.046f, 0.018f, oreBrush == CraftOreBrush.Soft ? 0.016f : 0.012f), bristle, oreBrush == CraftOreBrush.Wire, brushObject);
        Prim(PrimitiveType.Cube, "Handle", new Vector3(0.045f, 0f, -0.03f), new Vector3(0.06f, 0.012f, 0.008f), new Color(0.45f, 0.3f, 0.17f), false, brushObject).localRotation = Quaternion.Euler(0f, 25f, 0f);
    }

    /// <summary>
    /// The going-over just ended (the hand lifted, another brush or stone, the brush put down) goes to the workshop as one
    /// request: how much more of the stone the brush went over, counted as the workshop counts it (<see cref="CraftBrushCover"/>),
    /// the seconds it took, and the layout of the stones it was aimed at.
    /// </summary>
    private void FlushBrush()
    {
        CraftPiece p = Piece;
        float cover = (float)rock.GoingOver(oreBrush), seconds = brushSeconds;
        int stone = rock.Fragment;
        brushSeconds = 0f;
        rock.NewGoingOver(); // out of the count before it goes: never sent twice
        if (p == null || stone < 0 || stone >= p.ore.fragments.Count || cover <= 0.002f || CraftOres.Layout(p) != brushLayout)
            return;
        string id = p.id;
        CraftOreBrush brush = oreBrush;
        OreFragment f = p.ore.fragments[stone];
        double dirtBefore = f.mass * f.dirt;
        CraftOreDefinition o = station.Rules.Ore(p.ore.ore);
        bool clay = o != null && o.gangue == CraftGangue.Clay;
        Send(new CraftRequest(CraftRequestKind.BrushOre, id) { i0 = stone, i1 = (int)brush, a = Mathf.Min(1f, cover), b = Mathf.Max(0.05f, seconds), i2 = brushLayout, d = 1f }, (ok, why) =>
        {
            CraftPiece q = Ws?.Piece(id);
            if (!ok || !string.IsNullOrEmpty(why))
            {
                // Refused, or granted only in part: the stone shows again what the workshop has (what was scrubbed did not
                // all count).
                if (why != CraftAnswerWaits.NoAnswer && q != null && setup.piece == id && Active && mode == CraftHandsOnMode.Brush && rock.Fragment == stone && stone < q.ore.fragments.Count)
                    ShowStone(stone);
                if (!ok)
                    return;
            }
            if (q == null || stone >= q.ore.fragments.Count)
                return;
            OreFragment g = q.ore.fragments[stone];
            double gone = dirtBefore > 1e-9 ? CraftMath.Clamp01(1 - g.mass * g.dirt / dirtBefore) : 0;
            Say($"{gone * 100:0}% of its dirt came off" + (brush == CraftOreBrush.Wire ? ", and the weathered rind with a little ore." : ".")
                + (clay && brush == CraftOreBrush.Soft && g.Brushed(brush) >= 0.999 ? " The soft brush has done what it can with caked clay: take the stiff one." : ""));
        });
    }

    private static Color DirtDust(CraftOreDefinition o) => o != null && o.gangue == CraftGangue.Clay ? new Color(0.62f, 0.52f, 0.4f) : new Color(0.42f, 0.33f, 0.22f);

    /// <summary>Crumbs of dirt and rind falling off under the brush.</summary>
    private void Dust(Vector3 at, Color c)
    {
        if (chips.Count >= 50) return;
        Transform t = Flake("Dust", at, Vector3.one * UnityEngine.Random.Range(0.0015f, 0.003f), c);
        chips.Add((t, new Vector3(UnityEngine.Random.Range(-0.08f, 0.08f), UnityEngine.Random.Range(0f, 0.08f), UnityEngine.Random.Range(-0.08f, 0.08f)), 0.7f));
    }

    private void StepDust(float dt)
    {
        for (int i = chips.Count - 1; i >= 0; i--)
        {
            (Transform t, Vector3 v, float life) = chips[i];
            life -= dt;
            if (t == null || life <= 0f || t.localPosition.y < -0.005f) { Drop(t); chips.RemoveAt(i); continue; }
            v += Vector3.down * 1.5f * dt;
            t.localPosition += v * dt;
            chips[i] = (t, v, life);
        }
    }

    /// <summary>What the stone in the hand looks like now: its dirt by the workshop's own thresholds (clean below 5 %), its rind, and what the brush in hand can still do.</summary>
    private string StoneWords(CraftOreDefinition o, float dirt)
    {
        bool clay = o != null && o.gangue == CraftGangue.Clay;
        string looks = dirt <= 0.05f ? "Clean of dirt" : dirt > 0.15f ? (clay ? "Caked with clay" : "Crusted with dirt") : "Patches of dirt left";
        float r = rock.RindLeft;
        string rind = !rock.Rinded ? "" : r > 0.6f ? "; a dull weathered rind covers part of it (the wire brush takes it)" : "; some of the rind is still on it";
        string done = rock.Covered(oreBrush) >= 0.999f ? $" The {BrushNames[(int)oreBrush]} has been all over it: it takes no more off." : "";
        string tip = dirt > 0.05f && clay && oreBrush == CraftOreBrush.Soft ? " The soft brush barely shifts caked clay: take the stiff one." : "";
        return $"{looks}{rind}.{done}{tip}";
    }

    // ------------------------------------------------------------------ the crucible
    private double MeltMass => setup.fromFurnace ? Ws.Furnace.PoolMass : Ws.Crucible.Mass;

    private void UpdatePour(float dt, Vector2 pointer)
    {
        // A press starts a pour - checked first: the melt can cool or freeze while the hand waits, and the reason is told
        // before the hands start, not after.
        if (held && !wasHeld && pour == null)
            StartPour();
        if (pour != null && !held) tippingBack = true; // let go: it tips back - the metal already in the air still lands
        pourClock += dt;
        if (pour == null || !tippingBack)
        {
            // The hand carries the crucible: the pointer left and right brings its lip over the cup; the hands drift a little
            // (more when tired or unpractised), so the aim has to be held. Let go, it tips back with its lip still over the
            // cup: the dying stream falls where it fell.
            pourAim = Mathf.Clamp((pointer.x - Screen.width * 0.5f) / (Screen.width * 0.3f) * 0.12f, -0.06f, 0.2f);
            float wander = pour != null ? (float)hands.Tremble((int)(pourClock * 3f) + 51) * 0.8f : 0f;
            pourDrift = Mathf.Lerp(pourDrift, wander, Mathf.Min(1f, dt * 3f));
            if (pour != null) pourTilt = Mathf.Clamp01((pointer.y - pressAt.y) / (Screen.height * 0.3f));
            // How far out the stream reaches: as far as the tilt throws it, then as it slows when eased back; before the
            // pour, where one in the middle of the green band would land.
            pourReach = pour != null ? Mathf.Max((float)pour.Rate, (float)CraftPourControl.AimOf(pourTilt)) : (float)CraftSmelting.GoodPour;
        }
        else
            pourTilt = 0f;
        if (crucibleObject != null)
            crucibleObject.localPosition = new Vector3(PourBaseX + pourAim + pourDrift, 0.2f, 0f);
        pourLandX = PourBaseX + pourAim + pourDrift + 0.05f + 0.07f * pourReach;
        bool onTarget = Mathf.Abs(pourLandX - pourMouthX) <= CupRadius;
        // The ring: where the stream lands - and before it runs, where one in the green band would land as it is carried now.
        float ringX = pour != null && pour.Rate <= CraftPourControl.Flowing && !tippingBack
            ? PourBaseX + pourAim + pourDrift + 0.05f + 0.07f * Mathf.Max(pourReach, (float)CraftSmelting.GoodPour) : pourLandX;
        bool ringIn = Mathf.Abs(ringX - pourMouthX) <= CupRadius;
        if (aimOn != null)
        {
            aimOn.localPosition = new Vector3(ringX, 0.0225f, 0f);
            aimOff.localPosition = new Vector3(ringX, 0.0225f, 0f);
            aimOn.gameObject.SetActive(ringIn);
            aimOff.gameObject.SetActive(!ringIn);
        }
        if (pour != null)
        {
            // Up tips it; let go and it tips back - the metal already in the air still lands as the stream dies away.
            pour.Tick(dt, pourTilt, hands, fillSeconds, onTarget);
            shownFill = (float)pour.Poured;
            // Done once the stream has stopped: tipped back, or the mould full and the crucible upright, or nothing left.
            if (pour.Settled(tippingBack))
                FinishPour();
        }
        else
            pourTilt = Mathf.MoveTowards(pourTilt, 0f, dt * 2f);
        float rate = pour != null ? (float)pour.Rate : 0f;
        if (crucibleObject != null)
            crucibleObject.localRotation = Quaternion.Euler(0f, 0f, -pourTilt * 110f);
        // The stream: from the crucible's lip on the side it tips to, arcing down to where it lands (in the cup, or on the
        // sand) - two lengths, as the window draws it; none once it has died away.
        if (streamUpper != null && streamLower != null && crucibleObject != null && pourSpace != null)
        {
            bool running = rate > CraftPourControl.Flowing;
            streamUpper.gameObject.SetActive(running);
            streamLower.gameObject.SetActive(running);
            if (running)
            {
                Vector3 lip = pourSpace.InverseTransformPoint(crucibleObject.TransformPoint(new Vector3(-0.01f, 0.06f, 0f)));
                var land = new Vector3(pourLandX, 0.0225f, 0f);
                var mid = new Vector3(lip.x + (land.x - lip.x) * 0.7f, lip.y - (lip.y - land.y) * 0.25f, 0f);
                float width = 0.004f + 0.012f * rate;
                Stream(streamUpper, lip, mid, width);
                Stream(streamLower, mid, land, width * 0.85f);
            }
        }
        // The metal runs in at the mouth and creeps along the cavity to the far end (it stays there once cast).
        for (int i = 0; i < fillCells.Count; i++)
            fillCells[i].gameObject.SetActive(shownFill * fillCells.Count > i + 0.05f);
        double t = setup.fromFurnace ? Ws.Furnace.fire.temperature : Ws.Crucible.temperature;
        bool runningOver = pour != null && pour.Full && rate > CraftPourControl.Flowing;
        // The mould's meter: how full, and the red tick where the metal there is runs out.
        double have = pour != null ? pour.Supply : (pourNeed > 0 ? MeltMass / pourNeed : double.PositiveInfinity);
        float runsOut = have < 1 ? (float)have : -1f;
        // Full (green, the window's FILL); and the red tick where the metal there is runs out.
        Color full = new Color(0.3f, 0.75f, 0.4f, 1f);
        if (runsOut >= 0f) meter?.Set("Mould", shownFill, runningOver ? UITheme.Bad : UITheme.Gold, (0.97f, 1f, full), (Mathf.Max(0f, runsOut - 0.01f), Mathf.Min(1f, runsOut + 0.01f), new Color(0.9f, 0.3f, 0.22f, 1f)));
        else meter?.Set("Mould", shownFill, runningOver ? UITheme.Bad : UITheme.Gold, (0.97f, 1f, full));
        // The stream's pace as the window's FLOW shows it, by the verdict's own bounds: too slow it freezes short (cold), in
        // the green it fills sound, too fast it churns and traps air (red).
        meter2?.Set("Flow", rate, rate > CraftSmelting.FastPour ? UITheme.Bad : new Color(1f, 0.75f, 0.35f, 1f),
            (0f, (float)CraftSmelting.SlowPour, new Color(0.25f, 0.35f, 0.5f, 1f)), ((float)CraftSmelting.SlowPour, (float)CraftSmelting.FastPour, full),
            ((float)CraftSmelting.FastPour, 1f, new Color(0.55f, 0.2f, 0.15f, 1f)));
        hudTitle.text = $"Pouring · {station.Rules.Mold(setup.mold)?.Name ?? setup.mold}";
        // Before a pour: what stops it, if anything (empty, frozen, an ore still in it - the workshop's own check), told before
        // the hands start rather than after a press.
        string cannot = pour == null && !Ws.CanPour(setup.fromFurnace, out string why) ? why : null;
        hudFeel.text = pour == null
            ? !string.IsNullOrEmpty(cannot) ? $"<color=#FF9A80>{cannot}</color>"
            : $"The melt {MeltWords(t)}. Move the hand sideways until the ring sits in the clay cup (green), then hold {StationControls.K(StationControl.Work)} and raise the hand to tip it."
            : runningOver ? "<color=#FF9A80>Full: tip it back now - it is running over!</color>"
            : pour.Empty && rate > CraftPourControl.Flowing ? "The crucible is empty: tip it back."
            : rate > CraftPourControl.Flowing && !onTarget ? "Missing the cup: it splashes on the sand! Bring the lip back over it."
            : rate > CraftSmelting.FastPour ? "Too fast: it churns and traps air - ease it back into the green."
            : pour.Interruptions > 0 ? $"The stream broke {pour.Interruptions} time{(pour.Interruptions == 1 ? "" : "s")}: a cold shut."
            : rate > CraftPourControl.Flowing ? $"A steady stream runs into the cup{(rate < CraftSmelting.SlowPour ? " - a little slow: it may freeze short" : "")}. Tip it back as soon as it is full."
            : tippingBack ? "Tipping back..." : "Tip it further.";
    }

    /// <summary>The melt's heat in words ("glows yellow-white", in °C with a thermometer), or that it is dark - never an empty colour.</summary>
    private string MeltWords(double celsius)
    {
        string colour = CraftMetallurgy.HeatName(celsius, Ws.Variable("night") > 0.5);
        string words = string.IsNullOrEmpty(colour) || colour == "black heat" ? (celsius > 60 ? "is dark, too cool to glow" : "is cold") : $"glows {colour}";
        return station.Reveals || station.HasInstrument(CraftInstruments.Thermometer) ? $"{words} ({celsius:0} °C)" : words;
    }

    /// <summary>A length of the stream (a cylinder, two units tall) from <paramref name="a"/> to <paramref name="b"/>, <paramref name="width"/> across.</summary>
    private static void Stream(Transform t, Vector3 a, Vector3 b, float width)
    {
        Vector3 d = b - a;
        t.localPosition = (a + b) * 0.5f;
        t.localRotation = d.sqrMagnitude > 1e-8f ? Quaternion.FromToRotation(Vector3.up, d) : Quaternion.identity;
        t.localScale = new Vector3(width, d.magnitude * 0.5f, width);
    }

    /// <summary>A pour starts, if it can (the workshop's own check): as much as the crucible holds, filling as a mould of its size fills.</summary>
    private void StartPour()
    {
        CraftMold mold = station.Rules.Mold(setup.mold);
        if (mold == null)
        {
            Say("Choose a mould first.", true);
            return;
        }
        if (!Ws.CanPour(setup.fromFurnace, out string why))
        {
            Say(why, true);
            return;
        }
        if (Time.unscaledTime - lastPourAt < PourEvery)
        {
            Say("The metal is still running: a moment before the next mould.", true); // as the station would refuse it at the end
            return;
        }
        CraftAlloyProfile prof = setup.fromFurnace ? CraftSmelting.PoolProfile(station.Rules, Ws.Furnace) : CraftSmelting.MeltProfile(station.Rules, Ws.Crucible);
        pourNeed = CraftSmelting.MoldMass(station.Rules, mold, prof.Density);
        pour = new CraftPourControl { Supply = pourNeed > 1e-6 ? MeltMass / pourNeed : 0 };
        // A big mould takes longer to fill than a row of rivets.
        fillSeconds = (float)CraftPourControl.FillSeconds(pourNeed);
        tippingBack = false;
        pourClock = shownFill = 0f; // a fresh mould
    }

    /// <summary>The pour ends: what ran into the mould is cast, what missed it or ran over is lost on the floor - and what came of it is told.</summary>
    private void FinishPour()
    {
        CraftPourControl p = pour;
        pour = null;
        tippingBack = false;
        if (p == null || !p.Request(setup.mold, setup.fromFurnace, out CraftRequest r))
            return; // it never ran
        lastPourAt = Time.unscaledTime;
        double need = pourNeed;
        string mold = setup.mold;
        bool furnace = setup.fromFurnace;
        Send(r, (ok, why) => { if (ok) Say(Verdict(p, need, mold, furnace)); });
    }

    /// <summary>
    /// Inside the pour's answer: what came of it, in the workshop's own terms - the window's verdict
    /// (<see cref="CraftSmelting.PourVerdict"/>).
    /// </summary>
    private string Verdict(CraftPourControl p, double need, string moldId, bool fromFurnace)
    {
        CraftPiece cast = null;
        foreach (CraftEvent e in answerEvents)
            if (e.Kind == CraftEventKind.Poured || e.Kind == CraftEventKind.PieceCreated) cast = Ws?.Piece(e.Piece) ?? cast;
        return CraftSmelting.PourVerdict(station.Rules, cast, p, need, station.Rules.Mold(moldId), fromFurnace);
    }

    // ------------------------------------------------------------------ words
    private string HeatWords(CraftPiece p)
    {
        double t = p.MaxTemperature;
        string colour = CraftMetallurgy.HeatName(t, Ws.Variable("night") > 0.5);
        string words = string.IsNullOrEmpty(colour) || colour == "black heat" ? (t > 60 ? "Too hot to touch, but dark" : "It is cold") : CraftText.Capital("It glows " + colour);
        return station.Reveals || station.HasInstrument(CraftInstruments.Thermometer) ? $"{words} ({t:0} °C)" : words;
    }

    /// <summary>The lowest heat at which a hardened edge of <paramref name="p"/> starts to lose its temper on the wheel (infinite: nothing hardened to lose).</summary>
    private double TemperRuns(CraftPiece p)
    {
        double runs = double.PositiveInfinity;
        if (p.kind == CraftPieceKind.Metal)
            foreach (MetalSegment s in p.segments)
                runs = Math.Min(runs, CraftSurface.TemperRunsAt(station.Rules, s.edge));
        return runs;
    }

    /// <summary>How hot it is in the hand at the wheel, by the window's rule: a hardened edge's temper runs past its own heat; the wheel cannot hold it much past 250 °C.</summary>
    private string HandHeat(CraftPiece p, double runs)
    {
        double t = p.MaxTemperature;
        if (p.kind == CraftPieceKind.Metal && !double.IsInfinity(runs) && t > runs - 60)
            return $"<color=#FF8A70>Hot: its hardened edge is losing its temper (it goes blue and soft) - {CoolIt(p, "stop and let it cool", true)}.</color>";
        if (p.kind == CraftPieceKind.Metal && t > 250)
            return $"<color=#FFB070>Hot: the wheel cannot hold it much hotter - {CoolIt(p, "press lighter and let it cool", true)}.</color>";
        return t < 50 ? "Cool in the hand." : t < 120 ? "Warm in the hand." : "Hot in the hand.";
    }

    // ------------------------------------------------------------------ the work place
    private CraftTools PlaceTool =>
        mode == CraftHandsOnMode.Hammer ? CraftTools.Anvil : mode == CraftHandsOnMode.Bellows ? CraftTools.Forge
        : mode == CraftHandsOnMode.Grind ? CraftTools.Grinder : mode == CraftHandsOnMode.Sharpen ? CraftTools.Whetstone
        : mode == CraftHandsOnMode.Quench ? CraftTools.QuenchTank : mode == CraftHandsOnMode.Carve ? CraftTools.AssemblyBench
        : mode == CraftHandsOnMode.Brush ? CraftTools.OreBench : CraftTools.Crucible;

    /// <summary>
    /// The work place is built at the station's own point for it (<see cref="CraftStationVisuals.HandsOnPoint"/>: the
    /// anvil's, the wheel's, the tank's...) - the forge, the anvil and the tank without one in the row their effects use
    /// (the coals, the sparks, the steam), anything else in front of the station on the player's side, clear of all its
    /// colliders - and faces the side the player stands on (the other side when a wall stands close on theirs and that side
    /// has the room), where the view stands (drawn nearer when something stands in its way): x runs along the station as the
    /// player sees it (the view's left is +x), +z comes toward them, and its furniture stands on the floor. The station's own
    /// model of the place is hidden meanwhile (the hands bring theirs: the model set for it, else what of the station's own
    /// stands right there), and so is the player's own body.
    /// </summary>
    private void BuildPlace()
    {
        Vector3 front = Flat(visuals != null ? visuals.Front : transform.forward);
        Vector3 at;
        if (visuals == null || !visuals.HandsOnPoint(mode, out at))
            at = visuals != null && (mode == CraftHandsOnMode.Hammer || mode == CraftHandsOnMode.Bellows || mode == CraftHandsOnMode.Quench)
                ? visuals.PointFor(PlaceTool) : FreeSpot(front);
        Vector3 toPlayer = player != null ? Flat(player.transform.position - at) : front;
        // It faces the player's side, where the view stands - unless a wall (or the station itself) stands close on that side
        // and the other side has the room.
        Vector3 facing = Vector3.Dot(toPlayer, front) < 0f ? -front : front;
        Vector3 eye = at + Vector3.up * 0.3f, away = Vector3.up * 0.55f;
        float here = Room(eye, eye + facing * viewDistance + away), there = Room(eye, eye - facing * viewDistance + away);
        if (here < viewDistance * 0.9f && there > here + 0.15f)
            facing = -facing;
        var go = new GameObject("Craft Hands-On Place");
        root = go.transform;
        root.SetPositionAndRotation(at, Quaternion.LookRotation(facing, Vector3.up));
        floorY = Mathf.Min(0f, FloorHeight() - at.y);
        Color iron = new Color(0.2f, 0.2f, 0.22f), wood = new Color(0.36f, 0.24f, 0.14f), stone = new Color(0.45f, 0.43f, 0.4f);
        switch (mode)
        {
            case CraftHandsOnMode.Hammer:
            {
                Box("Anvil Face", new Vector3(0f, -0.04f, 0f), new Vector3(0.42f, 0.08f, 0.13f), iron, true);
                Box("Anvil Waist", new Vector3(0f, -0.14f, 0f), new Vector3(0.24f, 0.12f, 0.1f), iron, true);
                Box("Anvil Foot", new Vector3(0f, -0.24f, 0f), new Vector3(0.34f, 0.08f, 0.18f), iron, true);
                Transform horn = Prim(PrimitiveType.Capsule, "Horn", new Vector3(-0.27f, -0.04f, 0f), new Vector3(0.07f, 0.09f, 0.07f), iron, true);
                horn.localRotation = Quaternion.Euler(0f, 0f, 90f);
                Box("Hardy Hole", new Vector3(0.16f, 0.0005f, 0f), new Vector3(0.022f, 0.002f, 0.022f), new Color(0.03f, 0.03f, 0.03f), false);
                ToFloor("Stump", new Vector3(0f, -0.28f, 0f), new Vector2(0.4f, 0.4f), wood);
                hammerObject = new GameObject("Hammer").transform;
                hammerObject.SetParent(root, false);
                Transform head = Prim(PrimitiveType.Cube, "Head", Vector3.zero, new Vector3(0.035f, 0.035f, 0.1f), new Color(0.32f, 0.32f, 0.35f), true, hammerObject);
                head.localRotation = Quaternion.identity;
                Transform handle = Prim(PrimitiveType.Cylinder, "Handle", new Vector3(0f, 0.012f, 0.19f), new Vector3(0.018f, 0.16f, 0.018f), wood, false, hammerObject);
                handle.localRotation = Quaternion.Euler(90f, 0f, 0f);
                // The bevel a tilted blow will forge, shaded on the piece (shown while the hammer is up and tilted).
                bevelMark = Box("Bevel To Be", Vector3.zero, new Vector3(0.01f, 0.001f, 0.01f), new Color(1f, 0.7f, 0.25f), false);
                Material shade = AlchemyVesselParts.NewMaterial(new Color(1f, 0.7f, 0.25f, 0.55f), true, 0.2f);
                ownMaterials.Add(shade);
                Renderer shadeRenderer = bevelMark.GetComponent<Renderer>();
                if (shadeRenderer != null)
                {
                    shadeRenderer.sharedMaterial = shade;
                    shadeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                bevelMark.gameObject.SetActive(false);
                break;
            }
            case CraftHandsOnMode.Bellows:
            {
                // Beside the fire on the hearth (its board just clear of the hearth's top), the nozzle into the coals.
                Transform body = new GameObject("Bellows").transform;
                body.SetParent(root, false);
                body.localPosition = new Vector3(-0.55f, -0.0405f, 0.05f);
                Prim(PrimitiveType.Cube, "Bottom Board", Vector3.zero, new Vector3(0.36f, 0.015f, 0.22f), wood, false, body);
                bellowsTop = new GameObject("Top Board Hinge").transform;
                bellowsTop.SetParent(body, false);
                bellowsTop.localPosition = new Vector3(0.18f, 0f, 0f);
                Prim(PrimitiveType.Cube, "Top Board", new Vector3(-0.18f, 0f, 0f), new Vector3(0.36f, 0.015f, 0.22f), wood, false, bellowsTop);
                Prim(PrimitiveType.Cube, "Handle", new Vector3(-0.39f, 0f, 0f), new Vector3(0.08f, 0.012f, 0.03f), wood, false, bellowsTop);
                Transform nozzle = Prim(PrimitiveType.Cylinder, "Nozzle", new Vector3(0.3f, 0f, 0f), new Vector3(0.025f, 0.12f, 0.025f), iron, true, body);
                nozzle.localRotation = Quaternion.Euler(0f, 0f, 90f);
                break;
            }
            case CraftHandsOnMode.Grind:
                Bench("Bench", new Vector3(0f, -0.06f, 0.05f), new Vector3(0.5f, 0.06f, 0.3f), wood);
                // The wheel's frame: two posts from the bench up to its axle.
                Box("Wheel Post", new Vector3(-0.04f, 0.045f, -0.02f), new Vector3(0.02f, 0.15f, 0.03f), wood, false);
                Box("Wheel Post", new Vector3(0.04f, 0.045f, -0.02f), new Vector3(0.02f, 0.15f, 0.03f), wood, false);
                Prim(PrimitiveType.Cylinder, "Axle", new Vector3(0f, 0.12f, -0.02f), new Vector3(0.012f, 0.05f, 0.012f), iron, true).localRotation = Quaternion.Euler(0f, 0f, 90f);
                RebuildTool();
                break;
            case CraftHandsOnMode.Sharpen:
                Bench("Bench", new Vector3(0f, -0.02f, 0.03f), new Vector3(0.5f, 0.04f, 0.3f), wood);
                Box("Stone Block", new Vector3(0f, 0.015f, 0f), new Vector3(0.28f, 0.03f, 0.09f), wood, false);
                RebuildTool();
                break;
            case CraftHandsOnMode.Quench:
                BuildTank(iron);
                break;
            case CraftHandsOnMode.Pour:
            {
                // Seen as the window draws it - the crucible on the left, the mould on the right - so it is built mirrored
                // (the view's left is +x): the hand carries the crucible right, toward the mould, as the pointer moves right.
                pourSpace = new GameObject("Pour").transform;
                pourSpace.SetParent(root, false);
                pourSpace.localScale = new Vector3(-1f, 1f, 1f);
                Color sand = new Color(0.55f, 0.47f, 0.34f);
                Box("Sand", new Vector3(0.2f, -0.02f, 0f), new Vector3(0.34f, 0.04f, 0.18f), sand, false, pourSpace);
                ToFloor("Casting Bench", new Vector3(0.2f, -0.04f, 0f), new Vector2(0.36f, 0.2f), wood, pourSpace);
                float mouth = BuildMouldCavity(station.Rules.Mold(setup.mold));
                pourMouthX = mouth;
                pourAim = pourDrift = 0f;
                // The pouring cup: a clay funnel standing on the sprue - the mouth the stream must go into.
                Prim(PrimitiveType.Cylinder, "Pouring Cup", new Vector3(mouth, 0.011f, 0f), new Vector3(0.05f, 0.011f, 0.05f), new Color(0.78f, 0.64f, 0.46f), false, pourSpace);
                Prim(PrimitiveType.Cylinder, "Pouring Cup Mouth", new Vector3(mouth, 0.0222f, 0f), new Vector3(CupRadius * 2f, 0.0006f, CupRadius * 2f), new Color(0.05f, 0.04f, 0.03f), false, pourSpace);
                // Where the stream will land: amber off the cup, green in it.
                aimOff = Prim(PrimitiveType.Cylinder, "Pour Aim", new Vector3(mouth, 0.0225f, 0f), new Vector3(0.012f, 0.0008f, 0.012f), new Color(1f, 0.7f, 0.2f), false, pourSpace);
                aimOn = Prim(PrimitiveType.Cylinder, "Pour Aim (on target)", new Vector3(mouth, 0.0225f, 0f), new Vector3(0.012f, 0.0008f, 0.012f), new Color(0.4f, 1f, 0.5f), false, pourSpace);
                crucibleObject = new GameObject("Crucible Pivot").transform;
                crucibleObject.SetParent(pourSpace, false);
                crucibleObject.localPosition = new Vector3(PourBaseX, 0.2f, 0f);
                Prim(PrimitiveType.Cylinder, "Crucible", new Vector3(-0.06f, 0f, 0f), new Vector3(0.1f, 0.06f, 0.1f), stone, false, crucibleObject);
                Transform melt = Prim(PrimitiveType.Cylinder, "Melt", new Vector3(-0.06f, 0.05f, 0f), new Vector3(0.085f, 0.004f, 0.085f), Color.white, false, crucibleObject);
                SetGlow(melt, new Color(0.6f, 0.55f, 0.5f), setup.fromFurnace ? Ws.Furnace.fire.temperature : Ws.Crucible.temperature);
                Prim(PrimitiveType.Cube, "Tongs", new Vector3(-0.2f, 0.02f, 0f), new Vector3(0.22f, 0.012f, 0.012f), iron, true, crucibleObject);
                // The stream: two lengths from the lip down to where it lands, placed each frame.
                streamUpper = Prim(PrimitiveType.Cylinder, "Stream", Vector3.zero, new Vector3(0.01f, 0.01f, 0.01f), Color.white, false, pourSpace);
                streamLower = Prim(PrimitiveType.Cylinder, "Stream", Vector3.zero, new Vector3(0.01f, 0.01f, 0.01f), Color.white, false, pourSpace);
                SetGlow(streamUpper, new Color(0.6f, 0.55f, 0.5f), 1150);
                SetGlow(streamLower, new Color(0.6f, 0.55f, 0.5f), 1150);
                streamUpper.gameObject.SetActive(false);
                streamLower.gameObject.SetActive(false);
                // How much metal the mould takes of this melt (for the meter's tick before the first pour).
                CraftMold m = station.Rules.Mold(setup.mold);
                CraftAlloyProfile prof = setup.fromFurnace ? CraftSmelting.PoolProfile(station.Rules, Ws.Furnace) : CraftSmelting.MeltProfile(station.Rules, Ws.Crucible);
                pourNeed = m != null ? CraftSmelting.MoldMass(station.Rules, m, prof.Density) : 0;
                break;
            }
            case CraftHandsOnMode.Carve:
            {
                // The bench's top at 0, where the blank lies.
                Bench("Bench", new Vector3(0f, -0.02f, 0.03f), new Vector3(0.6f, 0.04f, 0.32f), wood);
                Box("Bench Stop", new Vector3(-0.24f, 0.015f, 0f), new Vector3(0.02f, 0.03f, 0.08f), wood, false);
                knifeObject = new GameObject("Carving Knife").transform;
                knifeObject.SetParent(root, false);
                Prim(PrimitiveType.Cube, "Blade", new Vector3(0.03f, 0f, 0f), new Vector3(0.07f, 0.003f, 0.016f), new Color(0.7f, 0.7f, 0.72f), true, knifeObject);
                Prim(PrimitiveType.Cylinder, "Handle", new Vector3(-0.03f, 0f, 0f), new Vector3(0.018f, 0.035f, 0.018f), new Color(0.3f, 0.2f, 0.12f), false, knifeObject).localRotation = Quaternion.Euler(0f, 0f, 90f);
                // The bit in its brace, for the slot: its point in the end grain, its shaft out along the blank (+z here),
                // the brace's crank round the shaft - it turns as the hand circles.
                bitObject = new GameObject("Brace And Bit").transform;
                bitObject.SetParent(root, false);
                Color steel = new Color(0.62f, 0.62f, 0.66f);
                Prim(PrimitiveType.Cylinder, "Bit", new Vector3(0f, 0f, 0.025f), new Vector3(0.006f, 0.035f, 0.006f), steel, true, bitObject).localRotation = Quaternion.Euler(90f, 0f, 0f);
                Prim(PrimitiveType.Cube, "Crank", new Vector3(0.02f, 0f, 0.06f), new Vector3(0.04f, 0.006f, 0.006f), steel, true, bitObject);
                Prim(PrimitiveType.Cylinder, "Grip", new Vector3(0.04f, 0f, 0.085f), new Vector3(0.014f, 0.025f, 0.014f), new Color(0.3f, 0.2f, 0.12f), false, bitObject).localRotation = Quaternion.Euler(90f, 0f, 0f);
                Prim(PrimitiveType.Cube, "Crank", new Vector3(0.02f, 0f, 0.11f), new Vector3(0.04f, 0.006f, 0.006f), steel, true, bitObject);
                Prim(PrimitiveType.Cylinder, "Head", new Vector3(0f, 0f, 0.118f), new Vector3(0.03f, 0.008f, 0.03f), new Color(0.3f, 0.2f, 0.12f), false, bitObject).localRotation = Quaternion.Euler(90f, 0f, 0f);
                bitObject.gameObject.SetActive(false);
                break;
            }
            case CraftHandsOnMode.Brush:
            {
                Bench("Bench", new Vector3(0f, -0.03f, 0.03f), new Vector3(0.5f, 0.04f, 0.32f), wood);
                Box("Sacking", new Vector3(0f, -0.008f, 0f), new Vector3(0.22f, 0.004f, 0.2f), new Color(0.55f, 0.47f, 0.33f), false);
                var rockGo = new GameObject("Ore Stone");
                rockObject = rockGo.transform;
                rockObject.SetParent(root, false);
                rockFilter = rockGo.AddComponent<MeshFilter>();
                MeshRenderer mr = rockGo.AddComponent<MeshRenderer>();
                rockMaterial = CraftOreRock.VertexColourMaterial();
                if (mr != null) mr.sharedMaterial = rockMaterial;
                oreBrush = setup.brush;
                rockTurn = rockShown = Quaternion.identity;
                brushSeconds = 0f;
                CraftPiece lump = Piece;
                ShowStone(lump != null ? Mathf.Clamp(setup.fragment, 0, lump.ore.fragments.Count - 1) : 0);
                BuildBrush();
                break;
            }
        }
        if (mode != CraftHandsOnMode.Pour && mode != CraftHandsOnMode.Brush)
        {
            var piece = new GameObject("Piece");
            pieceObject = piece.transform;
            pieceObject.SetParent(root, false);
            pieceFilter = piece.AddComponent<MeshFilter>();
            pieceRenderer = piece.AddComponent<MeshRenderer>();
            pieceObject.localRotation = Quaternion.LookRotation(Vector3.up, -Vector3.right); // as the window draws it: its first end on the left, the edge away, the left face up
            if (mode == CraftHandsOnMode.Bellows) pieceObject.localPosition = new Vector3(0f, 0.03f, 0f);
            RefreshPiece();
            if (mode == CraftHandsOnMode.Hammer) pieceObject.localPosition = new Vector3(0f, PieceTop(Piece) * 0.5f, 0f);
        }
        // The station's own anvil (wheel, tank...) steps aside for this one: the model set for it (Worked by hand), else what
        // of the station's own stands right where this one is built (said once: the model is better set); the player's body
        // is out of the way of the view.
        if (visuals != null && mode != CraftHandsOnMode.Bellows && !visuals.HasModel(mode))
        {
            if (PlaceBounds(out Bounds area))
            {
                area.Expand(0.04f);
                visuals.HideOverlapping(area, hiddenModels);
            }
            if (visuals.HandsOnPoint(mode, out _) && station != null && unmodelled.Add($"{station.GetInstanceID()}:{mode}"))
                Debug.LogWarning($"{station.name}: its {mode} work place point is set in Craft Station Visuals, but no 'Worked by hand' model for it: " +
                                 "what of the station's own stands there was hidden by where it stands. Set the model to hide exactly that.", visuals);
        }
        else visuals?.HideModel(mode, hiddenModels);
        if (player != null)
            foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
                if (r != null && r.enabled && !r.forceRenderingOff)
                {
                    r.forceRenderingOff = true;
                    hiddenPlayer.Add(r);
                }
    }

    /// <summary>Stations and work places already said to have no 'Worked by hand' model (said once each).</summary>
    private static readonly HashSet<string> unmodelled = new HashSet<string>();

    /// <summary>
    /// The station's footprint: the bounds of all its colliders, its own and its children's (an imported model's colliders
    /// too) - the solid ones, its triggers only when it has nothing else. False with none.
    /// </summary>
    private bool StationBounds(out Bounds bounds)
    {
        bounds = default;
        if (station == null)
            return false;
        bool found = false;
        Collider[] all = station.GetComponentsInChildren<Collider>();
        for (int pass = 0; pass < 2 && !found; pass++)
            foreach (Collider c in all)
            {
                if (c == null || !c.enabled || c.isTrigger != (pass == 1)) continue;
                if (found) bounds.Encapsulate(c.bounds);
                else { bounds = c.bounds; found = true; }
            }
        return found;
    }

    /// <summary>The bounds of what the work place built (its renderers), in the world.</summary>
    private bool PlaceBounds(out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        if (root != null)
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (found) bounds.Encapsulate(r.bounds);
                else { bounds = r.bounds; found = true; }
            }
        return found;
    }

    /// <summary>The floor under the station (its colliders' bottom, else its own height).</summary>
    private float FloorHeight() => StationBounds(out Bounds b) ? b.min.y : transform.position.y;

    /// <summary>How far round the view keeps from what is in its way (m): the camera's own near reach.</summary>
    private const float ViewRadius = 0.08f;
    private static readonly RaycastHit[] viewHits = new RaycastHit[16];

    /// <summary>
    /// How far from <paramref name="from"/> toward <paramref name="to"/> the view can stand before something solid is in the
    /// way - a wall, a beam, the station's own side - the whole way when nothing is. Not the work place itself, nobody's body
    /// (the player's, anybody walking by), no trigger, nor what the way starts inside (the station's box round its work places).
    /// </summary>
    private float Room(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float length = d.magnitude;
        if (length < 1e-4f)
            return 0f;
        int n = Physics.SphereCastNonAlloc(from, ViewRadius, d / length, viewHits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float room = length;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = viewHits[i];
            if (h.collider == null || h.distance <= 0f) continue; // overlapping where it starts: what the work place stands in
            if (root != null && h.collider.transform.IsChildOf(root)) continue;
            if (h.collider.GetComponentInParent<CombatEntity>() != null) continue;
            room = Mathf.Min(room, h.distance);
        }
        return room;
    }

    /// <summary>
    /// Where a work place the station has no point for stands: just clear of the station, between it and the player, at
    /// the height that work is done at (a bench's, an anvil's, a tank's rim, a casting bench's).
    /// </summary>
    private Vector3 FreeSpot(Vector3 front)
    {
        float height = mode == CraftHandsOnMode.Pour ? 0.5f : mode == CraftHandsOnMode.Quench ? 0.6f : mode == CraftHandsOnMode.Hammer ? 0.8f : 0.85f;
        Vector3 from = player != null ? player.transform.position : transform.position + front * 2f;
        bool solid = StationBounds(out Bounds footprint);
        Vector3 edge = solid ? footprint.ClosestPoint(from) : transform.position; // all its colliders (its children's too, any shape)
        Vector3 away = Flat(from - edge);
        if (!solid || (from - edge).sqrMagnitude < 1e-4f)
            away = Vector3.Dot(Flat(from - transform.position), front) < 0f ? -front : front;
        Vector3 spot = edge + away * 0.45f;
        spot.y = FloorHeight() + height;
        return spot;
    }

    /// <summary>A post or block from local height <paramref name="top"/> down to the floor (a stump, a bench's legs) - none when the floor is not below it.</summary>
    private void ToFloor(string name, Vector3 top, Vector2 size, Color c, Transform parent = null)
    {
        float h = top.y - floorY;
        if (h < 0.01f)
            return;
        Box(name, new Vector3(top.x, (top.y + floorY) * 0.5f, top.z), new Vector3(size.x, h, size.y), c, false, parent);
    }

    /// <summary>A bench top centred at <paramref name="centre"/> on four legs down to the floor.</summary>
    private void Bench(string name, Vector3 centre, Vector3 size, Color c)
    {
        Box(name, centre, size, c, false);
        float under = centre.y - size.y * 0.5f;
        for (int i = 0; i < 4; i++)
            ToFloor($"{name} Leg", new Vector3(centre.x + (i % 2 == 0 ? -1f : 1f) * (size.x * 0.5f - 0.03f), under, centre.z + (i < 2 ? -1f : 1f) * (size.z * 0.5f - 0.03f)), new Vector2(0.04f, 0.04f), c);
    }

    /// <summary>
    /// The quench tank: its rim just over the liquid's surface (the point), as long as the piece lies flat and deep enough
    /// to stand it in upright (sunk into the floor if need be), the liquid see-through enough to show what is under it,
    /// and the tongs that hold the piece by its tang.
    /// </summary>
    private void BuildTank(Color iron)
    {
        CraftPiece p = Piece;
        float len = p != null ? Mathf.Max(0.05f, (float)(p.Length * 0.001)) : 0.3f;
        tankLength = Mathf.Max(0.6f, len + 0.12f);
        float rim = 0.04f, bottom = Mathf.Min(floorY, -(len + 0.08f)), h = rim - bottom, mid = (rim + bottom) * 0.5f;
        Box("Tank Wall Back", new Vector3(0f, mid, -0.16f), new Vector3(tankLength + 0.04f, h, 0.02f), iron, true);
        Box("Tank Wall Front", new Vector3(0f, mid, 0.16f), new Vector3(tankLength + 0.04f, h, 0.02f), iron, true);
        Box("Tank Wall Left", new Vector3(-tankLength * 0.5f - 0.01f, mid, 0f), new Vector3(0.02f, h, 0.3f), iron, true);
        Box("Tank Wall Right", new Vector3(tankLength * 0.5f + 0.01f, mid, 0f), new Vector3(0.02f, h, 0.3f), iron, true);
        Box("Tank Bottom", new Vector3(0f, bottom + 0.01f, 0f), new Vector3(tankLength, 0.02f, 0.3f), iron, true);
        CraftQuenchant q = station.Rules.Quenchant(Ws.Quench.medium);
        Color liquid = q != null && q.id.Contains("oil") ? new Color(0.45f, 0.33f, 0.1f, 0.62f) : q != null && q.id.Contains("blood") ? new Color(0.45f, 0.04f, 0.04f, 0.75f) : new Color(0.2f, 0.33f, 0.42f, 0.45f);
        Transform surface = Box("Liquid", Vector3.zero, new Vector3(tankLength, 0.002f, 0.3f), liquid, false);
        Material see = AlchemyVesselParts.NewMaterial(liquid, true, 0.85f);
        ownMaterials.Add(see);
        Renderer r = surface.GetComponent<Renderer>();
        if (r != null)
        {
            r.sharedMaterial = see;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        // The tongs: their jaws on the tang, the reins up toward the hand.
        tongsObject = new GameObject("Tongs").transform;
        tongsObject.SetParent(root, false);
        Prim(PrimitiveType.Cube, "Jaw", new Vector3(0f, 0.015f, -0.006f), new Vector3(0.006f, 0.04f, 0.006f), iron, true, tongsObject);
        Prim(PrimitiveType.Cube, "Jaw", new Vector3(0f, 0.015f, 0.006f), new Vector3(0.006f, 0.04f, 0.006f), iron, true, tongsObject);
        Prim(PrimitiveType.Cube, "Reins", new Vector3(0f, 0.13f, 0f), new Vector3(0.008f, 0.2f, 0.016f), iron, true, tongsObject);
    }

    /// <summary>The wheel, the file, the brush or the stone of the abrasive in hand.</summary>
    private void RebuildTool()
    {
        if (root == null)
            return;
        if (wheelObject != null) Destroy(wheelObject.gameObject);
        wheelObject = null;
        CraftAbrasive a = Abrasive;
        if (a == null)
            return;
        float fine = Mathf.Clamp01(1f - (float)a.finish / 20f);
        if (a.kind == CraftAbrasiveKind.Wheel)
        {
            wheelObject = Prim(PrimitiveType.Cylinder, "Wheel", new Vector3(0f, 0.12f, -0.02f), new Vector3(0.22f, 0.02f, 0.22f), Color.Lerp(new Color(0.35f, 0.33f, 0.3f), new Color(0.7f, 0.68f, 0.64f), fine), false);
            wheelObject.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // A dark stripe across the rim, standing a little proud of it (the cylinder's own units: radius 0.5, its width
            // along y from -1 to 1): it flicks past as the wheel turns, so a turning wheel shows that it turns.
            Prim(PrimitiveType.Cube, "Wheel Mark", new Vector3(0.5f, 0f, 0f), new Vector3(0.04f, 1.8f, 0.12f), new Color(0.12f, 0.11f, 0.1f), false, wheelObject);
            Prim(PrimitiveType.Cube, "Wheel Mark", new Vector3(-0.5f, 0f, 0f), new Vector3(0.04f, 1.8f, 0.12f), new Color(0.12f, 0.11f, 0.1f), false, wheelObject);
        }
        else if (a.kind == CraftAbrasiveKind.Whetstone)
        {
            Color c = CraftSurface.IsStrop(a) ? new Color(0.45f, 0.3f, 0.18f) : Color.Lerp(new Color(0.35f, 0.33f, 0.3f), new Color(0.78f, 0.75f, 0.7f), fine);
            wheelObject = Prim(PrimitiveType.Cube, CraftSurface.IsStrop(a) ? "Strop" : "Whetstone", new Vector3(0f, 0.045f, 0f), new Vector3(0.24f, 0.025f, 0.06f), c, false);
        }
        else
            wheelObject = Prim(PrimitiveType.Cube, a.Name, new Vector3(0f, 0.15f, 0.02f), new Vector3(0.18f, 0.012f, 0.025f),
                a.kind == CraftAbrasiveKind.Brush ? new Color(0.5f, 0.4f, 0.25f) : a.kind == CraftAbrasiveKind.Polish ? new Color(0.75f, 0.7f, 0.6f) : new Color(0.4f, 0.4f, 0.43f), a.kind == CraftAbrasiveKind.File);
    }

    /// <summary>The top of the whetstone (local height): the edge rests on it.</summary>
    private const float StoneTop = 0.0575f;

    /// <summary>
    /// The piece as it is now: its shape (rebuilt when it changes - the eye, the fuller, the dish and a cut half made too,
    /// and the cracks that show), each segment's own surface (scale, polish, the temper colours on bright steel - the
    /// window's colours), burnt stretches dull and dark, and each glowing at its own heat. At the carving bench the
    /// design's shape is laid over it (<see cref="BuildGhost"/>).
    /// </summary>
    private void RefreshPiece()
    {
        CraftPiece p = Piece;
        if (pieceObject == null || p == null || p.segments.Count == 0)
        {
            if (pieceObject != null) pieceObject.gameObject.SetActive(false);
            return;
        }
        uint shape = CraftModels.ShapeKey(p);
        if (!pieceBuilt || shape != pieceShape)
        {
            pieceShape = shape;
            pieceBuilt = true;
            Mesh old = pieceFilter.sharedMesh;
            pieceFilter.sharedMesh = CraftModels.PieceMesh(p, true);
            if (old != null) Destroy(old);
            pieceLength = Mathf.Max(0.02f, (float)(p.Length * 0.001));
            double w = 0;
            foreach (MetalSegment s in p.segments) w = Math.Max(w, s.width);
            pieceWidth = Mathf.Max(0.005f, (float)(w * 0.001));
            if (mode == CraftHandsOnMode.Carve) BuildGhost(p);
        }
        bool metal = p.kind == CraftPieceKind.Metal;
        bool dark = Ws.Variable("night") > 0.5;
        Color baseColor = CraftArt.MetalColor(station.Rules, p);
        int n = p.segments.Count;
        var mats = new Material[CraftModels.MarkedMesh(pieceFilter.sharedMesh, n) ? n + 1 : n];
        for (int i = 0; i < n; i++)
        {
            MetalSegment s = p.segments[i];
            Color c = metal ? CraftArt.MetalSurface(baseColor, s) : Color.Lerp(baseColor, Color.black, p.wood.charred > 0.05 ? (float)p.wood.charred : 0f);
            float smooth = metal ? 0.25f + 0.6f * Mathf.Clamp01((float)s.polish) : 0.15f;
            // Flux on it: a glassy skin once molten, a pale crust below that (as the window shows it).
            if (metal && s.flux > 0.05)
            {
                float fl = Mathf.Clamp01((float)s.flux);
                if (s.Temperature > 750) smooth = Mathf.Lerp(smooth, 0.95f, fl);
                else c = Color.Lerp(c, new Color(0.86f, 0.83f, 0.74f), 0.35f * fl);
            }
            // Burnt: the grain coarse and the surface pitted - dull and dark where it burnt.
            float burnt = metal ? Mathf.Clamp01((float)Math.Max(s.body.burnt, s.edge.burnt) * 1.5f) : 0f;
            if (burnt > 0.05f)
            {
                c = Color.Lerp(c, new Color(0.17f, 0.13f, 0.11f), 0.75f * burnt);
                smooth *= 1f - 0.8f * burnt;
            }
            Color glow = CraftArt.Heat(s.Temperature, dark, out float k);
            mats[i] = CraftModels.Glowing(c, metal, smooth, glow, metal ? k : 0f);
        }
        if (mats.Length > n) mats[n] = CraftModels.Marks;
        pieceRenderer.sharedMaterials = mats;
    }

    /// <summary>
    /// The design's shape laid over the wood at the carving bench, as the window dashes it over the drawing: its outline
    /// seen from above (the width as it lies, the thickness when it is turned on its side to thin it) a hair over the
    /// wood or where the wood should still be, from the blank's own end, and marks across it at the shortest and the
    /// longest the design allows. Without a shape (a round grip), its diameter and length range.
    /// </summary>
    private void BuildGhost(CraftPiece p)
    {
        if (ghostWidth != null) { DestroyMesh(ghostWidth); Destroy(ghostWidth.gameObject); }
        if (ghostThick != null) { DestroyMesh(ghostThick); Destroy(ghostThick.gameObject); }
        ghostWidth = ghostThick = null;
        CraftShapeTarget shape = CraftArt.TargetShape(station.Rules, p, out float length);
        CraftComponentSpec spec = !string.IsNullOrEmpty(p.intentDesign) ? station.Rules.Design(p.intentDesign)?.Component(p.intentRole) : null;
        float lMin, lMax;
        if (shape != null) { lMin = shape.lengthMin; lMax = shape.lengthMax; }
        else if (spec != null && spec.kind == CraftPieceKind.Wood)
        {
            lMin = spec.wood.lengthMin;
            lMax = spec.wood.lengthMax;
            length = Mathf.Clamp((float)p.Length, lMin, Mathf.Max(lMin, lMax));
        }
        else
            return; // nothing wanted yet: the line says so
        float Want(float t, bool thick)
        {
            if (shape == null) return (spec.wood.diameterMin + spec.wood.diameterMax) * 0.5f;
            shape.At(t, out float w, out float th);
            return thick ? th : w;
        }
        if (ghostMaterial == null)
        {
            ghostMaterial = AlchemyVesselParts.NewMaterial(new Color(CraftArt.TargetColor.r, CraftArt.TargetColor.g, CraftArt.TargetColor.b, 0.85f), true, 0.1f);
            ownMaterials.Add(ghostMaterial);
        }
        ghostWidth = Ghost("Target (width)", p, length, lMin, lMax, false, Want);
        ghostThick = Ghost("Target (thickness)", p, length, lMin, lMax, true, Want);
    }

    /// <summary>
    /// One view of the target over the piece (its own frame: the length along +Y from -L/2, the width across X, the
    /// thickness along Z): two thin lines at plus and minus half of what is wanted, on the face that is up in that view,
    /// and ticks across at the shortest and longest lengths.
    /// </summary>
    private Transform Ghost(string name, CraftPiece p, float lengthMm, float minMm, float maxMm, bool thick, Func<float, bool, float> want)
    {
        var v = new List<Vector3>();
        var tri = new List<int>();
        float half = (float)(p.Length * 0.0005), line = 0.0012f;
        // A point of the piece's frame: along (m from its start), across (the width or the thickness, m), and the height
        // of the face up in that view (+Z for the width, -X for the thickness) a hair over it.
        Vector3 At(float along, float across, float over) => thick ? new Vector3(-over, along - half, across) : new Vector3(across, along - half, over);
        float Over(float along)
        {
            int i = Mathf.Clamp(p.SegmentAt(Mathf.Clamp01(along / Mathf.Max(1e-4f, half * 2f))), 0, p.segments.Count - 1);
            MetalSegment s = p.segments[i];
            return (float)(thick ? s.width : s.thickness) * 0.0005f + 0.0012f;
        }
        // Each strip faces up, as the view sees it (Unity's front faces wind clockwise).
        Vector3 up = thick ? Vector3.left : Vector3.forward;
        void Strip(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int k = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            bool front = Vector3.Dot(Vector3.Cross(b - a, c - a), up) >= 0f;
            tri.Add(k); tri.Add(front ? k + 1 : k + 2); tri.Add(front ? k + 2 : k + 1);
            tri.Add(k); tri.Add(front ? k + 2 : k + 3); tri.Add(front ? k + 3 : k + 2);
        }
        const int n = 32;
        float len = lengthMm * 0.001f;
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < n; i++)
            {
                float t0 = i / (float)n, t1 = (i + 1) / (float)n;
                float a0 = t0 * len, a1 = t1 * len;
                float w0 = want(t0, thick) * 0.0005f * side, w1 = want(t1, thick) * 0.0005f * side;
                float o0 = Mathf.Max(Over(a0), want(t0, !thick) * 0.0005f + 0.0012f), o1 = Mathf.Max(Over(a1), want(t1, !thick) * 0.0005f + 0.0012f);
                Strip(At(a0, w0 - line * 0.5f, o0), At(a0, w0 + line * 0.5f, o0), At(a1, w1 + line * 0.5f, o1), At(a1, w1 - line * 0.5f, o1));
            }
        // The far end of the shape, across it; ticks at the shortest and longest lengths the design allows.
        float end = want(1f, thick) * 0.0005f, endOver = Mathf.Max(Over(len), want(1f, !thick) * 0.0005f + 0.0012f);
        Strip(At(len - line * 0.5f, -end, endOver), At(len + line * 0.5f, -end, endOver), At(len + line * 0.5f, end, endOver), At(len - line * 0.5f, end, endOver));
        foreach (float mm in new[] { minMm, maxMm })
        {
            float a = mm * 0.001f, w = Mathf.Max(want(Mathf.Clamp01(a / Mathf.Max(1e-4f, len)), thick) * 0.0005f + 0.006f, 0.008f), over = Mathf.Max(Over(a), 0.002f);
            Strip(At(a - line * 0.4f, w - 0.004f, over), At(a + line * 0.4f, w - 0.004f, over), At(a + line * 0.4f, w + 0.004f, over), At(a - line * 0.4f, w + 0.004f, over));
            Strip(At(a - line * 0.4f, -w - 0.004f, over), At(a + line * 0.4f, -w - 0.004f, over), At(a + line * 0.4f, -w + 0.004f, over), At(a - line * 0.4f, -w + 0.004f, over));
        }
        var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
        mesh.SetVertices(v);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var go = new GameObject(name);
        go.transform.SetParent(pieceObject, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = ghostMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    private static void DestroyMesh(Transform t)
    {
        MeshFilter f = t != null ? t.GetComponent<MeshFilter>() : null;
        if (f != null && f.sharedMesh != null) Destroy(f.sharedMesh);
    }

    private static float PieceTop(CraftPiece p)
    {
        double t = 0;
        if (p != null)
            foreach (MetalSegment s in p.segments) t = Math.Max(t, s.thickness);
        return (float)(t * 0.001);
    }

    private void SetGlow(Transform t, Color c, double celsius)
    {
        Renderer r = t != null ? t.GetComponent<Renderer>() : null;
        if (r == null) return;
        Color glow = CraftArt.Heat(celsius, out float k);
        r.sharedMaterial = CraftModels.Glowing(c, false, 0.4f, glow, Mathf.Max(k, 0.6f));
    }

    private Transform Box(string name, Vector3 at, Vector3 size, Color c, bool metal, Transform parent = null) => Prim(PrimitiveType.Cube, name, at, size, c, metal, parent);

    /// <summary>The mould pressed into the sand: its cavity has the outline of what it casts (a blade, an axe head...), drawn
    /// as cells along its length, each with the metal that will fill it (hidden until the pour reaches it).</summary>
    private float BuildMouldCavity(CraftMold mold)
    {
        fillCells.Clear();
        if (mold == null)
            return 0.05f;
        var shape = mold.profile != null && mold.profile.Count >= 2 ? new CraftShapeTarget { profile = mold.profile } : null;
        const int n = 18;
        float widest = mold.width;
        if (shape != null) foreach (CraftProfilePoint q in mold.profile) widest = Mathf.Max(widest, q.width);
        float k = Mathf.Min(0.27f / Mathf.Max(1f, mold.length), 0.14f / Mathf.Max(1f, widest)); // metres per mm
        float x0 = 0.062f, cell = mold.length * k / n; // the mouth under the crucible's lip, the cavity running away from it
        double t = setup.fromFurnace ? Ws.Furnace.fire.temperature : Ws.Crucible.temperature;
        Box("Mould Runner", new Vector3(x0 - 0.006f, 0.0006f, 0f), new Vector3(0.014f, 0.002f, 0.006f), new Color(0.06f, 0.05f, 0.04f), false, pourSpace);
        for (int i = 0; i < n; i++)
        {
            float w = mold.width;
            if (shape != null) shape.At((i + 0.5f) / n, out w, out _);
            var at = new Vector3(x0 + cell * (i + 0.5f), 0.0005f, 0f);
            Box("Mould Cavity", at, new Vector3(cell * 1.02f, 0.002f, Mathf.Max(0.004f, w * k)), new Color(0.12f, 0.1f, 0.08f), false, pourSpace);
            Transform metal = Prim(PrimitiveType.Cube, "Metal In The Mould", at + new Vector3(0f, 0.002f, 0f), new Vector3(cell * 1.02f, 0.003f, Mathf.Max(0.003f, w * k * 0.95f)), Color.white, false, pourSpace);
            SetGlow(metal, new Color(0.6f, 0.5f, 0.45f), t);
            metal.gameObject.SetActive(false);
            fillCells.Add(metal);
        }
        return x0 - 0.022f; // the pouring cup stands just before the cavity; the stream runs in there
    }

    private Transform Prim(PrimitiveType type, string name, Vector3 at, Vector3 size, Color c, bool metal, Transform parent = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.SetParent(parent != null ? parent : root, false);
        go.transform.localPosition = at;
        go.transform.localScale = size;
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = CraftModels.Plain(c, metal, metal ? 0.45f : 0.2f);
        return go.transform;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }

    /// <summary>Where the pointer meets the work place's horizontal plane at <paramref name="height"/> (in the place's own frame).</summary>
    private bool PointOnPlane(Vector2 pointer, float height, out Vector3 local)
    {
        local = Vector3.zero;
        if (cam == null || root == null)
            return false;
        Ray ray = cam.ScreenPointToRay(pointer);
        var plane = new Plane(root.up, root.position + root.up * height);
        if (!plane.Raycast(ray, out float d))
            return false;
        local = root.InverseTransformPoint(ray.GetPoint(d));
        return true;
    }

    /// <summary>Where the view stands: on the player's side of the work place (its +z), looking at the work, nearer for small work.</summary>
    private void TargetView(out Vector3 position, out Quaternion rotation)
    {
        if (root == null)
        {
            position = transform.position;
            rotation = transform.rotation;
            return;
        }
        Vector3 at = root.position, front = root.forward;
        float distance = viewDistance, height = 0.55f;
        switch (mode)
        {
            case CraftHandsOnMode.Hammer: distance = Mathf.Max(viewDistance, pieceLength * 1.3f); height = 0.6f; break;
            case CraftHandsOnMode.Bellows: at += -root.right * 0.25f; distance = viewDistance * 1.3f; height = 0.7f; break;
            case CraftHandsOnMode.Grind: at += root.up * 0.1f; distance = Mathf.Max(viewDistance * 0.8f, pieceLength); height = 0.3f; break;
            case CraftHandsOnMode.Sharpen: distance = Mathf.Max(viewDistance * 0.7f, pieceLength * 0.9f); height = 0.45f; break;
            case CraftHandsOnMode.Quench: at += root.up * 0.15f; distance = Mathf.Max(viewDistance * 1.1f, pieceLength * 1.1f); height = 0.6f; break;
            case CraftHandsOnMode.Pour: at += root.up * 0.1f - root.right * 0.05f; distance = viewDistance * 0.8f; height = 0.3f; break;
            case CraftHandsOnMode.Carve: distance = Mathf.Max(viewDistance * 0.7f, pieceLength * 1.1f); height = 0.45f; break;
            case CraftHandsOnMode.Brush:
                // As close as the stone is small: it fills the view the same, a pebble or a cobble.
                at += root.up * rock.Radius;
                distance = Mathf.Clamp(rock.Radius * 5.2f, 0.2f, 0.45f);
                height = distance * 0.45f;
                break;
        }
        position = at + front * distance + Vector3.up * height;
        // Nearer, when a wall or a beam stands where the view would (not inside it, nor behind it).
        float room = Room(at, position), whole = (position - at).magnitude;
        if (room < whole)
            position = at + (position - at) / whole * Mathf.Max(0.3f, room - 0.05f);
        rotation = Quaternion.LookRotation(at - position, Vector3.up);
    }

    // ------------------------------------------------------------------ the HUD
    /// <summary>
    /// A labelled meter of the HUD: a value 0-1 along a bar, its bands under it (where the work wants it, where it goes
    /// wrong) - the rule the workshop applies, shown. <see cref="Set"/> each frame; <see cref="Hide"/> when the work place
    /// has nothing to show on it.
    /// </summary>
    private sealed class HudMeter
    {
        public RectTransform row;
        public TextMeshProUGUI label;
        public Image fill;
        public RectTransform back;
        private readonly List<Image> bands = new List<Image>();
        private readonly List<(float from, float to, Color color)> shown = new List<(float, float, Color)>();

        public void Set(string text, float value, Color color, params (float from, float to, Color color)[] marks)
        {
            if (row == null)
                return;
            if (label != null && !label.gameObject.activeSelf) label.gameObject.SetActive(true);
            if (!back.gameObject.activeSelf) back.gameObject.SetActive(true);
            if (label != null && label.text != text) label.text = text;
            bool banded = marks != null && marks.Length > 0;
            // Over bands the fill is slimmer, so the bands show above and below it (as the window's meters).
            fill.rectTransform.anchorMin = new Vector2(0f, banded ? 0.3f : 0f);
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), banded ? 0.7f : 1f);
            fill.color = color;
            int n = marks?.Length ?? 0;
            bool same = n == shown.Count;
            for (int i = 0; same && i < n; i++)
                same = Mathf.Abs(marks[i].from - shown[i].from) < 1e-4f && Mathf.Abs(marks[i].to - shown[i].to) < 1e-4f && marks[i].color == shown[i].color;
            if (same)
                return; // the bands as they are
            shown.Clear();
            for (int i = 0; i < n; i++) shown.Add(marks[i]);
            while (bands.Count < n)
            {
                Image band = UITheme.Panel("Band", back, Color.clear, 0);
                band.raycastTarget = false;
                band.transform.SetSiblingIndex(0); // under the fill
                bands.Add(band);
            }
            for (int i = 0; i < bands.Count; i++)
            {
                bool on = i < n;
                bands[i].gameObject.SetActive(on);
                if (!on) continue;
                (float from, float to, Color c) = marks[i];
                RectTransform rt = bands[i].rectTransform;
                rt.anchorMin = new Vector2(Mathf.Clamp01(Mathf.Min(from, to)), 0f);
                rt.anchorMax = new Vector2(Mathf.Clamp01(Mathf.Max(Mathf.Max(from, to), Mathf.Min(from, to) + 0.006f)), 1f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                bands[i].color = new Color(c.r, c.g, c.b, 0.55f);
            }
        }

        /// <summary>Nothing to show: the label and the bar go, the row keeps its place (the rows under it do not jump).</summary>
        public void Hide()
        {
            if (label != null && label.gameObject.activeSelf) label.gameObject.SetActive(false);
            if (back != null && back.gameObject.activeSelf) back.gameObject.SetActive(false);
        }
    }

    private void BuildHud()
    {
        hud = UITheme.Canvas("Craft Hands-On", 57, null);
        DontDestroyOnLoad(hud.gameObject);
        Image panel = UITheme.Panel("Panel", hud.transform, new Color(0.06f, 0.06f, 0.07f, 0.84f), UITheme.Radius);
        UITheme.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-460f, 24f), new Vector2(460f, 282f));
        // The message line, over the panel: what the station answered (a refusal's reason in red), two lines at the most - on
        // a dark band as wide as its words, for it stands over the scene (the forge's glow, a white-hot stream, the sand).
        hudMessageBack = UITheme.Panel("Message Backing", hud.transform, new Color(0.03f, 0.03f, 0.04f, 0.74f), 8);
        hudMessageBack.raycastTarget = false;
        UITheme.Place(hudMessageBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-460f, 284f), new Vector2(460f, 334f));
        hudMessageBack.gameObject.SetActive(false);
        hudMessage = UITheme.Label(hud.transform, "", 15f, UITheme.Gold, TextAlignmentOptions.BottomLeft, FontStyles.Normal, "Message");
        UITheme.Place(hudMessage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-452f, 288f), new Vector2(452f, 330f));
        hudMessage.textWrappingMode = TextWrappingModes.Normal;
        hudMessage.enableAutoSizing = true;
        hudMessage.fontSizeMin = 11f;
        hudMessage.fontSizeMax = 15f;
        hudMessage.overflowMode = TextOverflowModes.Ellipsis;
        hudMessage.raycastTarget = false;
        messageUntil = 0f;
        // The rows keep the heights they are given (12 + 26 + 58 + 44 + 3 × 12 + 44 + 12 and the gaps: 256 of the panel's
        // 258): their words wrap and shrink to fit (an ellipsis at the very worst), so no row ever runs into the next.
        RectTransform col = UITheme.Column("Column", panel.transform, 12, 4f);
        InventoryUIFactory.Stretch(col);
        hudTitle = Row(col, "Title", 20f, 13f, 26f, UITheme.Gold, FontStyles.Bold, false);
        hudFeel = Row(col, "Feel", 16f, 11f, 58f, UITheme.Text);
        hudLegend = Row(col, "Legend", 14f, 10f, 44f, UITheme.Text);
        meter = Bar(col, "Blow");
        meter2 = Bar(col, "");
        meter2.Hide();
        arms = Bar(col, "Arms");
        hudHint = Row(col, "Hint", 13f, 10f, 44f, UITheme.Muted);
        hudLegend.text = Legend();
        hudHint.text = Hint();
    }

    /// <summary>The message line's dark band: round its words as they are laid out now (8 px about them), hidden while there are none.</summary>
    private void FitMessageBacking()
    {
        if (hudMessageBack == null || hudMessage == null)
            return;
        bool any = !string.IsNullOrEmpty(hudMessage.text);
        if (hudMessageBack.gameObject.activeSelf != any) hudMessageBack.gameObject.SetActive(any);
        if (!any)
            return;
        hudMessage.ForceMeshUpdate();
        // The line's words sit at its bottom left: as wide and as high as they are laid out (its whole width at the most).
        RectTransform line = hudMessage.rectTransform;
        Vector2 size = line.offsetMax - line.offsetMin;
        float w = hudMessage.renderedWidth, h = hudMessage.renderedHeight;
        if (!(w > 1f) || !(h > 1f)) { w = size.x; h = size.y; }
        w = Mathf.Min(w, size.x);
        h = Mathf.Min(h, size.y);
        UITheme.Place(hudMessageBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            line.offsetMin - new Vector2(8f, 4f), line.offsetMin + new Vector2(w + 8f, h + 4f));
    }

    /// <summary>A row of words <paramref name="height"/> high that wraps (unless <paramref name="wrap"/> is off) and shrinks from <paramref name="size"/> to <paramref name="smallest"/> to fit.</summary>
    private static TextMeshProUGUI Row(RectTransform col, string name, float size, float smallest, float height, Color color, FontStyles style = FontStyles.Normal, bool wrap = true)
    {
        TextMeshProUGUI t = UITheme.Label(col, "", size, color, wrap ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left, style, name);
        InventoryUIFactory.Size(t, -1f, height);
        t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        t.enableAutoSizing = true;
        t.fontSizeMin = smallest;
        t.fontSizeMax = size;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        return t;
    }

    private static HudMeter Bar(RectTransform col, string label)
    {
        RectTransform row = UITheme.Row(string.IsNullOrEmpty(label) ? "Meter" : label, col, 12f, 8f);
        // Its name in a word or two ("Arms: tiring"), shrunk to fit its 92 px rather than run into the bar.
        TextMeshProUGUI text = UITheme.Label(row, label, 12f, UITheme.Muted);
        InventoryUIFactory.Size(text, 92f, 12f, 0f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = true;
        text.fontSizeMin = 9f;
        text.fontSizeMax = 12f;
        text.overflowMode = TextOverflowModes.Ellipsis;
        Image back = UITheme.Panel("Bar", row, new Color(0f, 0f, 0f, 0.4f), 4);
        InventoryUIFactory.Size(back, -1f, 8f, 1f);
        Image fill = UITheme.Panel("Fill", back.transform, UITheme.Good, 4);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        return new HudMeter { row = row, label = text, fill = fill, back = back.rectTransform };
    }

    /// <summary>
    /// The work place's choices as they stand - the one in use of each list (its place in the list, and its number key on a
    /// keyboard) and how much is set - each with the keys that change it: two short lines at the most (the lists are
    /// stepped through, not spelled out).
    /// </summary>
    private string Legend()
    {
        string On(string text) => $"<b><color=#FFFFFF>{text}</color></b>";
        // The number key of choice i on a keyboard ("[4] "); a gamepad has none (it steps through the choices).
        string Num(int i) => InputDeviceTracker.Current == InputDeviceTracker.Kind.Gamepad || i < 0 || i >= 10 ? "" : $"[{StationControls.Key((StationControl)((int)StationControl.Choice1 + i))}] ";
        string next = StationControls.K(StationControl.NextTool), turn = StationControls.K(StationControl.TurnOver), adjust = StationControls.AdjustKeys;
        int inHand = Mathf.Clamp(abrasive, 0, Mathf.Max(0, abrasives.Count - 1)) + 1;
        switch (mode)
        {
            case CraftHandsOnMode.Hammer:
            {
                int at = Mathf.Max(0, Array.IndexOf(ToolOrder, setup.tool));
                CraftPiece p = Piece;
                string line = $"Tool: {On(Num(at) + CraftForging.ToolName(setup.tool))} ({at + 1} of {ToolOrder.Length}) - {StationControls.ChoiceKeys(ToolOrder.Length)} or {next}" +
                              $"   Struck {On(setup.face == CraftStrikeFace.Flat ? "on its face" : "on its edge")} - {StationControls.K(StationControl.FaceEdge)}";
                string second = p != null ? $"The {On(p.faceUp == 0 ? "left" : "right")} face is up: a tilted blow bevels it - {turn}" : "";
                if (setup.tool == CraftStrikeTool.Horn) second += $"{(second.Length > 0 ? "   " : "")}Over the horn the far end bends {On(setup.bendDown ? "down" : "up")} - {adjust}";
                return second.Length > 0 ? line + "\n" + second : line;
            }
            case CraftHandsOnMode.Grind:
            {
                CraftAbrasive a = Abrasive;
                string line = $"Tool: {On(a != null ? a.Name : "none")} ({inHand} of {abrasives.Count}) - {next}";
                if (!Shaping)
                    return line + $"\nA {(a != null && a.kind == CraftAbrasiveKind.Polish ? "polish" : "brush")} works all the surface under it: no side, no angle";
                int side = Mathf.Max(0, Array.IndexOf(Sides, setup.side));
                if (BevelSide) line += $"   Angle: {On($"{GrindAngle:0}°")} a side - {adjust}";
                return line + $"\nSide: {On(Num(side) + SideNames[side])} ({side + 1} of {Sides.Length}) - {StationControls.ChoiceKeys(Sides.Length)}";
            }
            case CraftHandsOnMode.Sharpen:
            {
                CraftAbrasive s = Abrasive;
                return $"Stone: {On(s != null ? s.Name : "none")} ({inHand} of {abrasives.Count}) - {next}" +
                       $"   Face: {On(Num(leftFace ? 0 : 1) + (leftFace ? "the left" : "the right"))} - {turn} or {StationControls.ChoiceKeys(2)}" +
                       $"\nPress: {On($"{Pressure(0.45f) * 100:0}%")} - {adjust}";
            }
            case CraftHandsOnMode.Quench:
                return $"Into the tank: {On(Num(FlatEntry ? 1 : 0) + (FlatEntry ? "flat (it warps)" : "upright, point first"))} - {StationControls.ChoiceKeys(2)}" +
                       $"\nHow much of it goes in: {On(TankDepth >= 0.97f ? "all of it" : $"the far {TankDepth * 100:0}%")} - {adjust}";
            case CraftHandsOnMode.Brush:
            {
                CraftPiece lump = Piece;
                int stones = lump != null ? lump.ore.fragments.Count : 0;
                return $"Brush: {On(BrushNames[(int)oreBrush])} (soft, stiff, wire) - {next}   Stone: {On($"{Num(rock.Fragment)}{rock.Fragment + 1}")} of {stones} - {StationControls.ChoiceKeys(stones)}" +
                       $"\nTurn it in the hand: hold {StationControls.K(StationControl.Quick)} and move, {adjust} spins it, {turn} turns it over";
            }
            case CraftHandsOnMode.Carve:
                return $"Cut: {On(Num(carveCut) + CutNames[carveCut])} ({carveCut + 1} of {CutNames.Length}) - {StationControls.ChoiceKeys(CutNames.Length)} or {next}" +
                       $"\nThe knife: {On($"{Knife * 100:0}%")} - it bites about {BiteDepth(Knife, 0.5f):0.0} mm in a half-second stroke - {adjust}";
            case CraftHandsOnMode.Bellows:
            {
                CraftFuel f = !string.IsNullOrEmpty(setup.fuel) ? station.Rules.Fuel(setup.fuel) : null;
                return $"Fuel to add: {On(f?.Name ?? "none chosen (choose it in the window)")} - {StationControls.K(StationControl.FeedFire)}" +
                       $"   Air vent: {On($"{Vent * 100:0}% open")} - {adjust}";
            }
            default:
                return $"Mould: {On(station.Rules.Mold(setup.mold)?.Name ?? setup.mold)}   From the {On(setup.fromFurnace ? "furnace" : "crucible")}";
        }
    }

    /// <summary>How the work place is worked, with the keys actually bound (they follow a rebind and the device in use).</summary>
    private string Hint()
    {
        string work = StationControls.K(StationControl.Work), down = StationControls.K(StationControl.PutDown);
        string next = StationControls.K(StationControl.NextTool), quick = StationControls.K(StationControl.Quick), fire = StationControls.K(StationControl.Fire);
        string adjust = StationControls.AdjustKeys, turn = StationControls.K(StationControl.TurnOver);
        switch (mode)
        {
            case CraftHandsOnMode.Hammer:
                return $"Aim along the bar · hold {work} to raise the hammer, release to strike · while holding, move well up or down to tilt the face (a bevel) · " +
                       $"{StationControls.ChoiceKeys(10)} or {next}: tool · over the horn {adjust}: which way it bends · {StationControls.K(StationControl.FaceEdge)}: face/edge · " +
                       $"{turn}: turn over · {fire}: fire · {down}: put it down";
            case CraftHandsOnMode.Bellows:
                return $"Hold {work} and pull down to squeeze, up to fill (or {quick}) · a steady rhythm builds the draft · {fire}: piece in/out of the coals · " +
                       $"{turn}: turn it over · {StationControls.K(StationControl.FeedFire)}: fuel · {adjust}: air vent · {down}: let go";
            case CraftHandsOnMode.Grind:
                return $"Hold {work} to press the blade to the wheel · sideways: along the blade · higher: harder · {adjust}: angle · " +
                       $"{StationControls.ChoiceKeys(Sides.Length)}: side · {next}: tool · {quick}: dip it to cool · {down}: put it down";
            case CraftHandsOnMode.Sharpen:
                return $"Hold {work} and draw the blade across the stone in long strokes · the hand's height is the angle - keep it steady · {adjust}: press · " +
                       $"{turn}: the other face · {next}: the stone · {down}: put it down";
            case CraftHandsOnMode.Quench:
                return $"Hold {work} to plunge it and keep it moving: up and down along it, or round; side to side warps it; held still it stays soft · release to lift it out · " +
                       $"{StationControls.ChoiceKeys(2)}: upright or flat · {adjust}: how much goes in · {down}: step back";
            case CraftHandsOnMode.Brush:
                return $"Hold {work} and scrub back and forth over the stone · hold {quick} and move to turn it, {adjust} spins it, {turn} turns it over · {next}: brush · " +
                       $"soft takes loose dirt, stiff caked clay, wire the weathered rind (and a little ore) · {down}: put it down";
            case CraftHandsOnMode.Carve:
                return $"Hold {work} and draw the knife along the blank (where you start and stop is what is cut) - keep the hand level · {adjust}: the knife (too deep splits it) · " +
                       $"bore: hold {work} and circle the hand, round and even (the bit is at the end nearer the hand) · {StationControls.ChoiceKeys(4)} or {next}: the cut · {down}: put it down";
            default:
                return $"Carry the crucible sideways over the cup · hold {work} and raise the hand to tip it · keep the stream steady and unbroken · let go to tip it back as soon as the mould is full (kept tipped, it runs over) · {down}: put it down";
        }
    }
}
