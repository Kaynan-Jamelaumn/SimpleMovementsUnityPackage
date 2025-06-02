//#if UNITY_EDITOR
//using UnityEngine;
//using UnityEditor;
//using System.Collections.Generic;
//using System.IO;

//// Comprehensive setup utility for the enhanced player system
//public class PlayerSystemSetupUtility : EditorWindow
//{
//    private enum SetupStep
//    {
//        Welcome,
//        DatabaseSetup,
//        TraitCreation,
//        ClassCreation,
//        GameObjectSetup,
//        UISetup,
//        Complete
//    }

//    private SetupStep currentStep = SetupStep.Welcome;
//    private Vector2 scrollPosition;

//    // Setup configuration
//    private bool createExampleTraits = true;
//    private bool createExampleClasses = true;
//    private bool createTraitDatabase = true;
//    private bool setupPlayerGameObject = true;
//    private bool createUIElements = true;

//    // References
//    private TraitDatabase traitDatabase;
//    private List<PlayerClass> playerClasses = new List<PlayerClass>();
//    private GameObject playerPrefab;

//    [MenuItem("Tools/Player System/Setup Wizard")]
//    public static void ShowWindow()
//    {
//        var window = GetWindow<PlayerSystemSetupUtility>("Player System Setup");
//        window.minSize = new Vector2(500, 400);
//        window.Show();
//    }

//    private void OnGUI()
//    {
//        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

//        // Header
//        EditorGUILayout.Space();
//        EditorGUILayout.LabelField("Enhanced Player System Setup", EditorStyles.largeLabel);
//        EditorGUILayout.LabelField($"Step {(int)currentStep + 1} of {System.Enum.GetValues(typeof(SetupStep)).Length}", EditorStyles.centeredGreyMiniLabel);
//        EditorGUILayout.Space();

//        // Progress bar
//        DrawProgressBar();

//        EditorGUILayout.Space();

//        // Step content
//        switch (currentStep)
//        {
//            case SetupStep.Welcome:
//                DrawWelcomeStep();
//                break;
//            case SetupStep.DatabaseSetup:
//                DrawDatabaseSetupStep();
//                break;
//            case SetupStep.TraitCreation:
//                DrawTraitCreationStep();
//                break;
//            case SetupStep.ClassCreation:
//                DrawClassCreationStep();
//                break;
//            case SetupStep.GameObjectSetup:
//                DrawGameObjectSetupStep();
//                break;
//            case SetupStep.UISetup:
//                DrawUISetupStep();
//                break;
//            case SetupStep.Complete:
//                DrawCompleteStep();
//                break;
//        }

//        EditorGUILayout.Space();

//        // Navigation buttons
//        DrawNavigationButtons();

//        EditorGUILayout.EndScrollView();
//    }

//    private void DrawProgressBar()
//    {
//        float progress = (float)currentStep / (System.Enum.GetValues(typeof(SetupStep)).Length - 1);
//        EditorGUILayout.Space();
//        Rect rect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
//        EditorGUI.ProgressBar(rect, progress, $"{progress * 100:F0}% Complete");
//    }

//    private void DrawWelcomeStep()
//    {
//        EditorGUILayout.LabelField("Welcome to the Enhanced Player System!", EditorStyles.boldLabel);
//        EditorGUILayout.Space();

//        EditorGUILayout.LabelField("This wizard will help you set up:", EditorStyles.wordWrappedLabel);
//        EditorGUILayout.LabelField("• ScriptableObject-based trait and class systems", EditorStyles.miniLabel);
//        EditorGUILayout.LabelField("• Reference-based selection instead of string matching", EditorStyles.miniLabel);
//        EditorGUILayout.LabelField("• Selective stat upgrades on level up", EditorStyles.miniLabel);
//        EditorGUILayout.LabelField("• Unity Editor integration and tools", EditorStyles.miniLabel);
//        EditorGUILayout.LabelField("• Example traits, classes, and UI elements", EditorStyles.miniLabel);

//        EditorGUILayout.Space();

//        EditorGUILayout.LabelField("Setup Configuration:", EditorStyles.boldLabel);
//        createTraitDatabase = EditorGUILayout.Toggle("Create Trait Database", createTraitDatabase);
//        createExampleTraits = EditorGUILayout.Toggle("Create Example Traits", createExampleTraits);
//        createExampleClasses = EditorGUILayout.Toggle("Create Example Classes", createExampleClasses);
//        setupPlayerGameObject = EditorGUILayout.Toggle("Setup Player GameObject", setupPlayerGameObject);
//        createUIElements = EditorGUILayout.Toggle("Create UI Elements", createUIElements);

//        if (createTraitDatabase || createExampleTraits || createExampleClasses)
//        {
//            EditorGUILayout.HelpBox("Resources will be created in your project. You can customize them later.", MessageType.Info);
//        }
//    }

//    private void DrawDatabaseSetupStep()
//    {
//        EditorGUILayout.LabelField("Trait Database Setup", EditorStyles.boldLabel);
//        EditorGUILayout.Space();

//        if (createTraitDatabase)
//        {
//            EditorGUILayout.LabelField("Creating trait database...", EditorStyles.wordWrappedLabel);

//            if (GUILayout.Button("Create Trait Database"))
//            {
//                CreateTraitDatabase();
//            }

//            EditorGUILayout.Space();
//        }

//        EditorGUILayout.LabelField("Assign Trait Database:", EditorStyles.boldLabel);
//        traitDatabase = EditorGUILayout.ObjectField("Trait Database", traitDatabase, typeof(TraitDatabase), false) as TraitDatabase;

//        if (traitDatabase == null)
//        {
//            EditorGUILayout.HelpBox("Please create or assign a TraitDatabase to continue.", MessageType.Warning);
//        }
//        else
//        {
//            EditorGUILayout.HelpBox($"Database found with {traitDatabase.GetAllTraits().Count} traits.", MessageType.Info);
//        }
//    }

//    private void DrawTraitCreationStep()
//    {
//        EditorGUILayout.LabelField("Trait Creation", EditorStyles.boldLabel);
//        EditorGUILayout.Space();

//        if (createExampleTraits)
//        {
//            EditorGUILayout.LabelField("This will create example traits including:", EditorStyles.wordWrappedLabel);
//            EditorGUILayout.LabelField("• Combat traits (Combat Training, Berserker, Shield Master)", EditorStyles.miniLabel);
//            EditorGUILayout.LabelField("• Survival traits (Survivor, Iron Stomach, Cold Resistance)", EditorStyles.miniLabel);
//            EditorGUILayout.LabelField("• Magic traits (Arcane Knowledge, Mana Efficiency)", EditorStyles.miniLabel);
//            EditorGUILayout.LabelField("• Negative traits (Glass Jaw, Slow Learner, Cowardly)", EditorStyles.miniLabel);
//            EditorGUILayout.LabelField("• Movement traits (Fleet Footed, Acrobatic)", EditorStyles.miniLabel);
//            EditorGUILayout.LabelField("• Mental traits (Strong Willed, Insomniac)", EditorStyles.miniLabel);

//            EditorGUILayout.Space();

//            if (GUILayout.Button("Create Example Traits"))
//            {
//                ExampleTraitsCreator.CreateExampleTraits();
//                EditorGUILayout.HelpBox("Example traits created! Check the ExampleTraits folder.", MessageType.Info);
//            }
//        }
//        else
//        {
//            EditorGUILayout.LabelField("Skipping example trait creation.", EditorStyles.wordWrappedLabel);
//            EditorGUILayout.LabelField("You can create traits manually using Create > Player System > Trait", EditorStyles.miniLabel);
//        }

//        EditorGUILayout.Space();

//        if (GUILayout.Button("Open Trait Creation Menu"))
//        {
//            EditorApplication.ExecuteMenuItem("Assets/Create/Player System/Trait");
//        }
//    }

//    private void DrawClassCreationStep()
//    {
//        EditorGUILayout.LabelField("Player Class Creation", EditorStyles.boldLabel);
//        EditorGUILayout.Space();

//        if (createExampleClasses)
//        {
//            EditorGUILayout.LabelField("This will create example classes:", EditorStyles.wordWrappedLabel);
//            EditorGUILayout.LabelField("• Warrior - High health/strength, low speed/mana", EditorStyles.miniLabel);
//            EditorGUILayout.LabelField("• Mage - High mana/intelligence, low health/strength", EditorStyles.miniLabel);
//            EditorGUILayout.LabelField("• Rogue - High agility/speed, balanced other stats", EditorStyles.miniLabel);
//            EditorGUILayout.LabelField("• Archer - Balanced stats with agility focus", EditorStyles.miniLabel);

//            EditorGUILayout.Space();

//            if (GUILayout.Button("Create Example Classes"))
//            {
//                ExampleClassesCreator.CreateExampleClasses();
//                LoadExampleClasses();
//                EditorGUILayout.HelpBox("Example classes created! Check the ExampleClasses folder.", MessageType.Info);
//            }
//        }
//        else
//        {
//            EditorGUILayout.LabelField("Skipping example class creation.", EditorStyles.wordWrappedLabel);
//        }

//        EditorGUILayout.Space();

//        EditorGUILayout.LabelField("Available Classes:", EditorStyles.boldLabel);
//        for (int i = 0; i < playerClasses.Count; i++)
//        {
//            playerClasses[i] = EditorGUILayout.ObjectField($"Class {i + 1}", playerClasses[i], typeof(PlayerClass), false) as PlayerClass;
//        }

//        if (GUILayout.Button("Add Class Slot"))
//        {
//            playerClasses.Add(null);
//        }

//        if (GUILayout.Button("Open Class Creation Menu"))
//        {
//            EditorApplication.ExecuteMenuItem("Assets/Create/Player System/Player Class");
//        }
//    }

//    private void DrawGameObjectSetupStep()
//    {
//        EditorGUILayout.LabelField("Player GameObject Setup", EditorStyles.boldLabel);
//        EditorGUILayout.Space();

//        if (setupPlayerGameObject)
//        {
//            EditorGUILayout.LabelField("This will create a player GameObject with all required components:", EditorStyles.wordWrappedLabel);
//            EditorGUILayout.LabelField("• All StatusManager components", EditorStyles.miniLabel);
//            EditorGUILayout.LabelField("• PlayerStatusController", EditorStyles.miniLabel);
//            EditorGUILayout.LabelField("• ExperienceManager", EditorStyles.miniLabel);
//            EditorGUILayout.LabelField("• TraitManager", EditorStyles.miniLabel);

//            EditorGUILayout.Space();

//            if (GUILayout.Button("Create Player GameObject"))
//            {
//                CreatePlayerGameObject();
//            }
//        }
//        else
//        {
//            EditorGUILayout.LabelField("Skipping automatic GameObject setup.", EditorStyles.wordWrappedLabel);
//        }

//        EditorGUILayout.Space();

//        EditorGUILayout.LabelField("Player Prefab:", EditorStyles.boldLabel);
//        playerPrefab = EditorGUILayout.ObjectField("Player Prefab", playerPrefab, typeof(GameObject), true) as GameObject;

//        if (playerPrefab != null)
//        {
//            var controller = playerPrefab.GetComponent<PlayerStatusController>();
//            if (controller != null)
//            {
//                EditorGUILayout.HelpBox("Player GameObject is properly configured!", MessageType.Info);
//            }
//            else
//            {
//                EditorGUILayout.HelpBox("Player GameObject is missing PlayerStatusController component.", MessageType.Warning);
//            }
//        }
//    }

//    private void DrawUISetupStep()
//    {
//        EditorGUILayout.LabelField("UI Setup", EditorStyles.boldLabel);
//        EditorGUILayout.Space();

//        if (createUIElements)
//        {
//            EditorGUILayout.LabelField("Optional UI elements to create:", EditorStyles.wordWrappedLabel);

//            if (GUILayout.Button("Create Stat Selection UI"))
//            {
//                CreateStatSelectionUI();
//            }

//            if (GUILayout.Button("Create Class Selection UI"))
//            {
//                CreateClassSelectionUI();
//            }

//            if (GUILayout.Button("Create Trait Selection UI"))
//            {
//                CreateTraitSelectionUI();
//            }
//        }
//        else
//        {
//            EditorGUILayout.LabelField("Skipping UI creation.", EditorStyles.wordWrappedLabel);
//        }

//        EditorGUILayout.Space();
//        EditorGUILayout.LabelField("Manual UI Setup:", EditorStyles.boldLabel);
//        EditorGUILayout.LabelField("You can add these components to your UI:", EditorStyles.wordWrappedLabel);
//        EditorGUILayout.LabelField("• StatSelectionUI - For stat upgrades on level up", EditorStyles.miniLabel);
//        EditorGUILayout.LabelField("• ClassSelectionUI - For character creation", EditorStyles.miniLabel);
//        EditorGUILayout.LabelField("• TraitSelectionUI - For trait management", EditorStyles.miniLabel);
//    }

//    private void DrawCompleteStep()
//    {
//        EditorGUILayout.LabelField("Setup Complete!", EditorStyles.boldLabel);
//        EditorGUILayout.Space();

//        EditorGUILayout.LabelField("Your enhanced player system is ready to use!", EditorStyles.wordWrappedLabel);
//        EditorGUILayout.Space();

//        EditorGUILayout.LabelField("Next steps:", EditorStyles.boldLabel);
//        EditorGUILayout.LabelField("1. Customize the example traits and classes", EditorStyles.miniLabel);
//        EditorGUILayout.LabelField("2. Configure UI elements for your game", EditorStyles.miniLabel);
//        EditorGUILayout.LabelField("3. Set up save/load functionality", EditorStyles.miniLabel);
//        EditorGUILayout.LabelField("4. Test the system in play mode", EditorStyles.miniLabel);

//        EditorGUILayout.Space();

//        if (GUILayout.Button("Open Documentation"))
//        {
//            // You could open a documentation URL or file here
//            Debug.Log("Documentation: Check the setup guide artifact for detailed instructions");
//        }

//        if (GUILayout.Button("Close Setup Wizard"))
//        {
//            Close();
//        }
//    }

//    private void DrawNavigationButtons()
//    {
//        EditorGUILayout.BeginHorizontal();

//        GUI.enabled = currentStep > SetupStep.Welcome;
//        if (GUILayout.Button("Previous"))
//        {
//            currentStep--;
//        }

//        GUI.enabled = CanProceedToNextStep();
//        if (currentStep < SetupStep.Complete)
//        {
//            if (GUILayout.Button("Next"))
//            {
//                currentStep++;
//            }
//        }

//        GUI.enabled = true;
//        EditorGUILayout.EndHorizontal();
//    }

//    private bool CanProceedToNextStep()
//    {
//        switch (currentStep)
//        {
//            case SetupStep.DatabaseSetup:
//                return traitDatabase != null;
//            default:
//                return true;
//        }
//    }

//    private void CreateTraitDatabase()
//    {
//        string path = "Assets/Resources/TraitDatabase.asset";

//        // Create Resources folder if it doesn't exist
//        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
//        {
//            AssetDatabase.CreateFolder("Assets", "Resources");
//        }

//        var database = ScriptableObject.CreateInstance<TraitDatabase>();
//        AssetDatabase.CreateAsset(database, path);
//        AssetDatabase.SaveAssets();

//        traitDatabase = database;
//        Selection.activeObject = database;

//        Debug.Log($"Created TraitDatabase at {path}");
//    }

//    private void LoadExampleClasses()
//    {
//        string[] guids = AssetDatabase.FindAssets("t:PlayerClass", new[] { "Assets/ExampleClasses" });
//        playerClasses.Clear();

//        foreach (string guid in guids)
//        {
//            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
//            var playerClass = AssetDatabase.LoadAssetAtPath<PlayerClass>(assetPath);
//            if (playerClass != null)
//            {
//                playerClasses.Add(playerClass);
//            }
//        }
//    }

//    private void CreatePlayerGameObject()
//    {
//        GameObject player = new GameObject("Player");

//        // Add all required components
//        player.AddComponent<PlayerStatusController>();
//        player.AddComponent<ExperienceManager>();
//        player.AddComponent<TraitManager>();

//        // Add all status managers
//        player.AddComponent<HealthManager>();
//        player.AddComponent<StaminaManager>();
//        player.AddComponent<ManaManager>();
//        player.AddComponent<HungerManager>();
//        player.AddComponent<ThirstManager>();
//        player.AddComponent<SleepManager>();
//        player.AddComponent<SanityManager>();
//        player.AddComponent<WeightManager>();
//        player.AddComponent<SpeedManager>();
//        player.AddComponent<BodyHeatManager>();
//        player.AddComponent<OxygenManager>();

//        // Set up trait manager reference
//        var traitManager = player.GetComponent<TraitManager>();
//        if (traitManager != null && traitDatabase != null)
//        {
//            var serializedObject = new SerializedObject(traitManager);
//            serializedObject.FindProperty("traitDatabase").objectReferenceValue = traitDatabase;
//            serializedObject.ApplyModifiedProperties();
//        }

//        playerPrefab = player;
//        Selection.activeGameObject = player;

//        Debug.Log("Created player GameObject with all required components");
//    }

//    private void CreateStatSelectionUI()
//    {
//        GameObject canvas = FindOrCreateCanvas();
//        GameObject statUI = new GameObject("StatSelectionUI");
//        statUI.transform.SetParent(canvas.transform);
//        statUI.AddComponent<StatSelectionUI>();

//        Debug.Log("Created StatSelectionUI GameObject");
//    }

//    private void CreateClassSelectionUI()
//    {
//        GameObject canvas = FindOrCreateCanvas();
//        GameObject classUI = new GameObject("ClassSelectionUI");
//        classUI.transform.SetParent(canvas.transform);
//        classUI.AddComponent<ClassSelectionUI>();

//        Debug.Log("Created ClassSelectionUI GameObject");
//    }

//    private void CreateTraitSelectionUI()
//    {
//        GameObject canvas = FindOrCreateCanvas();
//        GameObject traitUI = new GameObject("TraitSelectionUI");
//        traitUI.transform.SetParent(canvas.transform);
//        traitUI.AddComponent<TraitSelectionUI>();

//        Debug.Log("Created TraitSelectionUI GameObject");
//    }

//    private GameObject FindOrCreateCanvas()
//    {
//        Canvas canvas = FindObjectOfType<Canvas>();
//        if (canvas != null)
//            return canvas.gameObject;

//        // Create new canvas
//        GameObject canvasGO = new GameObject("Canvas");
//        canvas = canvasGO.AddComponent<Canvas>();
//        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
//        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
//        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

//        return canvasGO;
//    }
//}

//// Quick access menu for common operations
//public static class PlayerSystemQuickMenu
//{
//    [MenuItem("Tools/Player System/Quick Setup/Create Basic Player")]
//    public static void CreateBasicPlayer()
//    {
//        var wizard = EditorWindow.GetWindow<PlayerSystemSetupUtility>();
//        wizard.Show();
//    }

//    [MenuItem("Tools/Player System/Quick Setup/Create Trait Database")]
//    public static void QuickCreateTraitDatabase()
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

//    [MenuItem("Tools/Player System/Documentation/Open Setup Guide")]
//    public static void OpenSetupGuide()
//    {
//        Debug.Log("Setup Guide: Check the Enhanced Player System Setup Guide artifact for detailed instructions");
//    }

//    [MenuItem("Tools/Player System/Documentation/System Overview")]
//    public static void ShowSystemOverview()
//    {
//        EditorUtility.DisplayDialog("Enhanced Player System",
//            "• ScriptableObject-based traits and classes\n" +
//            "• Reference-based selection system\n" +
//            "• Selective stat upgrades on level up\n" +
//            "• Unity Editor integration\n" +
//            "• Comprehensive UI components\n\n" +
//            "Use the Setup Wizard to get started!",
//            "OK");
//    }
//}
//#endif