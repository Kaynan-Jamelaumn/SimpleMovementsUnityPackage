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
    /// <summary>Index of the treatment in the item.</summary>
    public int treatmentIndex;
}

// Treatment: applying medical items, abilities and scripted treatments to injuries and missing limbs.
public partial class InjuryController
{
    private ActiveTreatment activeTreatment;
    private readonly List<InventoryItem> stackBuffer = new List<InventoryItem>();

    /// <summary>The treatment being applied (null = none).</summary>
    public ActiveTreatment CurrentTreatment => activeTreatment;
    public bool IsTreating => activeTreatment != null;

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
        if (e != null && (e.IsDead || e.IsUnconscious) && ctx.source == TreatmentSource.Item)
        {
            reason = "Not while down.";
            return false;
        }
        if (ctx.source == TreatmentSource.Item)
        {
            if (!Allows(BodyAction.UseItems))
            {
                reason = ReasonFor(BodyAction.UseItems);
                return false;
            }
            if (s.treatmentNeedsAHand && !Hands.AnyHand)
            {
                reason = "You need a usable hand.";
                return false;
            }
        }
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
        Apply(ref ctx);
        if (injury != null) // a reattachment says so itself
            Notify($"{a.Treatment.name}: {injury.Name} on your {a.Part.Name.ToLowerInvariant()}.");
    }

    /// <summary>Stops the treatment being applied (nothing is used). <paramref name="reason"/> is told to the player (null = silent).</summary>
    public void CancelTreatment(string reason)
    {
        ActiveTreatment a = activeTreatment;
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
        InventoryManager inv = InventoryManager.For(this);
        if (inv == null || part == null)
            return into;
        var seen = new HashSet<MedicalItemSO>();
        foreach (MedicalItemSO med in CarriedMedicalItems(inv))
        {
            if (!seen.Add(med))
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
                int count = inv.CountItem(med);
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
            Notify(firstReason ?? $"Nothing to treat with the {item.Name}.");
            return false;
        }
        return BeginTreatment(bestTreatment, bestPart, bestInjury, TreatmentSource.Item, item);

        void Consider(InjuryPartState part, Injury injury)
        {
            for (int t = 0; t < item.Treatments.Count; t++)
            {
                InjuryTreatment tr = item.Treatments[t];
                if (tr == null)
                    continue;
                TreatmentContext ctx = Context(tr, part, injury, TreatmentSource.Item, item, null, 1f);
                if (!CanTreat(ctx, out string reason))
                {
                    if (firstReason == null && tr.Matches(ctx))
                        firstReason = reason;
                    continue;
                }
                float score = injury != null ? injury.BleedingPerSecond * 100f + injury.Severity * 10f + (injury.CanWorsen ? 5f : 0f) : 50f;
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
        TreatmentRequest(new InjuryTreatmentRequest { partId = "", injuryUid = 0, itemId = id, treatmentIndex = -1 });
        return true;
    }

    /// <summary>Server: a client asked for a treatment (validated like a local one). Returns true when it started.</summary>
    public bool ServerHandleRequest(InjuryTreatmentRequest r)
    {
        if (!RulesHere)
            return false;
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
