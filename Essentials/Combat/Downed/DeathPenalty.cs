using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>What dying costs the player.</summary>
public enum DeathPenaltyMode
{
    /// <summary>Nothing: the player respawns.</summary>
    None,
    /// <summary>Respawn and lose items (all, some, or by chance; dropped where the player died or destroyed).</summary>
    RespawnLoseItems,
    /// <summary>The character is gone: back to character creation (permadeath).</summary>
    CharacterReset,
    /// <summary>Lose experience (can lose levels, and with them the stat points spent at them).</summary>
    LoseExperience,
    /// <summary>Back to the last absorption milestone level (57 → 45) and the latest absorbed ability is lost.</summary>
    MilestoneRegression,
}

/// <summary>Needs restored when the player comes back to life (at least these fractions of the max; never lowered).</summary>
[Serializable]
public class RespawnStatusRules
{
    [Tooltip("Health on respawn (fraction of the max). Networked players use the NetCombatStatus Respawn Health Fraction.")]
    [Range(0.05f, 1f)] public float health = 0.5f;
    [Tooltip("Hunger is at least this fraction after a respawn (0 = keep it as it was).")]
    [Range(0f, 1f)] public float hunger = 0.6f;
    [Tooltip("Thirst is at least this fraction after a respawn.")]
    [Range(0f, 1f)] public float thirst = 0.6f;
    [Tooltip("Stamina is at least this fraction.")]
    [Range(0f, 1f)] public float stamina = 1f;
    [Tooltip("Mana is at least this fraction.")]
    [Range(0f, 1f)] public float mana = 1f;
    [Tooltip("Rest (sleep) is at least this fraction.")]
    [Range(0f, 1f)] public float rest = 0.6f;
    [Tooltip("Sanity is at least this fraction.")]
    [Range(0f, 1f)] public float sanity = 0.5f;
    [Tooltip("Oxygen is full again.")]
    public bool refillOxygen = true;
    [Tooltip("The body temperature goes back to its ideal.")]
    public bool resetBodyHeat = true;
}

/// <summary>Which items are lost with Respawn Lose Items.</summary>
[Serializable]
public class ItemLossRules
{
    [Tooltip("Lose items in the bag (inventory) and hotbar.")]
    public bool inventory = true;
    [Tooltip("Lose equipped items (armour, weapons, rings).")]
    public bool equipped = false;
    [Tooltip("Chance (0-1) that each stack is lost (1 = everything).")]
    [Range(0f, 1f)] public float chancePerStack = 1f;
    [Tooltip("Lost items drop on the ground where the player died (they can go back for them). Off = destroyed.")]
    public bool dropWhereDied = true;
    [Tooltip("These items are never lost (quest items, keys, soulbound gear).")]
    public List<ItemSO> neverLose = new List<ItemSO>();
}

/// <summary>
/// The death penalty and the respawn of the player (choose the mode in the inspector):
/// <list type="number">
/// <item><b>Respawn Lose Items</b>: respawn, the items in the bag (and optionally the equipped ones) are lost or dropped.</item>
/// <item><b>Character Reset</b>: the character is deleted and the game goes back to character creation (permadeath).</item>
/// <item><b>Lose Experience</b>: a percentage of a level's experience is lost; it can cost levels, and with them their
/// points and the stats bought with them.</item>
/// <item><b>Milestone Regression</b>: the character goes back to its last ability-absorption milestone (levels 10, 20, 35,
/// 40, 45... → a level 57 character becomes 45) and loses the ability absorbed most recently. The milestones are the
/// ones of <see cref="AbsorptionMilestones"/> (this component's own when <see cref="ownMilestones"/> is on, else the class,
/// the race, then the Absorption Settings).</item>
/// </list>
/// <para><b>Respawn</b>: offline the player comes back after <see cref="respawnDelay"/> at <see cref="respawnPoint"/> (else
/// where it started). In a network session the server's NetCombatStatus respawns it. Either way the
/// <see cref="statusRules"/> refill hunger, thirst, stamina, mana, rest and sanity (they used to stay at 0 after a death).</para>
/// Put it on the player (Player Setup Validator adds it). The penalty is applied where the game rules run.
/// </summary>
[DisallowMultipleComponent]
public class DeathPenalty : MonoBehaviour
{
    [Header("Penalty")]
    [Tooltip("What dying costs.")]
    [SerializeField] private DeathPenaltyMode mode = DeathPenaltyMode.None;

    [Header("Respawn Lose Items")]
    [SerializeField] private ItemLossRules itemLoss = new ItemLossRules();

    [Header("Character Reset")]
    [Tooltip("Delete the character's save (permadeath). Off = only back to the menu.")]
    [SerializeField] private bool deleteCharacterSave = true;
    [Tooltip("Scene with the start / character creation screens (Offline Start Screens). Empty = the first scene of the Build Settings.")]
    [SerializeField] private string characterCreationScene = "";

    [Header("Lose Experience")]
    [Tooltip("Experience lost: this percent of the experience the current level needs.")]
    [SerializeField, Range(0f, 100f)] private float experienceLossPercent = 25f;
    [Tooltip("Plus this flat amount.")]
    [SerializeField, Min(0)] private int experienceLossFlat = 0;
    [Tooltip("The loss can take the character down levels (taking back their points and the stats bought with them).")]
    [SerializeField] private bool canLoseLevels = true;
    [Tooltip("Never below this level.")]
    [SerializeField, Min(1)] private int minimumLevel = 1;

    [Header("Milestone Regression")]
    [Tooltip("Absorbed abilities lost (the most recent first).")]
    [SerializeField, Min(0)] private int abilitiesLost = 1;
    [Tooltip("A character exactly on a milestone level goes back to the previous milestone (off: it keeps its level).")]
    [SerializeField] private bool onMilestoneDropToPrevious = false;
    [Tooltip("Stat points spent at the lost levels are undone.")]
    [SerializeField] private bool undoStatUpgrades = true;
    [Tooltip("Use the milestones below for this player (over the class, race and Absorption Settings).")]
    [SerializeField] private bool ownMilestones = false;
    [SerializeField] private AbsorptionMilestones milestones = new AbsorptionMilestones { enabled = true };

    [Header("Respawn")]
    [Tooltip("Offline: bring the player back after a death (network sessions: the server's NetCombatStatus does it).")]
    [SerializeField] private bool respawnOffline = true;
    [Tooltip("Seconds before the respawn.")]
    [SerializeField, Min(0f)] private float respawnDelay = 5f;
    [Tooltip("Where the player comes back. Empty = where it was when the game started.")]
    [SerializeField] private Transform respawnPoint;
    [Tooltip("Seconds of invulnerability after a respawn.")]
    [SerializeField, Min(0f)] private float spawnProtection = 2f;
    [Tooltip("Needs refilled when coming back to life (any revive: respawn, rest point, resurrection).")]
    [SerializeField] private RespawnStatusRules statusRules = new RespawnStatusRules();
    [Tooltip("Show \"You died\" with the respawn countdown and what was lost (local player).")]
    [SerializeField] private bool showDeathScreen = true;

    [Header("Events (where the rules run)")]
    public UnityEvent<string> onPenaltyApplied = new UnityEvent<string>();
    public UnityEvent onRespawned = new UnityEvent();
    /// <summary>Character Reset: raised before going back to the menu (hook your own flow; network clients only get this).</summary>
    public UnityEvent onCharacterReset = new UnityEvent();

    private CombatEntity entity;
    private Pose startPose;
    private bool hasStartPose;
    private Coroutine respawnRoutine;
    private string lastSummary = "";
    private float respawnAt;
    private Canvas screen;
    private TextMeshProUGUI screenText;
    private CanvasGroup screenGroup;

    public DeathPenaltyMode Mode { get => mode; set => mode = value; }
    /// <summary>This player's own milestones (null when it uses the class / race / project ones).</summary>
    public AbsorptionMilestones OwnMilestones => ownMilestones ? milestones : null;
    /// <summary>What the last death cost ("Lost 3 items").</summary>
    public string LastPenalty => lastSummary;
    public RespawnStatusRules StatusRules => statusRules;

    private CombatEntity Entity => entity != null ? entity : (entity = CombatEntity.Resolve(gameObject) ?? GetComponentInParent<CombatEntity>());

    private void Start()
    {
        CombatEntity e = Entity;
        if (e != null)
        {
            GameObject mover = PlayerLocator.MovableRoot(e);
            Transform t = mover != null ? mover.transform : e.transform;
            startPose = new Pose(t.position, t.rotation);
            hasStartPose = true;
        }
    }

    private void OnEnable()
    {
        CombatEntity e = Entity;
        if (e == null) return;
        e.Died += OnDied;
        e.Revived += OnRevived;
        e.ReplicatedLifeChanged += OnReplicatedLife;
    }

    private void OnDisable()
    {
        if (entity == null) return;
        entity.Died -= OnDied;
        entity.Revived -= OnRevived;
        entity.ReplicatedLifeChanged -= OnReplicatedLife;
    }

    private void OnDestroy()
    {
        if (screen != null) Destroy(screen.gameObject);
    }

    // ------------------------------------------------------------------ death

    private void OnDied(CombatEntity killer)
    {
        if (!GameNet.NetAuthority.RunsRules(gameObject))
            return;
        lastSummary = ApplyPenalty();
        if (!string.IsNullOrEmpty(lastSummary))
            Debug.Log($"[Death Penalty] {name}: {lastSummary}", this);
        onPenaltyApplied?.Invoke(lastSummary);

        if (mode == DeathPenaltyMode.CharacterReset)
        {
            StartCoroutine(ResetCharacterRoutine());
            return;
        }
        bool networked = GameNet.NetAuthority.IsNetworkedObject(gameObject);
        if (respawnOffline && !networked)
        {
            if (respawnRoutine != null) StopCoroutine(respawnRoutine);
            respawnRoutine = StartCoroutine(RespawnRoutine());
        }
        respawnAt = Time.time + respawnDelay;
    }

    private void OnReplicatedLife(bool dead)
    {
        if (dead)
        {
            respawnAt = Time.time + respawnDelay;
            lastSummary = "";
            if (mode == DeathPenaltyMode.CharacterReset && IsLocal())
                onCharacterReset?.Invoke();
        }
    }

    /// <summary>Applies the penalty of <see cref="mode"/> now and returns what it cost.</summary>
    public string ApplyPenalty()
    {
        switch (mode)
        {
            case DeathPenaltyMode.RespawnLoseItems: return LoseItems();
            case DeathPenaltyMode.LoseExperience: return LoseExperience();
            case DeathPenaltyMode.MilestoneRegression: return RegressToMilestone();
            case DeathPenaltyMode.CharacterReset: return "The character is lost.";
            default: return "";
        }
    }

    private string LoseItems()
    {
        InventoryManager inv = InventoryManager.For(this);
        if (inv == null) return "";
        CombatEntity e = Entity;
        Vector3 at = e != null ? e.Position + Vector3.up * 0.5f : transform.position;
        var slots = new List<InventorySlot>();
        if (itemLoss.inventory && inv.StorageSlots != null)
            foreach (GameObject go in inv.StorageSlots)
                if (go != null && go.GetComponent<InventorySlot>() is InventorySlot s) slots.Add(s);
        if (itemLoss.equipped && inv.EquipmentSlots != null)
            slots.AddRange(inv.EquipmentSlots);

        int lost = 0;
        foreach (InventorySlot slot in slots)
        {
            if (slot == null || slot.heldItem == null) continue;
            InventoryItem item = slot.heldItem.GetComponent<InventoryItem>();
            if (item == null || item.itemScriptableObject == null) continue;
            if (itemLoss.neverLose != null && itemLoss.neverLose.Contains(item.itemScriptableObject)) continue;
            if (UnityEngine.Random.value > itemLoss.chancePerStack) continue;
            lost++;
            if (itemLoss.dropWhereDied && item.itemScriptableObject.Prefab != null)
            {
                Vector2 r = UnityEngine.Random.insideUnitCircle * 0.8f;
                ItemHandler.DropItemAt(item.gameObject, slot.gameObject, inv.PlayerStatus, at + new Vector3(r.x, 0f, r.y), inv.Player);
            }
            else
            {
                InventoryUtils.UpdatePlayerWeight(inv.Player, -item.itemScriptableObject.Weight * item.stackCurrent);
                slot.heldItem = null;
                Destroy(item.gameObject);
            }
        }
        if (lost > 0) ArmorEquipmentHandler.NotifyEquipmentChanged(inv.PlayerStatus);
        return lost > 0 ? $"Lost {lost} item stack{(lost == 1 ? "" : "s")}{(itemLoss.dropWhereDied ? " (dropped where you died)" : "")}." : "";
    }

    private ExperienceManager XP => GetComponentInParent<ExperienceManager>() ?? GetComponentInChildren<ExperienceManager>();

    private string LoseExperience()
    {
        ExperienceManager xp = XP;
        if (xp == null) return "";
        int levelBefore = xp.CurrentLevel;
        int amount = Mathf.RoundToInt(xp.ExperienceToNextLevel * experienceLossPercent / 100f) + experienceLossFlat;
        xp.LoseExperience(amount, canLoseLevels, minimumLevel);
        string text = $"Lost {amount} experience";
        if (xp.CurrentLevel < levelBefore) text += $" and fell from level {levelBefore} to {xp.CurrentLevel}";
        return text + ".";
    }

    private string RegressToMilestone()
    {
        ExperienceManager xp = XP;
        AbsorptionMilestones m = AbsorptionMilestones.For(this);
        var parts = new List<string>();
        if (xp != null && m != null)
        {
            int level = xp.CurrentLevel;
            int target = m.MilestoneAtOrBelow(level);
            if (onMilestoneDropToPrevious && target == level) target = m.MilestoneBelow(level);
            if (target > 0 && target < level)
            {
                xp.RegressToLevel(target, undoStatUpgrades);
                parts.Add($"Fell back from level {level} to milestone {target}");
            }
        }
        var abilities = GetComponentInParent<PlayerAbilityController>() ?? GetComponentInChildren<PlayerAbilityController>();
        if (abilities != null && abilitiesLost > 0)
        {
            List<string> lost = abilities.ForgetLatestAbsorbed(abilitiesLost);
            if (lost.Count > 0) parts.Add("lost " + string.Join(", ", lost));
        }
        return parts.Count > 0 ? string.Join(", ", parts) + "." : "";
    }

    private IEnumerator ResetCharacterRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(1f, respawnDelay));
        onCharacterReset?.Invoke();
        if (GameNet.NetAuthority.IsNetworked)
        {
            Debug.LogWarning("[Death Penalty] Character Reset in a network session: hook On Character Reset to send the player back " +
                             "to the menu (the server cannot change a client's scene).", this);
            yield break;
        }
        CharacterSave save = CharacterSession.Current;
        if (deleteCharacterSave && save != null)
            CharacterSaveStore.Delete(save);
        CharacterSession.Set(null);
        string scene = characterCreationScene;
        if (!string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene))
            SceneManager.LoadScene(scene, LoadSceneMode.Single);
        else
            SceneManager.LoadScene(0, LoadSceneMode.Single);
    }

    // ------------------------------------------------------------------ respawn

    private IEnumerator RespawnRoutine()
    {
        if (respawnDelay > 0f)
            yield return new WaitForSeconds(respawnDelay);
        respawnRoutine = null;
        RespawnNow();
    }

    /// <summary>Offline: revives the player now at the respawn point (the statuses follow the Status Rules).</summary>
    public void RespawnNow()
    {
        CombatEntity e = Entity;
        if (e == null || !e.IsDead || !GameNet.NetAuthority.RunsRules(gameObject))
            return;
        Pose pose = respawnPoint != null ? new Pose(respawnPoint.position, respawnPoint.rotation) : startPose;
        if (respawnPoint != null || hasStartPose)
            HubWorld.RealmTravel.Place(e, pose);
        e.Revive(statusRules.health);
        if (spawnProtection > 0f) e.SetInvulnerable(spawnProtection);
        onRespawned?.Invoke();
    }

    private void OnRevived()
    {
        if (respawnRoutine != null) StopCoroutine(respawnRoutine);
        respawnRoutine = null;
        RestoreStatuses();
    }

    /// <summary>Refills the needs per the Status Rules (called on every revive where the rules run).</summary>
    public void RestoreStatuses()
    {
        if (!GameNet.NetAuthority.RunsRules(gameObject))
            return;
        PlayerStatusController s = GetComponentInParent<PlayerStatusController>() ?? GetComponentInChildren<PlayerStatusController>();
        Restore(s, statusRules);
    }

    /// <summary>
    /// Refills the needs of <paramref name="s"/> per <paramref name="rules"/> (at least those fractions; values above are
    /// kept). Players without a Death Penalty use the default rules on every revive (PlayerStatusController).
    /// </summary>
    public static void Restore(PlayerStatusController s, RespawnStatusRules rules)
    {
        if (s == null || rules == null) return;
        AtLeast(s.HungerManager, rules.hunger);
        AtLeast(s.ThirstManager, rules.thirst);
        AtLeast(s.StaminaManager, rules.stamina);
        AtLeast(s.ManaManager, rules.mana);
        AtLeast(s.SleepManager, rules.rest);
        AtLeast(s.SanityManager, rules.sanity);
        if (rules.refillOxygen && s.OxygenManager != null) s.OxygenManager.ResetForRespawn(1f);
        if (rules.resetBodyHeat && s.BodyHeatManager != null) s.BodyHeatManager.ResetForRespawn(1f);
    }

    private static void AtLeast(StatusManager m, float fraction)
    {
        if (m == null || fraction <= 0f) return;
        if (m.Fraction < fraction) m.ResetForRespawn(fraction);
        else m.ResetForRespawn(m.Fraction); // keeps the value, clears the drain / penalty timers
    }

    // ------------------------------------------------------------------ death screen (local player)

    private bool IsLocal()
    {
        CombatEntity e = Entity;
        if (e == null) return false;
        if (!GameNet.NetAuthority.IsNetworkedObject(e.gameObject)) return GameNet.NetAuthority.HasLocalPlayer;
        return GameNet.NetAuthority.IsLocallyControlled(e.gameObject);
    }

    private void Update()
    {
        if (!showDeathScreen) return;
        CombatEntity e = Entity;
        bool dead = e != null && e.IsDead && IsLocal();
        if (!dead)
        {
            if (screen != null && screen.gameObject.activeSelf) screen.gameObject.SetActive(false);
            return;
        }
        if (screen == null) BuildScreen();
        screen.gameObject.SetActive(true);
        screenGroup.alpha = Mathf.MoveTowards(screenGroup.alpha, 1f, Time.unscaledDeltaTime * 1.5f);
        float left = Mathf.Max(0f, respawnAt - Time.time);
        string next = mode == DeathPenaltyMode.CharacterReset ? "Your journey ends here."
            : left > 0.05f ? $"Respawning in {Mathf.CeilToInt(left)} s" : "Respawning...";
        screenText.text = $"YOU DIED\n<size=45%>{next}{(string.IsNullOrEmpty(lastSummary) ? "" : "\n" + lastSummary)}</size>";
    }

    private void BuildScreen()
    {
        var p = new HudPalette();
        screen = UITheme.Canvas("Death Screen", 62, null);
        screen.GetComponent<GraphicRaycaster>().enabled = false;
        screenGroup = screen.gameObject.AddComponent<CanvasGroup>();
        screenGroup.alpha = 0f;
        Image dim = HudStyle.Image("Dim", screen.transform, new Color(0.05f, 0f, 0f, 0.65f));
        HudStyle.Stretch(dim.rectTransform);
        screenText = HudStyle.Text("Text", screen.transform, "YOU DIED", 64f, p.warning, TextAlignmentOptions.Center, FontStyles.Bold);
        HudStyle.Place(screenText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1200f, 260f));
        screenText.textWrappingMode = TextWrappingModes.Normal;
    }

    private void OnValidate()
    {
        if (statusRules == null) statusRules = new RespawnStatusRules();
        if (itemLoss == null) itemLoss = new ItemLossRules();
        if (milestones == null) milestones = new AbsorptionMilestones { enabled = true };
    }
}
