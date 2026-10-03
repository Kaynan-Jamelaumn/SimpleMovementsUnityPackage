#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Race / class inspector: what the player will see, setup checks, a level preview of the growth, ready-made presets,
/// and in Play Mode the characters using it (re-apply after editing).
/// </summary>
[CustomEditor(typeof(CharacterArchetype))]
public class CharacterArchetypeEditor : Editor
{
    private static bool showHelp;
    private static bool showPreview = true;
    private static int previewLevel = 10;
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly AbilityEditorUI.Throttle checks = new AbilityEditorUI.Throttle();

    public override void OnInspectorGUI()
    {
        var a = (CharacterArchetype)target;
        if (checks.Due)
        {
            errors.Clear();
            warnings.Clear();
            a.Validate(errors, warnings);
        }

        showHelp = AbilityEditorUI.Help(showHelp, "Races, classes and archetypes",
            "One asset type for races, classes and backgrounds. A character combines several (a race + a class + a background) " +
            "through its Character Identity, and everything adds to the same systems as equipment and traits:\n\n" +
            "• BASE COMBAT STATS / RESISTANCES → Combat Stats (Strength, Defense, Critical Chance, Height, Threat, Poison resistance...).\n" +
            "• SCALING RULES → attribute points give other stats (+0.3 Critical Chance per Agility).\n" +
            "• CHARACTER STATS → max health, stamina, mana, move speed, damage taken (players).\n" +
            "• GROWTH PER LEVEL → the same, multiplied by (level - 1).\n" +
            "• PASSIVES & ABILITIES → the equipment effect catalogue: Ability On A Key (racial ability), On Hit, When Hit, Pulse, " +
            "Conditional (at night, low health), Grant Traits, Enhance Trait (a trait 20% stronger for this race).\n" +
            "• TRAITS → innate traits, extra traits offered at creation, forbidden traits, cheaper / dearer traits, bonus points.\n\n" +
            "Classes: keep using Player Class for resources and trait lists, and link an archetype in its 'Archetype' field for the rest. " +
            "Races: use an archetype of kind Race.");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(a.Name, EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"{a.kind}{(a.setsHeight ? $" · {a.heightRange.x:0.00}-{a.heightRange.y:0.00} m" : "")}", EditorStyles.miniLabel, GUILayout.Width(200));
        EditorGUILayout.EndHorizontal();
        showPreview = EditorGUILayout.Foldout(showPreview, new GUIContent("What the player sees", "The description on the creation screen."), true);
        if (showPreview)
            EditorGUILayout.LabelField(a.GetFormattedDescription(), EditorStyles.wordWrappedLabel);
        if (a.combatStatsPerLevel.Count > 0 || a.characterStatsPerLevel.Count > 0)
        {
            previewLevel = EditorGUILayout.IntSlider(new GUIContent("Growth at level", "Preview of the growth per level."), previewLevel, 1, 100);
            var lines = new List<string>();
            foreach (CombatStatModifier m in a.combatStatsPerLevel) if (m != null) lines.Add(m.Describe(previewLevel - 1));
            foreach (TraitModifier m in a.characterStatsPerLevel) if (m != null) lines.Add(m.Describe(previewLevel - 1));
            EditorGUILayout.LabelField(lines.Count > 0 ? string.Join(", ", lines) : "-", EditorStyles.wordWrappedMiniLabel);
        }
        EditorGUILayout.EndVertical();

        // What it changes, coloured like the game shows it (green helps, red hurts).
        DrawChanges(StatText.Of(a));

        // Which Player Classes use a class archetype (it does nothing until one links it).
        if (a.kind == ArchetypeKind.Class)
        {
            var users = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:PlayerClass"))
            {
                var pc = AssetDatabase.LoadAssetAtPath<PlayerClass>(AssetDatabase.GUIDToAssetPath(guid));
                if (pc != null && pc.archetype == a) users.Add(pc.GetClassName());
            }
            if (users.Count == 0)
                EditorGUILayout.HelpBox("No Player Class links this Class archetype, so it does nothing yet. Open a Player Class and set its Archetype field to this asset.", MessageType.Warning);
            else
                EditorGUILayout.HelpBox("Used by: " + string.Join(", ", users), MessageType.None);
        }
        else if (a.kind == ArchetypeKind.Race)
            EditorGUILayout.HelpBox("A race appears in the character creation race list" + (a.availableAtCreation ? "." : " - but Available At Creation is off, so it is hidden."), a.availableAtCreation ? MessageType.None : MessageType.Info);

        if (GUILayout.Button(new GUIContent("Apply Preset ▾", "Replace the settings with a ready-made race or class (Undo restores).")))
            PresetMenu(a);

        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);

        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("In Play Mode", EditorStyles.boldLabel);
            foreach (CharacterIdentity id in UnityEngine.Object.FindObjectsByType<CharacterIdentity>(FindObjectsInactive.Exclude))
            {
                if (!id.Archetypes.Contains(a))
                    continue;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(id.name, string.Join(", ", id.Describe()), EditorStyles.miniLabel);
                if (GUILayout.Button(new GUIContent("Re-apply", "Apply this asset's current values to the character."), GUILayout.Width(70)))
                    id.Reapply();
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    private static GUIStyle rich;

    /// <summary>Lines of changes, coloured: green helps, red hurts, grey neutral.</summary>
    public static void DrawChanges(List<StatText.Line> lines)
    {
        if (lines == null || lines.Count == 0)
            return;
        rich ??= new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true };
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Changes (as the player sees them)", EditorStyles.boldLabel);
        foreach (StatText.Line l in lines)
            EditorGUILayout.LabelField("• " + l.Rich, rich);
        EditorGUILayout.EndVertical();
    }

    private static void PresetMenu(CharacterArchetype a)
    {
        var menu = new GenericMenu();
        foreach (KeyValuePair<string, Action<CharacterArchetype>> p in ArchetypePresets.All)
        {
            Action<CharacterArchetype> apply = p.Value;
            string label = p.Key;
            menu.AddItem(new GUIContent(label), false, () =>
            {
                Undo.RecordObject(a, "Apply Archetype Preset");
                apply(a);
                EditorUtility.SetDirty(a);
            });
        }
        menu.ShowAsContext();
    }

    [MenuItem("Assets/Create/SimpleMovements/Character/Archetype Presets/Races (Human, Elf, Dwarf, Orc)")]
    private static void CreateRaces() => CreateMany("Races/");

    [MenuItem("Assets/Create/SimpleMovements/Character/Archetype Presets/Class Archetypes (Warrior, Rogue, Mage)")]
    private static void CreateClasses() => CreateMany("Classes/");

    private static void CreateMany(string prefix)
    {
        string folder = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(folder)) folder = "Assets";
        else if (!AssetDatabase.IsValidFolder(folder)) folder = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        UnityEngine.Object last = null;
        foreach (KeyValuePair<string, Action<CharacterArchetype>> p in ArchetypePresets.All)
        {
            if (!p.Key.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            var a = CreateInstance<CharacterArchetype>();
            p.Value(a);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{a.displayName}.asset");
            AssetDatabase.CreateAsset(a, path);
            last = a;
        }
        AssetDatabase.SaveAssets();
        if (last != null)
        {
            Selection.activeObject = last;
            EditorGUIUtility.PingObject(last);
        }
    }
}

/// <summary>Example races and class archetypes (starting points; tune the numbers for your game).</summary>
public static class ArchetypePresets
{
    public static readonly List<KeyValuePair<string, Action<CharacterArchetype>>> All = new List<KeyValuePair<string, Action<CharacterArchetype>>>
    {
        new KeyValuePair<string, Action<CharacterArchetype>>("Races/Human (balanced, extra trait points)", Human),
        new KeyValuePair<string, Action<CharacterArchetype>>("Races/Elf (agile, magical, frail)", Elf),
        new KeyValuePair<string, Action<CharacterArchetype>>("Races/Dwarf (sturdy, short, poison resistant)", Dwarf),
        new KeyValuePair<string, Action<CharacterArchetype>>("Races/Orc (strong, tall, draws attention)", Orc),
        new KeyValuePair<string, Action<CharacterArchetype>>("Classes/Warrior (tank: threat, defense)", Warrior),
        new KeyValuePair<string, Action<CharacterArchetype>>("Classes/Rogue (crits, penetration, low threat)", Rogue),
        new KeyValuePair<string, Action<CharacterArchetype>>("Classes/Mage (magic penetration, cheaper skills)", Mage),
    };

    private static CombatStatModifier S(CombatStatType s, float v) => new CombatStatModifier(s, v);

    private static void Reset(CharacterArchetype a, ArchetypeKind kind, string name, string description)
    {
        a.kind = kind;
        a.displayName = name;
        a.description = description;
        a.setsHeight = kind == ArchetypeKind.Race;
        a.combatStats = new List<CombatStatModifier>();
        a.resistances = new List<ElementalResistance>();
        a.scalingRules = new List<StatScalingRule>();
        a.characterStats = new List<TraitModifier>();
        a.combatStatsPerLevel = new List<CombatStatModifier>();
        a.characterStatsPerLevel = new List<TraitModifier>();
        a.effects = new List<EquipmentEffect>();
        a.traitAffinities = new List<TraitAffinity>();
        a.bonusTraitPoints = 0;
    }

    private static void Human(CharacterArchetype a)
    {
        Reset(a, ArchetypeKind.Race, "Human", "Adaptable and ambitious. No great strengths, no weaknesses.");
        a.heightRange = new Vector2(1.6f, 1.95f); a.defaultHeight = 1.78f;
        a.combatStats.AddRange(new[] { S(CombatStatType.Strength, 1), S(CombatStatType.Agility, 1), S(CombatStatType.Intelligence, 1), S(CombatStatType.Endurance, 1) });
        a.characterStatsPerLevel.Add(TraitModifier.Flat(TraitStat.MaxHealth, 4f));
        a.bonusTraitPoints = 2;
    }

    private static void Elf(CharacterArchetype a)
    {
        Reset(a, ArchetypeKind.Race, "Elf", "Graceful and long-lived, attuned to magic; less robust than other races.");
        a.heightRange = new Vector2(1.7f, 2.0f); a.defaultHeight = 1.85f;
        a.combatStats.AddRange(new[] { S(CombatStatType.Agility, 3), S(CombatStatType.Intelligence, 2), S(CombatStatType.Endurance, -2),
            S(CombatStatType.MagicResistance, 8), S(CombatStatType.DrawSpeed, 10) });
        a.scalingRules.Add(new StatScalingRule(CombatStatType.Agility, CombatStatType.CriticalChance, 0.1f));
        a.characterStats.Add(TraitModifier.Percent(TraitStat.MaxHealth, -5f));
        a.traitAffinities.Add(new TraitAffinity { type = TraitType.Magic, costMultiplier = 0.75f });
        a.traitAffinities.Add(new TraitAffinity { type = TraitType.Physical, costMultiplier = 1.25f });
    }

    private static void Dwarf(CharacterArchetype a)
    {
        Reset(a, ArchetypeKind.Race, "Dwarf", "Short, stubborn and nearly unbreakable. Hard to stun, hard to poison, slow on their feet.");
        a.heightRange = new Vector2(1.2f, 1.45f); a.defaultHeight = 1.35f;
        a.combatStats.AddRange(new[] { S(CombatStatType.Endurance, 3), S(CombatStatType.Strength, 1), S(CombatStatType.Agility, -2),
            S(CombatStatType.Defense, 10), S(CombatStatType.CrowdControlResistance, 15),
            CombatStatModifier.ForElement(CombatStatType.StatusResistance, 25f, ElementType.Poison) });
        a.resistances.Add(new ElementalResistance(ElementType.Poison, 15f));
        a.scalingRules.Add(new StatScalingRule(CombatStatType.Endurance, CombatStatType.Defense, 0.5f));
        a.characterStats.Add(TraitModifier.Percent(TraitStat.MoveSpeed, -5f));
        a.traitAffinities.Add(new TraitAffinity { type = TraitType.Crafting, costMultiplier = 0.75f });
    }

    private static void Orc(CharacterArchetype a)
    {
        Reset(a, ArchetypeKind.Race, "Orc", "Huge and fierce. Hits hard, takes a beating, and enemies notice them first.");
        a.heightRange = new Vector2(1.85f, 2.2f); a.defaultHeight = 2.0f;
        a.combatStats.AddRange(new[] { S(CombatStatType.Strength, 4), S(CombatStatType.Endurance, 1), S(CombatStatType.Intelligence, -2),
            S(CombatStatType.ThreatGenerated, 10), S(CombatStatType.ArmorPenetration, 5) });
        a.characterStats.Add(TraitModifier.Percent(TraitStat.MaxHealth, 10f));
        a.traitAffinities.Add(new TraitAffinity { type = TraitType.Combat, costMultiplier = 0.8f });
        a.traitAffinities.Add(new TraitAffinity { type = TraitType.Social, costMultiplier = 1.5f });
    }

    private static void Warrior(CharacterArchetype a)
    {
        Reset(a, ArchetypeKind.Class, "Warrior", "Front-line fighter: holds the enemies' attention and shrugs off hits.");
        a.combatStats.AddRange(new[] { S(CombatStatType.ThreatGenerated, 25), S(CombatStatType.PhysicalDefense, 5), S(CombatStatType.CrowdControlResistance, 10) });
        a.scalingRules.Add(new StatScalingRule(CombatStatType.Strength, CombatStatType.PhysicalPenetration, 0.2f));
        a.combatStatsPerLevel.AddRange(new[] { S(CombatStatType.Strength, 1f), S(CombatStatType.Endurance, 1f), S(CombatStatType.Defense, 0.5f) });
        a.characterStatsPerLevel.Add(TraitModifier.Flat(TraitStat.MaxHealth, 8f));
    }

    private static void Rogue(CharacterArchetype a)
    {
        Reset(a, ArchetypeKind.Class, "Rogue", "Precise and elusive: critical hits through armour, and mobs lose track of them.");
        a.combatStats.AddRange(new[] { S(CombatStatType.ThreatGenerated, -25), S(CombatStatType.ArmorPenetration, 10), S(CombatStatType.CriticalDamage, 20),
            CombatStatModifier.ForElement(CombatStatType.StatusChance, 10f, ElementType.Poison) });
        a.combatStatsPerLevel.AddRange(new[] { S(CombatStatType.Agility, 1.5f), S(CombatStatType.CriticalChance, 0.2f) });
        a.characterStatsPerLevel.Add(TraitModifier.Flat(TraitStat.MaxStamina, 3f));
    }

    private static void Mage(CharacterArchetype a)
    {
        Reset(a, ArchetypeKind.Class, "Mage", "Master of spells: pierces magic resistance and casts more for less.");
        a.combatStats.AddRange(new[] { S(CombatStatType.MagicPenetration, 15), CombatStatModifier.ForScope(CombatStatType.ManaCostReduction, 10f, StatScope.Skills),
            S(CombatStatType.DebuffDuration, 10) });
        a.scalingRules.Add(new StatScalingRule(CombatStatType.Intelligence, CombatStatType.MagicResistance, 0.3f));
        a.combatStatsPerLevel.AddRange(new[] { S(CombatStatType.Intelligence, 1.5f), S(CombatStatType.CastingSpeed, 0.3f) });
        a.characterStatsPerLevel.Add(TraitModifier.Flat(TraitStat.MaxMana, 6f));
    }
}
#endif
