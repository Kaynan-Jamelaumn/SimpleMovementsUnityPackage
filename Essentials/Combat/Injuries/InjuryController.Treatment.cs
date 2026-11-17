using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A treatment being applied (it takes a few seconds; the health panel shows its progress).</summary>
public sealed class ActiveTreatment
{
    public InjuryTreatment Treatment { get; internal set; }
    public InjuryPartState Part { get; internal set; }
    /// <summary>The injury treated (null for a missing limb).</summary>
    public Injury Injury { get; internal set; }
    public int InjuryUid { get; internal set; }
    public TreatmentSource Source { get; internal set; }
    public ItemSO Item { get; internal set; }
    public CombatEntity Healer { get; internal set; }
    public float Potency { get; internal set; } = 1f;
    public float Duration { get; internal set; }
    public float Elapsed { get; internal set; }

    public float Progress => Duration > 0f ? Mathf.Clamp01(Elapsed / Duration) : 1f;
    public string Label => Treatment != null ? $"{Treatment.name}: {Part?.Name}" : "";
}

/// <summary>One way to treat an injury (or a missing limb) with a medical item the player carries (health panel).</summary>
public struct TreatmentOption
{
    public MedicalItemSO item;
    public InjuryTreatment treatment;
    /// <summary>How many of the item the player has.</summary>
    public int count;
    /// <summary>It can be applied now.</summary>
    public bool usable;
    /// <summary>Why not (empty when usable).</summary>
    public string reason;
}

/// <summary>A client asks the server to apply a treatment with an item (network layer; ids are strings and ints).</summary>
public struct InjuryTreatmentRequest
{
    public string partId;
    /// <summary>0 = the missing limb of the part.</summary>
    public int injuryUid;
    /// <summary>The medical item's id in the Item Database.</summary>
    public string itemId;
    /// <summary>
    /// Index of the treatment in the item. <see cref="Quick"/> = the most urgent injury the item treats;
    /// <see cref="AllBleeding"/> = every bleeding wound in turn, with whatever the character carries (no item named).
    /// </summary>
    public int treatmentIndex;

    public const int Quick = -1;
    public const int AllBleeding = -2;
}

// Treatment: applying medical items, abilities and scripted treatments to injuries and missing limbs.
public partial class InjuryController
{
    private ActiveTreatment activeTreatment;
    private readonly List<InventoryItem> stackBuffer = new List<InventoryItem>();
    private readonly List<TreatmentOption> optionScratch = new List<TreatmentOption>();
    // The medical items carried, looked up once per change of the inventory (the Health tab asks many times a second).
    private readonly List<MedicalItemSO> carriedItems = new List<MedicalItemSO>();
    private readonly List<int> carriedCounts = new List<int>();
    private InventoryManager carriedFrom;
    private bool carriedDirty = true;
    private float carriedExpires;
    private bool chainBleeding;
    private bool saveUrgent;

    /// <summary>Bleeding wounds are being treated one after the other (<see cref="TreatAllBleeding"/>).</summary>
    public bool IsTreatingAllBleeding => chainBleeding && activeTreatment != null;

    /// <summary>The treatment being applied (null = none).</summary>
    public ActiveTreatment CurrentTreatment => activeTreatment;
    public bool IsTreating => activeTreatment != null;

    /// <summary>
    /// Is the character seen treating something? Like <see cref="IsTreating"/>, and also true on the copies other
    /// players have of the character (which are told that much, not what is treated). For animations.
    /// </summary>
    public bool ShowsTreating => activeTreatment != null || replicatedTreating;

    /// <summary>
    /// Raised where the rules run when a medical item's use sound plays. The network layer repeats it on the clients
    /// (<see cref="PresentItemSound"/>), which would otherwise never hear it.
    /// </summary>
    public event Action<ItemSO> ItemSoundPlayed;

    /// <summary>A treatment started (UI progress).</summary>
    public event Action<ActiveTreatment> TreatmentStarted;
    /// <summary>A treatment stopped before finishing (reason).</summary>
    public event Action<ActiveTreatment, string> TreatmentCancelled;

    /// <summary>
    /// Network layer (owning client): asks the server to apply a treatment. Null offline and on the server (it applies
    /// treatments itself).
    /// </summary>
    public Action<InjuryTreatmentRequest> TreatmentRequest { get; set; }

    // ------------------------------------------------------------------ context
    private TreatmentContext Context(InjuryTreatment t, InjuryPartState part, Injury injury, TreatmentSource source, ItemSO item, CombatEntity healer, float potency)
    {
        var ctx = new TreatmentContext
        {
            patient = this,
            part = part,
            injury = injury,
            treatment = t,
            source = source,
            healer = healer != null ? healer : (source == TreatmentSource.Item ? Entity : null),
            item = item,
            potency = potency <= 0f ? 1f : potency,
        };
        if (part != null && part.Lost)
        {
            bool ground = source != TreatmentSource.Item || !Settings.reattachment.limbMustBeCarried;
            ctx.limb = FindLimb(part, ground, out _, out _);
        }
        return ctx;
    }

    /// <summary>
    /// Can <paramref name="t"/> be applied to <paramref name="injury"/> on <paramref name="part"/> (or the part's missing
    /// limb when <paramref name="injury"/> is null) now? <paramref name="reason"/> says why not.
    /// </summary>
    public bool CanTreat(InjuryTreatment t, InjuryPartState part, Injury injury, TreatmentSource source, ItemSO item, out string reason)
    {
        TreatmentContext ctx = Context(t, part, injury, source, item, null, 1f);
        return CanTreat(ctx, out reason);
    }

    private bool CanTreat(in TreatmentContext ctx, out string reason)
    {
        reason = "";
        InjurySettings s = Settings;
        CombatEntity e = Entity;
        if (!s.enabled)
        {
            reason = "The injury system is off.";
            return false;
        }
        if (ctx.treatment == null || ctx.part == null)
        {
            reason = "Nothing to treat.";
            return false;
        }
        if (ctx.source == TreatmentSource.Item && !CanUseMedicine(out reason))
            return false;
        if (ctx.part.Lost && ctx.injury == null)
        {
            if (!ctx.treatment.ForMissingLimbs)
            {
                reason = $"The {ctx.part.Name.ToLowerInvariant()} is missing.";
                return false;
            }
        }
        else if (ctx.injury == null || ctx.part.Find(ctx.injury.Uid) == null)
        {
            reason = "That injury is gone.";
            return false;
        }
        if (!ctx.treatment.Matches(ctx))
        {
            reason = ctx.injury != null && ctx.injury.Bandaged ? "Already treated with it." : "It does not help with this.";
            return false;
        }
        return ctx.treatment.CanApply(ctx, out reason);
    }

    /// <summary>Can the character use a medical item at all right now (awake, allowed to use items, a usable hand)?</summary>
    private bool CanUseMedicine(out string reason)
    {
        reason = "";
        CombatEntity e = Entity;
        if (e != null && (e.IsDead || e.IsUnconscious))
        {
            reason = "Not while down.";
            return false;
        }
        if (!Allows(BodyAction.UseItems))
        {
            reason = ReasonFor(BodyAction.UseItems);
            return false;
        }
        if (Settings.treatmentNeedsAHand && !Hands.AnyHand)
        {
            reason = "You need a usable hand.";
            return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ applying
    /// <summary>
    /// Starts applying a treatment (it takes its Apply Seconds; <paramref name="instant"/> applies it at once). Items are
    /// used up only when it finishes. On a client's copy the request goes to the server. Returns false (and tells the
    /// player why) when it cannot start.
    /// </summary>
    public bool BeginTreatment(InjuryTreatment t, InjuryPartState part, Injury injury, TreatmentSource source, ItemSO item = null,
        CombatEntity healer = null, float potency = 1f, bool instant = false)
    {
        if (!RulesHere)
            return RequestTreatment(t, part, injury, item);
        TreatmentContext ctx = Context(t, part, injury, source, item, healer, potency);
        if (activeTreatment != null)
        {
            Notify("Already treating something.");
            return false;
        }
        if (!CanTreat(ctx, out string reason))
        {
            Notify(reason);
            return false;
        }
        if (source == TreatmentSource.Item && item is MedicalItemSO med && CountCarried(item) < med.UnitsPerUse)
        {
            Notify($"You need {med.UnitsPerUse} {item.Name}.");
            return false;
        }
        float duration = instant ? 0f : t.applySeconds * Settings.treatmentTimeMultiplier;
        activeTreatment = new ActiveTreatment
        {
            Treatment = t, Part = part, Injury = injury, InjuryUid = injury != null ? injury.Uid : 0, Source = source,
            Item = item, Healer = ctx.healer, Potency = ctx.potency, Duration = duration,
        };
        TreatmentStarted?.Invoke(activeTreatment);
        MarkChanged();
        if (duration <= 0f)
            FinishTreatment();
        return true;
    }

    /// <summary>
    /// Applies a treatment at once without using an item (abilities, scripts, city healers). Returns true when it was
    /// applied.
    /// </summary>
    public bool ApplyTreatment(InjuryTreatment t, InjuryPartState part, Injury injury, TreatmentSource source, CombatEntity healer = null, float potency = 1f)
    {
        if (!RulesHere)
            return false;
        TreatmentContext ctx = Context(t, part, injury, source, null, healer, potency);
        if (!CanTreat(ctx, out string reason))
        {
            if (source != TreatmentSource.Ability)
                Notify(reason);
            return false;
        }
        Apply(ref ctx);
        return true;
    }

    private void Apply(ref TreatmentContext ctx)
    {
        ctx.treatment.ApplyEffects(ref ctx);
        effectsDirty = true;
        saveUrgent = true;
        if (logChanges || Settings.logInjuries)
            Debug.Log($"[Injuries] {name}: {ctx.treatment.name} on {ctx.part.Name}{(ctx.injury != null ? " (" + ctx.injury.Name + ")" : "")} [{ctx.source}].", this);
        Treated?.Invoke(ctx.treatment, ctx.part, ctx.injury, ctx.source);
        MarkChanged();
    }

    private void TickTreatment(float dt)
    {
        ActiveTreatment a = activeTreatment;
        if (a == null)
            return;
        CombatEntity e = Entity;
        if (e != null && (e.IsDead || e.IsUnconscious))
        {
            CancelTreatment("Interrupted.");
            return;
        }
        a.Elapsed += dt;
        if (a.Elapsed >= a.Duration)
            FinishTreatment();
        else if (Mathf.FloorToInt((a.Elapsed - dt) * 4f) != Mathf.FloorToInt(a.Elapsed * 4f))
            MarkChanged(); // progress bar
    }

    private void FinishTreatment()
    {
        ActiveTreatment a = activeTreatment;
        activeTreatment = null;
        if (a == null)
            return;
        // Things may have changed while it was applied: the injury healed, the item was dropped.
        Injury injury = a.InjuryUid != 0 ? a.Part.Find(a.InjuryUid) : null;
        if (a.InjuryUid != 0 && injury == null)
        {
            TreatmentCancelled?.Invoke(a, "The injury is gone.");
            MarkChanged();
            return;
        }
        TreatmentContext ctx = Context(a.Treatment, a.Part, injury, a.Source, a.Item, a.Healer, a.Potency);
        if (!CanTreat(ctx, out string reason))
        {
            Notify(reason);
            TreatmentCancelled?.Invoke(a, reason);
            MarkChanged();
            return;
        }
        if (a.Source == TreatmentSource.Item && a.Item != null)
        {
            int units = a.Item is MedicalItemSO med ? med.UnitsPerUse : 1;
            if (UseCarried(a.Item, units) < units)
            {
                Notify($"No {a.Item.Name} left.");
                TreatmentCancelled?.Invoke(a, "No item left.");
                MarkChanged();
                return;
            }
            PlayItemSound(a.Item);
        }
        // An illness that does not show yet stays unnamed: the player only knows they took the medicine.
        bool unseen = injury != null && injury.Hidden;
        string what = injury != null ? injury.Name : "";
        Apply(ref ctx);
        if (unseen)
            Notify(a.Item != null ? $"You take the {a.Item.Name}." : $"{a.Treatment.name}.");
        else if (injury != null) // a reattachment says so itself
            Notify(a.Part.IsWholeBody ? $"{a.Treatment.name}: {what}." : $"{a.Treatment.name}: {what} on your {a.Part.Name.ToLowerInvariant()}.");
        if (chainBleeding && !StartNextBleeding())
            chainBleeding = false;
    }

    /// <summary>Stops the treatment being applied (nothing is used). <paramref name="reason"/> is told to the player (null = silent).</summary>
    public void CancelTreatment(string reason)
    {
        ActiveTreatment a = activeTreatment;
        chainBleeding = false;
        if (a == null)
            return;
        activeTreatment = null;
        if (!string.IsNullOrEmpty(reason))
            Notify($"{a.Treatment.name} stopped: {reason}");
        TreatmentCancelled?.Invoke(a, reason ?? "");
        MarkChanged();
    }

    private void PlayItemSound(ItemSO item)
    {
        if (item == null || item.UseAudioClip == null)
            return;
        PresentItemSound(item);
        ItemSoundPlayed?.Invoke(item);
    }

    /// <summary>
    /// Plays a medical item's use sound at the character. Presentation only: where the rules run it is played by the
    /// treatment itself; a client copy calls it for the sound the server reports.
    /// </summary>
    public void PresentItemSound(ItemSO item)
    {
        if (item == null || item.UseAudioClip == null)
            return;
        CombatEntity e = Entity;
        AbilityPool.PlaySound(item.UseAudioClip, e != null ? e.Center : transform.position, 1f);
    }

    // ------------------------------------------------------------------ items
    /// <summary>How many of <paramref name="item"/> the character carries (bag and hotbar).</summary>
    public int CountCarried(ItemSO item)
    {
        InventoryManager inv = InventoryManager.For(this);
        return inv != null ? inv.CountItem(item) : 0;
    }

    /// <summary>Uses up <paramref name="units"/> of <paramref name="item"/> from the inventory. Returns how many were used.</summary>
    private int UseCarried(ItemSO item, int units)
    {
        InventoryManager inv = InventoryManager.For(this);
        if (inv == null || !inv.IsAuthoritative)
            return 0;
        int used = 0;
        inv.FindStacks(item, true, stackBuffer);
        foreach (InventoryItem stack in stackBuffer)
        {
            if (used >= units)
                break;
            used += inv.ConsumeItem(stack, units - used);
        }
        stackBuffer.Clear();
        return used;
    }

    /// <summary>
    /// The medical items the character carries that treat <paramref name="injury"/> on <paramref name="part"/> (or its
    /// missing limb when <paramref name="injury"/> is null), each with whether it can be used now (health panel).
    /// </summary>
    public List<TreatmentOption> FindItemTreatments(InjuryPartState part, Injury injury, List<TreatmentOption> into = null)
    {
        into = into ?? new List<TreatmentOption>();
        into.Clear();
        if (part == null)
            return into;
        RefreshCarried();
        for (int m = 0; m < carriedItems.Count; m++)
        {
            MedicalItemSO med = carriedItems[m];
            if (med == null)
                continue;
            for (int i = 0; i < med.Treatments.Count; i++)
            {
                InjuryTreatment t = med.Treatments[i];
                if (t == null)
                    continue;
                TreatmentContext ctx = Context(t, part, injury, TreatmentSource.Item, med, null, 1f);
                bool lostTarget = part.Lost && injury == null;
                if (lostTarget ? !t.ForMissingLimbs || !t.target.MatchesPart(part) : !t.target.Matches(injury, part))
                    continue;
                bool usable = CanTreat(ctx, out string reason);
                int count = carriedCounts[m];
                if (usable && count < med.UnitsPerUse)
                {
                    usable = false;
                    reason = $"Needs {med.UnitsPerUse}.";
                }
                into.Add(new TreatmentOption { item = med, treatment = t, count = count, usable = usable, reason = reason });
            }
        }
        return into;
    }

    /// <summary>The medical items carried and how many of each: read again when the inventory changes (and once a second, to be safe).</summary>
    private void RefreshCarried()
    {
        InventoryManager inv = InventoryManager.For(this);
        if (inv != carriedFrom)
        {
            if (carriedFrom != null)
                carriedFrom.InventoryChanged -= OnCarriedChanged;
            carriedFrom = inv;
            if (inv != null)
                inv.InventoryChanged += OnCarriedChanged;
            carriedDirty = true;
        }
        if (!carriedDirty && Time.unscaledTime < carriedExpires)
            return;
        carriedDirty = false;
        carriedExpires = Time.unscaledTime + 1f;
        carriedItems.Clear();
        carriedCounts.Clear();
        if (inv == null)
            return;
        foreach (MedicalItemSO med in CarriedMedicalItems(inv))
        {
            if (carriedItems.Contains(med))
                continue;
            carriedItems.Add(med);
            carriedCounts.Add(inv.CountItem(med));
        }
    }

    private void OnCarriedChanged() => carriedDirty = true;

    private void ReleaseCarried()
    {
        if (carriedFrom != null)
            carriedFrom.InventoryChanged -= OnCarriedChanged;
        carriedFrom = null;
        carriedDirty = true;
        carriedItems.Clear();
        carriedCounts.Clear();
    }

    /// <summary>
    /// The best thing the character carries for <paramref name="injury"/> right now (what stops its bleeding best, lets
    /// it heal, keeps it from getting worse), or for the missing limb of <paramref name="part"/> when
    /// <paramref name="injury"/> is null. False when nothing usable is carried.
    /// </summary>
    public bool BestItemTreatment(InjuryPartState part, Injury injury, out TreatmentOption best)
    {
        best = default;
        FindItemTreatments(part, injury, optionScratch);
        float bestScore = 0f;
        bool found = false;
        for (int i = 0; i < optionScratch.Count; i++)
        {
            TreatmentOption o = optionScratch[i];
            if (!o.usable || o.treatment == null)
                continue;
            float score = o.treatment.ScoreFor(injury);
            if (score <= bestScore)
                continue;
            bestScore = score;
            best = o;
            found = true;
        }
        optionScratch.Clear();
        return found;
    }

    /// <summary>
    /// The most urgent injury the player sees that something carried can treat now, with that item (Health tab ▸ Treat
    /// Most Urgent). False when there is none.
    /// </summary>
    public bool MostUrgentTreatable(out InjuryPartState part, out Injury injury, out TreatmentOption option, bool bleedingOnly = false)
    {
        part = null;
        injury = null;
        option = default;
        float bestUrgency = float.NegativeInfinity;
        for (int p = 0; p < parts.Count; p++)
        {
            InjuryPartState candidate = parts[p];
            for (int i = 0; i < candidate.injuries.Count; i++)
            {
                Injury inj = candidate.injuries[i];
                if (inj.Hidden || (bleedingOnly && !inj.IsBleeding))
                    continue;
                float urgency = inj.Urgency;
                if (urgency <= bestUrgency || !BestItemTreatment(candidate, inj, out TreatmentOption o))
                    continue;
                // Only something that stops bleeding counts while looking for bleeding to stop.
                if (bleedingOnly && o.treatment.BleedingStopped <= 0f)
                    continue;
                bestUrgency = urgency;
                part = candidate;
                injury = inj;
                option = o;
            }
        }
        return injury != null;
    }

    /// <summary>Starts treating the most urgent injury with the best item carried. Returns true when a treatment started.</summary>
    public bool TreatMostUrgent()
    {
        if (!MostUrgentTreatable(out InjuryPartState part, out Injury injury, out TreatmentOption option))
        {
            Notify("You carry nothing that treats your injuries.");
            return false;
        }
        return BeginTreatment(option.treatment, part, injury, TreatmentSource.Item, option.item);
    }

    /// <summary>
    /// Treats every bleeding wound in turn, the worst first, with what the character carries that stops bleeding; it
    /// stops when nothing bleeds, nothing carried helps, or a hit interrupts it. Returns true when the first treatment
    /// started.
    /// </summary>
    public bool TreatAllBleeding()
    {
        if (!RulesHere)
        {
            if (TreatmentRequest == null)
                return false;
            TreatmentRequest(new InjuryTreatmentRequest { partId = "", injuryUid = 0, itemId = "", treatmentIndex = InjuryTreatmentRequest.AllBleeding });
            return true;
        }
        if (activeTreatment != null)
        {
            Notify("Already treating something.");
            return false;
        }
        if (!StartNextBleeding())
        {
            Notify(IsBleeding ? "You carry nothing that stops the bleeding." : "Nothing is bleeding.");
            return false;
        }
        chainBleeding = true;
        return true;
    }

    private bool StartNextBleeding()
    {
        if (!MostUrgentTreatable(out InjuryPartState part, out Injury injury, out TreatmentOption option, true))
            return false;
        bool chaining = chainBleeding;
        bool started = BeginTreatment(option.treatment, part, injury, TreatmentSource.Item, option.item);
        chainBleeding = chaining && started; // an instant treatment finished already and went on by itself
        return started;
    }

    /// <summary>
    /// Cuts <paramref name="part"/> off on purpose (a treatment: gangrene that can no longer be saved). Its injuries go
    /// with it; the stump is closed at once and the limb cannot be put back. Returns false when the part cannot be lost.
    /// </summary>
    public bool Amputate(InjuryPartState part, CombatEntity surgeon = null)
    {
        if (part == null || part.Lost || part.IsWholeBody || !RulesHere || part.Def.limbLoss == null || !part.Def.limbLoss.canBeLost)
            return false;
        amputating = true;
        SeveredLimbRecord record;
        try
        {
            record = LoseLimb(part, surgeon, Vector3.zero, part.Condition01);
        }
        finally
        {
            amputating = false;
        }
        if (record == null)
            return false;
        // A surgeon's cut: the stump is closed (it only has to heal).
        for (int i = 0; i < part.injuries.Count; i++)
        {
            Injury stump = part.injuries[i];
            if (stump.Type == null || !stump.Type.HasTag(InjuryDefaults.TagStump))
                continue;
            stump.Bandaged = true;
            stump.BandageEffectiveness = 1f;
            stump.Treated = true;
            stump.TreatmentLabel = "Amputated";
            stump.HealingAllowed = true;
            stump.WorseningStopped = true;
            stump.TreatedHealMultiplier = Mathf.Max(stump.TreatedHealMultiplier, 1.5f);
        }
        effectsDirty = true;
        MarkChanged();
        return true;
    }

    private static IEnumerable<MedicalItemSO> CarriedMedicalItems(InventoryManager inv)
    {
        GameObject[] slots = inv.StorageSlots;
        if (slots == null)
            yield break;
        foreach (GameObject go in slots)
        {
            InventorySlot slot = go != null ? go.GetComponent<InventorySlot>() : null;
            InventoryItem it = slot != null && slot.heldItem != null ? slot.heldItem.GetComponent<InventoryItem>() : null;
            if (it != null && it.itemScriptableObject is MedicalItemSO med && it.stackCurrent > 0)
                yield return med;
        }
    }

    /// <summary>
    /// Treats the most urgent thing <paramref name="item"/> can treat (hotbar / quickslot use): bleeding first, then the
    /// worst injury, then a missing limb it can reattach. Returns true when a treatment started.
    /// <para>An illness that does not show yet is reached only by a medicine taken to be safe (Preventive: a vaccine, an
    /// antitoxin), and taking one looks the same whether it found something or not - at once, used up, the same words -
    /// so it tells nothing about what is incubating. Other medicines act on what the player can see.</para>
    /// </summary>
    public bool QuickTreat(MedicalItemSO item)
    {
        if (item == null)
            return false;
        if (!RulesHere)
        {
            // The server decides; ask it to treat the most urgent injury with this item.
            return TreatmentRequest != null && RequestQuick(item);
        }
        InjuryPartState bestPart = null;
        Injury bestInjury = null;
        InjuryTreatment bestTreatment = null;
        float bestScore = float.NegativeInfinity;
        string firstReason = null;
        for (int p = 0; p < parts.Count; p++)
        {
            InjuryPartState part = parts[p];
            if (part.Lost && part.LostWith == null)
                Consider(part, null);
            for (int i = 0; i < part.injuries.Count; i++)
                Consider(part, part.injuries[i]);
        }
        if (bestTreatment == null)
        {
            // A medicine taken to be safe (a vaccine after a bite): it is used up whether it found something or not, so
            // taking it tells nothing about an illness that does not show yet. The same checks as a treatment, in the
            // same order, so that what stops it says nothing either.
            if (item.Preventive && firstReason == null)
            {
                if (activeTreatment != null)
                {
                    Notify("Already treating something.");
                    return false;
                }
                if (!CanUseMedicine(out string why))
                {
                    Notify(why);
                    return false;
                }
                if (CountCarried(item) < item.UnitsPerUse)
                {
                    Notify($"You need {item.UnitsPerUse} {item.Name}.");
                    return false;
                }
                if (UseCarried(item, item.UnitsPerUse) < item.UnitsPerUse)
                {
                    Notify($"No {item.Name} left.");
                    return false;
                }
                PlayItemSound(item);
                Notify($"You take the {item.Name}.");
                return true;
            }
            Notify(firstReason ?? $"Nothing to treat with the {item.Name}.");
            return false;
        }
        // Against something that does not show yet it works at once, as it does when it finds nothing.
        return BeginTreatment(bestTreatment, bestPart, bestInjury, TreatmentSource.Item, item, null, 1f, bestInjury != null && bestInjury.Hidden);

        void Consider(InjuryPartState part, Injury injury)
        {
            bool unseen = injury != null && injury.Hidden;
            if (unseen && !item.Preventive)
                return;
            for (int t = 0; t < item.Treatments.Count; t++)
            {
                InjuryTreatment tr = item.Treatments[t];
                if (tr == null)
                    continue;
                TreatmentContext ctx = Context(tr, part, injury, TreatmentSource.Item, item, null, 1f);
                if (!CanTreat(ctx, out string reason))
                {
                    // Why it cannot treat something that does not show yet is not told (it would give it away).
                    if (firstReason == null && !unseen && tr.Matches(ctx))
                        firstReason = reason;
                    continue;
                }
                // What shows comes first; something incubating only when nothing else needs the medicine.
                float score = injury == null ? 50f
                    : unseen ? -1000f
                    : injury.BleedingPerSecond * 100f + injury.Severity * 10f + (injury.CanWorsen ? 5f : 0f);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPart = part;
                    bestInjury = injury;
                    bestTreatment = tr;
                }
            }
        }
    }

    // ------------------------------------------------------------------ network requests
    private bool RequestTreatment(InjuryTreatment t, InjuryPartState part, Injury injury, ItemSO item)
    {
        if (TreatmentRequest == null || part == null || !(item is MedicalItemSO med))
            return false;
        int index = -1;
        for (int i = 0; i < med.Treatments.Count; i++)
            if (med.Treatments[i] == t) { index = i; break; }
        string id = ItemDatabase.Id(item);
        if (index < 0 || string.IsNullOrEmpty(id))
            return false;
        TreatmentRequest(new InjuryTreatmentRequest { partId = part.Id, injuryUid = injury != null ? injury.Uid : 0, itemId = id, treatmentIndex = index });
        return true;
    }

    private bool RequestQuick(MedicalItemSO item)
    {
        string id = ItemDatabase.Id(item);
        if (string.IsNullOrEmpty(id))
            return false;
        TreatmentRequest(new InjuryTreatmentRequest { partId = "", injuryUid = 0, itemId = id, treatmentIndex = InjuryTreatmentRequest.Quick });
        return true;
    }

    /// <summary>Server: a client asked for a treatment (validated like a local one). Returns true when it started.</summary>
    public bool ServerHandleRequest(InjuryTreatmentRequest r)
    {
        if (!RulesHere)
            return false;
        if (r.treatmentIndex == InjuryTreatmentRequest.AllBleeding)
            return TreatAllBleeding();
        MedicalItemSO med = ItemDatabase.Item(r.itemId) as MedicalItemSO;
        if (med == null || CountCarried(med) <= 0)
            return false;
        if (r.treatmentIndex < 0)
            return QuickTreat(med);
        if (r.treatmentIndex >= med.Treatments.Count)
            return false;
        InjuryPartState part = GetPart(r.partId);
        if (part == null)
            return false;
        Injury injury = r.injuryUid != 0 ? part.Find(r.injuryUid) : null;
        if (r.injuryUid != 0 && injury == null)
            return false;
        return BeginTreatment(med.Treatments[r.treatmentIndex], part, injury, TreatmentSource.Item, med);
    }
}
