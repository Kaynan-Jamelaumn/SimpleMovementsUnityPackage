
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerClass))]
public class PlayerClassEditor : Editor
{
    private PlayerClass playerClass;
    private TraitDatabase traitDatabase;
    private Vector2 scrollPosition;
    private string searchFilter = "";
    private TraitType selectedTraitType = TraitType.Combat;
    private bool showTraitPicker = false;
    private List<Trait> filteredTraits = new List<Trait>();

    private void OnEnable()
    {
        playerClass = (PlayerClass)target;
        traitDatabase = TraitDatabase.Instance;

        if (traitDatabase == null)
        {
            // Try to find it in assets
            traitDatabase = AssetDatabase.FindAssets("t:TraitDatabase")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TraitDatabase>)
                .FirstOrDefault();
        }

        RefreshFilteredTraits();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Summary, checks and how the class fits with races and archetypes.
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(playerClass.GetClassName(), EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Health {playerClass.health:0} · Stamina {playerClass.stamina:0} · Mana {playerClass.mana:0} · Speed {playerClass.speed:0.#} · " +
                                   $"{playerClass.traitPoints} trait points" + (playerClass.archetype != null ? $" · archetype: {playerClass.archetype.Name}" : ""),
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField("A Player Class sets the starting status values, the trait lists and points, and the basic combat block. " +
                                   "Link a Character Archetype (kind Class) for growth per level, stat scaling, passives and class abilities. " +
                                   "Races are separate archetypes (kind Race).", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();
        var errors = new List<string>();
        var warnings = new List<string>();
        playerClass.Validate(errors, warnings);
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
        if (playerClass.archetype == null && GUILayout.Button(new GUIContent("Create Class Archetype", "Creates a Character Archetype of kind Class next to this asset and links it.")))
        {
            var a = ScriptableObject.CreateInstance<CharacterArchetype>();
            a.kind = ArchetypeKind.Class;
            a.displayName = playerClass.GetClassName();
            a.setsHeight = false;
            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(playerClass)).Replace('\\', '/');
            AssetDatabase.CreateAsset(a, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{playerClass.GetClassName()} Archetype.asset"));
            Undo.RecordObject(playerClass, "Link Class Archetype");
            playerClass.archetype = a;
            EditorUtility.SetDirty(playerClass);
            AssetDatabase.SaveAssets();
        }

        // Draw default inspector first
        DrawDefaultInspector();

        // The class's combat block reaches Combat Stats through a Character Identity on the player.
        var combat = playerClass.GetCombatStatModifiers();
        EditorGUILayout.HelpBox(
            "Combat Stats are applied by a Character Identity on the player (with the linked Archetype, if any):\n" +
            (combat.Count > 0 ? string.Join(", ", combat.ConvertAll(m => m.Describe())) : "(none)") +
            "\nCritical Damage counts above 150, Attack / Casting Speed above 1.", MessageType.Info);

        EditorGUILayout.Space(10);

        // Trait Management Section
        EditorGUILayout.LabelField("Trait Management Tools", EditorStyles.boldLabel);

        if (traitDatabase == null)
        {
            EditorGUILayout.HelpBox("No TraitDatabase found! Please create one or place it in a Resources folder.", MessageType.Warning);
            if (GUILayout.Button("Try to Find TraitDatabase"))
            {
                OnEnable(); // Refresh search
            }
            return;
        }

        // Show current trait counts
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Available: {playerClass.availableTraits.Count}", GUILayout.Width(100));
        EditorGUILayout.LabelField($"Exclusive: {playerClass.exclusiveTraits.Count}", GUILayout.Width(100));
        EditorGUILayout.LabelField($"Starting: {playerClass.startingTraits.Count}", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Quick add buttons
        EditorGUILayout.LabelField("Quick Add by Type:", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Combat", GUILayout.Height(25)))
            AddTraitsByType(TraitType.Combat);
        if (GUILayout.Button("Survival", GUILayout.Height(25)))
            AddTraitsByType(TraitType.Survival);
        if (GUILayout.Button("Magic", GUILayout.Height(25)))
            AddTraitsByType(TraitType.Magic);
        if (GUILayout.Button("Social", GUILayout.Height(25)))
            AddTraitsByType(TraitType.Social);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Movement", GUILayout.Height(25)))
            AddTraitsByType(TraitType.Movement);
        if (GUILayout.Button("Mental", GUILayout.Height(25)))
            AddTraitsByType(TraitType.Mental);
        if (GUILayout.Button("Physical", GUILayout.Height(25)))
            AddTraitsByType(TraitType.Physical);
        if (GUILayout.Button("Crafting", GUILayout.Height(25)))
            AddTraitsByType(TraitType.Crafting);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Trait picker toggle
        showTraitPicker = EditorGUILayout.Foldout(showTraitPicker, "Advanced Trait Picker", true);

        if (showTraitPicker)
        {
            EditorGUILayout.BeginVertical("box");

            // Search and filter
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            string newSearchFilter = EditorGUILayout.TextField(searchFilter);
            if (newSearchFilter != searchFilter)
            {
                searchFilter = newSearchFilter;
                RefreshFilteredTraits();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Filter Type:", GUILayout.Width(70));
            TraitType newSelectedType = (TraitType)EditorGUILayout.EnumPopup(selectedTraitType);
            if (newSelectedType != selectedTraitType)
            {
                selectedTraitType = newSelectedType;
                RefreshFilteredTraits();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Trait list with buttons
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

            foreach (var trait in filteredTraits)
            {
                if (trait == null) continue;

                EditorGUILayout.BeginHorizontal();

                // Trait info
                EditorGUILayout.LabelField(trait.Name, GUILayout.Width(150));
                EditorGUILayout.LabelField($"Cost: {trait.cost}", GUILayout.Width(60));
                EditorGUILayout.LabelField(trait.type.ToString(), GUILayout.Width(80));

                // Status indicators
                bool inAvailable = playerClass.availableTraits.Contains(trait);
                bool inExclusive = playerClass.exclusiveTraits.Contains(trait);
                bool inStarting = playerClass.startingTraits.Contains(trait);

                if (inAvailable) GUI.color = Color.green;
                if (GUILayout.Button("Available", GUILayout.Width(70)))
                {
                    if (inAvailable)
                        playerClass.availableTraits.Remove(trait);
                    else
                        AddToAvailable(trait);
                }
                GUI.color = Color.white;

                if (inExclusive) GUI.color = Color.cyan;
                if (GUILayout.Button("Exclusive", GUILayout.Width(70)))
                {
                    if (inExclusive)
                        playerClass.exclusiveTraits.Remove(trait);
                    else
                        AddToExclusive(trait);
                }
                GUI.color = Color.white;

                if (inStarting) GUI.color = Color.yellow;
                if (GUILayout.Button("Starting", GUILayout.Width(60)))
                {
                    if (inStarting)
                        playerClass.startingTraits.Remove(trait);
                    else
                        AddToStarting(trait);
                }
                GUI.color = Color.white;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(5);

        // Utility buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear All Lists", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Clear All Trait Lists",
                "This will remove all traits from Available, Exclusive, and Starting lists. Are you sure?",
                "Yes", "Cancel"))
            {
                playerClass.ClearAllTraitLists();
            }
        }

        if (GUILayout.Button("Remove Duplicates", GUILayout.Height(30)))
        {
            RemoveDuplicates();
        }
        EditorGUILayout.EndHorizontal();

        // Apply changes
        if (GUI.changed)
        {
            EditorUtility.SetDirty(playerClass);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void RefreshFilteredTraits()
    {
        if (traitDatabase == null)
        {
            filteredTraits.Clear();
            return;
        }

        var allTraits = traitDatabase.GetAllTraits();

        filteredTraits = allTraits.Where(trait =>
        {
            if (trait == null) return false;

            // Type filter
            if (selectedTraitType != trait.type && selectedTraitType != TraitType.Combat) // Using Combat as "All" for now
            {
                // Allow showing all if Combat is selected - you could add an "All" enum value
            }

            // Search filter
            if (!string.IsNullOrEmpty(searchFilter))
            {
                return trait.Name.ToLower().Contains(searchFilter.ToLower()) ||
                       trait.description.ToLower().Contains(searchFilter.ToLower());
            }

            return true;
        }).ToList();
    }

    private void AddTraitsByType(TraitType traitType)
    {
        var traits = traitDatabase.GetTraitsByType(traitType);
        foreach (var trait in traits)
        {
            AddToAvailable(trait);
        }
    }

    private void AddToAvailable(Trait trait)
    {
        if (!playerClass.availableTraits.Contains(trait))
        {
            playerClass.availableTraits.Add(trait);
            EditorUtility.SetDirty(playerClass);
        }
    }

    private void AddToExclusive(Trait trait)
    {
        if (!playerClass.exclusiveTraits.Contains(trait))
        {
            playerClass.exclusiveTraits.Add(trait);
            // Remove from available if it's there
            playerClass.availableTraits.Remove(trait);
            EditorUtility.SetDirty(playerClass);
        }
    }

    private void AddToStarting(Trait trait)
    {
        if (!playerClass.startingTraits.Contains(trait))
        {
            playerClass.startingTraits.Add(trait);
            EditorUtility.SetDirty(playerClass);
        }
    }

    private void RemoveDuplicates()
    {
        playerClass.availableTraits = playerClass.availableTraits.Distinct().Where(t => t != null).ToList();
        playerClass.exclusiveTraits = playerClass.exclusiveTraits.Distinct().Where(t => t != null).ToList();
        playerClass.startingTraits = playerClass.startingTraits.Distinct().Where(t => t != null).ToList();
        EditorUtility.SetDirty(playerClass);
    }
}
