using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>What the soul is focused on (design spec 15.1): nothing, meditation, or a healing ritual.</summary>
    public enum SoulChannel
    {
        None = 0,
        Meditation = 1,
        Ritual = 2,
    }

    /// <summary>
    /// The soul as its owner's UI shows it (the HUD's Soul gauge, the Rune Soul window, the meditation screen, the debug panel):
    /// computed where the rules run (<see cref="RuneSoul"/>) and sent to the owner only, as JSON, when it changes (NetRuneMagic,
    /// decision D18). Plain data: the owner's client never needs the server's state. Numbers are compared with a tolerance
    /// (<see cref="SameAs"/>): hundredths are not worth a network update.
    /// </summary>
    [Serializable]
    public class RuneSoulView
    {
        /// <summary>The tolerance of <see cref="SameAs"/> for numbers.</summary>
        public const float Tolerance = 0.01f;

        [Tooltip("The player is a rune user (Mage): only they have a soul resource.")]
        public bool runeUser;
        [Tooltip("The soul is kept in the world's store.")]
        public bool kept;

        public float integrity;
        public float maxIntegrity;
        public float deepDamage;
        public string conditionId = "";
        public string conditionName = "";
        public string castingDebuff = "";
        public bool collapseLocked;
        [Tooltip("The percent the soul must recover to after a collapse.")]
        public float lockUntilPercent;
        public bool synthesisAllowed;
        public bool castingAllowed;

        public int capacity;
        public int usedSlots;
        public string capacityStage = "";
        [Tooltip("The level of the next capacity stage (0: none).")]
        public int nextStageLevel;
        public int nextStageCapacity;

        public SoulChannel channel;
        public string channelName = "";
        [Tooltip("0-1: meditation settling, or the ritual's progress.")]
        public float channelProgress;
        public float recoveryPerMinute;
        public string recoverySources = "";
        [Tooltip("Seconds the collapse still holds the player still.")]
        public float incapacitatedFor;
        public float pendingEnergy;
        [Tooltip("The version of the rune list this view goes with.")]
        public int runesVersion;

        [Tooltip("The last thing the player was told; a new one has a new messageSeq.")]
        public string message = "";
        public int messageSeq;

        /// <summary>Highest Soul Integrity ordinary recovery can reach (the maximum minus deep damage).</summary>
        public float Ceiling => Math.Max(0f, maxIntegrity - deepDamage);

        /// <summary>Soul Integrity as a percent (0-100) of the maximum.</summary>
        public float Percent => maxIntegrity > 0f ? Math.Max(0f, Math.Min(100f, integrity / maxIntegrity * 100f)) : 0f;

        /// <summary>Free soul slots (never below 0).</summary>
        public int FreeSlots => CapacityRules.Free(capacity, usedSlots);

        /// <summary>Is the soul meditating or performing a ritual?</summary>
        public bool IsChannelling => channel != SoulChannel.None;

        /// <summary>Does <paramref name="o"/> show the same (numbers within <see cref="Tolerance"/>)?</summary>
        public bool SameAs(RuneSoulView o) =>
            o != null && runeUser == o.runeUser && kept == o.kept &&
            Near(integrity, o.integrity) && Near(maxIntegrity, o.maxIntegrity) && Near(deepDamage, o.deepDamage) &&
            conditionId == o.conditionId && conditionName == o.conditionName && castingDebuff == o.castingDebuff &&
            collapseLocked == o.collapseLocked && Near(lockUntilPercent, o.lockUntilPercent) &&
            synthesisAllowed == o.synthesisAllowed && castingAllowed == o.castingAllowed &&
            capacity == o.capacity && usedSlots == o.usedSlots && capacityStage == o.capacityStage &&
            nextStageLevel == o.nextStageLevel && nextStageCapacity == o.nextStageCapacity &&
            channel == o.channel && channelName == o.channelName && Near(channelProgress, o.channelProgress) &&
            Near(recoveryPerMinute, o.recoveryPerMinute) && recoverySources == o.recoverySources &&
            Near(incapacitatedFor, o.incapacitatedFor) && Near(pendingEnergy, o.pendingEnergy) && runesVersion == o.runesVersion &&
            messageSeq == o.messageSeq && message == o.message;

        /// <summary>Takes every value of <paramref name="o"/>.</summary>
        public void CopyFrom(RuneSoulView o)
        {
            if (o == null) return;
            runeUser = o.runeUser; kept = o.kept;
            integrity = o.integrity; maxIntegrity = o.maxIntegrity; deepDamage = o.deepDamage;
            conditionId = o.conditionId ?? ""; conditionName = o.conditionName ?? ""; castingDebuff = o.castingDebuff ?? "";
            collapseLocked = o.collapseLocked; lockUntilPercent = o.lockUntilPercent;
            synthesisAllowed = o.synthesisAllowed; castingAllowed = o.castingAllowed;
            capacity = o.capacity; usedSlots = o.usedSlots; capacityStage = o.capacityStage ?? "";
            nextStageLevel = o.nextStageLevel; nextStageCapacity = o.nextStageCapacity;
            channel = o.channel; channelName = o.channelName ?? ""; channelProgress = o.channelProgress;
            recoveryPerMinute = o.recoveryPerMinute; recoverySources = o.recoverySources ?? "";
            incapacitatedFor = o.incapacitatedFor; pendingEnergy = o.pendingEnergy; runesVersion = o.runesVersion;
            message = o.message ?? ""; messageSeq = o.messageSeq;
        }

        /// <summary>A copy.</summary>
        public RuneSoulView Clone()
        {
            var c = new RuneSoulView();
            c.CopyFrom(this);
            return c;
        }

        private static bool Near(float a, float b) => a == b || Math.Abs(a - b) < Tolerance;
    }

    /// <summary>
    /// The runes, concept knowledge and spells of a soul as its owner receives them (NetRuneMagic sends it when they change), with the
    /// report of the latest synthesis, so a result and the changed runes arrive together. The owner's client reads the runes from it
    /// and previews syntheses with it (<see cref="SynthesisReplica"/>).
    /// </summary>
    [Serializable]
    public class RuneListView
    {
        [Tooltip("The soul's rune list version it was made from (RuneSoul.StructureVersion).")]
        public int version;
        public List<RuneInstance> runes = new List<RuneInstance>();
        public List<ConceptKnowledge> knowledge = new List<ConceptKnowledge>();
        public List<SpellRecord> spells = new List<SpellRecord>();
        [Tooltip("The latest performed synthesis (seq 0: none yet).")]
        public SynthesisReport report = new SynthesisReport();
    }

    /// <summary>
    /// The energy of every rune, sent instead of the whole <see cref="RuneListView"/> when only energy changed (a cast, giving energy, an
    /// imprint): a few numbers. It applies only to the list it was made for (<see cref="baseVersion"/>); anything else is ignored, since
    /// a newer list carries the energies itself.
    /// </summary>
    [Serializable]
    public class RuneEnergyView
    {
        [Tooltip("The version of the rune list these energies belong to.")]
        public int baseVersion;
        [Tooltip("Rune instance ids.")]
        public List<string> ids = new List<string>();
        [Tooltip("Their energy, in the same order.")]
        public List<float> energies = new List<float>();

        /// <summary>The energies of <paramref name="runes"/> (null: none), for the list of version <paramref name="listVersion"/>.</summary>
        public static RuneEnergyView Of(IEnumerable<RuneInstance> runes, int listVersion)
        {
            var v = new RuneEnergyView { baseVersion = listVersion };
            if (runes == null) return v;
            foreach (RuneInstance r in runes)
            {
                if (r == null || string.IsNullOrEmpty(r.instanceId)) continue;
                v.ids.Add(r.instanceId);
                v.energies.Add(r.energy);
            }
            return v;
        }

        /// <summary>
        /// Gives the runes of <paramref name="list"/> their new energy. False, changing nothing, when the list is another version or the
        /// update is malformed (ids and energies of different lengths). An unknown rune is skipped, a number that is not finite is
        /// ignored, and energy never goes below 0.
        /// </summary>
        public bool ApplyTo(RuneListView list)
        {
            if (list == null || list.version != baseVersion) return false;
            int n = ids != null ? ids.Count : 0;
            if (n != (energies != null ? energies.Count : 0)) return false;
            if (n == 0 || list.runes == null) return true;
            for (int i = 0; i < n; i++)
            {
                float e = energies[i];
                if (float.IsNaN(e) || float.IsInfinity(e)) continue;
                string id = ids[i];
                if (string.IsNullOrEmpty(id)) continue;
                foreach (RuneInstance r in list.runes)
                    if (r != null && r.instanceId == id)
                    {
                        r.energy = Math.Max(0f, e);
                        break;
                    }
            }
            return true;
        }
    }
}
