using System.Collections.Generic;
using GameNet;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies a character (<see cref="CharacterSave"/>) to a player: class, race, height, the traits picked with their
/// remaining points, name and appearance.
/// <para><b>Rules</b> (<c>runsRules</c> true: offline, the server, the host) also hand out the traits and the trait
/// points. A client's copy only shows the class, race, height and look; its traits and points come from the server
/// (<c>NetCombatStatus</c> mirrors them).</para>
/// Single player: the character picked on the start screens (<see cref="CharacterSession.Current"/>) is applied to the
/// local player as soon as it is in the game. Networked: <c>NetPlayer</c> sends it to the server, which checks and
/// applies it, then every machine shows it.
/// </summary>
public static class CharacterProfileApplier
{
    /// <summary>A character was applied to a player (any machine).</summary>
    public static event System.Action<GameObject, CharacterSave> Applied;

    /// <summary>Apply the session character to the single player as soon as it is in the game.</summary>
    public static bool AutoApplyOffline = true;

    /// <summary>Build the pause and settings menu at runtime when the game scene has none.</summary>
    public static bool CreatePauseMenuIfMissing = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Applied = null;
        AutoApplyOffline = true;
        CreatePauseMenuIfMissing = true;
    }

    /// <summary>
    /// Applies <paramref name="save"/> to <paramref name="player"/> (the player's root, or any object of it). The
    /// player's Start must have run (<see cref="PlayerStatusController.IsInitialized"/>), or its class would be applied twice.
    /// </summary>
    public static void Apply(GameObject player, CharacterSave save, bool runsRules)
    {
        if (player == null || save == null)
            return;
        CharacterCatalog catalog = CharacterCatalog.Instance;
        CharacterBuild build = CharacterBuild.From(save, catalog);
        PlayerStatusController status = player.GetComponentInChildren<PlayerStatusController>(true);
        if (status == null)
            status = player.GetComponentInParent<PlayerStatusController>();
        GameObject root = status != null ? RootOf(status) : player;

        AppliedCharacter marker = root.GetComponent<AppliedCharacter>();
        if (marker == null)
            marker = root.AddComponent<AppliedCharacter>();

        TraitManager traits = status != null ? (status.TraitManager != null ? status.TraitManager : status.GetComponentInChildren<TraitManager>(true)) : null;
        bool ownsTraits = runsRules && traits != null && !traits.IsMirror && (status == null || !status.IsReplica);

        // A character applied before (a different one picked): take back exactly what it gave.
        if (ownsTraits)
        {
            foreach (Trait t in marker.grantedTraits)
                if (t != null) traits.RemoveTrait(t, true);
            marker.grantedTraits.Clear();
        }

        // Class: the old class's starting traits go, the new class brings its own.
        if (status != null && build.playerClass != null && build.playerClass != status.CurrentPlayerClass)
        {
            PlayerClass old = status.CurrentPlayerClass;
            if (ownsTraits && old != null)
                foreach (Trait t in old.GetStartingTraits())
                    traits.RemoveTrait(t, true);
            status.SetPlayerClass(build.playerClass);
        }

        // The race's look: build (width), ears, beards, blend shapes and parts. Height and build need a Character Body.
        AppearanceRules rules = AppearanceRules.From(build.Archetypes);
        // A model without a body gets its placeholder body first, so it is measured and scaled with the rest.
        CharacterAppearance look = CharacterAppearance.For(root);
        if (look != null)
            look.EnsureBody();
        bool sized = build.ClampedHeight > 0f || !Mathf.Approximately(rules.bodyWidth, 1f);
        CharacterBody body = root.GetComponentInChildren<CharacterBody>(true);
        if (body == null && sized)
            body = AddBody(root);
        if (body != null)
            body.SetBodyWidth(rules.bodyWidth);

        // Race and height (Character Identity: race stats, innate traits, height through the Character Body).
        if (status != null && (build.race != null || (build.playerClass != null && build.playerClass.archetype != null) ||
                               status.GetComponentInChildren<CharacterIdentity>(true) != null))
        {
            CharacterIdentity identity = CharacterIdentity.For(status);
            if (identity.Race != build.race)
                identity.SetRace(build.race);
            float h = build.ClampedHeight;
            if (h > 0f)
                identity.SetHeight(h);
        }

        // The traits picked on the creation screen, and the points left.
        if (ownsTraits)
        {
            foreach (Trait t in build.traits)
            {
                if (t == null) continue;
                traits.AddTrait(t, true); // a creation grant (points are counted here, not by the manager)
                marker.grantedTraits.Add(t);
            }
            traits.SetTraitPoints(Mathf.Max(0, build.RemainingPoints));
        }

        // Name (in a network session NetPlayer's synced name decides).
        if (!NetAuthority.IsNetworked && !string.IsNullOrWhiteSpace(save.characterName))
        {
            PlayerNameComponent n = root.GetComponentInChildren<PlayerNameComponent>(true);
            if (n != null) n.SetPlayerName(save.characterName);
            Player p = root.GetComponentInChildren<Player>(true);
            if (p != null) p.PlayerName = save.characterName;
        }

        if (look != null)
        {
            look.SetRules(rules);
            look.Apply(save.appearance);
        }

        marker.Save = save;
        Applied?.Invoke(root, save);
    }

    /// <summary>
    /// Adds a Character Body (it scales the model and the CharacterController for the race's height and build), with
    /// the model's height measured so a 1.8 m race default keeps a normal model at its size.
    /// </summary>
    private static CharacterBody AddBody(GameObject root)
    {
        Transform model = CharacterBody.FindModel(root.transform);
        float measured = 0f;
        ProceduralBody placeholder = model != null ? model.GetComponentInChildren<ProceduralBody>(true) : null;
        if (placeholder != null)
        {
            float s = Mathf.Abs(model.lossyScale.y);
            if (s > 1e-4f)
                measured = placeholder.WorldHeight / s * Mathf.Abs(model.localScale.y);
        }
        else if (model != null)
        {
            bool any = false;
            var b = new Bounds(model.position, Vector3.zero);
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer || r.transform.name == "Appearance Parts" || r.GetComponentInParent<CharacterAppearancePartsMarker>() != null)
                    continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            float s = Mathf.Abs(model.lossyScale.y);
            if (any && s > 1e-4f)
                measured = b.size.y / s * Mathf.Abs(model.localScale.y);
        }
        CharacterBody body = root.AddComponent<CharacterBody>();
        if (measured > 0.3f && measured < 6f)
            body.modelHeight = measured;
        body.height = body.modelHeight;
        body.Apply();
        Debug.Log($"[Characters] Added a Character Body to '{root.name}' (model height {body.modelHeight:0.00} m) for the race's height and build. " +
                  "Add one to the player prefab to set it up yourself.", root);
        return body;
    }

    /// <summary>The root of a player (its CharacterController object, else the status object).</summary>
    public static GameObject RootOf(PlayerStatusController status)
    {
        if (status == null) return null;
        CharacterController cc = status.GetComponentInParent<CharacterController>();
        if (cc == null) cc = status.GetComponentInChildren<CharacterController>();
        return cc != null ? cc.gameObject : status.gameObject;
    }

    /// <summary>The player this machine controls (null when there is none).</summary>
    public static PlayerStatusController LocalPlayer()
    {
        CombatEntity local = PlayerLocator.Local;
        if (local != null)
        {
            PlayerStatusController s = local.GetComponentInParent<PlayerStatusController>();
            if (s == null) s = local.GetComponentInChildren<PlayerStatusController>();
            if (s != null) return s;
        }
        if (NetAuthority.IsNetworked)
            return null;
        return Object.FindAnyObjectByType<PlayerStatusController>();
    }

    // ------------------------------------------------------------------ single player
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        CharacterAutoApplyRunner.Ensure();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => CharacterAutoApplyRunner.Ensure();
}

