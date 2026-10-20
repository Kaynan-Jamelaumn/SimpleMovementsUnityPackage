using System.Collections.Generic;
using UnityEngine;

// Limb loss and reattachment: severing a part (and what is attached to it), dropping its armour and the item it held,
// spawning the limb as an item to retrieve, hiding it on the character, and putting it back.
public partial class InjuryController
{
    private sealed class HiddenVisuals
    {
        public readonly List<Transform> bones = new List<Transform>();
        public readonly List<Vector3> scales = new List<Vector3>();
        public readonly List<Renderer> renderers = new List<Renderer>();
    }

    private readonly Dictionary<InjuryPartState, HiddenVisuals> hidden = new Dictionary<InjuryPartState, HiddenVisuals>();
    private readonly List<InjuryBodyPart> attachedBuffer = new List<InjuryBodyPart>();
    private Transform[] boneCache;
    private bool warnedRuntimeLimbInSession;

    // ------------------------------------------------------------------ losing a limb
    private bool TryLoseFromHit(InjuryPartState part, in DamageInfo info, float hitFraction, float conditionBefore)
    {
        LimbLossRules rules = part.Def.limbLoss;
        if (rules == null || !rules.canBeLost || part.Lost)
            return false;
        if ((rules.damageKinds & BodyActions.KindOf(info.type)) == 0)
            return false;
        if (info.delivery == DamageDelivery.Area && !rules.areaDamageCanSever)
            return false;
        if (rules.onlyWeapons != null && rules.onlyWeapons.Count > 0 && (info.weapon == null || !rules.onlyWeapons.Contains(info.weapon.Category)))
            return false;
        bool reachedZero = part.Condition <= 0f && conditionBefore > 0f;
        bool bySingleHit = rules.singleHitThreshold > 0f && hitFraction + 1e-5f >= rules.singleHitThreshold;
        bool byZero = reachedZero && (rules.atZeroCondition || part.Def.atZeroCondition == ZeroConditionRule.LoseLimb);
        if (!bySingleHit && !byZero)
            return false;
        // Blades sever, blunt blows almost never do; sturdy limbs (traits, items) resist.
        DamageForm form = DamageForms.Resolve(info);
        float chance = rules.chance * (rules.formChances != null ? rules.formChances.For(form) : 1f);
        InjuryResistances res = Resistances;
        if (res != null)
            chance *= res.LimbLossMultiplier(part.Def, form);
        if (chance <= 0f || (chance < 1f && UnityEngine.Random.value > chance))
            return false;
        LoseLimb(part, info.source, info.direction, Mathf.Clamp01(conditionBefore / part.MaxCondition));
        return true;
    }

    /// <summary>Cuts a part off by script (ignores the limb rules' chances, not Can Be Lost). Returns its record, or null.</summary>
    public SeveredLimbRecord LoseLimb(string partId, CombatEntity cause = null)
    {
        InjuryPartState part = GetPart(partId);
        if (part == null || part.Lost || part.Def.limbLoss == null || !part.Def.limbLoss.canBeLost)
            return null;
        return LoseLimb(part, cause, Vector3.zero, part.Condition01);
    }

    /// <summary>
    /// Cuts <paramref name="part"/> off with everything attached to it: its injuries go with it, its armour and the item it
    /// held fall, the limb drops as an item (its condition and time limit kept in a <see cref="SeveredLimbRecord"/>), it
    /// is hidden on the character, and the stump bleeds.
    /// </summary>
    public SeveredLimbRecord LoseLimb(InjuryPartState part, CombatEntity cause, Vector3 direction, float condition01)
    {
        if (part == null || part.Lost || !RulesHere)
            return null;
        InjurySettings s = Settings;
        CombatEntity e = Entity;

        SeveredLimbRecord record = SeveredLimbRegistry.Create();
        record.PartId = part.Id;
        record.PartName = part.Name;
        record.Owner = this;
        record.OwnerName = OwnerName();
        record.ConditionAtLoss = Mathf.Clamp(condition01, 0.05f, 1f);
        record.LostAt = Time.time;
        record.TimeLimit = s.TimeLimitFor(part.Def);
        record.DecayPerSecond = s.reattachment.limbDecayPerSecond;

        // Where it comes off (before the bones are hidden).
        Vector3 at = LimbPosition(part);

        var lostParts = new List<InjuryPartState> { part };
        attachedBuffer.Clear();
        s.CollectAttached(part.Def, attachedBuffer);
        foreach (InjuryBodyPart def in attachedBuffer)
        {
            InjuryPartState child = GetPart(def);
            if (child == null || child.Lost)
                continue;
            lostParts.Add(child);
            record.IncludedParts.Add(child.Name);
        }
        foreach (InjuryPartState p in lostParts)
        {
            for (int i = p.injuries.Count - 1; i >= 0; i--)
                RemoveRunner(p.injuries[i]);
            p.injuries.Clear();
            p.Lost = true;
            p.Condition = 0f;
            p.LostWith = p == part ? null : part;
            p.LostLimb = p == part ? record : null;
        }

        DropWornAndHeld(lostParts, record, at);
        SpawnLimb(part, record, at, direction);
        foreach (InjuryPartState p in lostParts)
            HideVisuals(p);

        LimbLossRules rules = part.Def.limbLoss;
        if (rules != null)
        {
            InjuryTypeDefinition stump = string.IsNullOrWhiteSpace(rules.stumpInjury) ? null : s.FindType(rules.stumpInjury);
            if (stump != null)
                AddInjury(part, stump, stump.StageForSeverity(rules.stumpSeverity), cause, false);
            if (rules.vfx != null)
                AbilityPool.PlayVfx(rules.vfx, at, Quaternion.identity, 0f);
            if (rules.sound != null)
                AbilityPool.PlaySound(rules.sound, at, 1f);
        }

        effectsDirty = true;
        if (logChanges || s.logInjuries)
            Debug.Log($"[Injuries] {name} lost the {part.Name}{(record.IncludedParts.Count > 0 ? " with the " + string.Join(", ", record.IncludedParts) : "")}.", this);
        string with = record.IncludedParts.Count > 0 ? $" (with your {string.Join(", ", record.IncludedParts).ToLowerInvariant()})" : "";
        Notify($"You lost your {part.Name.ToLowerInvariant()}{with}! Recover it and reattach it within {SeveredLimbRecord.FormatTime(record.TimeLimit)}.");
        LimbLost?.Invoke(part, record);
        AnyLimbLost?.Invoke(this, part, record);
        SeveredLimbRegistry.NotifyChanged(record);
        MarkChanged();
        RecomputeEffects();
        return record;
    }

    private string OwnerName()
    {
        PlayerNameComponent n = GetComponentInChildren<PlayerNameComponent>(true);
        if (n == null)
            n = GetComponentInParent<PlayerNameComponent>();
        if (n != null && !string.IsNullOrWhiteSpace(n.PlayerName))
            return n.PlayerName;
        CombatEntity e = Entity;
        return e != null ? e.name : name;
    }

    /// <summary>Where a part is on the body (its bone, else from the character's size and the part's side and height).</summary>
    public Vector3 LimbPosition(InjuryPartState part)
    {
        Transform bone = PrimaryBone(part);
        if (bone != null)
            return bone.position;
        CombatEntity e = Entity;
        if (e == null)
            return transform.position + Vector3.up;
        float h = (part.Def.heightRange.x + part.Def.heightRange.y) * 0.5f;
        float side = part.Def.side == BodyPartSide.Right ? 1f : part.Def.side == BodyPartSide.Left ? -1f : 0f;
        return e.BasePosition + Vector3.up * e.Height * h + e.transform.right * side * e.Radius * 1.2f;
    }

    /// <summary>Drops the armour worn on the lost parts and the item held by a lost hand (or a two-handed weapon).</summary>
    private void DropWornAndHeld(List<InjuryPartState> lostParts, SeveredLimbRecord record, Vector3 at)
    {
        InventoryManager inv = InventoryManager.For(this);
        if (inv == null || !inv.IsAuthoritative)
            return;
        InjurySettings s = Settings;
        bool changed = false;

        // Armour worn on the lost parts.
        var slots = new List<ArmorSlotType>();
        foreach (InjuryPartState p in lostParts)
            if (p.Def.armorSlots != null)
                foreach (ArmorSlotType a in p.Def.armorSlots)
                    if (!slots.Contains(a)) slots.Add(a);
        if (slots.Count > 0)
        {
            foreach (InventorySlot slot in new List<InventorySlot>(inv.EquipmentSlots))
            {
                InventoryItem item = slot != null && slot.heldItem != null ? slot.heldItem.GetComponent<InventoryItem>() : null;
                if (item == null || !(item.itemScriptableObject is ArmorSO armor) || !slots.Contains(armor.ArmorSlotType))
                    continue;
                if (DropToWorld(inv, item, slot, record, at))
                    changed = true;
            }
        }

        // Items held by a lost hand.
        bool lostMain = false, lostOff = false;
        foreach (InjuryPartState p in lostParts)
        {
            if (!p.Def.HoldsAHand || p.Def.side == BodyPartSide.Center)
                continue;
            if (p.Def.side == s.mainHandSide) lostMain = true;
            else lostOff = true;
        }
        InventoryItem held = inv.SelectedHotbarItem;
        bool heldTwoHanded = held != null && held.itemScriptableObject is WeaponSO w && w.IsTwoHanded;
        if (held != null && (lostMain || (lostOff && heldTwoHanded)))
        {
            InventorySlot slot = inv.FindSlotHolding(held.gameObject);
            if (DropToWorld(inv, held, slot, record, at))
                changed = true;
        }
        InventoryItem off = inv.OffHandItem;
        if (off != null && lostOff && DropToWorld(inv, off, inv.OffHandSlot, record, at))
            changed = true;

        if (changed)
        {
            ArmorEquipmentHandler.NotifyEquipmentChanged(inv.PlayerStatus);
            inv.RefreshHands();
            inv.NotifyInventoryChanged();
        }
    }

    /// <summary>Drops one item into the world next to the limb (into the bag when it has no model to drop).</summary>
    private bool DropToWorld(InventoryManager inv, InventoryItem item, InventorySlot slot, SeveredLimbRecord record, Vector3 at)
    {
        if (item == null || item.itemScriptableObject == null)
            return false;
        ItemSO so = item.itemScriptableObject;
        Vector2 r = UnityEngine.Random.insideUnitCircle * 0.6f;
        GameObject go = ItemHandler.SpawnWorldItem(so, item.stackCurrent, item.DurabilityList, at + new Vector3(r.x, 0.2f, r.y), inv.Player);
        if (go == null)
        {
            // No model (or a network client): it cannot lie in the world, so it goes to the bag instead of staying in use.
            if (slot != null && slot.SlotType != SlotType.Common && inv.PlaceInBag(item))
            {
                record.DroppedItems.Add(so.Name + " (to your bag)");
                return true;
            }
            return false;
        }
        SeveredLimbLink.Attach(go, record, false, Settings.cleanup != null ? Settings.cleanup.droppedItemSeconds : 0f);
        InventoryUtils.UpdatePlayerWeight(inv.Player, -so.Weight * item.stackCurrent);
        if (slot != null && slot.heldItem == item.gameObject)
            slot.heldItem = null;
        Destroy(item.gameObject);
        record.DroppedItems.Add(so.Name);
        return true;
    }

    /// <summary>Spawns the lost limb as an item in the world (carrying its record id).</summary>
    private void SpawnLimb(InjuryPartState part, SeveredLimbRecord record, Vector3 at, Vector3 direction)
    {
        if (!Application.isPlaying)
            return; // inspector tests and Edit Mode: no world objects
        LimbLossRules rules = part.Def.limbLoss;
        ItemSO item = rules != null && rules.limbItem != null ? (ItemSO)rules.limbItem : null;
        if (item == null)
        {
            if (GameNet.NetAuthority.IsNetworked)
            {
                if (!warnedRuntimeLimbInSession)
                    Debug.LogWarning($"[Injuries] {part.Name} has no Limb Item asset: lost limbs cannot be dropped in a network session. " +
                                     "Injury Settings ▸ Create Limb Items.", this);
                warnedRuntimeLimbInSession = true;
                return;
            }
            item = SeveredLimbItemSO.CreateRuntime(part.Def, record, rules != null ? rules.icon : null, rules != null ? rules.severedPrefab : null);
        }
        Vector3 push = direction.sqrMagnitude > 1e-4f ? new Vector3(direction.x, 0f, direction.z).normalized * 0.5f : Vector3.zero;
        Quaternion rot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
        GameObject go = ItemHandler.SpawnWorldItemAt(item, 1, new List<int> { record.Id }, at + push, rot, Entity != null ? Entity.gameObject : gameObject, true, false);
        if (go != null)
            SeveredLimbLink.Attach(go, record, true, Settings.LimbDespawnSecondsFor(part.Def));
    }

    // ------------------------------------------------------------------ finding a lost limb
    /// <summary>
    /// Finds the lost limb of <paramref name="part"/>: in the inventory (always), or lying within the Reattachment rules'
    /// Ground Range when <paramref name="allowGround"/>. Returns its record (null = not found).
    /// </summary>
    public SeveredLimbRecord FindLimb(InjuryPartState part, bool allowGround, out InventoryItem carried, out ItemPickable lying)
    {
        carried = null;
        lying = null;
        if (part == null || !part.Lost)
            return null;
        InjuryPartState root = part.LostWith ?? part;
        InjurySettings s = Settings;
        InventoryManager inv = InventoryManager.For(this);
        if (inv != null && inv.StorageSlots != null)
        {
            foreach (GameObject go in inv.StorageSlots)
            {
                InventorySlot slot = go != null ? go.GetComponent<InventorySlot>() : null;
                InventoryItem it = slot != null && slot.heldItem != null ? slot.heldItem.GetComponent<InventoryItem>() : null;
                SeveredLimbRecord r = SeveredLimbItemSO.RecordOf(it);
                if (Fits(r, root, s))
                {
                    carried = it;
                    return r;
                }
            }
        }
        if (!allowGround)
            return null;
        CombatEntity e = Entity;
        Vector3 pos = e != null ? e.Position : transform.position;
        float range = s.reattachment.groundRange;
        foreach (SeveredLimbLink link in FindObjectsByType<SeveredLimbLink>(FindObjectsInactive.Exclude))
        {
            if (link == null || !link.isLimb || (link.transform.position - pos).sqrMagnitude > range * range)
                continue;
            ItemPickable p = link.GetComponent<ItemPickable>();
            SeveredLimbRecord r = p != null ? SeveredLimbItemSO.RecordOf(p) : link.Record;
            if (p != null && !p.Taken && Fits(r, root, s))
            {
                lying = p;
                return r;
            }
        }
        return null;
    }

    private bool Fits(SeveredLimbRecord r, InjuryPartState part, InjurySettings s)
    {
        if (r == null || r.Consumed || r.Gone || !part.Def.Matches(r.PartId))
            return false;
        return !s.reattachment.onlyOwnLimb || r.Owner == this;
    }

    // ------------------------------------------------------------------ reattaching
    /// <summary>
    /// Puts a lost limb back (called by <see cref="ReattachLimbTreatment"/>; the checks are done there): the part and what
    /// came off with it are back with part of the limb's condition, visuals return, the stump injury goes, the knitting
    /// injury is added, the limb item is used up and the hands work again.
    /// </summary>
    public void ReattachLimb(InjuryPartState part, SeveredLimbRecord limb, ReattachLimbTreatment how, TreatmentSource source)
    {
        if (part == null || !part.Lost || !RulesHere)
            return;
        InjurySettings s = Settings;
        if (limb != null)
        {
            FindLimb(part, true, out InventoryItem carried, out ItemPickable lying);
            if (carried != null)
            {
                InventoryManager inv = InventoryManager.For(this);
                if (inv != null)
                    inv.ConsumeItem(carried, 1);
            }
            else if (lying != null)
            {
                lying.Taken = true;
                WorldItems.Remove(lying.gameObject);
            }
            limb.Consumed = true;
            SeveredLimbRegistry.NotifyChanged(limb);
        }
        float fraction = how != null && how.restoredCondition >= 0f
            ? how.restoredCondition
            : s.reattachment.restoredCondition * (limb != null ? limb.Condition : 1f);
        fraction = Mathf.Clamp(fraction, 0.05f, 1f);

        RestorePart(part);
        part.Condition = part.MaxCondition * fraction;
        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i].LostWith != part)
                continue;
            RestorePart(parts[i]);
            parts[i].Condition = parts[i].MaxCondition * fraction;
        }
        if (how == null || !how.skipAfterInjury)
        {
            InjuryTypeDefinition after = string.IsNullOrWhiteSpace(s.reattachment.afterInjury) ? null : s.FindType(s.reattachment.afterInjury);
            if (after != null)
            {
                Injury knit = AddInjury(part, after, after.StageForSeverity(s.reattachment.afterSeverity), null, false);
                if (knit != null)
                {
                    knit.Treated = true;
                    knit.TreatmentLabel = source == TreatmentSource.Ability ? "Mended" : "Reattached";
                    knit.HealingAllowed = true;
                }
            }
        }
        effectsDirty = true;
        Notify($"Your {part.Name.ToLowerInvariant()} is back on.");
        LimbReattached?.Invoke(part, limb);
        MarkChanged();
        RecomputeEffects();
    }

    /// <summary>A lost part is back (no injury, visuals shown again).</summary>
    private void RestorePart(InjuryPartState p)
    {
        for (int i = p.injuries.Count - 1; i >= 0; i--)
            RemoveRunner(p.injuries[i]);
        p.injuries.Clear();
        p.Lost = false;
        p.LostWith = null;
        p.LostLimb = null;
        RestoreVisuals(p);
    }

    /// <summary>Renames limbs whose time ran out ("Rotten ...") and tells the player once.</summary>
    private void TickLimbRecords()
    {
        if (Time.time < nextExpiryCheck)
            return;
        nextExpiryCheck = Time.time + 1f;
        for (int i = 0; i < parts.Count; i++)
        {
            SeveredLimbRecord r = parts[i].LostLimb;
            if (r == null || r.Consumed || !r.Expired || expiredNotified.Contains(r.Id))
                continue;
            expiredNotified.Add(r.Id);
            Notify($"Your {r.PartName.ToLowerInvariant()} can no longer be reattached normally.");
            MarkChanged();
        }
    }

    private readonly HashSet<int> expiredNotified = new HashSet<int>();

    /// <summary>A lost limb's record changed outside this controller (its world item was removed): the panel and the network follow.</summary>
    internal void LimbRecordChanged(SeveredLimbRecord r)
    {
        if (r != null && r.Gone)
            Notify($"Your {r.PartName.ToLowerInvariant()} lay too long and is gone.");
        MarkChanged();
    }

    // ------------------------------------------------------------------ visuals
    private Transform PrimaryBone(InjuryPartState part)
    {
        if (hidden.TryGetValue(part, out HiddenVisuals hv) && hv.bones.Count > 0)
            return hv.bones[0];
        InjuryBodyPart def = part.Def;
        if (def.useHumanoidBone)
        {
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                Transform t = anim.GetBoneTransform(def.humanoidBone);
                if (t != null)
                    return t;
            }
        }
        if (def.bones != null)
            foreach (string b in def.bones)
            {
                Transform t = FindBone(b);
                if (t != null)
                    return t;
            }
        return null;
    }

    private void HideVisuals(InjuryPartState part)
    {
        if (hidden.ContainsKey(part))
            return;
        var hv = new HiddenVisuals();
        InjuryBodyPart def = part.Def;
        if (def.useHumanoidBone)
        {
            Animator anim = GetComponentInChildren<Animator>();
            Transform t = anim != null && anim.isHuman ? anim.GetBoneTransform(def.humanoidBone) : null;
            if (t != null) Collapse(hv, t);
        }
        if (def.bones != null)
            foreach (string b in def.bones)
            {
                Transform t = FindBone(b);
                if (t != null) Collapse(hv, t);
            }
        if (def.renderers != null)
            foreach (string rn in def.renderers)
            {
                Transform t = FindBone(rn);
                Renderer r = t != null ? t.GetComponent<Renderer>() : null;
                if (r == null || !r.enabled) continue;
                r.enabled = false;
                hv.renderers.Add(r);
            }
        hidden[part] = hv;
    }

    private static void Collapse(HiddenVisuals hv, Transform t)
    {
        if (hv.bones.Contains(t))
            return;
        hv.bones.Add(t);
        hv.scales.Add(t.localScale);
        t.localScale = Vector3.zero;
    }

    private void RestoreVisuals(InjuryPartState part)
    {
        if (!hidden.TryGetValue(part, out HiddenVisuals hv))
            return;
        for (int i = 0; i < hv.bones.Count; i++)
            if (hv.bones[i] != null)
                hv.bones[i].localScale = hv.scales[i];
        foreach (Renderer r in hv.renderers)
            if (r != null) r.enabled = true;
        hidden.Remove(part);
    }

    private void RestoreAllVisuals()
    {
        foreach (InjuryPartState p in new List<InjuryPartState>(hidden.Keys))
            RestoreVisuals(p);
    }

    /// <summary>Collapsed bones stay collapsed even when an animation writes their scale.</summary>
    private void KeepVisualsHidden()
    {
        foreach (KeyValuePair<InjuryPartState, HiddenVisuals> kv in hidden)
            for (int i = 0; i < kv.Value.bones.Count; i++)
                if (kv.Value.bones[i] != null)
                    kv.Value.bones[i].localScale = Vector3.zero;
    }

    private Transform FindBone(string boneName)
    {
        if (string.IsNullOrWhiteSpace(boneName))
            return null;
        if (boneCache == null)
            boneCache = GetComponentsInChildren<Transform>(true);
        string n = boneName.Trim();
        for (int i = 0; i < boneCache.Length; i++)
            if (boneCache[i] != null && string.Equals(boneCache[i].name, n, System.StringComparison.OrdinalIgnoreCase))
                return boneCache[i];
        return null;
    }
}
