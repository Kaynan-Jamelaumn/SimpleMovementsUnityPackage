using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The assembly bench: start an item from a design, fit each part into its place (a part must be the right kind, cut
/// to the right pattern, cool, and it must go on: a slot too tight is filed bigger, a haft too thick rasped down), then
/// make each joint by hand: punch the holes and stitch in the rhythm of the awl (each click on the beat; off the beat
/// the spacing and tension wander), wind the wrap, set rivets and peen the tang or drive the wedge with the hammer
/// (hold and release: too light a rivet stays loose, too hard crushes leather or cracks a hard rivet). Finish it, then
/// try it - flex it, strike with it, pull its straps - to know what it is.
/// </summary>
public sealed class CraftAssemblyMode : CraftMode
{
    private TMP_Dropdown category, group, design, part, joint, work, slot;
    // The designs this station makes, by category and group (the menu narrows down to the designs of one group).
    private readonly List<CraftDesign> makeable = new List<CraftDesign>();
    private readonly List<string> categories = new List<string>(), groups = new List<string>();
    private readonly List<string> designs = new List<string>(), parts = new List<string>(), joints = new List<string>(), slots = new List<string>();
    private readonly List<int> slotIndex = new List<int>();
    private readonly List<CraftJointWork> works = new List<CraftJointWork>();
    private readonly CraftRhythm rhythm = new CraftRhythm();
    private readonly CraftHammer hammer = new CraftHammer { fullCharge = 0.6f };
    private Slider pull, supplyCount;
    private string shownFor, partSignature;
    private TextMeshProUGUI info;
    private int beats;

    public override string Title => "Assemble";
    public override bool Available(CraftStation s) => s.Has(CraftTools.AssemblyBench) || s.Has(CraftTools.LeatherBench);
    public override string Hint => "Start a design, fit the parts, then work each joint: click on the beat to punch and stitch; hold and release to rivet, peen or wedge.";

    private CraftPiece Assembly
    {
        get
        {
            CraftPiece p = Selected;
            if (p != null && p.kind == CraftPieceKind.Assembly)
                return p;
            CraftPiece last = null;
            foreach (CraftPiece q in Ws.Pieces)
                if (q.kind == CraftPieceKind.Assembly) last = q;
            return last;
        }
    }

    public override void Build(RectTransform controls)
    {
        // Only what this station can make (its tools, and its list of designs when it has one), sorted into categories.
        makeable.Clear();
        categories.Clear();
        foreach (CraftDesign d in Rules.Designs)
            if (Station.Makes(d))
            {
                makeable.Add(d);
                CraftDesignMenu.Of(d, out string c, out _);
                if (!categories.Contains(c)) categories.Add(c);
            }
        categories.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
        RectTransform r0 = Row(controls);
        Text(r0, "Make", 13f, 44f);
        category = Drop(r0, categories.Count > 0 ? categories : new List<string> { "Nothing is made here" }, 0, 150f);
        group = Drop(r0, new List<string> { "All" }, 0, 190f);
        category.onValueChanged.AddListener(_ => { group.SetValueWithoutNotify(0); FillGroups(); });
        group.onValueChanged.AddListener(_ => { design.SetValueWithoutNotify(0); FillDesigns(); });
        RectTransform r1 = Row(controls);
        design = Drop(r1, new List<string> { "-" }, 0, 220f);
        FillGroups();
        Btn(r1, "Start", () => { if (designs.Count > 0) Send(new CraftRequest(CraftRequestKind.StartAssembly) { id = designs[Mathf.Clamp(design.value, 0, designs.Count - 1)] }); shownFor = null; }, 80f, UITheme.ButtonStyle.Primary);
        Btn(r1, "Finish", () => Send(new CraftRequest(CraftRequestKind.Finish, Assembly?.id)), 90f);
        Btn(r1, "Try it (inspect)", () => Send(new CraftRequest(CraftRequestKind.Inspect, Assembly?.id)), 150f);
        RectTransform r2 = Row(controls);
        slot = Drop(r2, new List<string> { "-" }, 0, 200f);
        part = Drop(r2, new List<string> { "-" }, 0, 230f);
        Btn(r2, "Fit it", FitPart, 80f, UITheme.ButtonStyle.Primary);
        Btn(r2, "Take off", () =>
        {
            CraftPiece a = Assembly;
            if (a == null || slots.Count == 0) return;
            Send(new CraftRequest(CraftRequestKind.Detach, a.id) { id = slots[slot.value], i0 = slotIndex[slot.value] });
        }, 90f, UITheme.ButtonStyle.Ghost);
        RectTransform r3 = Row(controls);
        joint = Drop(r3, new List<string> { "-" }, 0, 200f);
        work = Drop(r3, new List<string> { "-" }, 0, 120f);
        pull = Slide(r3, "Pull", 0f, 1f, 0.6f, false, 40f);
        supplyCount = Slide(r3, "Supplies", 1f, 6f, 1f, true, 60f);
        info = InfoRow(controls, 48f);
        shownFor = null;
    }

    private static int Rank(string c)
    {
        int i = Array.IndexOf(CraftDesignMenu.Order, c);
        return i >= 0 ? i : 100;
    }

    private string CategoryShown => categories.Count > 0 ? categories[Mathf.Clamp(category.value, 0, categories.Count - 1)] : null;

    /// <summary>The groups of the category picked ("All" first), then its designs.</summary>
    private void FillGroups()
    {
        groups.Clear();
        groups.Add("All");
        string cat = CategoryShown;
        foreach (CraftDesign d in makeable)
        {
            CraftDesignMenu.Of(d, out string c, out string g);
            if (c == cat && !groups.Contains(g)) groups.Add(g);
        }
        Reset(group, groups);
        FillDesigns();
    }

    /// <summary>The designs of the group picked, with what they are where that helps ("Kettle hat · Head").</summary>
    private void FillDesigns()
    {
        designs.Clear();
        var names = new List<string>();
        string cat = CategoryShown;
        string grp = groups.Count > 0 ? groups[Mathf.Clamp(group.value, 0, groups.Count - 1)] : "All";
        foreach (CraftDesign d in makeable)
        {
            CraftDesignMenu.Of(d, out string c, out string g);
            if (c != cat || grp != "All" && g != grp) continue;
            designs.Add(d.id);
            names.Add(d.kind == CraftDesignKind.Armor && cat == CraftDesignMenu.Armour ? $"{d.Name} · {CraftDesignMenu.Slot(d.armorSlot)}"
                : grp == "All" ? $"{d.Name} · {g}" : d.Name);
        }
        if (designs.Count == 0) names.Add("Nothing is made here");
        Reset(design, names);
    }

    private void FitPart()
    {
        CraftPiece a = Assembly;
        if (a == null) { ui.Message("Start an item first."); return; }
        if (slots.Count == 0) return;
        CraftComponentSpec spec = Rules.Design(a.assembly.design)?.Component(slots[slot.value]);
        if (spec != null && spec.supply)
        {
            Send(new CraftRequest(CraftRequestKind.Attach, a.id) { id = spec.role, count = Mathf.RoundToInt(supplyCount.value) });
            return;
        }
        if (parts.Count == 0 || part.value >= parts.Count) { ui.Message("Nothing on the station fits there."); return; }
        Send(new CraftRequest(CraftRequestKind.Attach, a.id) { id = slots[slot.value], i0 = slotIndex[slot.value], other = parts[part.value] });
    }

    /// <summary>Fills the slot, part, joint and work choices for the assembly in hand.</summary>
    private void Fill()
    {
        CraftPiece a = Assembly;
        string sig = a == null ? "-" : a.id + a.assembly.parts.Count + a.assembly.finished;
        var ps = new System.Text.StringBuilder();
        foreach (CraftPiece q in Ws.Pieces) if (q.location == CraftLocation.Air && q.kind != CraftPieceKind.Assembly) ps.Append(q.id);
        if (sig == shownFor && ps.ToString() == partSignature)
            return;
        shownFor = sig;
        partSignature = ps.ToString();
        slots.Clear();
        slotIndex.Clear();
        joints.Clear();
        parts.Clear();
        var sn = new List<string>();
        var jn = new List<string>();
        var pn = new List<string>();
        CraftDesign d = a != null ? Rules.Design(a.assembly.design) : null;
        if (d != null)
        {
            foreach (CraftComponentSpec c in d.components)
            {
                int count = c.supply ? 1 : Math.Max(1, c.count);
                for (int i = 0; i < count; i++)
                {
                    bool filled = !c.supply && a.assembly.PieceIn(c.role, i) != null;
                    slots.Add(c.role);
                    slotIndex.Add(i);
                    sn.Add($"{c.Name}{(count > 1 ? $" {i + 1}" : "")}{(c.supply ? $" ({a.assembly.Supply(c.role):0}/{c.supplyCount})" : filled ? " ✓" : "")}{(c.optional ? " (optional)" : "")}");
                }
            }
            foreach (CraftJointSpec j in d.joints) { joints.Add(j.id); jn.Add(j.Name); }
        }
        foreach (CraftPiece q in Ws.Pieces)
            if (q.location == CraftLocation.Air && q.kind != CraftPieceKind.Assembly)
            {
                parts.Add(q.id);
                pn.Add(CraftLook.Name(Rules, q, ui.Known));
            }
        Reset(slot, sn);
        Reset(part, pn);
        Reset(joint, jn);
        joint.onValueChanged.RemoveAllListeners();
        joint.onValueChanged.AddListener(_ => FillWorks());
        FillWorks();
    }

    private static void Reset(TMP_Dropdown dd, List<string> options)
    {
        int keep = dd.value;
        dd.ClearOptions();
        dd.AddOptions(options.Count > 0 ? options : new List<string> { "-" });
        dd.SetValueWithoutNotify(Mathf.Clamp(keep, 0, Math.Max(0, options.Count - 1)));
    }

    private void FillWorks()
    {
        works.Clear();
        CraftPiece a = Assembly;
        CraftJointSpec j = a != null && joints.Count > 0 ? Rules.Design(a.assembly.design)?.Joint(joints[Mathf.Clamp(joint.value, 0, joints.Count - 1)]) : null;
        if (j != null)
            switch (j.kind)
            {
                case CraftJointKind.Rivet: works.Add(CraftJointWork.Punch); works.Add(CraftJointWork.Rivet); break;
                case CraftJointKind.Stitch: works.Add(CraftJointWork.Punch); works.Add(CraftJointWork.Stitch); break;
                case CraftJointKind.Buckle: works.Add(CraftJointWork.Punch); works.Add(CraftJointWork.Stitch); works.Add(CraftJointWork.Rivet); break;
                case CraftJointKind.Fit:
                case CraftJointKind.Peen: works.Add(CraftJointWork.Peen); break;
                case CraftJointKind.Wrap: works.Add(CraftJointWork.Wrap); break;
                case CraftJointKind.Wedge: works.Add(CraftJointWork.Wedge); break;
                case CraftJointKind.Socket: works.Add(CraftJointWork.Wedge); works.Add(CraftJointWork.Punch); works.Add(CraftJointWork.Rivet); break;
            }
        var names = new List<string>();
        foreach (CraftJointWork w in works) names.Add(w.ToString());
        Reset(work, names);
    }

    private CraftJointWork? Work => works.Count > 0 ? works[Mathf.Clamp(work.value, 0, works.Count - 1)] : (CraftJointWork?)null;
    private static bool Rhythmic(CraftJointWork w) => w == CraftJointWork.Punch || w == CraftJointWork.Stitch || w == CraftJointWork.Wrap;

    public override void Tick(float dt)
    {
        rhythm.Tick(dt);
        hammer.Tick(dt);
    }

    public override void Leave()
    {
        FlushRhythm();
        hammer.Lower();
    }

    /// <summary>Sends the beats answered so far as one piece of joint work.</summary>
    private void FlushRhythm()
    {
        CraftPiece a = Assembly;
        CraftJointWork? w = Work;
        if (a == null || beats <= 0 || w == null || joints.Count == 0)
        {
            rhythm.Clear();
            beats = 0;
            return;
        }
        double spacing = rhythm.MeanError, spread = rhythm.Spread;
        var r = new CraftRequest(CraftRequestKind.JointWork, a.id) { id = joints[Mathf.Clamp(joint.value, 0, joints.Count - 1)], i0 = (int)w.Value, count = beats };
        if (w == CraftJointWork.Punch) { r.a = (float)spacing; r.b = (float)spread; }
        else { r.a = (float)rhythm.Pull; r.b = (float)spread; }
        Send(r);
        rhythm.Clear();
        beats = 0;
    }

    public override void Draw(CraftPixels c)
    {
        Fill();
        CraftPiece a = Assembly;
        if (a == null)
            return;
        CraftDesign d = Rules.Design(a.assembly.design);
        if (d == null)
            return;
        // The parts in their places.
        int n = d.components.Count;
        float w = 680f / Mathf.Max(1, n);
        var centres = new Dictionary<string, Vector2>();
        for (int i = 0; i < n; i++)
        {
            CraftComponentSpec cs = d.components[i];
            float x = 20 + i * w;
            int have = 0;
            foreach (AssemblyPart ap in a.assembly.parts) if (ap.role == cs.role) have++;
            bool full = cs.supply ? a.assembly.Supply(cs.role) >= cs.supplyCount : have >= cs.count;
            c.Rect(x + 6, 260, x + w - 6, 360, full ? new Color(0.25f, 0.4f, 0.28f, 0.9f) : new Color(0.25f, 0.25f, 0.3f, 0.9f));
            CraftPiece piece = Ws.Piece(a.assembly.PieceIn(cs.role));
            if (piece != null && piece.kind != CraftPieceKind.Hide && piece.segments.Count > 0)
            {
                float mmp = Fit(piece, w - 20, 80);
                CraftArt.DrawTop(c, Rules, piece, new Rect(x + 10, 270, (float)(piece.Length / mmp), 80), mmp);
            }
            else if (piece != null && piece.kind == CraftPieceKind.Hide)
                CraftArt.DrawHide(c, Rules, piece.hide, new Rect(x + 10, 270, w - 20, 80));
            centres[cs.role] = new Vector2(x + w * 0.5f, 260);
        }
        // The joints and how far along they are.
        int k = 0;
        foreach (CraftJointSpec j in d.joints)
        {
            JointState s = a.assembly.Joint(j.id);
            if (!centres.TryGetValue(j.roleA, out Vector2 pa) || !centres.TryGetValue(j.roleB, out Vector2 pb)) continue;
            float y = 230 - k * 22;
            c.Line(pa.x, 260, pa.x, y, new Color(1f, 1f, 1f, 0.3f), 1f);
            c.Line(pb.x, 260, pb.x, y, new Color(1f, 1f, 1f, 0.3f), 1f);
            c.Line(pa.x, y, pb.x, y, s != null && s.done ? new Color(0.5f, 0.9f, 0.55f) : new Color(1f, 0.75f, 0.35f), 3f);
            k++;
        }
        // The rhythm: a beat marker sweeping; click when it reaches the line.
        CraftJointWork? wk = Work;
        if (wk != null && Rhythmic(wk.Value))
        {
            float phase = (float)rhythm.Phase;
            c.Rect(40, 40, 680, 70, new Color(0f, 0f, 0f, 0.4f));
            c.Rect(356, 36, 364, 74, Mark);
            float x = 40 + 640 * (phase < 0.5f ? 0.5f + phase : phase - 0.5f);
            c.Disc(x, 55, 10, new Color(0.6f, 0.85f, 1f));
            Meter(c, 40, 90, 200, 8, 1f - (float)rhythm.MeanError, new Color(0.5f, 0.85f, 0.6f));
        }
        else if (wk != null)
            Meter(c, 40, 60, 200, 10, (float)hammer.Charge, new Color(1f, 0.8f, 0.4f));
    }

    public override void Pointer(CraftPointer phase, Vector2 pos, bool right, float scroll)
    {
        CraftPiece a = Assembly;
        CraftJointWork? w = Work;
        if (a == null || w == null || joints.Count == 0)
            return;
        if (Rhythmic(w.Value))
        {
            if (phase != CraftPointer.Down) return;
            rhythm.Answer(pull.value, Hands);
            beats++;
            if (beats >= 4) FlushRhythm();
            return;
        }
        if (phase == CraftPointer.Down)
            hammer.Raise();
        else if (phase == CraftPointer.Up && hammer.Raised)
        {
            CraftBlow b = hammer.Release(0.5, 0, Hands);
            Send(new CraftRequest(CraftRequestKind.JointWork, a.id) { id = joints[Mathf.Clamp(joint.value, 0, joints.Count - 1)], i0 = (int)w.Value, count = 1, a = (float)(b.force * 4) });
        }
    }

    public override void Refresh()
    {
        if (info == null)
            return;
        CraftPiece a = Assembly;
        if (a == null) { info.text = "Choose a design and Start."; return; }
        CraftDesign d = Rules.Design(a.assembly.design);
        CraftJointSpec j = d != null && joints.Count > 0 ? d.Joint(joints[Mathf.Clamp(joint.value, 0, joints.Count - 1)]) : null;
        JointState s = j != null ? a.assembly.Joint(j.id) : null;
        info.text = j == null || s == null ? "" :
            j.kind == CraftJointKind.Fit ? $"{j.Name}: gap {(Station.Reveals || Station.HasInstrument(CraftInstruments.Calipers) ? $"{s.gap:0.0} mm" : s.gap > 1.5 ? "it rattles" : s.gap >= 0 ? "snug" : "too tight")}" :
            $"{j.Name}: holes {s.holes}, stitches {s.stitches}, rivets {s.rivets}, turns {s.turns}{(s.done ? " - done" : "")}";
    }
}
