using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    // The runes of a soul (design spec 2, 3.2, 4.2, 5): absorbing (capacity checked), releasing (if allowed), pending
    // magical energy and giving it to the rune the player chooses, concept knowledge. Changes happen where the rules run;
    // the owner's client asks with requests and sees the result in the replicated rune list.
    public partial class RuneSoul
    {
        private int runesVersion = 1;
        // Runes whose energy alone changed (a cast, giving energy, an imprint) are sent as a few numbers, not as the whole list.
        private int energyVersion;
        private int replicatedEnergyCount;
        private readonly RuneListView replicatedRunes = new RuneListView();
        private readonly List<ItemSO> absorbQueue = new List<ItemSO>();
        private bool energyHinted;

        /// <summary>A rune was absorbed (where the rules run).</summary>
        public event Action<RuneSoul, RuneInstance> RuneAbsorbed;
        /// <summary>A rune was released (where the rules run).</summary>
        public event Action<RuneSoul, RuneInstance> RuneReleased;
        /// <summary>Magical energy was gained (where the rules run): the amount kept in the pending pool.</summary>
        public event Action<RuneSoul, float> EnergyGained;

        private static RuneCatalog Catalog => RuneMagicDatabase.Instance.Catalog;

        /// <summary>The runes absorbed into the soul (on the owner's client: the server's, as last sent).</summary>
        public IReadOnlyList<RuneInstance> Runes => IsAuthority ? (IReadOnlyList<RuneInstance>)state.runes : replicatedRunes.runes;

        /// <summary>
        /// Changes whenever anything about the runes changes: which runes are held, the concept knowledge, or the energy of a rune (the
        /// windows refresh on it). Only ever goes up.
        /// </summary>
        public int RunesVersion => IsAuthority ? runesVersion + energyVersion : replicatedRunes.version + replicatedEnergyCount;

        /// <summary>Changes when runes are gained, lost or changed, or concepts are learnt (the whole rune list is sent then). Where the rules run.</summary>
        public int StructureVersion => runesVersion;

        /// <summary>Changes when only the energy of runes changed (the network layer sends just the energies then). Where the rules run.</summary>
        public int EnergyVersion => energyVersion;

        /// <summary>Magical energy waiting to be given to a rune.</summary>
        public float PendingEnergy => view.pendingEnergy;

        /// <summary>The absorbed rune with this instance id (null when none).</summary>
        public RuneInstance FindRune(string instanceId)
        {
            IReadOnlyList<RuneInstance> list = Runes;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].instanceId == instanceId) return list[i];
            return null;
        }

        /// <summary>The concepts the player knows of rune definition <paramref name="runeId"/>.</summary>
        public IReadOnlyList<string> KnownConcepts(string runeId)
        {
            if (IsAuthority) return RuneRules.KnownConcepts(state, runeId);
            foreach (ConceptKnowledge k in replicatedRunes.knowledge)
                if (k != null && k.runeId == runeId) return k.concepts;
            return Array.Empty<string>();
        }

        // ------------------------------------------------------------------ energy (where the rules run)
        /// <summary>
        /// Magical energy from a kill (or a reward) goes to the pending pool; the player gives it to a rune of their choice.
        /// Returns what was kept (Max Pending Energy may cap it). Only rune users gain energy.
        /// </summary>
        public float AddPendingEnergy(float amount, string source = null)
        {
            if (!IsAuthority || !CheckRuneUser(true) || !(amount > 0f))
                return 0f;
            float kept = EnergyRules.AddPending(Balance, state, amount);
            if (kept <= 0f)
                return 0f;
            MarkSaveDirty(false);
            EnergyGained?.Invoke(this, kept);
            if (!energyHinted)
            {
                energyHinted = true;
                Say(state.runes.Count > 0
                    ? $"+{kept:N0} magical energy{(string.IsNullOrEmpty(source) ? "" : " from " + source)}: give it to a rune in the Rune Soul window."
                    : $"+{kept:N0} magical energy: it waits until you absorb a rune to give it to.");
            }
            else Refresh();
            return kept;
        }

        /// <summary>
        /// Gives up to <paramref name="amount"/> pending energy (0 or less = all of it) to the rune
        /// <paramref name="instanceId"/>. A full rune takes nothing (choose another); the rest stays pending. Never advances a rank.
        /// </summary>
        public EnergyTransfer AssignEnergy(string instanceId, float amount)
        {
            if (!IsAuthority || !CheckRuneUser(true))
                return new EnergyTransfer { refusal = EnergyRefusal.NoSuchRune };
            EnergyTransfer t = EnergyRules.Assign(Balance, state, instanceId, amount);
            RuneInstance r = state.Rune(instanceId);
            if (t.Ok)
            {
                energyVersion++;
                MarkSaveDirty(true);
            }
            Say(t.Describe(r != null ? RuneRules.DisplayName(Catalog, r) : "That rune"));
            return t;
        }

        // ------------------------------------------------------------------ absorbing and releasing (where the rules run)
        /// <summary>Can <paramref name="d"/> be absorbed now (a rune user with room in the soul)?</summary>
        public bool CanAbsorb(RuneDrop d, out string reason)
        {
            reason = "";
            if (!IsAuthority)
            {
                if (!view.runeUser) { reason = "Only rune users (Mages) can absorb runes."; return false; }
                int cost = RuneRules.SlotCost(Catalog, d.runeId);
                if (!CapacityRules.CanFit(view.capacity, view.usedSlots, cost))
                {
                    reason = $"No room in your soul ({view.usedSlots} / {view.capacity} slots): free a slot or increase your capacity.";
                    return false;
                }
                return Catalog.Rune(d.runeId) != null;
            }
            if (!CheckRuneUser(true)) { reason = "Only rune users (Mages) can absorb runes."; return false; }
            return RuneRules.CanAbsorb(Catalog, state, ComputeCapacity(Balance), d.runeId, out reason);
        }

        /// <summary>
        /// Absorbs a rune that is not an item (a quest reward, a synthesis, a tool): capacity checked, alignment conflicts
        /// strain the soul. Returns the absorbed rune, or null with the reason.
        /// </summary>
        public RuneInstance AbsorbDrop(RuneDrop d, RuneOrigin origin, out string reason)
        {
            if (!IsAuthority) { reason = "Only the server absorbs runes."; return null; }
            if (!CanAbsorb(d, out reason)) return null;
            RuneDef def = Catalog.Rune(d.runeId);
            float strain = RuneRules.AbsorptionDamage(Catalog, state, def);
            RuneInstance r = RuneRules.Absorb(Catalog, state, d, origin ?? new RuneOrigin { kind = "reward" }, Guid.NewGuid().ToString("N"), DateTime.UtcNow.Ticks);
            if (r == null) { reason = "The rune could not be absorbed."; return null; }
            if (r.origin.utc == 0) r.origin.utc = r.acquiredUtc;
            runesVersion++;
            MarkSaveDirty(true);
            if (strain > 0f)
                Damage(strain, "incompatible_absorption", def != null ? def.Name : d.runeId);
            int used = RuneRules.UsedSlots(Catalog, state), cap = ComputeCapacity(Balance);
            Say($"You absorb {RuneRules.DisplayName(Catalog, r)} into your soul ({used} / {cap} slots)." +
                (strain > 0f ? " Its alignment clashes with your runes: your soul is strained." : ""));
            if (logEvents)
                Debug.Log($"[Rune Magic] {name}: absorbed {r.runeId} r{r.rank} q{r.quality:0} e{r.energy:0} ({origin?.kind}).", this);
            RuneAbsorbed?.Invoke(this, r);
            Refresh();
            return r;
        }

        /// <summary>
        /// Absorbs one rune item from the player's inventory: it must be there, and the soul must have room; the item is
        /// consumed only when the rune is absorbed.
        /// </summary>
        public bool AbsorbItem(ItemSO item, out string reason)
        {
            reason = "";
            if (!IsAuthority) { reason = "Only the server absorbs runes."; return false; }
            if (!(item is RuneItemSO rune)) { reason = "That is not a rune."; return false; }
            RuneDrop d = rune.Drop;
            if (!CanAbsorb(d, out reason)) { Say(reason); return false; }
            InventoryManager inv = InventoryManager.For(this);
            if (inv == null || inv.CountItem(item) < 1) { reason = "You do not have that rune."; Say(reason); return false; }
            if (inv.RemoveItems(item, 1) < 1) { reason = "The rune could not be taken from your inventory."; Say(reason); return false; }
            RuneInstance r = AbsorbDrop(d, new RuneOrigin { kind = "item", source = item.Name, utc = DateTime.UtcNow.Ticks }, out reason);
            if (r != null)
                return true;
            // Absorbing failed after all: the item goes back.
            inv.AddItem(item, 1);
            Say(reason);
            return false;
        }

        /// <summary>Releases a rune from the soul (if allowed; not in combat by default). Its energy is lost. Decision D12.</summary>
        public bool ReleaseRune(string instanceId, out string reason)
        {
            if (!IsAuthority) { reason = "Only the server releases runes."; return false; }
            if (!CheckRuneUser(true)) { reason = NotRuneUser; return false; }
            CombatEntity e = Entity;
            SoulRecoveryBalance rb = Balance.soulRecovery;
            bool inCombat = e != null && CombatActivity.InCombat(e, rb != null ? rb.combatWindowSeconds : 6f);
            if (!RuneRules.CanRelease(Balance.acquisition, state, instanceId, inCombat, out reason))
            {
                Say(reason);
                return false;
            }
            RuneInstance r = RuneRules.Release(state, instanceId);
            runesVersion++;
            MarkSaveDirty(true);
            Say($"You release {RuneRules.DisplayName(Catalog, r)} from your soul" + (r.energy > 0f ? $"; its {r.energy:N0} energy is lost." : "."));
            RuneReleased?.Invoke(this, r);
            Refresh();
            return true;
        }

        /// <summary>Records a discovered concept (permanent; concept discovery calls it). Where the rules run.</summary>
        public bool LearnConcept(string conceptId)
        {
            if (!IsAuthority || !CheckRuneUser(true)) return false;
            RuneDef def = Catalog.RuneOfConcept(conceptId);
            if (def == null || !RuneRules.Learn(state, def.id, conceptId)) return false;
            runesVersion++;
            MarkSaveDirty(true);
            Refresh();
            return true;
        }

        // ------------------------------------------------------------------ requests (any machine: the UI's entry points)
        /// <summary>Absorb this rune item (offline / host: next frame; client: asks the server).</summary>
        public bool RequestAbsorb(ItemSO item)
        {
            if (item == null) return false;
            if (!IsAuthority)
            {
                string id = ItemDatabase.Id(item);
                return !string.IsNullOrEmpty(id) && Send(RuneRequestKind.Absorb, id, 0f, false);
            }
            // Not during the inventory's own use of the item: absorbed at the next frame.
            if (!absorbQueue.Contains(item)) absorbQueue.Add(item);
            return true;
        }

        /// <summary>Release the rune <paramref name="instanceId"/> (the UI asks for confirmation first).</summary>
        public bool RequestRelease(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return false;
            if (!IsAuthority) return Send(RuneRequestKind.Release, instanceId, 0f, false);
            return ReleaseRune(instanceId, out _);
        }

        /// <summary>Give pending energy to the rune <paramref name="instanceId"/> (0 or less = all of it).</summary>
        public bool RequestAssignEnergy(string instanceId, float amount)
        {
            if (string.IsNullOrEmpty(instanceId)) return false;
            if (!IsAuthority) return Send(RuneRequestKind.AssignEnergy, instanceId, amount, false);
            return AssignEnergy(instanceId, amount).Ok;
        }

        private bool Send(RuneRequestKind kind, string text, float amount, bool flag)
        {
            IRuneMagicNet net = Net;
            return net != null && net.Send(new RuneSoulRequest { kind = kind, seq = ++requestSeq, flag = flag, text = text ?? "", amount = amount });
        }

        private void HandleRuneRequest(RuneSoulRequest request)
        {
            switch (request.kind)
            {
                case RuneRequestKind.Absorb:
                    ItemSO item = ItemDatabase.Item(request.text);
                    if (item is RuneItemSO) AbsorbItem(item, out _);
                    else Say("That is not a rune.");
                    break;
                case RuneRequestKind.Release:
                    ReleaseRune(request.text, out _);
                    break;
                case RuneRequestKind.AssignEnergy:
                    AssignEnergy(request.text, request.amount);
                    break;
                case RuneRequestKind.DiscoveryMinigame:
                case RuneRequestKind.DiscoveryAnswer:
                case RuneRequestKind.DiscoveryAbandon:
                    HandleDiscoveryRequest(request);
                    break;
                case RuneRequestKind.DismissSpell:
                    DismissSpell(request.text, out _);
                    break;
                case RuneRequestKind.Synthesize:
                    HandleSynthesisRequest(request);
                    break;
                case RuneRequestKind.AssignSpell:
                case RuneRequestKind.UnassignSpell:
                    HandleSpellSlotRequest(request);
                    break;
                case RuneRequestKind.SpellingStart:
                    HandleSpellingStart(request);
                    break;
                case RuneRequestKind.SpellingResult:
                    HandleSpellingResult(request);
                    break;
                case RuneRequestKind.GrimoireOpen:
                case RuneRequestKind.GrimoireImprint:
                case RuneRequestKind.GrimoireAbsorb:
                case RuneRequestKind.GrimoireErase:
                    HandleGrimoireRequest(request);
                    break;
            }
        }

        private void ProcessAbsorbQueue()
        {
            if (absorbQueue.Count == 0) return;
            ItemSO[] items = absorbQueue.ToArray();
            absorbQueue.Clear();
            foreach (ItemSO item in items)
                if (item != null) AbsorbItem(item, out _);
        }

        // ------------------------------------------------------------------ the rune list on the network
        /// <summary>The runes and knowledge to send to the owner (where the rules run).</summary>
        public RuneListView BuildRuneList()
        {
            var v = new RuneListView { version = runesVersion };
            foreach (RuneInstance r in state.runes)
                if (r != null) v.runes.Add(r.Clone());
            foreach (ConceptKnowledge k in state.knowledge)
                if (k != null) v.knowledge.Add(new ConceptKnowledge { runeId = k.runeId, concepts = new List<string>(k.concepts) });
            foreach (SpellRecord sp in state.spells)
                if (sp != null) v.spells.Add(sp.Clone());
            v.report = lastReport.Clone();
            return v;
        }

        /// <summary>The energies of every rune, to send the owner when only energy changed (where the rules run).</summary>
        public RuneEnergyView BuildEnergyView() => RuneEnergyView.Of(state.runes, runesVersion);

        /// <summary>The new energies of the runes the owner holds (owner's client, network layer). Ignored when they belong to another list.</summary>
        public void ApplyReplicatedEnergies(RuneEnergyView v)
        {
            // An update for another version of the list is ignored: a newer list is on its way and carries the energies itself.
            if (v == null || IsAuthority || !v.ApplyTo(replicatedRunes)) return;
            replicatedEnergyCount++;
            Changed?.Invoke(this);
        }

        /// <summary>The runes and knowledge from the server (owner's client, network layer).</summary>
        public void ApplyReplicatedRunes(RuneListView v)
        {
            if (v == null || IsAuthority) return;
            replicatedRunes.version = v.version;
            replicatedRunes.runes.Clear();
            if (v.runes != null)
                foreach (RuneInstance r in v.runes)
                    if (r != null) { r.EnsureLists(); replicatedRunes.runes.Add(r); }
            replicatedRunes.knowledge.Clear();
            if (v.knowledge != null)
                foreach (ConceptKnowledge k in v.knowledge)
                    if (k != null && k.concepts != null) replicatedRunes.knowledge.Add(k);
            replicatedRunes.spells.Clear();
            if (v.spells != null)
                foreach (SpellRecord sp in v.spells)
                    if (sp != null) { sp.EnsureLists(); replicatedRunes.spells.Add(sp); }
            if (v.report != null) { v.report.EnsureLists(); replicatedRunes.report = v.report; }
            version++;
            Changed?.Invoke(this);
        }
    }
}
