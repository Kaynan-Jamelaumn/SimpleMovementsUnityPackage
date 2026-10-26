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
    public float angle, pressure, depth, entryAngle;
    public string mold, fuel;
    public bool fromFurnace;
    /// <summary>The ore bench: the stone to brush first and the brush in hand.</summary>
    public int fragment;
    public CraftOreBrush brush;
}

/// <summary>
/// A crafting station worked with the hands, in 3D, instead of on the window's drawing: the window steps aside, the
/// view moves to the work place and the mouse becomes the hand. Put Down (right-click or Q) puts the tool down and
/// brings the window back; Escape goes back to the game. Every key is an input action of the station map
/// (<see cref="StationControls"/>): rebindable, and the hints show the key bound; the keys below are the defaults. The work goes on as it is, and the tool can be taken up again at any time.
/// <list type="bullet">
/// <item><b>Anvil</b>: the piece lies on the anvil as it really is (its shape segment by segment, glowing at each
/// segment's heat). Move the mouse along it to aim; hold the left button to raise the hammer (the longer, the harder,
/// until the arm wavers), move the mouse up or down while holding to tilt the face (a bevel), release to strike. 1-0
/// or the wheel choose the tool, F strikes on the edge or the face, T turns the piece over, R puts it in the fire and
/// takes it out.</item>
/// <item><b>Bellows</b>: hold the left button and pull the mouse down to squeeze them, up to let them fill (or tap
/// Space); a steady rhythm builds the draft. R puts the piece in the coals and takes it out, F feeds the fire, [ and ]
/// close and open the air vent.</item>
/// <item><b>Wheel</b> (or file, brush, polish): hold the left button to press the blade to the spinning wheel, move
/// the mouse sideways to slide it along, up for a harder press; the wheel turns the angle. 1-7 choose the side, Tab the
/// tool, Space dips the blade in the tank before it gets too hot to hold.</item>
/// <item><b>Whetstone</b>: hold the left button and draw the blade across the stone; the height of the mouse is the
/// angle you hold, the steadier the keener. Tab changes the face, the wheel the stone.</item>
/// <item><b>Quench</b>: hold the left button to plunge the hot piece into the tank and keep it moving - up and down,
/// side to side or round - and release to lift it out. 1-3: edge first, point first, flat.</item>
/// <item><b>Crucible</b>: hold the left button and raise the mouse to tip the crucible over the mould; keep the stream
/// steady and unbroken until the mould is full (a stop and a restart leaves a cold shut).</item>
/// <item><b>Ore bench</b>: a stone of the lump sits on the bench as it is - ore in veins through the rock, a dull
/// weathered rind, dirt and caked clay. Hold the left button and scrub back and forth over it with the brush; hold
/// Space and move the mouse to turn it in the hand, the wheel spins it, T turns it over. 1-3 (or Tab) choose the soft,
/// stiff or wire brush; F puts the next stone of the lump on the bench.</item>
/// </list>
/// Presentation and input only: what reaches the workshop goes through <see cref="CraftStation.Request"/> as the
/// window's requests do, measured by the same hand-skill models (<see cref="CraftHammer"/>, <see cref="CraftStroke"/>,
/// <see cref="CraftPourControl"/>, <see cref="CraftBellowsPump"/>) - so it works the same offline and in a network
/// session, where the server checks it.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Crafting/Craft Hands On")]
public class CraftHandsOn : MonoBehaviour
{
    [Header("View")]
    [SerializeField, Range(20f, 90f)] private float fieldOfView = 48f;
    [Tooltip("How far from the work place the view stands (metres).")]
    [SerializeField, Range(0.3f, 3f)] private float viewDistance = 0.85f;

    private static readonly CraftStrikeTool[] ToolOrder =
    {
        CraftStrikeTool.Flat, CraftStrikeTool.PeenAcross, CraftStrikeTool.PeenAlong, CraftStrikeTool.Fuller, CraftStrikeTool.Horn,
        CraftStrikeTool.Hardy, CraftStrikeTool.Drift, CraftStrikeTool.Flatter, CraftStrikeTool.Dishing, CraftStrikeTool.Straighten,
    };
    private static readonly CraftSurfaceSide[] Sides =
        { CraftSurfaceSide.LeftBevel, CraftSurfaceSide.RightBevel, CraftSurfaceSide.Faces, CraftSurfaceSide.EdgeLine, CraftSurfaceSide.Spine, CraftSurfaceSide.Tang, CraftSurfaceSide.Eye };
    private static readonly string[] SideNames = { "left bevel", "right bevel", "faces", "edge line", "spine", "tang", "eye" };

    private CraftStation station;
    private CraftStationVisuals visuals;
    private CombatEntity player;
    private CraftHands hands;
    private CraftHandsOnMode mode;
    private CraftHandsOnSetup setup;
    private Action<bool> onEnd;

    // The view and the HUD.
    private Camera cam;
    private bool cursorWasVisible;
    private CursorLockMode cursorWasLocked;
    private Canvas hud;
    private TextMeshProUGUI hudTitle, hudFeel, hudHint, hudLegend;
    private Image meterFill, armsFill;
    private TextMeshProUGUI meterLabel;
    private float shake, hintTimer;

    // The work place: a frame at the tool's place (x along the station, y up, z toward the player).
    private Transform root;
    private Transform pieceObject;
    private MeshFilter pieceFilter;
    private MeshRenderer pieceRenderer;
    private string pieceShape;
    private float pieceTimer;
    private float pieceLength = 0.2f, pieceWidth = 0.03f;

    // Pointer.
    private Vector2 pressAt;
    private bool held, wasHeld;

    // Anvil.
    private readonly CraftHammer hammer = new CraftHammer();
    private Transform hammerObject;
    private float aim = 0.5f, tilt, swing = -1f, flipShown;
    private float slipShown;
    private CraftBlow pendingBlow;
    private string lastBlow = "";

    // Bellows.
    private readonly CraftBellowsPump pump = new CraftBellowsPump();
    private Transform bellowsTop;
    private float opening = 1f;
    private bool squeezing;

    // Wheel and stone.
    private readonly List<CraftAbrasive> abrasives = new List<CraftAbrasive>();
    private int abrasive;
    private readonly CraftStroke stroke = new CraftStroke();
    private float sendTimer, dipTimer = -1f;
    private Transform wheelObject;
    private float wheelTurn;
    private bool leftFace = true;

    // Quench.
    private readonly Queue<Vector2> moves = new Queue<Vector2>();
    private CraftQuenchMotion sentMotion = CraftQuenchMotion.Still;
    private float motionTimer, quenchDepth;
    private bool plunged;

    // Carving.
    private static readonly string[] CutNames = { "shave the width", "shave the thickness", "round it", "bore the tang slot" };
    private int carveCut;
    private float carveFrom, carveStart, carveMinY, carveMaxY;
    private Transform knifeObject;

    // Ore bench: the stone in 3D, turned in the hand, and the brush.
    private static readonly string[] BrushNames = { "soft brush", "stiff brush", "wire brush" };
    private readonly CraftOreRock rock = new CraftOreRock();
    private Transform rockObject, brushObject;
    private MeshFilter rockFilter;
    private Material rockMaterial;
    private CraftOreBrush oreBrush;
    private Quaternion rockTurn = Quaternion.identity, rockShown = Quaternion.identity;
    private Vector2 lastPointer;
    private float brushCover, brushSeconds, brushTired, scrubPhase;
    private readonly CraftSwirl boreSwirl = new CraftSwirl();
    private readonly List<(Transform t, Vector3 v, float life)> chips = new List<(Transform, Vector3, float)>();

    // Crucible.
    private CraftPourControl pour;
    private Transform crucibleObject, streamObject;
    // The mould's cavity in the shape of what it casts, and the metal filling it cell by cell from the mouth.
    private readonly List<Transform> fillCells = new List<Transform>();
    private float pourTilt;
    // Aiming: the hand carries the crucible left and right (the pointer's x); the stream must land in the pouring cup.
    private const float PourBaseX = -0.09f, CupRadius = 0.016f;
    private float pourAim, pourDrift, pourClock, pourMouthX, pourLandX;
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

    private CraftWorkshop Ws => station != null ? station.Workshop : null;
    private CraftPiece Piece => Ws?.Piece(setup.piece);

    // ------------------------------------------------------------------ starting and stopping
    /// <summary>
    /// Takes up <paramref name="what"/> for <paramref name="who"/> with the window's <paramref name="hand"/> (their
    /// tremble and fatigue carry over). <paramref name="ended"/> is called when it is put down (true = back to the
    /// window). False (and why) when it cannot be used now.
    /// </summary>
    public bool Begin(CraftHandsOnMode what, CombatEntity who, CraftHands hand, CraftHandsOnSetup with, Action<bool> ended, out string reason)
    {
        reason = "";
        station = GetComponent<CraftStation>();
        if (station == null || who == null || what == CraftHandsOnMode.None || station.Workshop == null)
            return false;
        if (Active)
            End(false, false);
        setup = with;
        CraftPiece p = station.Workshop.Piece(with.piece);
        bool metal = p != null && (p.kind == CraftPieceKind.Metal || p.kind == CraftPieceKind.Wood) && p.segments.Count > 0;
        switch (what)
        {
            case CraftHandsOnMode.Hammer:
                if (!station.Has(CraftTools.Anvil)) { reason = "There is no anvil here."; return false; }
                if (!metal) { reason = "Pick a piece of metal to forge."; return false; }
                break;
            case CraftHandsOnMode.Bellows:
                if (!station.Has(CraftTools.Forge)) { reason = "There is no forge here."; return false; }
                break;
            case CraftHandsOnMode.Grind:
            case CraftHandsOnMode.Sharpen:
                if (!metal) { reason = "Pick a blade to work."; return false; }
                abrasives.Clear();
                foreach (CraftAbrasive a in station.Rules.Abrasives)
                {
                    bool stone = a.kind == CraftAbrasiveKind.Whetstone;
                    if (what == CraftHandsOnMode.Sharpen ? !stone : stone) continue;
                    if (a.kind == CraftAbrasiveKind.Wheel && !station.Has(CraftTools.Grinder)) continue;
                    if ((a.kind == CraftAbrasiveKind.File || a.kind == CraftAbrasiveKind.Brush) && !station.Has(CraftTools.Files)) continue;
                    if (a.kind == CraftAbrasiveKind.Polish && !station.Has(CraftTools.Polishing)) continue;
                    if (stone && !station.Has(CraftTools.Whetstone)) continue;
                    abrasives.Add(a);
                }
                if (abrasives.Count == 0) { reason = what == CraftHandsOnMode.Sharpen ? "There are no whetstones here." : "There is no wheel or file here."; return false; }
                abrasive = Mathf.Max(0, abrasives.FindIndex(a => a.id == with.abrasive));
                break;
            case CraftHandsOnMode.Quench:
                if (!station.Has(CraftTools.QuenchTank)) { reason = "There is no quench tank here."; return false; }
                if (!metal) { reason = "Pick a hot piece to quench."; return false; }
                break;
            case CraftHandsOnMode.Carve:
                if (!station.Has(CraftTools.Files)) { reason = "There is no carving knife here."; return false; }
                if (p == null || p.kind != CraftPieceKind.Wood || p.segments.Count == 0) { reason = "Pick a wooden blank to carve."; return false; }
                break;
            case CraftHandsOnMode.Brush:
                if (!station.Has(CraftTools.OreBench)) { reason = "There are no brushes here."; return false; }
                if (p == null || p.kind != CraftPieceKind.Ore || p.ore.fragments.Count == 0) { reason = "Pick a lump of ore to clean."; return false; }
                if (p.location != CraftLocation.Air) { reason = "Take it out first."; return false; }
                break;
            case CraftHandsOnMode.Pour:
                if (string.IsNullOrEmpty(with.mold)) { reason = "Choose a mould first."; return false; }
                if (with.fromFurnace ? station.Workshop.Furnace.PoolMass <= 1 : station.Workshop.Crucible.Mass <= 1) { reason = "There is no liquid metal to pour."; return false; }
                break;
        }
        visuals = station.GetComponentInChildren<CraftStationVisuals>();
        player = who;
        hands = hand ?? new CraftHands(1);
        mode = what;
        onEnd = ended;
        held = wasHeld = false;
        swing = -1f;
        lastBlow = "";
        stroke.Clear();
        moves.Clear();
        sentMotion = CraftQuenchMotion.Still;
        plunged = false;
        pour = null;
        pourTilt = 0f;
        aimOn = aimOff = null;
        opening = 1f;
        pieceShape = null;
        leftFace = with.side != CraftSurfaceSide.RightBevel;
        flipShown = p != null && p.faceUp == 1 ? 180f : 0f;

        BuildPlace();
        // The view: a camera of its own over the work place, gliding there from the player's.
        Camera main = Camera.main;
        var go = new GameObject("Craft Hands-On View");
        cam = go.AddComponent<Camera>();
        if (main != null)
        {
            cam.CopyFrom(main);
            cam.depth = main.depth + 5f;
            go.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
        }
        cam.fieldOfView = fieldOfView;
        cam.nearClipPlane = 0.02f;
        TargetView(out Vector3 vp, out Quaternion vr);
        if (main == null) go.transform.SetPositionAndRotation(vp, vr);
        cursorWasVisible = Cursor.visible;
        cursorWasLocked = Cursor.lockState;
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = what == CraftHandsOnMode.Pour;
        StationControls.BeginHandsOn(this); // the gameplay keys go off while the hands are at work
        BuildHud();
        hintTimer = 0f;
        return true;
    }

    /// <summary>Puts the tool down: back to the window (<paramref name="backToWindow"/>) or to the game.</summary>
    public void End(bool backToWindow, bool notify = true)
    {
        if (!Active)
            return;
        CraftHandsOnMode was = mode;
        mode = CraftHandsOnMode.None;
        if (was == CraftHandsOnMode.Grind && stroke.Samples > 0) FlushGrind(); // the last pass counts
        if (was == CraftHandsOnMode.Pour && pour != null) FinishPour();
        if (was == CraftHandsOnMode.Brush) FlushBrush(); // the last scrubbing counts
        if (was == CraftHandsOnMode.Quench && plunged) LiftOut();
        hammer.Lower();
        StationControls.EndHandsOn(this);
        if (pieceFilter != null && pieceFilter.sharedMesh != null) Destroy(pieceFilter.sharedMesh); // built here, not shared
        pieceFilter = null;
        chips.Clear(); // the shavings go with the work place
        fillCells.Clear();
        rockObject = brushObject = null;
        rockFilter = null;
        if (rock.Mesh != null) Destroy(rock.Mesh);
        if (rockMaterial != null) Destroy(rockMaterial);
        rockMaterial = null;
        knifeObject = null;
        if (root != null) Destroy(root.gameObject);
        root = null;
        pieceObject = null;
        if (cam != null) Destroy(cam.gameObject);
        cam = null;
        if (hud != null) Destroy(hud.gameObject);
        hud = null;
        Cursor.lockState = backToWindow ? CursorLockMode.None : cursorWasLocked;
        Cursor.visible = backToWindow || cursorWasVisible;
        Action<bool> callback = onEnd;
        onEnd = null;
        if (notify) callback?.Invoke(backToWindow);
    }

    private void OnDisable() => End(false);

    // ------------------------------------------------------------------ every frame
    private void Update()
    {
        if (!Active)
            return;
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
        held = StationControls.Held(StationControl.Work);
        if (held && !wasHeld) pressAt = pointer;
        float scroll = StationControls.Scroll;
        hands.Rest(dt);
        switch (mode)
        {
            case CraftHandsOnMode.Hammer: UpdateHammer(dt, pointer, scroll); break;
            case CraftHandsOnMode.Bellows: UpdateBellows(dt, pointer); break;
            case CraftHandsOnMode.Grind: UpdateGrind(dt, pointer, scroll); break;
            case CraftHandsOnMode.Sharpen: UpdateSharpen(pointer, scroll); break;
            case CraftHandsOnMode.Quench: UpdateQuench(dt, pointer); break;
            case CraftHandsOnMode.Pour: UpdatePour(dt, pointer); break;
            case CraftHandsOnMode.Brush: UpdateBrush(dt, pointer, scroll); break;
            case CraftHandsOnMode.Carve: UpdateCarve(dt, pointer); break;
        }
        if (!Active)
            return;
        string arm = hands.Feeling;
        if (arm.Length > 0 && hudFeel != null) hudFeel.text += $"\n<color=#ffb070>{arm}</color>";
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
        if (armsFill != null)
        {
            float f = (float)hands.fatigue;
            armsFill.rectTransform.anchorMax = new Vector2(1f - f, 1f);
            armsFill.color = f < 0.5f ? UITheme.Good : f < 0.8f ? UITheme.Gold : UITheme.Bad;
        }
    }

    // ------------------------------------------------------------------ the anvil
    private void UpdateHammer(float dt, Vector2 pointer, float scroll)
    {
        CraftPiece p = Piece;
        if (p == null)
        {
            End(true);
            return;
        }
        hammer.Tick(dt);
        int choice = StationControls.ChoicePressed(ToolOrder.Length);
        if (choice >= 0) { setup.tool = ToolOrder[choice]; hintTimer = 0f; }
        if (StationControls.Pressed(StationControl.FaceEdge)) { setup.face = setup.face == CraftStrikeFace.Flat ? CraftStrikeFace.Edge : CraftStrikeFace.Flat; hintTimer = 0f; }
        if (StationControls.Pressed(StationControl.TurnOver) && Send(new CraftRequest(CraftRequestKind.Turn, p.id))) hintTimer = 0f;
        if (StationControls.Pressed(StationControl.Fire)) ToFireAndBack(p);
        if (Mathf.Abs(scroll) > 0.01f || StationControls.Pressed(StationControl.NextTool))
        {
            int i = Array.IndexOf(ToolOrder, setup.tool);
            setup.tool = ToolOrder[(i + (scroll >= 0 ? 1 : ToolOrder.Length - 1)) % ToolOrder.Length];
            hintTimer = 0f;
        }
        bool onAnvil = p.location == CraftLocation.Air;
        if (PointOnPlane(pointer, 0f, out Vector3 local))
            aim = Mathf.Clamp01(local.x / Mathf.Max(0.01f, pieceLength) + 0.5f);
        if (held)
            tilt = Mathf.Clamp((pointer.y - pressAt.y) / (Screen.height * 0.12f), -1f, 1f);
        else if (!hammer.Raised)
            tilt = Mathf.MoveTowards(tilt, 0f, dt * 3f);
        if (onAnvil && held && !wasHeld && swing < 0f)
            hammer.Raise();
        if (hammer.Raised && !held)
        {
            pendingBlow = hammer.Release(aim, tilt, hands);
            swing = 0f; // the hammer falls; the blow lands when it meets the work
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
            float x = (aim - 0.5f) * pieceLength;
            hammerObject.localPosition = new Vector3(x, rest + lift, 0f);
            float waver = hammer.Raised && hammer.Held > hammer.fullCharge + hammer.holdBeforeWaver ? Mathf.Sin(Time.unscaledTime * 22f) * 3f : 0f;
            // A tired arm trembles while it holds the hammer up; a slipped blow glances off and twists the hammer aside.
            if (hammer.Raised && hands.fatigue > 0.5) waver += Mathf.Sin(Time.unscaledTime * 31f) * 4f * (float)(hands.fatigue - 0.5);
            slipShown = Mathf.Max(0f, slipShown - dt * 2.5f);
            hammerObject.localRotation = Quaternion.Euler(tilt * 22f + waver + lift * 120f, 0f, slipShown * 28f); // cocked back as it rises, the face tilted for a bevel
            hammerObject.gameObject.SetActive(onAnvil);
        }
        if (pieceObject != null)
        {
            pieceObject.gameObject.SetActive(onAnvil);
            // Turning it over: it rolls over about its length (the edge goes to the other side, the other face comes up).
            flipShown = Mathf.MoveTowards(flipShown, p.faceUp == 1 ? 180f : 0f, dt * 540f);
            float roll = Mathf.Sin(flipShown * Mathf.Deg2Rad);
            pieceObject.localPosition = new Vector3(0f, PieceTop(p) * 0.5f + roll * pieceWidth * 0.6f, 0f);
            pieceObject.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.right) * Quaternion.Euler(0f, flipShown, 0f);
        }
        float shown = hammer.Raised ? (float)hammer.Charge : swing >= 0f ? (float)pendingBlow.force : 0f;
        SetMeter("Blow", shown, shown > 1.0f ? UITheme.Bad : UITheme.Gold);
        hudTitle.text = $"Anvil · {CraftForging.ToolName(setup.tool)}{(setup.face == CraftStrikeFace.Edge ? " · on its edge" : "")} · {(p.faceUp == 0 ? "left" : "right")} face up";
        hudFeel.text = onAnvil ? $"{HeatWords(p)}. {lastBlow}" : p.location == CraftLocation.Fire ? $"It is in the fire. {StationControls.K(StationControl.Fire)}: take it out." : "It is not on the anvil.";
    }

    private void Strike(CraftPiece p, CraftBlow blow)
    {
        if (!Send(new CraftRequest(CraftRequestKind.Strike, p.id) { a = (float)blow.position, i0 = (int)setup.tool, i1 = (int)setup.face, b = (float)blow.force, c = (float)blow.tilt }))
            return;
        shake = Mathf.Clamp01((float)blow.force);
        if (blow.slipped) slipShown = 1f;
        lastBlow = blow.Words;
        // Offline the workshop answers at once (in a session the sound comes with the server's answer).
        IReadOnlyList<CraftEvent> recent = Ws.RecentEvents;
        if (recent.Count > 0 && recent[recent.Count - 1].Kind == CraftEventKind.Struck && recent[recent.Count - 1].Piece == p.id)
            lastBlow += " " + CraftLook.StrikeSound(recent[recent.Count - 1].Id);
    }

    private void ToFireAndBack(CraftPiece p)
    {
        if (p.location == CraftLocation.Fire)
            Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Air });
        else if (station.Has(CraftTools.Forge))
            Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Fire, a = setup.depth > 0f ? setup.depth : 0.7f });
    }

    // ------------------------------------------------------------------ the bellows
    private void UpdateBellows(float dt, Vector2 pointer)
    {
        pump.Tick(dt);
        CraftPiece p = Piece;
        if (StationControls.Pressed(StationControl.Fire) && p != null) ToFireAndBack(p);
        if (StationControls.Pressed(StationControl.FeedFire) && !string.IsNullOrEmpty(setup.fuel))
            Send(new CraftRequest(CraftRequestKind.AddFuel) { id = setup.fuel, count = 1 });
        if (StationControls.Pressed(StationControl.VentClose)) Send(new CraftRequest(CraftRequestKind.Damper) { a = (float)Math.Max(0, Ws.Forge.damper - 0.2) });
        if (StationControls.Pressed(StationControl.VentOpen)) Send(new CraftRequest(CraftRequestKind.Damper) { a = (float)Math.Min(1, Ws.Forge.damper + 0.2) });
        if (StationControls.Pressed(StationControl.Quick)) { opening = 0.15f; Puff(); }
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
            pieceObject.gameObject.SetActive(p != null && p.location == CraftLocation.Fire);
        CraftFireState f = Ws.Forge;
        SetMeter("Draft", (float)pump.Strength, UITheme.Gold);
        hudTitle.text = "The forge · the bellows";
        string fire = !f.lit ? "The fire is out" : f.level < 0.25 ? "The coals smoulder" : f.level < 0.6 ? "The fire burns steadily" : "The fire roars";
        string fuel = f.fuel < 1 ? ", the fuel is nearly gone" : f.fuel < 3 ? ", little fuel left" : "";
        hudFeel.text = $"{fire}{fuel}. {(p != null ? (p.location == CraftLocation.Fire ? $"The piece is in the coals: {HeatWords(p).ToLowerInvariant()}." : $"{StationControls.K(StationControl.Fire)}: put the piece in the coals.") : "")}";
    }

    private void Puff()
    {
        float s = (float)pump.Pump(hands);
        Send(new CraftRequest(CraftRequestKind.Bellows) { a = s });
        visuals?.Puff();
    }

    // ------------------------------------------------------------------ the wheel, the file, the brush
    private void UpdateGrind(float dt, Vector2 pointer, float scroll)
    {
        CraftPiece p = Piece;
        if (p == null)
        {
            End(true);
            return;
        }
        CraftAbrasive a = abrasives[Mathf.Clamp(abrasive, 0, abrasives.Count - 1)];
        int side = StationControls.ChoicePressed(Sides.Length);
        if (side >= 0) { FlushGrind(); setup.side = Sides[side]; hintTimer = 0f; }
        if (StationControls.Pressed(StationControl.NextTool)) { FlushGrind(); abrasive = (abrasive + 1) % abrasives.Count; RebuildTool(); hintTimer = 0f; }
        if (StationControls.Pressed(StationControl.Quick)) Dip(p);
        if (Mathf.Abs(scroll) > 0.01f)
            setup.angle = Mathf.Clamp((setup.angle > 0f ? setup.angle : 15f) + Mathf.Sign(scroll) * 1f, 5f, 45f);
        float angle = setup.angle > 0f ? setup.angle : 15f;
        if (dipTimer > 0f)
        {
            dipTimer -= dt;
            if (dipTimer <= 0f) Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Air });
        }
        bool free = p.location == CraftLocation.Air || p.location == CraftLocation.Assembled;
        // Where along the blade the wheel bites, and how hard the hand presses (higher on the screen = harder).
        float along = Mathf.Clamp01(pointer.x / Mathf.Max(1f, Screen.width));
        float press = Mathf.Clamp01(0.15f + 0.85f * pointer.y / Mathf.Max(1f, Screen.height));
        bool touching = held && free;
        if (touching)
        {
            stroke.Add(Time.unscaledTime, along, 0.5, angle, press, hands);
            sendTimer += dt;
            if (sendTimer >= 0.2f) FlushGrind();
        }
        else if (wasHeld && stroke.Samples > 0)
            FlushGrind();
        bool wheel = a.kind == CraftAbrasiveKind.Wheel;
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
            pieceObject.gameObject.SetActive(free);
            // The side decides how the blade is presented (length along -x, so the first segment is to the right, the
            // bitten point at x = 0): a bevel edge-on and tilted to the angle (one way for the left face, the other for
            // the right), the edge line straight in, a face flat to the rim, the spine first.
            float x = (along - 0.5f) * pieceLength;
            float gap = touching ? -0.002f - 0.004f * press : 0.03f;
            PresentToWheel(setup.side, angle, out Quaternion turn, out float reach);
            float z = wheel ? WheelFront + reach + gap : 0.02f;
            pieceObject.localPosition = new Vector3(x, wheel ? 0.12f : 0.135f, z);
            pieceObject.localRotation = turn;
        }
        if (touching && wheel && p.kind == CraftPieceKind.Metal && UnityEngine.Random.value < 0.6f && root != null)
            visuals?.GrindSparks(root.TransformPoint(new Vector3(0f, 0.11f, WheelFront)), 3 + (int)(press * 6));
        SetMeter("Pressure", touching ? press : 0f, UITheme.Gold);
        hudTitle.text = $"{a.Name} · the {SideNames[Mathf.Max(0, Array.IndexOf(Sides, setup.side))]} · {angle:0}°";
        hudFeel.text = free ? $"{HandHeat(p)}{(touching && wheel ? " Sparks stream off the wheel." : "")}" : p.location == CraftLocation.Quench ? "Cooling in the tank..." : "It is not in your hands.";
    }

    private const float WheelFront = 0.09f; // the wheel's rim nearest the player (its centre at z = -0.02, radius 0.11)

    /// <summary>How the blade is held to the wheel for <paramref name="side"/>, and how far it reaches from the rim.</summary>
    private void PresentToWheel(CraftSurfaceSide side, float angle, out Quaternion turn, out float reach)
    {
        Quaternion edgeIn = Quaternion.LookRotation(Vector3.up, -Vector3.right); // the edge toward the wheel, faces up and down
        Quaternion spineIn = Quaternion.LookRotation(Vector3.up, Vector3.right);  // the spine toward the wheel
        float thick = Mathf.Max(0.002f, PieceTop(Piece));
        switch (side)
        {
            case CraftSurfaceSide.LeftBevel: turn = Quaternion.Euler(angle, 0f, 0f) * edgeIn; reach = pieceWidth * 0.5f; break;
            case CraftSurfaceSide.RightBevel: turn = Quaternion.Euler(-angle, 0f, 0f) * edgeIn; reach = pieceWidth * 0.5f; break;
            case CraftSurfaceSide.Faces: turn = Quaternion.Euler(-90f, 0f, 0f) * edgeIn; reach = thick * 0.5f; break;
            case CraftSurfaceSide.Spine: turn = spineIn; reach = pieceWidth * 0.5f; break;
            default: turn = edgeIn; reach = pieceWidth * 0.5f; break; // the edge line, the tang, the eye: straight in
        }
    }

    private void FlushGrind()
    {
        CraftPiece p = Piece;
        float window = Mathf.Max(0.05f, sendTimer);
        sendTimer = 0f;
        if (p == null || stroke.Samples < 1 || abrasives.Count == 0)
        {
            stroke.Clear();
            return;
        }
        stroke.Span(p.segments.Count, out int from, out int to);
        double moved = Math.Abs(stroke.To - stroke.From) * p.Length;
        double segLength = p.Length / Math.Max(1, p.segments.Count);
        double dwell = window * segLength / Math.Max(segLength, moved);
        CraftAbrasive a = abrasives[Mathf.Clamp(abrasive, 0, abrasives.Count - 1)];
        float press = (float)stroke.Pressure;
        // d = 1: by hand, at the full pace of the hands (the window's careful work is slower).
        var r = new CraftRequest(CraftRequestKind.Grind, p.id) { i0 = from, i1 = to, i2 = (int)setup.side, id = a.id, a = (float)stroke.Angle, b = press, c = (float)dwell, d = 1f };
        switch (a.kind)
        {
            case CraftAbrasiveKind.File: r.kind = CraftRequestKind.File; break;
            case CraftAbrasiveKind.Brush: r = new CraftRequest(CraftRequestKind.Descale, p.id) { i0 = from, i1 = to, id = a.id, a = press, b = (float)dwell, d = 1f }; break;
            case CraftAbrasiveKind.Polish: r = new CraftRequest(CraftRequestKind.Polish, p.id) { i0 = from, i1 = to, id = a.id, a = press, b = (float)dwell, d = 1f }; break;
        }
        Send(r);
        stroke.Clear();
    }

    private void Dip(CraftPiece p)
    {
        if (!station.Has(CraftTools.QuenchTank))
        {
            station.ShowLocalMessage("There is no water here to dip it in.");
            return;
        }
        FlushGrind();
        if (Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Quench, a = 1f }))
            dipTimer = 1.2f;
    }

    // ------------------------------------------------------------------ the whetstone
    private void UpdateSharpen(Vector2 pointer, float scroll)
    {
        CraftPiece p = Piece;
        if (p == null)
        {
            End(true);
            return;
        }
        if (StationControls.Pressed(StationControl.NextTool)) { leftFace = !leftFace; hintTimer = 0f; }
        if (Mathf.Abs(scroll) > 0.01f)
        {
            abrasive = (abrasive + (scroll > 0 ? 1 : abrasives.Count - 1)) % abrasives.Count;
            RebuildTool();
            hintTimer = 0f;
        }
        CraftAbrasive s = abrasives[Mathf.Clamp(abrasive, 0, abrasives.Count - 1)];
        float along = Mathf.Clamp01(pointer.x / Mathf.Max(1f, Screen.width));
        float angle = Mathf.Lerp(5f, 35f, Mathf.Clamp01(pointer.y / Mathf.Max(1f, Screen.height)));
        float pressure = setup.pressure > 0f ? setup.pressure : 0.45f;
        bool free = p.location == CraftLocation.Air || p.location == CraftLocation.Assembled;
        if (held && free)
            stroke.Add(Time.unscaledTime, along, leftFace ? along : 1f - along, angle, pressure, hands);
        else if (wasHeld && stroke.Samples > 2)
        {
            EdgeSpan(p, out int a, out int b);
            Send(new CraftRequest(CraftRequestKind.Sharpen, p.id)
            {
                i0 = a, i1 = b, i2 = (int)(leftFace ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel), id = s.id,
                a = (float)stroke.Angle, b = (float)stroke.Pressure, count = Math.Max(1, stroke.Passes), c = (float)stroke.Wobble, flag = stroke.EdgeLeading,
            });
            lastBlow = $"{stroke.Passes} stroke{(stroke.Passes == 1 ? "" : "s")} at about {stroke.Angle:0}°{(stroke.Wobble > 3 ? ", rocking" : stroke.Wobble > 1.2 ? ", a little unsteady" : ", steady")}{(stroke.EdgeLeading ? ", edge leading" : ", edge trailing")}.";
            stroke.Clear();
        }
        else if (!held)
            stroke.Clear();
        if (pieceObject != null)
        {
            pieceObject.gameObject.SetActive(free);
            float x = (along - 0.5f) * 0.22f;
            pieceObject.localPosition = new Vector3(x, 0.07f + Mathf.Sin(angle * Mathf.Deg2Rad) * 0.02f, 0.02f);
            pieceObject.localRotation = Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler((leftFace ? 1f : -1f) * angle, 0f, 0f) * Quaternion.LookRotation(Vector3.up, Vector3.right);
        }
        SetMeter("Angle", (angle - 5f) / 30f, UITheme.Gold);
        hudTitle.text = $"{s.Name} · the {(leftFace ? "left" : "right")} face · about {angle:0}°";
        hudFeel.text = free ? $"{EdgeWords(p)} {lastBlow}" : "It is not in your hands.";
    }

    private static void EdgeSpan(CraftPiece p, out int a, out int b)
    {
        a = p.segments.Count;
        b = -1;
        for (int i = 0; i < p.segments.Count; i++)
            if (p.segments[i].Beveled) { a = Mathf.Min(a, i); b = Mathf.Max(b, i); }
        if (b < 0) { a = 0; b = p.segments.Count - 1; }
    }

    private static string EdgeWords(CraftPiece p)
    {
        EdgeSpan(p, out int a, out int b);
        double apex = 0, burr = 0;
        int n = 0;
        for (int i = a; i <= b && i < p.segments.Count; i++)
        {
            apex += p.segments[i].apex;
            burr = Math.Max(burr, p.segments[i].burr);
            n++;
        }
        apex /= Math.Max(1, n);
        string edge = apex > 120 ? "Light glints all along the dull edge." : apex > 40 ? "The edge still catches the light in places." : apex > 12 ? "The edge bites a fingernail." : "The edge disappears into a line too fine to see.";
        return edge + (burr > 0.3 ? " A wire burr has rolled over." : "");
    }

    // ------------------------------------------------------------------ the quench tank
    private void UpdateQuench(float dt, Vector2 pointer)
    {
        CraftPiece p = Piece;
        if (p == null)
        {
            End(true);
            return;
        }
        int entry = StationControls.ChoicePressed(3);
        if (entry >= 0) { setup.entryAngle = entry == 0 ? 0f : entry == 1 ? 0.1f : 0.9f; hintTimer = 0f; }
        if (held && !wasHeld && !plunged && p.location != CraftLocation.Quench)
        {
            plunged = Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Quench, a = setup.depth > 0f ? setup.depth : 1f, b = setup.entryAngle });
            sentMotion = CraftQuenchMotion.Still;
            moves.Clear();
        }
        if (!held && plunged)
            LiftOut();
        Vector2 hand = new Vector2((pointer.x / Mathf.Max(1f, Screen.width) - 0.5f) * 0.4f, (pointer.y / Mathf.Max(1f, Screen.height) - 0.5f) * 0.25f);
        if (plunged)
        {
            moves.Enqueue(pointer);
            while (moves.Count > 24) moves.Dequeue();
            motionTimer += dt;
            if (motionTimer >= 0.3f)
            {
                motionTimer = 0f;
                CraftQuenchMotion m = Motion();
                if (m != sentMotion)
                {
                    sentMotion = m;
                    Send(new CraftRequest(CraftRequestKind.Agitate, p.id) { i0 = (int)m });
                }
            }
        }
        quenchDepth = Mathf.MoveTowards(quenchDepth, plunged ? 1f : 0f, dt * 5f);
        if (pieceObject != null)
        {
            float edgeDown = setup.entryAngle < 0.05f ? 90f : 0f;
            pieceObject.localPosition = new Vector3(hand.x * 0.6f, Mathf.Lerp(0.42f, 0.02f + hand.y * 0.3f, quenchDepth), hand.y * 0.3f);
            pieceObject.localRotation = setup.entryAngle > 0.05f && setup.entryAngle < 0.5f
                ? Quaternion.identity // point first: hung by the tang
                : Quaternion.Euler(edgeDown, 0f, 0f) * Quaternion.LookRotation(Vector3.up, Vector3.right);
            pieceObject.gameObject.SetActive(p.location == CraftLocation.Air || p.location == CraftLocation.Quench);
        }
        CraftQuenchant q = station.Rules.Quenchant(Ws.Quench.medium);
        SetMeter("Moving", plunged ? (sentMotion == CraftQuenchMotion.Still ? 0.1f : 0.9f) : 0f, UITheme.Gold);
        hudTitle.text = $"The tank · {q?.Name ?? Ws.Quench.medium} · {(setup.entryAngle < 0.05f ? "edge first" : setup.entryAngle < 0.5f ? "point first" : "flat")}";
        double hot = p.MaxTemperature;
        hudFeel.text = plunged
            ? (hot > 400 ? "It hisses and boils the liquid round it." : hot > 120 ? "It bubbles, quieter now." : "It is cool.") + (sentMotion == CraftQuenchMotion.Still ? " Keep it moving." : "")
            : $"{HeatWords(p)}.";
    }

    private void LiftOut()
    {
        plunged = false;
        CraftPiece p = Piece;
        if (p != null)
            Send(new CraftRequest(CraftRequestKind.Move, p.id) { location = CraftLocation.Air });
    }

    /// <summary>The hand's motion over the last moves: up and down, side to side, round, or still.</summary>
    private CraftQuenchMotion Motion()
    {
        CraftQuenchMotion m = CraftQuenchMotion.Still;
        if (moves.Count >= 4)
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
        return m;
    }

    // ------------------------------------------------------------------ carving
    private void UpdateCarve(float dt, Vector2 pointer)
    {
        CraftPiece p = Piece;
        if (p == null)
        {
            End(true);
            return;
        }
        int pick = StationControls.ChoicePressed(CutNames.Length);
        if (pick >= 0) { carveCut = pick; hintTimer = 0f; }
        if (StationControls.Pressed(StationControl.NextTool)) { carveCut = (carveCut + 1) % CutNames.Length; hintTimer = 0f; }
        float along = Mathf.Clamp01(pointer.x / Mathf.Max(1f, Screen.width));
        float press = Mathf.Clamp01(0.15f + 0.85f * pointer.y / Mathf.Max(1f, Screen.height));
        bool bench = p.location == CraftLocation.Air;
        if (carveCut == 3)
        {
            // Boring: circles of the hand where the slot goes.
            if (held && !wasHeld) boreSwirl.Begin(pointer);
            if (held && boreSwirl.Active)
            {
                boreSwirl.Add(pointer);
                if (boreSwirl.Turns >= 1f)
                {
                    boreSwirl.Spend(1f);
                    int seg = Mathf.Clamp(p.SegmentAt(along), 0, p.segments.Count - 1);
                    if (Send(new CraftRequest(CraftRequestKind.Carve, p.id) { i0 = seg, i1 = seg, i2 = (int)CraftSurface.CarveCut.Bore, a = 1.2f * press + 0.3f, b = boreSwirl.Steadiness, d = 1f }))
                        Chips(along, 6);
                }
            }
            if (!held) boreSwirl.Clear();
        }
        else
        {
            // A knife stroke along the blank: where it starts and ends is the stretch cut; higher presses harder.
            if (held && !wasHeld && bench) { carveFrom = along; carveStart = Time.unscaledTime; carveMinY = carveMaxY = pointer.y; }
            if (held) { carveMinY = Mathf.Min(carveMinY, pointer.y); carveMaxY = Mathf.Max(carveMaxY, pointer.y); if (UnityEngine.Random.value < 0.4f) Chips(along, 1); }
            if (!held && wasHeld && bench && Mathf.Abs(along - carveFrom) > 0.02f)
            {
                int a = Mathf.Clamp(p.SegmentAt(Mathf.Min(carveFrom, along)), 0, p.segments.Count - 1), b = Mathf.Clamp(p.SegmentAt(Mathf.Max(carveFrom, along)), 0, p.segments.Count - 1);
                float steady = Mathf.Clamp01(1f - (carveMaxY - carveMinY) / (Screen.height * 0.08f)) * Mathf.Clamp01(1f - (float)hands.Unsteadiness * 5f);
                float depth = press * 1.8f * Mathf.Clamp((Time.unscaledTime - carveStart) / 0.5f, 0.4f, 1.2f) * (0.6f + 0.4f * (float)hands.Strength);
                bool slipped = hands.Slips(23);
                if (slipped) { steady *= 0.25f; depth *= 1.4f; shake = 0.4f; }
                hands.Move(press * 0.4f);
                var cut = carveCut == 0 ? CraftSurface.CarveCut.Width : carveCut == 1 ? CraftSurface.CarveCut.Thickness : CraftSurface.CarveCut.Round;
                if (Send(new CraftRequest(CraftRequestKind.Carve, p.id) { i0 = a, i1 = b, i2 = (int)cut, a = depth, b = steady, d = 1f }))
                    lastBlow = slipped ? "The knife slipped and dug in: your arm is tired." : depth > 1.4f ? "A deep cut: careful, it can split along the grain." : steady > 0.75f ? "A clean, steady shaving curls off." : "A wobbly cut: it leaves the surface rough.";
            }
        }
        // The knife over the blank, the chips flying.
        if (knifeObject != null)
        {
            float x = (along - 0.5f) * pieceLength;
            knifeObject.localPosition = new Vector3(x, PieceTop(p) + (held ? 0.004f : 0.03f), 0f);
            knifeObject.localRotation = Quaternion.Euler(0f, carveCut == 1 ? 90f : 0f, held ? -18f : -35f);
        }
        if (pieceObject != null)
        {
            pieceObject.gameObject.SetActive(bench);
            pieceObject.localPosition = new Vector3(0f, PieceTop(p) * 0.5f, 0f);
            pieceObject.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.right) * Quaternion.Euler(0f, carveCut == 1 ? 90f : 0f, 0f); // on its side to thin it
        }
        for (int i = chips.Count - 1; i >= 0; i--)
        {
            (Transform t, Vector3 v, float life) = chips[i];
            life -= dt;
            if (t == null || life <= 0f) { if (t != null) Destroy(t.gameObject); chips.RemoveAt(i); continue; }
            v += Vector3.down * 2f * dt;
            t.localPosition += v * dt;
            t.localRotation *= Quaternion.Euler(400f * dt, 250f * dt, 0f);
            chips[i] = (t, v, life);
        }
        SetMeter("Press", held ? press : 0f, UITheme.Gold);
        hudTitle.text = $"Carving · {CutNames[carveCut]}";
        Target(p, out string target);
        hudFeel.text = bench ? $"{p.Length:0} mm long. {target} {lastBlow}" : "It is not on the bench.";
    }

    private void Target(CraftPiece p, out string text)
    {
        text = "";
        CraftComponentSpec spec = !string.IsNullOrEmpty(p.intentDesign) ? station.Rules.Design(p.intentDesign)?.Component(p.intentRole) : null;
        if (spec == null || spec.kind != CraftPieceKind.Wood) return;
        double w = 0, t = 0;
        foreach (MetalSegment s in p.segments) { w = Math.Max(w, s.width); t = Math.Max(t, s.thickness); }
        text = $"Wanted: {spec.wood.lengthMin:0}-{spec.wood.lengthMax:0} mm long, {spec.wood.diameterMin:0}-{spec.wood.diameterMax:0} mm thick (now {w:0} × {t:0}).";
    }

    private void Chips(float along, int count)
    {
        if (root == null) return;
        CraftPiece p = Piece;
        CraftWoodDefinition wd = p != null ? station.Rules.Wood(p.wood.wood) : null;
        Color c = wd != null ? Color.Lerp(wd.color, Color.white, 0.25f) : new Color(0.75f, 0.6f, 0.4f);
        for (int i = 0; i < count && chips.Count < 60; i++)
        {
            Transform t = Prim(PrimitiveType.Cube, "Shaving", new Vector3((along - 0.5f) * pieceLength, PieceTop(p) + 0.01f, 0.01f), new Vector3(0.008f, 0.001f, 0.004f), c, false);
            chips.Add((t, new Vector3(UnityEngine.Random.Range(-0.15f, 0.15f), UnityEngine.Random.Range(0.1f, 0.35f), UnityEngine.Random.Range(0.05f, 0.25f)), 0.8f));
        }
    }

    // ------------------------------------------------------------------ the ore bench
    private void UpdateBrush(float dt, Vector2 pointer, float scroll)
    {
        CraftPiece p = Piece;
        if (p == null || p.kind != CraftPieceKind.Ore || p.ore.fragments.Count == 0 || p.location != CraftLocation.Air)
        {
            End(true);
            return;
        }
        int pick = StationControls.ChoicePressed(BrushNames.Length);
        if (pick >= 0) SetBrush((CraftOreBrush)pick);
        if (StationControls.Pressed(StationControl.NextTool)) SetBrush((CraftOreBrush)(((int)oreBrush + 1) % BrushNames.Length));
        if (StationControls.Pressed(StationControl.FaceEdge)) { FlushBrush(); ShowStone((rock.Fragment + 1) % p.ore.fragments.Count); hintTimer = 0f; }
        if (rock.Fragment < 0 || rock.Fragment >= p.ore.fragments.Count) ShowStone(0); // the lump changed under it
        // Turning it in the hand: hold Quick and move the mouse; the wheel spins it; Turn Over rolls it upside down.
        Vector2 delta = pointer - lastPointer;
        lastPointer = pointer;
        bool turning = StationControls.Held(StationControl.Quick);
        if (turning && delta.sqrMagnitude > 0.01f)
            rockTurn = Quaternion.AngleAxis(-delta.x * 0.45f, Vector3.up) * Quaternion.AngleAxis(delta.y * 0.45f, Vector3.right) * rockTurn;
        if (Mathf.Abs(scroll) > 0.01f) rockTurn = Quaternion.AngleAxis(scroll * 20f, Vector3.up) * rockTurn;
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
        float move = Mathf.Clamp01(delta.magnitude / Mathf.Max(1f, Screen.height * 0.008f));
        CraftOreDefinition o = station.Rules.Ore(p.ore.ore);
        if (scrubbing && move > 0.05f)
        {
            // Back and forth: the bristles go over what is under them. A tired arm scrubs less hard.
            float effort = 4f * dt * move * (0.45f + 0.55f * (float)hands.Strength);
            brushCover += rock.Brush(from, dir, Reach, effort, oreBrush, (float)CraftOres.BrushOnDirt(oreBrush, o), (float)CraftOres.BrushOnRind(oreBrush));
            brushSeconds += dt;
            scrubPhase += dt * 30f;
            if ((brushTired += dt) >= 0.6f) { brushTired = 0f; hands.Move(oreBrush == CraftOreBrush.Wire ? 0.35 : 0.2); }
            if (rock.Dirty || rock.Rinded) Dust(rockObject.localRotation * hit + rockObject.localPosition, rock.Dirty ? DirtDust(o) : new Color(0.55f, 0.52f, 0.48f));
            if (brushCover >= 0.05f) FlushBrush();
        }
        if (!held && wasHeld) FlushBrush();
        if (brushObject != null)
        {
            brushObject.gameObject.SetActive(on || held);
            if (on && rockObject != null)
            {
                Vector3 at = rockObject.localRotation * (hit + normal * 0.012f) + rockObject.localPosition;
                Vector3 n = rockObject.localRotation * normal;
                brushObject.localPosition = at + (scrubbing ? Vector3.Cross(n, Vector3.up).normalized * Mathf.Sin(scrubPhase) * 0.006f : Vector3.zero);
                brushObject.localRotation = Quaternion.LookRotation(-n, Vector3.up) * Quaternion.Euler(0f, 0f, scrubbing ? Mathf.Sin(scrubPhase) * 8f : 0f);
            }
        }
        StepDust(dt);
        SetMeter("Clean", Mathf.Clamp01(1f - rock.DirtLeft * 0.7f - rock.RindLeft * 0.3f), UITheme.Good);
        OreFragment f = p.ore.fragments[Mathf.Clamp(rock.Fragment, 0, p.ore.fragments.Count - 1)];
        hudTitle.text = $"Ore bench · {BrushNames[(int)oreBrush]} · stone {rock.Fragment + 1} of {p.ore.fragments.Count} ({f.size:0.#} cm)";
        hudFeel.text = StoneWords(o);
    }

    private const float Reach = 0.022f;

    private void SetBrush(CraftOreBrush b)
    {
        if (b == oreBrush) return;
        FlushBrush();
        oreBrush = b;
        BuildBrush();
        hintTimer = 0f;
    }

    /// <summary>Puts fragment <paramref name="index"/> of the lump on the bench.</summary>
    private void ShowStone(int index)
    {
        CraftPiece p = Piece;
        if (p == null || p.ore.fragments.Count == 0) return;
        rock.Build(station.Rules, p, Mathf.Clamp(index, 0, p.ore.fragments.Count - 1));
        if (rockFilter != null) rockFilter.sharedMesh = rock.Mesh;
        brushCover = brushSeconds = 0f;
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

    /// <summary>What was scrubbed so far goes to the workshop (as the window's requests do).</summary>
    private void FlushBrush()
    {
        CraftPiece p = Piece;
        if (brushCover > 0.002f && p != null && rock.Fragment >= 0 && rock.Fragment < p.ore.fragments.Count)
            Send(new CraftRequest(CraftRequestKind.BrushOre, p.id) { i0 = rock.Fragment, i1 = (int)oreBrush, a = Mathf.Min(1f, brushCover), b = Mathf.Max(0.05f, brushSeconds), d = 1f });
        brushCover = brushSeconds = 0f;
    }

    private static Color DirtDust(CraftOreDefinition o) => o != null && o.gangue == CraftGangue.Clay ? new Color(0.62f, 0.52f, 0.4f) : new Color(0.42f, 0.33f, 0.22f);

    /// <summary>Crumbs of dirt and rind falling off under the brush.</summary>
    private void Dust(Vector3 at, Color c)
    {
        if (chips.Count >= 50 || UnityEngine.Random.value > 0.35f) return;
        Transform t = Prim(PrimitiveType.Cube, "Dust", at, Vector3.one * UnityEngine.Random.Range(0.0015f, 0.003f), c, false);
        chips.Add((t, new Vector3(UnityEngine.Random.Range(-0.08f, 0.08f), UnityEngine.Random.Range(0f, 0.08f), UnityEngine.Random.Range(-0.08f, 0.08f)), 0.7f));
    }

    private void StepDust(float dt)
    {
        for (int i = chips.Count - 1; i >= 0; i--)
        {
            (Transform t, Vector3 v, float life) = chips[i];
            life -= dt;
            if (t == null || life <= 0f || t.localPosition.y < -0.005f) { if (t != null) Destroy(t.gameObject); chips.RemoveAt(i); continue; }
            v += Vector3.down * 1.5f * dt;
            t.localPosition += v * dt;
            chips[i] = (t, v, life);
        }
    }

    /// <summary>What the stone in the hand looks like now.</summary>
    private string StoneWords(CraftOreDefinition o)
    {
        float d = rock.DirtLeft, r = rock.RindLeft;
        bool clay = o != null && o.gangue == CraftGangue.Clay;
        string dirt = !rock.Dirty ? "Clean of dirt" : d > 0.7f ? (clay ? "Caked with clay" : "Crusted with dirt") : d > 0.3f ? "Patches of dirt left" : "Nearly clean";
        string rind = !rock.Rinded ? "" : r > 0.6f ? "; a dull weathered rind covers part of it (the wire brush takes it)" : "; some of the rind is still on it";
        string tip = rock.Dirty && clay && oreBrush == CraftOreBrush.Soft && d > 0.6f ? " The soft brush barely shifts caked clay: take the stiff one." : "";
        return $"{dirt}{rind}.{tip} {lastBlow}".Trim();
    }

    // ------------------------------------------------------------------ the crucible
    private void UpdatePour(float dt, Vector2 pointer)
    {
        if (held && !wasHeld && pour == null)
        {
            pour = new CraftPourControl();
            pourClock = 0f;
        }
        // The hand carries the crucible: the pointer left and right brings its lip over the cup; the hands drift a little
        // (more when tired or unpractised), so the aim has to be held.
        pourAim = Mathf.Clamp((pointer.x - Screen.width * 0.5f) / (Screen.width * 0.3f) * 0.12f, -0.06f, 0.2f);
        pourClock += dt;
        float wander = pour != null ? (float)hands.Tremble((int)(pourClock * 3f) + 51) * 0.8f : 0f;
        pourDrift = Mathf.Lerp(pourDrift, wander, Mathf.Min(1f, dt * 3f));
        if (crucibleObject != null)
            crucibleObject.localPosition = new Vector3(PourBaseX + pourAim + pourDrift, 0.2f, 0f);
        float expected = pour != null ? (float)pour.Rate : 0.35f; // before the pour: where a moderate stream would land
        pourLandX = PourBaseX + pourAim + pourDrift + 0.05f + 0.07f * expected;
        bool onTarget = Mathf.Abs(pourLandX - pourMouthX) <= CupRadius;
        if (aimOn != null)
        {
            aimOn.localPosition = new Vector3(pourLandX, 0.0225f, 0f);
            aimOff.localPosition = new Vector3(pourLandX, 0.0225f, 0f);
            aimOn.gameObject.SetActive(onTarget);
            aimOff.gameObject.SetActive(!onTarget);
        }
        if (pour != null)
        {
            pourTilt = Mathf.Clamp01((pointer.y - pressAt.y) / (Screen.height * 0.3f));
            pour.Tick(dt, pourTilt, hands, 4, onTarget);
            if (!held || pour.Poured >= 1)
            {
                FinishPour();
                if (!Active)
                    return;
            }
        }
        else
            pourTilt = Mathf.MoveTowards(pourTilt, 0f, dt * 2f);
        float rate = pour != null ? (float)pour.Rate : 0f;
        if (crucibleObject != null)
            crucibleObject.localRotation = Quaternion.Euler(0f, 0f, -pourTilt * 110f);
        if (streamObject != null)
        {
            streamObject.gameObject.SetActive(rate > 0.04f);
            streamObject.localScale = new Vector3(0.004f + 0.012f * rate, 0.11f, 0.004f + 0.012f * rate);
            Vector3 sp = streamObject.localPosition;
            streamObject.localPosition = new Vector3(pourLandX, sp.y, sp.z);
        }
        // The metal runs in at the mouth and creeps along the cavity to the far end.
        float full = pour != null ? (float)pour.Poured : 0f;
        for (int i = 0; i < fillCells.Count; i++)
            fillCells[i].gameObject.SetActive(full * fillCells.Count > i + 0.05f);
        double t = setup.fromFurnace ? Ws.Furnace.fire.temperature : Ws.Crucible.temperature;
        SetMeter("Mould", pour != null ? (float)pour.Poured : 0f, UITheme.Gold);
        hudTitle.text = $"Pouring · {station.Rules.Mold(setup.mold)?.Name ?? setup.mold}";
        hudFeel.text = pour == null ? $"The melt is {CraftMetallurgy.HeatName(t, false)}. Move the hand sideways until the ring sits in the clay cup (green), then hold {StationControls.K(StationControl.Work)} and raise the hand to tip it."
            : rate > 0.04f && !onTarget ? "Missing the cup: it splashes on the sand! Bring the lip back over it."
            : pour.Interruptions > 0 ? $"The stream broke {pour.Interruptions} time{(pour.Interruptions == 1 ? "" : "s")}." : rate > 0.04f ? "A bright, steady stream runs into the cup." : "Tip it further.";
    }

    private void FinishPour()
    {
        CraftPourControl p = pour;
        pour = null;
        if (p == null || !p.Started)
            return;
        float rate = (float)p.MeanRate * (float)Math.Max(0.3, p.Poured);
        int share = Mathf.Clamp(Mathf.RoundToInt((float)p.Poured * 1000f), 1, 1000);
        float spilt = (float)p.SpillShare, over = (float)Math.Min(1, p.Overflow * 2); // what missed the cup, what ran over
        Send(setup.fromFurnace
            ? new CraftRequest(CraftRequestKind.TakeMelt) { id = setup.mold, a = rate, b = (float)p.Steadiness, c = spilt, d = over, i1 = share }
            : new CraftRequest(CraftRequestKind.Pour) { id = setup.mold, a = rate, b = (float)p.Steadiness, i0 = p.Interruptions, c = spilt, d = over, i1 = share });
        if (mode == CraftHandsOnMode.Pour)
            End(true);
    }

    // ------------------------------------------------------------------ words
    private string HeatWords(CraftPiece p)
    {
        double t = p.MaxTemperature;
        string colour = CraftMetallurgy.HeatName(t, Ws.Variable("night") > 0.5);
        string words = string.IsNullOrEmpty(colour) || colour == "black heat" ? (t > 60 ? "Too hot to touch, but dark" : "It is cold") : CraftText.Capital("It glows " + colour);
        return station.Reveals || station.HasInstrument(CraftInstruments.Thermometer) ? $"{words} ({t:0} °C)" : words;
    }

    private static string HandHeat(CraftPiece p)
    {
        double t = p.MaxTemperature;
        return t < 50 ? "Cool in the hand." : t < 120 ? "Warm in the hand." : t < 220 ? $"Hot - dip it before the temper goes ({StationControls.K(StationControl.Quick)})." : "Too hot: the colours are running.";
    }

    private bool Send(CraftRequest r) => station != null && player != null && station.RequestAndTell(player, r);

    // ------------------------------------------------------------------ the work place
    private CraftTools PlaceTool =>
        mode == CraftHandsOnMode.Hammer ? CraftTools.Anvil : mode == CraftHandsOnMode.Bellows ? CraftTools.Forge
        : mode == CraftHandsOnMode.Grind ? CraftTools.Grinder : mode == CraftHandsOnMode.Sharpen ? CraftTools.Whetstone
        : mode == CraftHandsOnMode.Quench ? CraftTools.QuenchTank : mode == CraftHandsOnMode.Carve ? CraftTools.AssemblyBench
        : mode == CraftHandsOnMode.Brush ? CraftTools.OreBench : CraftTools.Crucible;

    private void BuildPlace()
    {
        Vector3 at = visuals != null ? visuals.PointFor(PlaceTool) : transform.position + transform.forward + Vector3.up * 0.9f;
        Vector3 right = visuals != null ? visuals.Right : transform.right;
        Vector3 front = visuals != null ? visuals.Front : transform.forward;
        var go = new GameObject("Craft Hands-On Place");
        root = go.transform;
        root.SetPositionAndRotation(at, Quaternion.LookRotation(Flat(front), Vector3.up));
        if (Vector3.Dot(root.right, right) < 0f) root.rotation = Quaternion.LookRotation(-Flat(front), Vector3.up);
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
                Box("Stump", new Vector3(0f, -0.5f, 0f), new Vector3(0.4f, 0.44f, 0.4f), wood, false);
                hammerObject = new GameObject("Hammer").transform;
                hammerObject.SetParent(root, false);
                Transform head = Prim(PrimitiveType.Cube, "Head", Vector3.zero, new Vector3(0.035f, 0.035f, 0.1f), new Color(0.32f, 0.32f, 0.35f), true, hammerObject);
                head.localRotation = Quaternion.identity;
                Transform handle = Prim(PrimitiveType.Cylinder, "Handle", new Vector3(0f, 0.012f, 0.19f), new Vector3(0.018f, 0.16f, 0.018f), wood, false, hammerObject);
                handle.localRotation = Quaternion.Euler(90f, 0f, 0f);
                break;
            }
            case CraftHandsOnMode.Bellows:
            {
                Transform body = new GameObject("Bellows").transform;
                body.SetParent(root, false);
                body.localPosition = new Vector3(-0.55f, -0.05f, 0.05f);
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
                Box("Bench", new Vector3(0f, -0.06f, 0.05f), new Vector3(0.5f, 0.06f, 0.3f), wood, false);
                RebuildTool();
                break;
            case CraftHandsOnMode.Sharpen:
                Box("Bench", new Vector3(0f, -0.02f, 0.03f), new Vector3(0.5f, 0.04f, 0.3f), wood, false);
                Box("Stone Block", new Vector3(0f, 0.015f, 0f), new Vector3(0.28f, 0.03f, 0.09f), wood, false);
                RebuildTool();
                break;
            case CraftHandsOnMode.Quench:
            {
                Box("Tank Wall Back", new Vector3(0f, 0f, -0.15f), new Vector3(0.6f, 0.36f, 0.02f), iron, true);
                Box("Tank Wall Front", new Vector3(0f, 0f, 0.15f), new Vector3(0.6f, 0.36f, 0.02f), iron, true);
                Box("Tank Wall Left", new Vector3(-0.3f, 0f, 0f), new Vector3(0.02f, 0.36f, 0.3f), iron, true);
                Box("Tank Wall Right", new Vector3(0.3f, 0f, 0f), new Vector3(0.02f, 0.36f, 0.3f), iron, true);
                CraftQuenchant q = station.Rules.Quenchant(Ws.Quench.medium);
                Color liquid = q != null && q.id.Contains("oil") ? new Color(0.45f, 0.33f, 0.1f) : q != null && q.id.Contains("blood") ? new Color(0.45f, 0.04f, 0.04f) : new Color(0.2f, 0.33f, 0.42f);
                Box("Liquid", new Vector3(0f, 0.13f, 0f), new Vector3(0.58f, 0.004f, 0.28f), liquid, false);
                break;
            }
            case CraftHandsOnMode.Pour:
            {
                Box("Sand", new Vector3(0.2f, -0.02f, 0f), new Vector3(0.34f, 0.04f, 0.18f), new Color(0.55f, 0.47f, 0.34f), false);
                float mouth = BuildMouldCavity(station.Rules.Mold(setup.mold));
                pourMouthX = mouth;
                pourAim = pourDrift = 0f;
                // The pouring cup: a clay funnel standing on the sprue - the mouth the stream must go into.
                Prim(PrimitiveType.Cylinder, "Pouring Cup", new Vector3(mouth, 0.011f, 0f), new Vector3(0.05f, 0.011f, 0.05f), new Color(0.78f, 0.64f, 0.46f), false);
                Prim(PrimitiveType.Cylinder, "Pouring Cup Mouth", new Vector3(mouth, 0.0222f, 0f), new Vector3(CupRadius * 2f, 0.0006f, CupRadius * 2f), new Color(0.05f, 0.04f, 0.03f), false);
                // Where the stream will land: amber off the cup, green in it.
                aimOff = Prim(PrimitiveType.Cylinder, "Pour Aim", new Vector3(mouth, 0.0225f, 0f), new Vector3(0.012f, 0.0008f, 0.012f), new Color(1f, 0.7f, 0.2f), false);
                aimOn = Prim(PrimitiveType.Cylinder, "Pour Aim (on target)", new Vector3(mouth, 0.0225f, 0f), new Vector3(0.012f, 0.0008f, 0.012f), new Color(0.4f, 1f, 0.5f), false);
                crucibleObject = new GameObject("Crucible Pivot").transform;
                crucibleObject.SetParent(root, false);
                crucibleObject.localPosition = new Vector3(PourBaseX, 0.2f, 0f);
                Transform pot = Prim(PrimitiveType.Cylinder, "Crucible", new Vector3(-0.06f, 0f, 0f), new Vector3(0.1f, 0.06f, 0.1f), stone, false, crucibleObject);
                Transform melt = Prim(PrimitiveType.Cylinder, "Melt", new Vector3(-0.06f, 0.05f, 0f), new Vector3(0.085f, 0.004f, 0.085f), Color.white, false, crucibleObject);
                SetGlow(melt, new Color(0.6f, 0.55f, 0.5f), setup.fromFurnace ? Ws.Furnace.fire.temperature : Ws.Crucible.temperature);
                Prim(PrimitiveType.Cube, "Tongs", new Vector3(-0.2f, 0.02f, 0f), new Vector3(0.22f, 0.012f, 0.012f), iron, true, crucibleObject);
                streamObject = Prim(PrimitiveType.Cylinder, "Stream", new Vector3(mouth, -0.11f, 0f), new Vector3(0.01f, 0.11f, 0.01f), Color.white, false);
                SetGlow(streamObject, new Color(0.6f, 0.55f, 0.5f), 1150);
                streamObject.gameObject.SetActive(false);
                break;
            }
            case CraftHandsOnMode.Carve:
            {
                Box("Bench", new Vector3(0f, -0.03f, 0.03f), new Vector3(0.6f, 0.04f, 0.32f), wood, false);
                Box("Bench Stop", new Vector3(-0.24f, 0.005f, 0f), new Vector3(0.02f, 0.03f, 0.08f), wood, false);
                knifeObject = new GameObject("Carving Knife").transform;
                knifeObject.SetParent(root, false);
                Prim(PrimitiveType.Cube, "Blade", new Vector3(0.03f, 0f, 0f), new Vector3(0.07f, 0.003f, 0.016f), new Color(0.7f, 0.7f, 0.72f), true, knifeObject);
                Prim(PrimitiveType.Cylinder, "Handle", new Vector3(-0.03f, 0f, 0f), new Vector3(0.018f, 0.035f, 0.018f), new Color(0.3f, 0.2f, 0.12f), false, knifeObject).localRotation = Quaternion.Euler(0f, 0f, 90f);
                break;
            }
            case CraftHandsOnMode.Brush:
            {
                Box("Bench", new Vector3(0f, -0.03f, 0.03f), new Vector3(0.5f, 0.04f, 0.32f), wood, false);
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
                brushCover = brushSeconds = 0f;
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
            pieceObject.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.right); // length along x, width along z, thickness up
            if (mode == CraftHandsOnMode.Bellows) pieceObject.localPosition = new Vector3(0f, 0.03f, 0f);
            RefreshPiece();
            if (mode == CraftHandsOnMode.Hammer) pieceObject.localPosition = new Vector3(0f, PieceTop(Piece) * 0.5f, 0f);
        }
    }

    /// <summary>The wheel, the file, the brush or the stone of the abrasive in hand.</summary>
    private void RebuildTool()
    {
        if (root == null)
            return;
        if (wheelObject != null) Destroy(wheelObject.gameObject);
        wheelObject = null;
        CraftAbrasive a = abrasives.Count > 0 ? abrasives[Mathf.Clamp(abrasive, 0, abrasives.Count - 1)] : null;
        if (a == null)
            return;
        float fine = Mathf.Clamp01(1f - (float)a.finish / 20f);
        if (a.kind == CraftAbrasiveKind.Wheel)
        {
            wheelObject = Prim(PrimitiveType.Cylinder, "Wheel", new Vector3(0f, 0.12f, -0.02f), new Vector3(0.22f, 0.02f, 0.22f), Color.Lerp(new Color(0.35f, 0.33f, 0.3f), new Color(0.7f, 0.68f, 0.64f), fine), false);
            wheelObject.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Prim(PrimitiveType.Cube, "Wheel Mark", new Vector3(0f, 0.6f, 0f), new Vector3(0.12f, 0.2f, 0.12f), new Color(0.2f, 0.2f, 0.2f), false, wheelObject);
        }
        else if (a.kind == CraftAbrasiveKind.Whetstone)
        {
            Color c = a.apex <= 1.5 ? new Color(0.45f, 0.3f, 0.18f) : Color.Lerp(new Color(0.35f, 0.33f, 0.3f), new Color(0.78f, 0.75f, 0.7f), fine);
            wheelObject = Prim(PrimitiveType.Cube, a.apex <= 1.5 ? "Strop" : "Whetstone", new Vector3(0f, 0.045f, 0f), new Vector3(0.24f, 0.025f, 0.06f), c, false);
        }
        else
            wheelObject = Prim(PrimitiveType.Cube, a.Name, new Vector3(0f, 0.15f, 0.02f), new Vector3(0.18f, 0.012f, 0.025f),
                a.kind == CraftAbrasiveKind.Brush ? new Color(0.5f, 0.4f, 0.25f) : a.kind == CraftAbrasiveKind.Polish ? new Color(0.75f, 0.7f, 0.6f) : new Color(0.4f, 0.4f, 0.43f), a.kind == CraftAbrasiveKind.File);
    }

    /// <summary>The piece as it is now: its shape (rebuilt when it changes) and each segment glowing at its own heat.</summary>
    private void RefreshPiece()
    {
        CraftPiece p = Piece;
        if (pieceObject == null || p == null || p.segments.Count == 0)
        {
            if (pieceObject != null) pieceObject.gameObject.SetActive(false);
            return;
        }
        string shape = ShapeOf(p);
        if (shape != pieceShape)
        {
            pieceShape = shape;
            Mesh old = pieceFilter.sharedMesh;
            pieceFilter.sharedMesh = CraftModels.PieceMesh(p, true);
            if (old != null) Destroy(old);
            pieceLength = Mathf.Max(0.02f, (float)(p.Length * 0.001));
            double w = 0;
            foreach (MetalSegment s in p.segments) w = Math.Max(w, s.width);
            pieceWidth = Mathf.Max(0.005f, (float)(w * 0.001));
        }
        bool metal = p.kind == CraftPieceKind.Metal;
        Color baseColor = CraftArt.MetalColor(station.Rules, p);
        var mats = new Material[p.segments.Count];
        for (int i = 0; i < mats.Length; i++)
        {
            MetalSegment s = p.segments[i];
            Color c = metal ? Color.Lerp(baseColor, new Color(0.15f, 0.14f, 0.14f), Mathf.Clamp01((float)s.scale * 4f)) : baseColor;
            Color glow = CraftArt.Heat(s.Temperature, out float k);
            mats[i] = CraftModels.Glowing(c, metal, metal ? 0.25f + 0.6f * (float)s.polish : 0.15f, glow, metal ? k : 0f);
        }
        pieceRenderer.sharedMaterials = mats;
    }

    private static string ShapeOf(CraftPiece p)
    {
        var h = new CraftChecksum();
        h.Add(p.segments.Count);
        foreach (MetalSegment s in p.segments)
        {
            h.Add(s.length, 0.2);
            h.Add(s.width, 0.2);
            h.Add(s.thickness, 0.1);
            h.Add(s.bevelDepthL + s.bevelDepthR, 0.1);
            h.Add(s.bevelWidthL + s.bevelWidthR, 0.2);
            h.Add(s.bend, 0.5);
        }
        return h.Value.ToString();
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

    private Transform Box(string name, Vector3 at, Vector3 size, Color c, bool metal) => Prim(PrimitiveType.Cube, name, at, size, c, metal);

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
        Box("Mould Runner", new Vector3(x0 - 0.006f, 0.0006f, 0f), new Vector3(0.014f, 0.002f, 0.006f), new Color(0.06f, 0.05f, 0.04f), false);
        for (int i = 0; i < n; i++)
        {
            float w = mold.width;
            if (shape != null) shape.At((i + 0.5f) / n, out w, out _);
            var at = new Vector3(x0 + cell * (i + 0.5f), 0.0005f, 0f);
            Box("Mould Cavity", at, new Vector3(cell * 1.02f, 0.002f, Mathf.Max(0.004f, w * k)), new Color(0.12f, 0.1f, 0.08f), false);
            Transform metal = Prim(PrimitiveType.Cube, "Metal In The Mould", at + new Vector3(0f, 0.002f, 0f), new Vector3(cell * 1.02f, 0.003f, Mathf.Max(0.003f, w * k * 0.95f)), Color.white, false);
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
            case CraftHandsOnMode.Sharpen: distance = viewDistance * 0.7f; height = 0.45f; break;
            case CraftHandsOnMode.Quench: at += root.up * 0.15f; distance = viewDistance * 1.1f; height = 0.6f; break;
            case CraftHandsOnMode.Pour: at += root.up * 0.1f + root.right * 0.05f; distance = viewDistance * 0.8f; height = 0.3f; break;
            case CraftHandsOnMode.Carve: distance = Mathf.Max(viewDistance * 0.7f, pieceLength * 1.1f); height = 0.45f; break;
            case CraftHandsOnMode.Brush: at += root.up * 0.07f; distance = viewDistance * 0.42f; height = 0.16f; break;
        }
        position = at + front * distance + Vector3.up * height;
        rotation = Quaternion.LookRotation(at - position, Vector3.up);
    }

    // ------------------------------------------------------------------ the HUD
    private void SetMeter(string label, float value, Color color)
    {
        if (meterFill == null)
            return;
        meterFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        meterFill.color = color;
        if (meterLabel != null) meterLabel.text = label;
    }

    private void BuildHud()
    {
        hud = UITheme.Canvas("Craft Hands-On", 57, null);
        DontDestroyOnLoad(hud.gameObject);
        Image panel = UITheme.Panel("Panel", hud.transform, new Color(0.06f, 0.06f, 0.07f, 0.84f), UITheme.Radius);
        UITheme.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-460f, 24f), new Vector2(460f, 262f));
        RectTransform col = UITheme.Column("Column", panel.transform, 12, 4f);
        InventoryUIFactory.Stretch(col);
        hudTitle = UITheme.Label(col, "", 20f, UITheme.Gold, TextAlignmentOptions.Left, FontStyles.Bold, "Title");
        InventoryUIFactory.Size(hudTitle, -1f, 26f);
        hudFeel = UITheme.Label(col, "", 16f, UITheme.Text, TextAlignmentOptions.Left, FontStyles.Normal, "Feel");
        InventoryUIFactory.Size(hudFeel, -1f, 44f);
        hudLegend = UITheme.Label(col, Legend(), 14f, UITheme.Text, TextAlignmentOptions.Left, FontStyles.Normal, "Legend");
        InventoryUIFactory.Size(hudLegend, -1f, 44f);
        meterFill = Bar(col, "Blow", out meterLabel);
        armsFill = Bar(col, "Arms", out _);
        hudHint = UITheme.Label(col, Hint(), 13f, UITheme.Muted, TextAlignmentOptions.Left, FontStyles.Normal, "Hint");
        InventoryUIFactory.Size(hudHint, -1f, 44f);
    }

    private static Image Bar(RectTransform col, string label, out TextMeshProUGUI text)
    {
        RectTransform row = UITheme.Row(label, col, 12f, 8f);
        text = UITheme.Label(row, label, 12f, UITheme.Muted);
        InventoryUIFactory.Size(text, 64f, 12f, 0f);
        Image back = UITheme.Panel("Bar", row, new Color(0f, 0f, 0f, 0.4f), 4);
        InventoryUIFactory.Size(back, -1f, 8f, 1f);
        Image fill = UITheme.Panel("Fill", back.transform, UITheme.Good, 4);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        return fill;
    }

    /// <summary>Every choice of the work place - the tools, the sides, the stones - with its key, the one in use highlighted.</summary>
    private string Legend()
    {
        var sb = new System.Text.StringBuilder();
        string Pick(bool on, string key, string text) => on ? $"<b><color=#FFD27A>[{key}] {text}</color></b>" : $"<color=#B8BDC6>[{key}] {text}</color>";
        string Choice(int i) => StationControls.Key((StationControl)((int)StationControl.Choice1 + i));
        switch (mode)
        {
            case CraftHandsOnMode.Hammer:
            {
                sb.Append("Tool: ");
                for (int i = 0; i < ToolOrder.Length; i++)
                    sb.Append(i > 0 ? "  " : "").Append(Pick(ToolOrder[i] == setup.tool, Choice(i), CraftForging.ToolName(ToolOrder[i])));
                CraftPiece p = Piece;
                sb.Append("\n").Append(Pick(true, StationControls.Key(StationControl.FaceEdge), setup.face == CraftStrikeFace.Flat ? "strike on its face" : "strike on its edge"));
                if (p != null) sb.Append("   ").Append(Pick(false, StationControls.Key(StationControl.TurnOver), $"turn over (the {(p.faceUp == 0 ? "left" : "right")} face is up: a tilted blow bevels it)"));
                break;
            }
            case CraftHandsOnMode.Grind:
            {
                sb.Append($"Tool [{StationControls.Key(StationControl.NextTool)}]: ");
                for (int i = 0; i < abrasives.Count; i++)
                    sb.Append(i > 0 ? "  " : "").Append(i == abrasive ? $"<b><color=#FFD27A>{abrasives[i].Name}</color></b>" : $"<color=#B8BDC6>{abrasives[i].Name}</color>");
                sb.Append("\nSide: ");
                for (int i = 0; i < Sides.Length; i++)
                    sb.Append(i > 0 ? "  " : "").Append(Pick(Sides[i] == setup.side, Choice(i), SideNames[i]));
                break;
            }
            case CraftHandsOnMode.Sharpen:
            {
                sb.Append($"Stone [{StationControls.Key(StationControl.Scroll)}]: ");
                for (int i = 0; i < abrasives.Count; i++)
                    sb.Append(i > 0 ? "  " : "").Append(i == abrasive ? $"<b><color=#FFD27A>{abrasives[i].Name}</color></b>" : $"<color=#B8BDC6>{abrasives[i].Name}</color>");
                sb.Append("\n").Append(Pick(leftFace, StationControls.Key(StationControl.NextTool), "left face")).Append("  ").Append(Pick(!leftFace, StationControls.Key(StationControl.NextTool), "right face"));
                break;
            }
            case CraftHandsOnMode.Quench:
                sb.Append("Into the tank: ").Append(Pick(setup.entryAngle < 0.05f, Choice(0), "edge first")).Append("  ")
                  .Append(Pick(setup.entryAngle >= 0.05f && setup.entryAngle < 0.5f, Choice(1), "point first")).Append("  ")
                  .Append(Pick(setup.entryAngle >= 0.5f, Choice(2), "flat"));
                break;
            case CraftHandsOnMode.Brush:
            {
                sb.Append($"Brush [{StationControls.Key(StationControl.NextTool)}]: ");
                for (int i = 0; i < BrushNames.Length; i++)
                    sb.Append(i > 0 ? "  " : "").Append(Pick(i == (int)oreBrush, Choice(i), BrushNames[i]));
                CraftPiece lump = Piece;
                sb.Append("\n").Append(Pick(false, StationControls.Key(StationControl.FaceEdge), $"next stone ({rock.Fragment + 1} of {(lump != null ? lump.ore.fragments.Count : 0)})"));
                sb.Append("   ").Append(Pick(StationControls.Held(StationControl.Quick), StationControls.Key(StationControl.Quick), "hold and move: turn it in the hand"));
                sb.Append("   ").Append(Pick(false, StationControls.Key(StationControl.TurnOver), "turn it over"));
                break;
            }
            case CraftHandsOnMode.Carve:
                sb.Append($"Cut [{StationControls.Key(StationControl.NextTool)}]: ");
                for (int i = 0; i < CutNames.Length; i++)
                    sb.Append(i > 0 ? "  " : "").Append(Pick(i == carveCut, Choice(i), CutNames[i]));
                break;
            case CraftHandsOnMode.Bellows:
            {
                CraftFuel f = !string.IsNullOrEmpty(setup.fuel) ? station.Rules.Fuel(setup.fuel) : null;
                sb.Append($"Fuel to add: {f?.Name ?? "none chosen"}   Air vent: {Ws.Forge.damper * 100:0}% open");
                break;
            }
            default:
                sb.Append($"Mould: {station.Rules.Mold(setup.mold)?.Name ?? setup.mold}   From the {(setup.fromFurnace ? "furnace" : "crucible")}");
                break;
        }
        return sb.ToString();
    }

    /// <summary>How the work place is worked, with the keys actually bound (they follow a rebind and the device in use).</summary>
    private string Hint()
    {
        string work = StationControls.K(StationControl.Work), down = StationControls.K(StationControl.PutDown), wheel = StationControls.K(StationControl.Scroll);
        string next = StationControls.K(StationControl.NextTool), quick = StationControls.K(StationControl.Quick), fire = StationControls.K(StationControl.Fire);
        switch (mode)
        {
            case CraftHandsOnMode.Hammer:
                return $"Aim along the bar · hold {work} to raise the hammer, release to strike · while holding, move up/down to tilt the face · " +
                       $"{StationControls.Choices(ToolOrder.Length)}, {wheel} or {next}: tool · {StationControls.K(StationControl.FaceEdge)}: face/edge · " +
                       $"{StationControls.K(StationControl.TurnOver)}: turn over · {fire}: fire · {down}: put it down";
            case CraftHandsOnMode.Bellows:
                return $"Hold {work} and pull down to squeeze, up to fill (or {quick}) · a steady rhythm builds the draft · {fire}: piece in/out of the coals · " +
                       $"{StationControls.K(StationControl.FeedFire)}: fuel · {StationControls.K(StationControl.VentClose)} {StationControls.K(StationControl.VentOpen)}: air vent · {down}: let go";
            case CraftHandsOnMode.Grind:
                return $"Hold {work} to press the blade to the wheel · sideways: along the blade · higher: harder · {wheel}: angle · " +
                       $"{StationControls.Choices(Sides.Length)}: side · {next}: tool · {quick}: dip it · {down}: put it down";
            case CraftHandsOnMode.Sharpen:
                return $"Hold {work} and draw the blade across the stone · the hand's height is the angle - keep it steady · {next}: other face · {wheel}: stone · {down}: put it down";
            case CraftHandsOnMode.Quench:
                return $"Hold {work} to plunge it and keep it moving (up and down, side to side, round) · release to lift it out · " +
                       $"{StationControls.Choices(3)}: edge first, point first, flat · {down}: step back";
            case CraftHandsOnMode.Brush:
                return $"Hold {work} and scrub back and forth over the stone · hold {quick} and move to turn it, {wheel} spins it · soft takes loose dirt, stiff caked clay, wire the weathered rind (and a little ore) · {down}: put it down";
            case CraftHandsOnMode.Carve:
                return $"Hold {work} and draw the knife along the blank (where you start and stop is what is cut) · higher presses harder - too hard splits it · keep the stroke straight · bore: circle the hand · {StationControls.Choices(4)} or {next}: the cut · {down}: put it down";
            default:
                return $"Hold {work} and raise the hand to tip the crucible · keep the stream steady until the mould is full · {down}: stop";
        }
    }
}
