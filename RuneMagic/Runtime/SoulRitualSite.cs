using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>A material a ritual needs (rare materials, design spec 15.1).</summary>
    [Serializable]
    public class RitualMaterial
    {
        public ItemSO item;
        [Min(1)] public int count = 1;
    }

    /// <summary>
    /// A healing ritual at a magical location (design spec 15.1): a rune user interacts ("[E] Perform Ritual"), stays
    /// focused for Ritual Seconds, and the ritual consumes its materials and mends the soul - deep damage above all, which
    /// rest and meditation cannot reach (design spec 15.2). Moving away, taking damage or crowd control breaks it (nothing
    /// is consumed then). A ritual can also grant a lasting breakthrough: extra soul rune capacity, once per player.
    /// <para>Put it on the altar or circle (it needs a collider, or one on a child, for the Player Interactor to find it).
    /// Network sessions: the interaction runs on the server, which checks and consumes everything. Players who are not
    /// rune users do not see it.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SimpleMovements/Rune Magic/Soul Ritual Site")]
    public class SoulRitualSite : MonoBehaviour, IInteractable
    {
        [Tooltip("Name in the prompt and messages (empty = the object's name).")]
        [SerializeField] private string displayName = "Healing Ritual";
        [SerializeField] private string verb = "Perform Ritual";
        [Tooltip("How close the player must be to start it (metres); leaving this range (plus 1 m) breaks the ritual.")]
        [SerializeField, Min(0.5f)] private float range = 2.5f;
        [Tooltip("Seconds Interact must be held (0 = a press).")]
        [SerializeField, Min(0f)] private float holdSeconds = 0.5f;

        [Header("Ritual")]
        [Tooltip("Seconds the rune user must stay focused.")]
        [SerializeField, Min(0f)] private float ritualSeconds = 20f;
        [Tooltip("The character cannot move during the ritual (off: moving breaks it).")]
        [SerializeField] private bool locksMovement = false;
        [Tooltip("The ritual can be started in combat.")]
        [SerializeField] private bool allowedInCombat = false;
        [Tooltip("Materials the ritual needs; consumed when it completes.")]
        [SerializeField] private List<RitualMaterial> materials = new List<RitualMaterial>();

        [Header("Healing")]
        [Tooltip("Deep damage removed.")]
        [SerializeField, Min(0f)] private float deepHeal = 25f;
        [Tooltip("Removes all deep damage (ignores Deep Heal).")]
        [SerializeField] private bool healAllDeepDamage = false;
        [Tooltip("Soul Integrity restored (after the deep damage is removed, so it can reach higher).")]
        [SerializeField, Min(0f)] private float restoreAmount = 0f;
        [Tooltip("Soul Integrity restored, as a percent of the maximum.")]
        [SerializeField, Range(0f, 100f)] private float restorePercent = 30f;

        [Header("Breakthrough (optional)")]
        [Tooltip("Lasting extra soul rune capacity this ritual grants (0 = none). Once per player: performing it again does not add more.")]
        [SerializeField, Min(0)] private int capacityBonusSlots = 0;
        [Tooltip("Id of the breakthrough (the same id anywhere counts as the same breakthrough). Empty = this site's name.")]
        [SerializeField] private string breakthroughId = "";

        [Header("Limits")]
        [Tooltip("Seconds before the same player may perform it again (0 = any time).")]
        [SerializeField, Min(0f)] private float cooldownSeconds = 300f;
        [Tooltip("Times the site can be used in all (0 = unlimited). Not saved: a scene reload restores it.")]
        [SerializeField, Min(0)] private int maxUses = 0;

        private readonly Dictionary<CombatEntity, float> readyAt = new Dictionary<CombatEntity, float>(ReferenceComparer<CombatEntity>.Instance);
        private int uses;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public float RitualSeconds => ritualSeconds;
        public bool LocksMovement => locksMovement;
        public IReadOnlyList<RitualMaterial> Materials => materials;
        public string BreakthroughId => string.IsNullOrEmpty(breakthroughId) ? "ritual:" + name : breakthroughId;

        // ------------------------------------------------------------------ ritual (where the rules run)
        /// <summary>Can <paramref name="soul"/> begin the ritual (materials, cooldown, uses, combat)?</summary>
        public bool CanBegin(RuneSoul soul, out string reason)
        {
            reason = "";
            if (soul == null) return false;
            if (maxUses > 0 && uses >= maxUses) { reason = "The site's power is spent."; return false; }
            CombatEntity e = soul.Entity;
            if (e != null && readyAt.TryGetValue(e, out float t) && Time.time < t)
            {
                reason = $"You must wait {Mathf.CeilToInt(t - Time.time)} s before performing this ritual again.";
                return false;
            }
            if (!allowedInCombat && e != null)
            {
                SoulRecoveryBalance r = RuneMagicDatabase.Instance.Catalog.Balance.soulRecovery;
                if (CombatActivity.InCombat(e, r != null ? r.combatWindowSeconds : 6f)) { reason = "You cannot perform a ritual in combat."; return false; }
            }
            return HasMaterials(soul, out reason);
        }

        /// <summary>The ritual completed: consumes the materials and mends the soul. False (nothing consumed) when materials are missing.</summary>
        public bool Complete(RuneSoul soul, out string result)
        {
            result = "";
            if (soul == null) return false;
            if (!HasMaterials(soul, out result))
                return false;
            InventoryManager inv = InventoryManager.For(soul);
            foreach (RitualMaterial m in materials)
                if (m != null && m.item != null && inv != null)
                    inv.RemoveItems(m.item, m.count);

            float healed = healAllDeepDamage ? soul.HealDeep(float.MaxValue) : soul.HealDeep(deepHeal);
            float restored = soul.Restore(restoreAmount + restorePercent / 100f * soul.MaxIntegrity, DisplayName);
            string breakthrough = "";
            if (capacityBonusSlots > 0 && soul.CapacityBonusOf(BreakthroughId) < capacityBonusSlots)
            {
                soul.SetCapacityBonus(BreakthroughId, capacityBonusSlots);
                breakthrough = $" Breakthrough: soul rune capacity +{capacityBonusSlots}.";
            }
            uses++;
            CombatEntity e = soul.Entity;
            if (e != null && cooldownSeconds > 0f) readyAt[e] = Time.time + cooldownSeconds;
            result = $"The ritual is complete: deep damage −{healed:0.#}, Soul Integrity +{restored:0.#}.{breakthrough}";
            return true;
        }

        private bool HasMaterials(RuneSoul soul, out string reason)
        {
            reason = "";
            if (materials.Count == 0) return true;
            InventoryManager inv = InventoryManager.For(soul);
            foreach (RitualMaterial m in materials)
            {
                if (m == null || m.item == null) continue;
                int have = inv != null ? inv.CountItem(m.item) : 0;
                if (have < m.count)
                {
                    reason = $"The ritual needs {m.count} × {m.item.Name} (you have {have}).";
                    return false;
                }
            }
            return true;
        }

        /// <summary>Is <paramref name="point"/> close enough to keep the ritual going?</summary>
        public bool InRange(Vector3 point)
        {
            Vector3 d = point - InteractionPoint.position;
            d.y = 0f;
            float r = range + 1f;
            return d.sqrMagnitude <= r * r;
        }

        // ------------------------------------------------------------------ interaction
        public string InteractionName => DisplayName;
        public string InteractionVerb => string.IsNullOrEmpty(verb) ? "Perform Ritual" : verb;
        public Transform InteractionPoint => transform;
        public float InteractionRange => range;
        public int InteractionPriority => 5;
        public float HoldDuration => holdSeconds;
        public bool AllowsClick => true;

        public bool CanInteract(PlayerInteractor interactor, out string reason)
        {
            reason = "";
            RuneSoul soul = SoulOf(interactor);
            if (soul == null || !soul.IsRuneUser)
                return false; // only rune users see it
            if (soul.View.IsChannelling) { reason = "Busy"; return false; }
            if (!soul.IsAuthority)
                return HasMaterials(soul, out reason); // the server checks the rest
            return CanBegin(soul, out reason);
        }

        public void Interact(PlayerInteractor interactor)
        {
            RuneSoul soul = SoulOf(interactor);
            if (soul != null && soul.IsAuthority)
                soul.BeginRitual(this, out _);
        }

        public void OnFocusChanged(PlayerInteractor interactor, bool focused) { }

        private static RuneSoul SoulOf(PlayerInteractor interactor) =>
            interactor != null && interactor.PlayerObject != null ? RuneSoul.For(interactor.PlayerObject) : null;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.45f, 0.86f, 0.82f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
