using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Put it on the player: its inspector lists every script the player needs (required, recommended, optional, added
/// automatically), shows which are missing with an Add button for each (or Add All), checks the important references
/// (hands, animator, layer) and the scene / project setup (Combat Settings, Trait Database, EventSystem, pause menu),
/// and offers the setup buttons of the other components. In Play Mode it can log what is missing. It does nothing else
/// at runtime and can stay on the prefab.
/// </summary>
[DisallowMultipleComponent]
public class PlayerSetupValidator : MonoBehaviour
{
    public enum Importance
    {
        /// <summary>The player does not work without it.</summary>
        Required,
        /// <summary>A normal player has it (inventory, camera, animation...).</summary>
        Recommended,
        /// <summary>Features you may not use (abilities, races, body parts...).</summary>
        Optional,
        /// <summary>Added by other scripts at runtime when missing; add it only to change its settings.</summary>
        Automatic,
    }

    /// <summary>One script the player should have.</summary>
    public sealed class Check
    {
        public Type type;
        public Importance importance;
        public string purpose;
        /// <summary>Found on a child too (the model, a hand) - not only on the player's root.</summary>
        public bool inChildren;
        /// <summary>Created by the Player Status Controller's setup wizard (status managers, experience, traits, dash, roll).</summary>
        public bool byStatusWizard;

        public string Name => type != null ? type.Name : "?";
    }

    [Tooltip("Log the missing required and recommended scripts to the Console when Play starts.")]
    [SerializeField] private bool logProblemsOnPlay = true;

    private static Check C(Type t, Importance i, string purpose, bool inChildren = false, bool wizard = false) =>
        new Check { type = t, importance = i, purpose = purpose, inChildren = inChildren, byStatusWizard = wizard };

    /// <summary>Every script checked, in the order they are listed.</summary>
    public static readonly Check[] Checks =
    {
        // Required
        C(typeof(CharacterController), Importance.Required, "Moves the player and collides with the world."),
        C(typeof(PlayerMovementModel), Importance.Required, "Movement settings: gravity, jump force, stamina costs."),
        C(typeof(PlayerMovementController), Importance.Required, "Gravity, rotation toward the camera, ground check."),
        C(typeof(MovementStateMachine), Importance.Required, "Idle, walk, sprint, crouch, jump, dash, roll."),
        C(typeof(AvailabilityStateMachine), Importance.Required, "Unaffected / stunned / dead: blocks movement, abilities and attacks."),
        C(typeof(PlayerStatusController), Importance.Required, "Health, stamina and the other status bars, class, experience, traits."),
        C(typeof(HealthManager), Importance.Required, "Health (status setup wizard).", false, true),
        C(typeof(StaminaManager), Importance.Required, "Stamina (status setup wizard).", false, true),
        C(typeof(SpeedManager), Importance.Required, "Walk / run / crouch speeds (status setup wizard).", false, true),
        C(typeof(Player), Importance.Required, "Interaction: pick up items, open storage, use interactables."),

        // Recommended
        C(typeof(ManaManager), Importance.Recommended, "Mana (status setup wizard).", false, true),
        C(typeof(WeightManager), Importance.Recommended, "Carry weight (status setup wizard).", false, true),
        C(typeof(ExperienceManager), Importance.Recommended, "Levels and stat points (status setup wizard).", false, true),
        C(typeof(TraitManager), Importance.Recommended, "Traits (status setup wizard).", false, true),
        C(typeof(PlayerDashModel), Importance.Recommended, "Dash (status setup wizard).", false, true),
        C(typeof(PlayerRollModel), Importance.Recommended, "Roll (status setup wizard).", false, true),
        C(typeof(PlayerAnimationModel), Importance.Recommended, "Animator parameters (needs an Animator on the model).", true),
        C(typeof(PlayerAnimationController), Importance.Recommended, "Plays movement and attack animations.", true),
        C(typeof(PlayerCameraModel), Importance.Recommended, "Camera list, first / third person, FOV."),
        C(typeof(PlayerCameraController), Importance.Recommended, "Switches and shakes the cameras."),
        C(typeof(PlayerCameraView), Importance.Recommended, "Look and zoom input for the cameras."),
        C(typeof(InventoryManager), Importance.Recommended, "Inventory, hotbar, equipment, using the item in hand (builds its UI)."),
        C(typeof(WeaponController), Importance.Recommended, "Weapon attacks: melee, ranged, dual wield, combos, reload."),
        C(typeof(PlayerNameComponent), Importance.Recommended, "The character's name (set by character creation)."),
        C(typeof(CharacterIdentity), Importance.Recommended, "Race, class combat stats, height, level and attribute points."),
        C(typeof(UnityEngine.InputSystem.PlayerInput), Importance.Recommended, "Unity's Player Input: camera look, interact and pause events (assign the input actions, Invoke Unity Events)."),

        // Optional
        C(typeof(AbilitiesStateMachine), Importance.Optional, "Ability keys."),
        C(typeof(PlayerAbilityController), Importance.Optional, "Ability slots (also receives abilities absorbed from mobs)."),
        C(typeof(CharacterBody), Importance.Optional, "Height: scales the model and the CharacterController."),
        C(typeof(BodyPartController), Importance.Optional, "Head / torso / arm / leg damage (then add hitboxes)."),
        C(typeof(PlayerStartItemController), Importance.Optional, "Starting items per class."),
        C(typeof(AudioSource), Importance.Optional, "Item, weapon and interaction sounds."),

        // Added automatically at runtime
        C(typeof(CombatEntity), Importance.Automatic, "The player as a combatant: team, faction, crowd control, threat."),
        C(typeof(CombatStats), Importance.Automatic, "Combat stats from race, class, traits, items and buffs.", true),
        C(typeof(EquipmentManager), Importance.Automatic, "Applies worn items' effects.", true),
        C(typeof(ArmorSetManager), Importance.Automatic, "Armor set bonuses.", true),
        C(typeof(EquipmentVisuals), Importance.Automatic, "Weapons and armour shown on the body.", true),
        C(typeof(BlockController), Importance.Automatic, "Blocking with shields and weapon guards.", true),
        C(typeof(QuickSlotBar), Importance.Automatic, "Consumable quickslots (on the inventory).", true),
    };

    /// <summary>Is the script on the player (or one of its children when the check allows it)?</summary>
    public static bool Has(GameObject root, Check check)
    {
        if (root == null || check?.type == null)
            return false;
        if (root.GetComponent(check.type) != null)
            return true;
        return check.inChildren && root.GetComponentInChildren(check.type, true) != null;
    }

    /// <summary>The checks of one importance that fail on <paramref name="root"/>.</summary>
    public static List<Check> Missing(GameObject root, Importance importance)
    {
        var list = new List<Check>();
        foreach (Check c in Checks)
            if (c.importance == importance && !Has(root, c))
                list.Add(c);
        return list;
    }

    private void Start()
    {
        if (!logProblemsOnPlay)
            return;
        List<Check> required = Missing(gameObject, Importance.Required);
        List<Check> recommended = Missing(gameObject, Importance.Recommended);
        if (required.Count > 0)
            Debug.LogError($"[Player Setup] {name} is missing required scripts: {string.Join(", ", required.ConvertAll(c => c.Name))}. " +
                           "Select it and use the Player Setup Validator's Add buttons.", this);
        if (recommended.Count > 0)
            Debug.LogWarning($"[Player Setup] {name} is missing recommended scripts: {string.Join(", ", recommended.ConvertAll(c => c.Name))}.", this);
    }
}
