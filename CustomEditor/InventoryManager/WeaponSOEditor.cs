#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Weapon inspector, in sections: Item, Damage, Attacks (one tab per input, every attack grouped into Timing, Cost &amp;
/// Movement, Damage, Hit Area with a top-down drawing of what it hits, On Hit, Charge, Behaviours, Sound &amp; Visual,
/// Chain and Traits, each with a ▶ Preview), Hit Area &amp; Range, Combos, Traits &amp; While Wielded, Animation &amp; Sound.
/// Every section says in plain words what its settings do and how they differ from the look-alike ones.
/// </summary>
[CustomEditor(typeof(WeaponSO), true)]
[CanEditMultipleObjects]
public class WeaponSOEditor : ItemSOEditor
{
    private const string Key = "WeaponSOEditor.";

    private static readonly string[] InputHelp =
    {
        "Primary attack - the Use Item input (left click). Required.",
        "Quick attack on its own input. Optional.",
        "Strong attack on its own input (templates make it charged: hold to charge, release to strike). Optional.",
        "Special move: a lunge, a poisoned strike, a nova... Optional.",
        "Secondary attack - right click / alternate input: a shield bash, a thrust, a charged shot. Optional.",
    };
    private static readonly string[] ActionFields = { "normalAction", "lightAction", "heavyAction", "specialAction", "alternateAction" };

    private readonly HashSet<string> drawn = new HashSet<string>();
    private bool open;

    private WeaponSO Weapon => (WeaponSO)target;

    // ------------------------------------------------------------------ presets and top summary
    protected override void AddPresets(GenericMenu menu)
    {
        var weapon = (WeaponSO)target;
        foreach (WeaponTemplates.Template tpl in System.Enum.GetValues(typeof(WeaponTemplates.Template)))
        {
            WeaponTemplates.Template captured = tpl;
            menu.AddItem(new GUIContent($"Moveset (attacks, combos)/{ObjectNames.NicifyVariableName(tpl.ToString())}"), false, () =>
            {
                if (EditorUtility.DisplayDialog("Apply weapon moveset", $"Replace the attacks of '{weapon.name}' with the {captured} moveset? (Undo restores them.)", "Apply", "Cancel"))
                    ApplyPreset($"{captured} moveset", () => WeaponTemplates.Apply(weapon, captured));
            });
        }
        foreach (ItemPresets.WeaponTier t in System.Enum.GetValues(typeof(ItemPresets.WeaponTier)))
        {
            ItemPresets.WeaponTier captured = t;
            menu.AddItem(new GUIContent($"Tier (damage, durability, value)/{t}"), false, () =>
                ApplyPreset($"{captured} tier", () => ItemPresets.ApplyWeaponTier(weapon, captured)));
        }
    }

    protected override void DrawToolsTop()
    {
        if (serializedObject.isEditingMultipleObjects)
            return;
        WeaponSO weapon = Weapon;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Attacks at a glance", EditorStyles.boldLabel);
        foreach (AttackType t in System.Enum.GetValues(typeof(AttackType)))
        {
            AttackAction a = weapon.GetAction(t);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (a == null)
                {
                    EditorGUILayout.LabelField(t.ToString(), "—  (not used)", EditorStyles.miniLabel);
                    continue;
                }
                string chain = a.GetVariationCount() > 0 ? $"chain of {a.GetVariationCount() + 1}" : "single hit";
                string charge = a.charge != null && a.charge.enabled ? $" · hold to charge (≤ ×{a.charge.damageAtFullCharge:0.#})" : "";
                EditorGUILayout.LabelField(new GUIContent(t.ToString(), InputHelp[(int)t]),
                    new GUIContent($"{a.DisplayName}: {AttackPreviewUtility.Duration(weapon, a):0.##}s · ×{a.damageMultiplier:0.##} dmg · reach {AttackPreviewUtility.Reach(weapon, a):0.#} m · {chain}{charge}"),
                    EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button(new GUIContent("Edit", "Opens this attack in the Attacks section below."), EditorStyles.miniButtonLeft, GUILayout.Width(36f)))
                {
                    SessionState.SetInt(Key + "tab", (int)t);
                    SessionState.SetBool(Key + "Attacks", true);
                }
                if (GUILayout.Button(new GUIContent("▶", "Preview: plays the attack on the character in the scene and draws its hit area in the Scene view."),
                        EditorStyles.miniButtonRight, GUILayout.Width(24f)))
                    AttackPreviewWindow.Open(weapon, t, 0);
            }
        }
        EditorGUILayout.EndVertical();
    }

    // ------------------------------------------------------------------ sections
    protected override void DrawFields()
    {
        if (serializedObject.isEditingMultipleObjects)
        {
            base.DrawFields();
            return;
        }
        drawn.Clear();
        drawn.Add("m_Script");
        WeaponSO weapon = Weapon;

        if (Section("Item", "Name, icon, stack size, weight, price, durability and how the model sits in the hand.", true))
        {
            P("name", "description", "itemType", "icon", "prefab");
            Sub("Inventory");
            P("stackMax", "weight", "price", "pickUpTime");
            Sub("Durability");
            P("maxDurability", "durability", "durabilityReductionPerUse", "shouldBeDestroyedOn0UsesLeft", "cooldown");
            Sub("In the hand");
            P("position", "rotation", "scale");
        }
        else Mark("name", "description", "itemType", "icon", "prefab", "stackMax", "weight", "price", "pickUpTime", "maxDurability",
                  "durability", "durabilityReductionPerUse", "shouldBeDestroyedOn0UsesLeft", "cooldown", "position", "rotation", "scale");
        // Use Feedback belongs to usable items (potions, food); weapons play their attacks' animation and sound instead.
        Mark("useAnimation", "useAudioClip", "useParticles");

        if (Section("Damage & Stats", "The base numbers of every attack. Each attack multiplies them (its Damage Multiplier, Knockback Multiplier...).", true))
            DrawDamage(weapon);
        else Mark("weaponCategory", "scaling", "minDamage", "maxDamage", "criticalChance", "criticalDamageMultiplier", "knockBack", "attackSpeed",
                  "elementType", "elementalBuildupRate", "toolType", "toolDamage");

        if (Section("Attacks", "One attack per input. Pressing the same input again within 'Variant Time' plays the next hit of its chain. " +
                               "▶ previews an attack: its animation on the character in the scene and the area it hits in the Scene view.", true))
            DrawAttacks(weapon);
        else Mark(ActionFields);

        if (Section("Hit Area & Range", null, false))
            DrawRange(weapon);
        else Mark("minRange", "maxRange", "attackCast");

        if (Section("Combos", "Combos: a fixed string of inputs (Light, Light, Heavy) plays a special attack. The Combo Tree adds branches " +
                              "with conditions (a finisher when stamina ≥ 30...).", false))
        {
            P("comboSequences", "comboTree");
            if (weapon.ComboTree == null && GUILayout.Button(new GUIContent("Create Combo Tree", "Creates a Combo Tree asset next to the weapon and assigns it.")))
                CreateComboTree(weapon);
        }
        else Mark("comboSequences", "comboTree");

        if (Section("Traits & While Wielded", null, false))
            DrawTraits(weapon);
        else Mark("weaponTraits", "applyTraitsToEnemy", "traitEffectMultiplier", "applyTraitsToWielder", "passiveEffects");

        if (Section("Animation & Sound", null, false))
        {
            Help("Attack Sound: the swing of every attack that has no sound of its own. An attack's Hit Sound: when a hit lands. " +
                 "Equip / Unequip: when the weapon is put in or taken out of the hand. (Use Feedback of other items - potions, food - is not " +
                 "used by weapons, so it is hidden here.)");
            P("animationSet", "useCustomAnimatorController", "attackSound", "equipSound", "unequipSound");
        }
        else Mark("animationSet", "useCustomAnimatorController", "attackSound", "equipSound", "unequipSound");

        DrawLeftovers();
    }

    private void DrawDamage(WeaponSO weapon)
    {
        P("weaponCategory", "scaling");
        SerializedProperty min = serializedObject.FindProperty("minDamage");
        SerializedProperty max = serializedObject.FindProperty("maxDamage");
        Mark("minDamage", "maxDamage");
        Rect r = EditorGUILayout.GetControlRect();
        r = EditorGUI.PrefixLabel(r, new GUIContent("Damage (min - max)", "Damage of one hit is picked between these (before the attack's " +
                                                                          "multiplier, stats, charge and the target's defense)."));
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        float half = (r.width - 16f) * 0.5f;
        EditorGUI.PropertyField(new Rect(r.x, r.y, half, r.height), min, GUIContent.none);
        EditorGUI.LabelField(new Rect(r.x + half, r.y, 16f, r.height), "–", EditorStyles.centeredGreyMiniLabel);
        EditorGUI.PropertyField(new Rect(r.x + half + 16f, r.y, half, r.height), max, GUIContent.none);
        EditorGUI.indentLevel = indent;

        SerializedProperty crit = serializedObject.FindProperty("criticalChance");
        Mark("criticalChance");
        EditorGUILayout.Slider(crit, 0f, 1f, new GUIContent("Critical Chance", "Chance (0-1) that a hit is critical. 0.1 = 10%."));
        P("criticalDamageMultiplier", "knockBack", "attackSpeed", "elementType", "elementalBuildupRate");

        float avg = (weapon.MinDamage + weapon.MaxDamage) * 0.5f;
        float withCrit = avg * (1f + Mathf.Clamp01(weapon.CriticalChance) * (Mathf.Max(1f, weapon.CriticalDamageMultiplier) - 1f));
        AttackAction first = weapon.NormalAction;
        float normalHit = first != null ? withCrit * first.damageMultiplier : 0f;
        float normalTime = first != null ? AttackPreviewUtility.Duration(weapon, first) : 0f;
        Help($"Average hit {avg:0.#} (≈{withCrit:0.#} with crits)" +
             (normalTime > 0f ? $" · repeating the Normal attack ≈ {normalHit / normalTime:0.#} damage per second (before stats and defense)" : ""));

        Sub("Tool");
        P("toolType");
        using (new EditorGUI.DisabledScope(weapon.ToolType == ToolType.None))
            P("toolDamage");
    }

    // ------------------------------------------------------------------ attacks
    private void DrawAttacks(WeaponSO weapon)
    {
        Mark(ActionFields);
        int tab = Mathf.Clamp(SessionState.GetInt(Key + "tab", 0), 0, 4);
        var names = new GUIContent[5];
        for (int i = 0; i < 5; i++)
        {
            AttackType t = (AttackType)i;
            bool used = weapon.GetAction(t) != null;
            names[i] = new GUIContent(used ? t.ToString() : t + " –", InputHelp[i] + (used ? "" : "\nNot used yet."));
        }
        int newTab = GUILayout.Toolbar(tab, names, GUILayout.Height(24f));
        if (newTab != tab)
        {
            SessionState.SetInt(Key + "tab", newTab);
            tab = newTab;
            GUI.FocusControl(null);
        }
        var type = (AttackType)tab;
        Help(InputHelp[tab]);

        SerializedProperty actionProp = serializedObject.FindProperty(ActionFields[tab]);
        AttackAction action = AttackPreviewUtility.RawAction(weapon, type);
        if (actionProp == null || action == null)
            return;

        bool used2 = type == AttackType.Normal || WeaponSO.IsUsable(action);
        if (!used2)
        {
            EditorGUILayout.HelpBox($"No {type} attack. It becomes active as soon as it has a name, an animation, effects, behaviours or chain hits.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent($"Create {type} Attack", "Names it so it is used; then set its animation, timing and hit area.")))
                    actionProp.FindPropertyRelative("actionName").stringValue = ObjectNames.NicifyVariableName(type.ToString()) + " Attack";
                if (GUILayout.Button(new GUIContent("Use a Moveset…", "Weapon templates fill every input at once (Presets ▸ Moveset).")))
                {
                    var menu = new GenericMenu();
                    AddPresets(menu);
                    menu.ShowAsContext();
                }
            }
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("▶ Preview", "Plays this attack on the character in the scene and draws its hit area in the Scene view."), GUILayout.Height(22f)))
                AttackPreviewWindow.Open(weapon, type, 0);
            if (type != AttackType.Normal && GUILayout.Button(new GUIContent("Remove", $"Clears the {type} attack (Undo restores it)."), GUILayout.Width(70f), GUILayout.Height(22f)))
            {
                if (EditorUtility.DisplayDialog("Remove attack", $"Clear the {type} attack of '{weapon.name}'?", "Remove", "Cancel"))
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(weapon, "Remove attack");
                    weapon.SetAction(type, new AttackAction());
                    EditorUtility.SetDirty(weapon);
                    serializedObject.Update();
                    GUIUtility.ExitGUI();
                }
            }
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        DrawAttack(actionProp, action, weapon, type, 0);
        EditorGUILayout.EndVertical();
    }

    /// <summary>One attack (the action itself or a chain hit), grouped in foldouts.</summary>
    private void DrawAttack(SerializedProperty a, AttackComponent obj, WeaponSO weapon, AttackType type, int variation)
    {
        var used = new HashSet<string>();
        bool isAction = obj is AttackAction;
        R(a, used, true, isAction ? "actionName" : "variationName");

        Rect bar = GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true));
        AttackPreviewUtility.DrawTimeline(bar, obj, AttackPreviewUtility.Duration(weapon, obj));
        EditorGUILayout.LabelField($"{AttackPreviewUtility.Duration(weapon, obj):0.##} s in game · {obj.StaminaCost:0.#} stamina · reach " +
                                   $"{AttackPreviewUtility.Reach(weapon, obj):0.#} m", EditorStyles.centeredGreyMiniLabel);

        bool g;
        g = Group(a, "timing", "Animation & Timing", "Startup (wind-up, yellow) → Active (it hits, red) → Recovery (grey). Seconds at speed 1; " +
                                                    "the clip is stretched over the whole attack. Cancel Point (cyan) = when a queued next attack may start.");
        R(a, used, g, "animationClip", "animationSpeed", "startupFrames", "activeFrames", "recoveryFrames", "cancelPoint");

        g = Group(a, "move", "Cost & Movement", "Stamina spent when the attack starts, and how the attacker moves (Forward Movement = a lunge).");
        R(a, used, g, "staminaCost", "lockMovement", "movementSpeedMultiplier", "forwardMovement");

        g = Group(a, "damage", "Damage", "Multiplies the weapon's damage (Damage & Stats) for this attack.");
        R(a, used, g, "damageMode", "damageMultiplier", "damageType", "elementOverride", "criticalChanceBonus", "knockbackMultiplier");

        g = Group(a, "hit", "Hit Area & Targets", "WHAT the attack hits. Hit Shape: an area in front of the character. Weapon Blade: the " +
                                                  "weapon itself, following the animation. Hit Filter = who counts (see the buttons' tooltips); " +
                                                  "Target Rules = friendly fire, kinds and factions.");
        if (g)
        {
            R(a, used, true, "hitDetection");
            HitDetectionMode mode = AttackPreviewUtility.Detection(weapon, obj);
            if (obj.hitDetection == HitDetectionMode.Auto)
                Help(mode == HitDetectionMode.WeaponCast
                    ? "Auto → Weapon Cast: this weapon's old Attack Cast has Target Layers, so it is used instead of the Hit Shape. Pick 'Hit Shape' to use the area below."
                    : "Auto → Hit Shape (the weapon has no Attack Cast layers).");
            if (mode == HitDetectionMode.HitShape)
                R(a, used, true, "hitShape");
            else
            {
                used.Add("hitShape");
                if (mode == HitDetectionMode.WeaponCast)
                    Help("Uses the weapon's Attack Cast (section Hit Area & Range) around the hand; the Hit Shape is ignored.");
                if (mode == HitDetectionMode.WeaponBlade)
                {
                    Help("Hits whatever the weapon itself touches while the attack is Active: the weapon's Hit Volumes (capsules, " +
                         "spheres, boxes on the model in the hand), following the animation and sweeping between frames. The volumes " +
                         "are set once for the weapon (section Hit Area & Range); pick here which ones this attack uses. ▶ Preview shows them.");
                    DrawVolumePicker(a.FindPropertyRelative("bladeVolumes"), weapon);
                    if (weapon.Prefab == null)
                        EditorGUILayout.HelpBox("The weapon has no Prefab: the volumes are measured from the hand bone.", MessageType.Warning);
                }
            }
            used.Add("bladeVolumes");
            R(a, used, mode != HitDetectionMode.None, "hitFilter", "targetRules", "maxTargets", "rehitInterval");
            used.Add("hitFilter"); used.Add("targetRules"); used.Add("maxTargets"); used.Add("rehitInterval");

            float area = 1f;
            if (obj.charge != null && obj.charge.enabled)
            {
                float charge = SessionState.GetFloat(Key + "diagramCharge", 0f);
                charge = EditorGUILayout.Slider(new GUIContent("Preview Charge", "Only for the drawing: how charged the attack is (the area grows up to Area At Full Charge)."), charge, 0f, 1f);
                SessionState.SetFloat(Key + "diagramCharge", charge);
                area = obj.charge.AreaMultiplier(charge);
            }
            Rect d = GUILayoutUtility.GetRect(10f, 170f, GUILayout.ExpandWidth(true));
            d = EditorGUI.IndentedRect(d);
            HitAreaDiagram.Draw(d, weapon, obj, area, new Color(1f, 0.3f, 0.25f), 0f);
        }
        else Mark(used, "hitDetection", "hitShape", "hitFilter", "targetRules", "maxTargets", "rehitInterval", "bladeVolumes");

        bool impactOn = obj.impact != null && obj.impact.enabled;
        g = Group(a, "impact", impactOn ? $"Impact - ground slam (on: {obj.impact.area?.Describe(1f)})" : "Impact - ground slam (off)",
            "A second hit when the weapon strikes the ground: its own, usually bigger, area around the impact point. Characters " +
            "inside it are hit whether or not they touch the weapon. Ground = physics layers; who is hit = its filter and rules. " +
            "E.g. a hammer: Weapon Blade on the head for the swing + an Impact circle of 3 m that knocks up.");
        R(a, used, g, "impact");

        g = Group(a, "onhit", "On Hit", "What happens to each character hit: status effects (burn, poison, slow, stun, knockback, life steal...), " +
                                        "an effect spawned on them and the Hit Sound (the sound of the hit landing).");
        R(a, used, g, "onHitEffects", "hitVfx", "hitSound");

        g = Group(a, "charge", obj.charge != null && obj.charge.enabled ? "Charge (on)" : "Charge (off)",
            "Hold the input to charge, release to attack: more damage and a bigger area the longer it is held.");
        R(a, used, g, "charge");

        g = Group(a, "behaviours", $"Behaviours ({obj.behaviours?.Count ?? 0})", "Extra things done at a moment of the attack: cast an ability, " +
                                                                               "fire a projectile, lunge at the target, buff the attacker, spawn an effect.");
        R(a, used, g, "behaviours");

        g = Group(a, "look", "Sound & Visual", "Attack Sound = the swing (empty = the weapon's Attack Sound). Particles where it hits, a trail on the hand.");
        R(a, used, g, "attackSound", "attackParticles", "trailEffect");

        if (isAction)
        {
            var action = (AttackAction)obj;
            g = Group(a, "chain", $"Chain ({action.GetVariationCount() + 1} hit{(action.GetVariationCount() > 0 ? "s" : "")})",
                "Press the same input again within Variant Time to play the next hit; after the last one the chain starts over.");
            used.Add("variations");
            if (g)
            {
                R(a, used, true, "variantTime");
                DrawChain(a.FindPropertyRelative("variations"), action, weapon, type);
            }
            else used.Add("variantTime");

            g = Group(a, "traits", "Trait Requirements & Enhancements", "Required: the player OR the weapon must have one of these traits to " +
                                                                         "perform the attack. Enhancement: when one is present the attack is faster / cheaper / stronger.");
            R(a, used, g, "requiredTraits", "enhancementTraits", "enhancedAnimationSpeedMultiplier", "enhancedStaminaCostMultiplier",
              "enhancedDamageMultiplier", "enhancementEffects");
        }

        g = Group(a, "classic", $"Classic Effects ({obj.Effects.Count}) - old", "Older effect list (Hp, Speed, Stamina amounts). On Hit effects do more; " +
                                                                               "kept so existing weapons keep working.");
        R(a, used, g, "effects");

        // Anything not grouped above (fields added later)
        SerializedProperty it = a.Copy();
        SerializedProperty end = a.GetEndProperty();
        bool enter = true;
        while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
        {
            enter = false;
            if (!used.Contains(it.name))
                EditorGUILayout.PropertyField(it, true);
        }
    }

    private void DrawChain(SerializedProperty list, AttackAction action, WeaponSO weapon, AttackType type)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty el = list.GetArrayElementAtIndex(i);
            AttackVariation v = action.variations != null && i < action.variations.Count ? action.variations[i] : null;
            using (new EditorGUILayout.HorizontalScope())
            {
                string title = v != null ? $"Hit {i + 2}: {v.DisplayName}  ({AttackPreviewUtility.Duration(weapon, v):0.##}s, ×{v.damageMultiplier:0.##} dmg)" : $"Hit {i + 2}";
                el.isExpanded = EditorGUILayout.Foldout(el.isExpanded, title, true);
                if (v != null && GUILayout.Button(new GUIContent("▶", "Preview this hit."), EditorStyles.miniButtonLeft, GUILayout.Width(24f)))
                    AttackPreviewWindow.Open(weapon, type, i + 1);
                using (new EditorGUI.DisabledScope(i == 0))
                    if (GUILayout.Button(new GUIContent("↑", "Earlier in the chain."), EditorStyles.miniButtonMid, GUILayout.Width(22f)))
                    {
                        list.MoveArrayElement(i, i - 1);
                        serializedObject.ApplyModifiedProperties();
                        GUIUtility.ExitGUI();
                    }
                using (new EditorGUI.DisabledScope(i == list.arraySize - 1))
                    if (GUILayout.Button(new GUIContent("↓", "Later in the chain."), EditorStyles.miniButtonMid, GUILayout.Width(22f)))
                    {
                        list.MoveArrayElement(i, i + 1);
                        serializedObject.ApplyModifiedProperties();
                        GUIUtility.ExitGUI();
                    }
                if (GUILayout.Button(new GUIContent("✕", "Remove this hit."), EditorStyles.miniButtonRight, GUILayout.Width(22f)))
                {
                    list.DeleteArrayElementAtIndex(i);
                    serializedObject.ApplyModifiedProperties();
                        GUIUtility.ExitGUI();
                }
            }
            if (el.isExpanded && v != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawAttack(el, v, weapon, type, i + 1);
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }
        if (GUILayout.Button(new GUIContent("+ Add Hit to the Chain", "Adds the next hit with the timing, damage and hit area of the last one " +
                                                                    "(its on-hit effects and behaviours start empty).")))
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(weapon, "Add chain hit");
            AttackComponent last = action.GetVariationCount() > 0 ? (AttackComponent)action.variations[action.variations.Count - 1] : action;
            var hit = new AttackVariation { variationName = $"{action.DisplayName} {action.GetVariationCount() + 2}" };
            CopyBasics(last, hit);
            action.variations.Add(hit);
            EditorUtility.SetDirty(weapon);
            serializedObject.Update();
            GUIUtility.ExitGUI();
        }
    }

    private static void CopyBasics(AttackComponent from, AttackComponent to)
    {
        to.SetTiming(from.StartupFrames, from.ActiveFrames, from.RecoveryFrames, from.AnimationSpeed);
        to.SetMovement(from.LockMovement, from.MovementSpeedMultiplier, from.ForwardMovement);
        to.SetPresentation(from.AnimationClip, from.AttackSound, from.AttackParticles, from.TrailEffect);
        to.cancelPoint = from.cancelPoint;
        to.staminaCost = from.staminaCost;
        to.damageMode = from.damageMode;
        to.damageMultiplier = from.damageMultiplier;
        to.damageType = from.damageType;
        to.elementOverride = from.elementOverride;
        to.criticalChanceBonus = from.criticalChanceBonus;
        to.knockbackMultiplier = from.knockbackMultiplier;
        to.hitDetection = from.hitDetection;
        to.hitShape = from.hitShape != null ? from.hitShape.Clone() : null;
        to.hitFilter = from.hitFilter;
        to.maxTargets = from.maxTargets;
        to.rehitInterval = from.rehitInterval;
        to.hitVfx = from.hitVfx;
        to.hitSound = from.hitSound;
    }

    // ------------------------------------------------------------------ range
    private void DrawRange(WeaponSO weapon)
    {
        Help("• Hit Shape (on each attack) = WHERE that attack hits: the area in front of the character while it is Active. This is the real reach.\n" +
             "• Weapon Blade (below) = the weapon itself: a capsule along the model in the hand that follows the animation (attacks set to Weapon Blade).\n" +
             "• Attack Cast (below) = the OLD way: one physics overlap around the hand for every attack, only when it has Target Layers. New " +
             "weapons do not need it.\n" +
             "• Max / Min Range = a HINT of how far the weapon reaches, for AI, gizmos and your scripts. It never changes what is hit.");

        var rows = new List<string>();
        foreach (AttackType t in System.Enum.GetValues(typeof(AttackType)))
        {
            AttackAction a = weapon.GetAction(t);
            if (a == null) continue;
            HitDetectionMode mode = AttackPreviewUtility.Detection(weapon, a);
            rows.Add($"{t}: {AttackPreviewUtility.DescribeDetection(weapon, a, mode, 1f)} → reach {AttackPreviewUtility.Reach(weapon, a):0.#} m" +
                     (a.ForwardMovement.sqrMagnitude > 0.0001f ? " (lunge included)" : ""));
        }
        if (rows.Count > 0)
            EditorGUILayout.HelpBox(string.Join("\n", rows), MessageType.None);

        Sub("Range (hint)");
        P("minRange", "maxRange");
        float reach = AttackPreviewUtility.MaxReach(weapon);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent($"Set Max Range From Attacks ({reach:0.#} m)", "The farthest any attack (or chain hit, at full charge) can hit, lunge included.")))
            {
                serializedObject.FindProperty("maxRange").floatValue = Mathf.Round(reach * 10f) / 10f;
            }
        }

        Sub("Hit Volumes (for attacks set to Weapon Blade)");
        Help("The parts of the weapon that hit, on the model in the hand: Capsule (blade, shaft, handle), Sphere (mace head, pommel) " +
             "or Box (axe / hammer head, shield). Place each with values relative to the grip, or with empty marker children of the " +
             "prefab (exact on any model). Attacks pick them by name; ▶ Preview draws them on the animated character.");
        DrawVolumes(weapon);

        Sub("Attack Cast (old hit detection)");
        if (!weapon.HasUsableAttackCast)
            Help("Not used: its Target Layers are empty, so every attack uses its own Hit Shape. Set Target Layers only to keep an older weapon's hand overlap.");
        else
            Help("In use by the attacks whose Hit Detection is Auto or Weapon Cast: every one of them hits what overlaps this cast at the hand, whatever their Hit Shape.");
        P("attackCast");
    }

    // ------------------------------------------------------------------ hit volumes
    private void DrawVolumes(WeaponSO weapon)
    {
        SerializedProperty list = serializedObject.FindProperty("hitVolumes");
        SerializedProperty single = serializedObject.FindProperty("blade");
        Mark("hitVolumes", "blade");
        if (list == null)
            return;
        if (list.arraySize == 0 && single != null)
        {
            EditorGUILayout.LabelField("No list yet: the single Blade below is used.", EditorStyles.miniLabel);
            EditorGUILayout.PropertyField(single, new GUIContent("Blade"), true);
        }
        else
        {
            EditorGUILayout.PropertyField(list, new GUIContent("Hit Volumes", list.tooltip), true);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (EditorGUILayout.DropdownButton(new GUIContent("Add Volume ▾", "Add a ready-made volume (then adjust it with ▶ Preview)."), FocusType.Keyboard))
            {
                var menu = new GenericMenu();
                AddVolumePreset(menu, weapon, "Blade (capsule)", new WeaponBlade { name = "Blade", shape = WeaponBlade.VolumeShape.Capsule, start = 0.15f, end = 1.1f, radius = 0.1f });
                AddVolumePreset(menu, weapon, "Handle (capsule)", new WeaponBlade { name = "Handle", shape = WeaponBlade.VolumeShape.Capsule, start = 0f, end = 0.7f, radius = 0.06f, startMarker = "", endMarker = "" });
                AddVolumePreset(menu, weapon, "Head (box: axe, hammer)", new WeaponBlade { name = "Head", shape = WeaponBlade.VolumeShape.Box, center = new Vector3(0f, 0.85f, 0f), size = new Vector3(0.25f, 0.25f, 0.45f), startMarker = "Head", endMarker = "" });
                AddVolumePreset(menu, weapon, "Head (sphere: mace, flail)", new WeaponBlade { name = "Head", shape = WeaponBlade.VolumeShape.Sphere, center = new Vector3(0f, 0.8f, 0f), radius = 0.18f, startMarker = "Head", endMarker = "" });
                AddVolumePreset(menu, weapon, "Pommel (sphere)", new WeaponBlade { name = "Pommel", shape = WeaponBlade.VolumeShape.Sphere, center = new Vector3(0f, -0.1f, 0f), radius = 0.07f, startMarker = "Pommel", endMarker = "" });
                AddVolumePreset(menu, weapon, "Spear tip (capsule)", new WeaponBlade { name = "Tip", shape = WeaponBlade.VolumeShape.Capsule, start = 1.6f, end = 1.95f, radius = 0.08f, startMarker = "", endMarker = "" });
                AddVolumePreset(menu, weapon, "Shield (box)", new WeaponBlade { name = "Shield", shape = WeaponBlade.VolumeShape.Box, center = Vector3.zero, size = new Vector3(0.6f, 0.8f, 0.12f), startMarker = "Shield", endMarker = "" });
                menu.ShowAsContext();
            }
            if (GUILayout.Button(new GUIContent("Preview", "Open the Attack Preview on the Normal attack."), GUILayout.Width(70f)))
                AttackPreviewWindow.Open(weapon, AttackType.Normal, 0);
        }
    }

    private void AddVolumePreset(GenericMenu menu, WeaponSO weapon, string label, WeaponBlade preset)
    {
        menu.AddItem(new GUIContent(label), false, () =>
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(weapon, "Add hit volume");
            var so = new SerializedObject(weapon);
            SerializedProperty list = so.FindProperty("hitVolumes");
            AddVolume(list, preset);
            so.ApplyModifiedProperties();
            serializedObject.Update();
        });
    }

    private static void AddVolume(SerializedProperty list, WeaponBlade v)
    {
        list.arraySize++;
        SerializedProperty e = list.GetArrayElementAtIndex(list.arraySize - 1);
        e.FindPropertyRelative("name").stringValue = v.name;
        e.FindPropertyRelative("shape").enumValueIndex = (int)v.shape;
        e.FindPropertyRelative("axis").enumValueIndex = (int)v.axis;
        e.FindPropertyRelative("start").floatValue = v.start;
        e.FindPropertyRelative("end").floatValue = v.end;
        e.FindPropertyRelative("radius").floatValue = v.radius;
        e.FindPropertyRelative("center").vector3Value = v.center;
        e.FindPropertyRelative("size").vector3Value = v.size;
        e.FindPropertyRelative("rotation").vector3Value = v.rotation;
        e.FindPropertyRelative("startMarker").stringValue = v.startMarker;
        e.FindPropertyRelative("endMarker").stringValue = v.endMarker;
    }

    /// <summary>Toggle buttons for the weapon's volumes; none on = all of them.</summary>
    private static void DrawVolumePicker(SerializedProperty names, WeaponSO weapon)
    {
        if (names == null)
            return;
        IReadOnlyList<WeaponBlade> all = weapon.HitVolumes;
        var chosen = new List<string>();
        for (int i = 0; i < names.arraySize; i++)
        {
            string n = names.GetArrayElementAtIndex(i).stringValue;
            if (!string.IsNullOrWhiteSpace(n)) chosen.Add(n.Trim());
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(new GUIContent("Volumes Used", "Which hit volumes this attack uses. None selected = all of them."));
            bool changed = false;
            foreach (WeaponBlade v in all)
            {
                if (v == null) continue;
                bool on = chosen.Exists(n => string.Equals(n, v.name, System.StringComparison.OrdinalIgnoreCase));
                bool now = GUILayout.Toggle(on, new GUIContent(string.IsNullOrEmpty(v.name) ? "(unnamed)" : v.name, v.Describe()), EditorStyles.miniButton);
                if (now != on)
                {
                    changed = true;
                    if (now) chosen.Add(v.name);
                    else chosen.RemoveAll(n => string.Equals(n, v.name, System.StringComparison.OrdinalIgnoreCase));
                }
            }
            if (changed)
            {
                names.arraySize = chosen.Count;
                for (int i = 0; i < chosen.Count; i++)
                    names.GetArrayElementAtIndex(i).stringValue = chosen[i];
            }
        }
        if (chosen.Count == 0)
            EditorGUILayout.LabelField(" ", "All volumes", EditorStyles.miniLabel);
        foreach (string n in chosen)
            if (weapon.FindVolume(n) == null)
                EditorGUILayout.HelpBox($"The weapon has no hit volume named '{n}'.", MessageType.Warning);
    }

    // ------------------------------------------------------------------ traits
    private void DrawTraits(WeaponSO weapon)
    {
        Help("WEAPON TRAITS belong to the weapon. They change only this weapon's attacks (damage, attack speed, stamina cost, elements, " +
             "lifesteal, slow...) and count for the attacks' Required / Enhancement Traits. The player does not get them (unless Apply Traits To Wielder).\n" +
             "WHILE WIELDED effects act on the PLAYER while the weapon is in the hand: stats, resistances, procs, abilities on keys. A 'Grant " +
             "Traits' effect there gives traits to the player - they work on everything the player does and show as the player's traits.\n" +
             "Rule of thumb: 'this sword deals +20% fire damage' → Weapon Trait.  'Holding this sword makes me faster / gives me Berserker' → While Wielded.");

        Sub("Weapon Traits (of the weapon)");
        P("weaponTraits", "traitEffectMultiplier", "applyTraitsToEnemy", "applyTraitsToWielder");

        Sub("While Wielded (on the player)");
        P("passiveEffects");

        // The same trait in both places is usually a mistake: it counts twice.
        if (weapon.WeaponTraits != null && weapon.PassiveEffects != null)
        {
            var both = new List<string>();
            foreach (EquipmentEffect e in weapon.PassiveEffects)
                if (e is GrantTraitsEffect g && g.traits != null)
                    foreach (Trait t in g.traits)
                        if (t != null && weapon.WeaponTraits.Contains(t) && !both.Contains(t.Name))
                            both.Add(t.Name);
            if (both.Count > 0)
                EditorGUILayout.HelpBox($"{string.Join(", ", both)} is both a Weapon Trait and granted While Wielded: its effects on the weapon count " +
                                        "twice (once from the weapon, once from the player). Keep it in one place.", MessageType.Warning);
        }
    }

    // ------------------------------------------------------------------ helpers
    private bool Section(string title, string help, bool defaultOpen)
    {
        EditorGUILayout.Space(3);
        bool o = SessionState.GetBool(Key + title, defaultOpen);
        o = EditorGUILayout.BeginFoldoutHeaderGroup(o, title);
        EditorGUILayout.EndFoldoutHeaderGroup();
        SessionState.SetBool(Key + title, o);
        if (o && !string.IsNullOrEmpty(help))
            Help(help);
        open = o;
        return o;
    }

    private static bool Group(SerializedProperty attack, string id, string title, string help)
    {
        string key = Key + "group." + attack.propertyPath + "." + id; // remembered per attack / chain hit
        bool o = SessionState.GetBool(key, id == "timing" || id == "hit");
        bool now = EditorGUILayout.Foldout(o, new GUIContent(title, help), true);
        if (now != o) SessionState.SetBool(key, now);
        if (now && !string.IsNullOrEmpty(help))
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(help, EditorStyles.wordWrappedMiniLabel);
            EditorGUI.indentLevel--;
        }
        return now;
    }

    private static void Help(string text) => EditorGUILayout.LabelField(text, EditorStyles.wordWrappedMiniLabel);

    private void Sub(string title)
    {
        if (!open) return;
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    private void Mark(params string[] names)
    {
        foreach (string n in names) drawn.Add(n);
    }

    private static void Mark(HashSet<string> set, params string[] names)
    {
        foreach (string n in names) set.Add(n);
    }

    private void P(params string[] names)
    {
        foreach (string n in names)
        {
            drawn.Add(n);
            if (!open) continue;
            SerializedProperty p = serializedObject.FindProperty(n);
            if (p != null)
                EditorGUILayout.PropertyField(p, true);
        }
    }

    private static void R(SerializedProperty parent, HashSet<string> used, bool draw, params string[] names)
    {
        foreach (string n in names)
        {
            used.Add(n);
            if (!draw) continue;
            SerializedProperty p = parent.FindPropertyRelative(n);
            if (p != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(p, true);
                EditorGUI.indentLevel--;
            }
        }
    }

    private void DrawLeftovers()
    {
        SerializedProperty it = serializedObject.GetIterator();
        bool header = false;
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            if (drawn.Contains(it.name))
                continue;
            if (!header)
            {
                open = true;
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("Other", EditorStyles.boldLabel);
                header = true;
            }
            EditorGUILayout.PropertyField(it, true);
        }
    }

    private static void CreateComboTree(WeaponSO weapon)
    {
        string path = AssetDatabase.GetAssetPath(weapon);
        string dir = string.IsNullOrEmpty(path) ? "Assets" : System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        var tree = CreateInstance<ComboTree>();
        tree.treeName = weapon.Name + " Combos";
        tree.associatedWeapon = weapon;
        string treePath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{weapon.name}_ComboTree.asset");
        AssetDatabase.CreateAsset(tree, treePath);
        var so = new SerializedObject(weapon);
        so.FindProperty("comboTree").objectReferenceValue = tree;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(tree);
    }
}
#endif
