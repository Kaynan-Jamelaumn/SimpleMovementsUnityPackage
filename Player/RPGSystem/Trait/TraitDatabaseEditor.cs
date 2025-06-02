//#if UNITY_EDITOR
//using UnityEngine;
//using UnityEditor;
//using System.Collections.Generic;
//using System.Linq;

//[CustomEditor(typeof(TraitDatabase))]
//public class TraitDatabaseEditor : Editor
//{
//    private TraitDatabase database;
//    private Vector2 scrollPosition;
//    private string searchFilter = "";
//    private TraitType filterType = TraitType.Combat;
//    private bool showPositiveTraits = true;
//    private bool showNegativeTraits = true;

//    private void OnEnable()
//    {
//        database = (TraitDatabase)target;
//    }

//    public override void OnInspectorGUI()
//    {
//        serializedObject.Update();

//        EditorGUILayout.Space();
//        EditorGUILayout.LabelField("Trait Database Manager", EditorStyles.boldLabel);
//        EditorGUILayout.Space();

//        // Search and filter section
//        DrawSearchAndFilters();

//        EditorGUILayout.Space();

//        // Quick actions
//        DrawQuickActions();

//        EditorGUILayout.Space();

//        // Trait list
//        DrawTraitList();

//        EditorGUILayout.Space();

//        // Default inspector
//        EditorGUILayout.LabelField("Database Contents", EditorStyles.boldLabel);
//        DrawDefaultInspector();

//        serializedObject.ApplyModifiedProperties();
//    }

//    private void DrawSearchAndFilters()
//    {
//        EditorGUILayout.LabelField("Search & Filter", EditorStyles.boldLabel);

//        EditorGUILayout.BeginHorizontal();
//        searchFilter = EditorGUILayout.TextField("Search:", searchFilter);
//        if (GUILayout.Button("Clear", GUILayout.Width(50)))
//        {
//            searchFilter = "";
//        }
//        EditorGUILayout.EndHorizontal();

//        EditorGUILayout.BeginHorizontal();
//        filterType = (TraitType)EditorGUILayout.EnumPopup("Type Filter:", filterType);
//        showPositiveTraits = EditorGUILayout.Toggle("Positive", showPositiveTraits);
//        showNegativeTraits = EditorGUILayout.Toggle("Negative", showNegativeTraits);
//        EditorGUILayout.EndHorizontal();
//    }

//    private void DrawQuickActions()
//    {
//        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

//        EditorGUILayout.BeginHorizontal();

//        if (GUILayout.Button("Create New Trait"))
//        {
//            CreateNewTrait();
//        }

//        if (GUILayout.Button("Organize by Type"))
//        {
//            database.SendMessage("OrganizeTraitsByType");
//            EditorUtility.SetDirty(database);
//        }

//        if (GUILayout.Button("Remove Null Traits"))
//        {
//            database.SendMessage("RemoveNullTraits");
//            EditorUtility.SetDirty(database);
//        }

//        EditorGUILayout.EndHorizontal();

//        EditorGUILayout.BeginHorizontal();

//        if (GUILayout.Button("Create Example Traits"))
//        {
//            CreateExampleTraits();
//        }

//        if (GUILayout.Button("Validate All Traits"))
//        {
//            ValidateAllTraits();
//        }

//        EditorGUILayout.EndHorizontal();
//    }

//    private void DrawTraitList()
//    {
//        EditorGUILayout.LabelField("Available Traits", EditorStyles.boldLabel);

//        var filteredTraits = GetFilteredTraits();

//        if (filteredTraits.Count == 0)
//        {
//            EditorGUILayout.HelpBox("No traits match the current filter.", MessageType.Info);
//            return;
//        }

//        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

//        foreach (var trait in filteredTraits)
//        {
//            if (trait == null) continue;

//            DrawTraitItem(trait);
//        }

//        EditorGUILayout.EndScrollView();
//    }

//    private void DrawTraitItem(Trait trait)
//    {
//        EditorGUILayout.BeginVertical(GUI.skin.box);

//        EditorGUILayout.BeginHorizontal();

//        // Trait icon and name
//        if (trait.icon != null)
//        {
//            GUILayout.Label(trait.icon.texture, GUILayout.Width(32), GUILayout.Height(32));
//        }

//        EditorGUILayout.BeginVertical();
//        EditorGUILayout.LabelField(trait.Name, EditorStyles.boldLabel);
//        EditorGUILayout.LabelField($"Type: {trait.type} | Cost: {trait.cost}", EditorStyles.miniLabel);
//        EditorGUILayout.EndVertical();

//        // Quick edit button
//        if (GUILayout.Button("Edit", GUILayout.Width(50)))
//        {
//            Selection.activeObject = trait;
//        }

//        EditorGUILayout.EndHorizontal();

//        // Description
//        if (!string.IsNullOrEmpty(trait.description))
//        {
//            EditorGUILayout.LabelField(trait.description, EditorStyles.wordWrappedMiniLabel);
//        }

//        EditorGUILayout.EndVertical();
//    }

//    private List<Trait> GetFilteredTraits()
//    {
//        var allTraits = database.GetAllTraits();
//        var filtered = allTraits.Where(t => t != null);

//        // Apply search filter
//        if (!string.IsNullOrEmpty(searchFilter))
//        {
//            filtered = filtered.Where(t =>
//                t.Name.ToLower().Contains(searchFilter.ToLower()) ||
//                t.description.ToLower().Contains(searchFilter.ToLower())
//            );
//        }

//        // Apply type filter
//        filtered = filtered.Where(t => t.type == filterType);

//        // Apply positive/negative filter
//        if (!showPositiveTraits)
//            filtered = filtered.Where(t => !t.IsPositive);
//        if (!showNegativeTraits)
//            filtered = filtered.Where(t => !t.IsNegative);

//        return filtered.ToList();
//    }

//    private void CreateNewTrait()
//    {
//        string path = EditorUtility.SaveFilePanelInProject(
//            "Create New Trait",
//            "NewTrait",
//            "asset",
//            "Choose location for new trait"
//        );

//        if (!string.IsNullOrEmpty(path))
//        {
//            var newTrait = CreateInstance<Trait>();
//            newTrait.traitName = "New Trait";
//            newTrait.description = "Enter trait description here...";
//            newTrait.cost = 1;
//            newTrait.type = TraitType.Combat;

//            AssetDatabase.CreateAsset(newTrait, path);
//            AssetDatabase.SaveAssets();

//            Selection.activeObject = newTrait;
//            EditorGUIUtility.PingObject(newTrait);
//        }
//    }

//    private void CreateExampleTraits()
//    {
//        if (EditorUtility.DisplayDialog("Create Example Traits",
//            "This will create example traits in the current project. Continue?",
//            "Yes", "Cancel"))
//        {
//            CreateExampleCombatTrait();
//            CreateExampleNegativeTrait();
//            AssetDatabase.SaveAssets();
//            AssetDatabase.Refresh();
//        }
//    }

//    private void CreateExampleCombatTrait()
//    {
//        var trait = CreateInstance<Trait>();
//        trait.traitName = "Example Combat Training";
//        trait.description = "Basic combat training. Increases health and damage.";
//        trait.cost = 3;
//        trait.type = TraitType.Combat;
//        trait.effects = new List<TraitEffect>
//        {
//            new TraitEffect
//            {
//                effectType = TraitEffectType.StatMultiplier,
//                targetStat = "health",
//                value = 1.2f,
//                effectDescription = "+20% health"
//            },
//            new TraitEffect
//            {
//                effectType = TraitEffectType.StatAddition,
//                targetStat = "damage",
//                value = 10f,
//                effectDescription = "+10 damage"
//            }
//        };

//        AssetDatabase.CreateAsset(trait, "Assets/ExampleCombatTrait.asset");
//    }

//    private void CreateExampleNegativeTrait()
//    {
//        var trait = CreateInstance<Trait>();
//        trait.traitName = "Example Glass Jaw";
//        trait.description = "Fragile constitution. Reduced health but gives trait points.";
//        trait.cost = -2;
//        trait.type = TraitType.Physical;
//        trait.effects = new List<TraitEffect>
//        {
//            new TraitEffect
//            {
//                effectType = TraitEffectType.StatMultiplier,
//                targetStat = "health",
//                value = 0.8f,
//                effectDescription = "-20% health"
//            }
//        };

//        AssetDatabase.CreateAsset(trait, "Assets/ExampleGlassJaw.asset");
//    }

//    private void ValidateAllTraits()
//    {
//        var allTraits = database.GetAllTraits();
//        int issues = 0;

//        foreach (var trait in allTraits)
//        {
//            if (trait == null)
//            {
//                Debug.LogWarning("Found null trait in database");
//                issues++;
//                continue;
//            }

//            if (string.IsNullOrEmpty(trait.Name))
//            {
//                Debug.LogWarning($"Trait has no name: {trait.name}");
//                issues++;
//            }

//            if (string.IsNullOrEmpty(trait.description))
//            {
//                Debug.LogWarning($"Trait has no description: {trait.Name}");
//                issues++;
//            }

//            if (trait.effects == null || trait.effects.Count == 0)
//            {
//                Debug.LogWarning($"Trait has no effects: {trait.Name}");
//                issues++;
//            }
//        }

//        if (issues == 0)
//        {
//            EditorUtility.DisplayDialog("Validation Complete", "All traits are valid!", "OK");
//        }
//        else
//        {
//            EditorUtility.DisplayDialog("Validation Complete",
//                $"Found {issues} issues. Check console for details.", "OK");
//        }
//    }
//}

//// Custom property drawer for TraitEffect
//[CustomPropertyDrawer(typeof(TraitEffect))]
//public class TraitEffectDrawer : PropertyDrawer
//{
//    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
//    {
//        EditorGUI.BeginProperty(position, label, property);

//        var effectType = property.FindPropertyRelative("effectType");
//        var value = property.FindPropertyRelative("value");
//        var targetStat = property.FindPropertyRelative("targetStat");
//        var description = property.FindPropertyRelative("effectDescription");

//        float lineHeight = EditorGUIUtility.singleLineHeight;
//        float spacing = EditorGUIUtility.standardVerticalSpacing;

//        Rect rect = new Rect(position.x, position.y, position.width, lineHeight);

//        // Effect type
//        EditorGUI.PropertyField(rect, effectType);
//        rect.y += lineHeight + spacing;

//        // Target stat and value on same line
//        float halfWidth = (position.width - spacing) / 2f;
//        Rect leftRect = new Rect(rect.x, rect.y, halfWidth, lineHeight);
//        Rect rightRect = new Rect(rect.x + halfWidth + spacing, rect.y, halfWidth, lineHeight);

//        EditorGUI.PropertyField(leftRect, targetStat);
//        EditorGUI.PropertyField(rightRect, value);
//        rect.y += lineHeight + spacing;

//        // Description
//        EditorGUI.PropertyField(rect, description);

//        EditorGUI.EndProperty();
//    }

//    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
//    {
//        return EditorGUIUtility.singleLineHeight * 3 + EditorGUIUtility.standardVerticalSpacing * 2;
//    }
//}

//// Menu item to create trait database
//public class TraitSystemMenuItems
//{
//    [MenuItem("Assets/Create/Player System/Trait Database")]
//    public static void CreateTraitDatabase()
//    {
//        string path = EditorUtility.SaveFilePanelInProject(
//            "Create Trait Database",
//            "TraitDatabase",
//            "asset",
//            "Choose location for trait database"
//        );

//        if (!string.IsNullOrEmpty(path))
//        {
//            var database = ScriptableObject.CreateInstance<TraitDatabase>();
//            AssetDatabase.CreateAsset(database, path);
//            AssetDatabase.SaveAssets();

//            Selection.activeObject = database;
//            EditorGUIUtility.PingObject(database);
//        }
//    }

//    [MenuItem("GameObject/Player System/Player Status Controller", false, 10)]
//    public static void CreatePlayerStatusController()
//    {
//        GameObject go = new GameObject("Player Status Controller");
//        go.AddComponent<PlayerStatusController>();
//        go.AddComponent<ExperienceManager>();
//        go.AddComponent<TraitManager>();

//        Selection.activeGameObject = go;
//        Undo.RegisterCreatedObjectUndo(go, "Create Player Status Controller");
//    }
//}
//#endif