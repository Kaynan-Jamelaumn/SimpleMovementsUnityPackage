#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AbilityEffectSO))]
public class AbilityEffectSOEditor : Editor
{
    private SerializedProperty nameProp;
    private SerializedProperty singleTargetSelfTarget;
    private SerializedProperty numberOfTargets;
    private SerializedProperty shouldLaunch;
    private SerializedProperty speed;
    private SerializedProperty lifeSpan;
    private SerializedProperty isGroundFixedPosition;
    private SerializedProperty duration;
    private SerializedProperty coolDown;
    private SerializedProperty castDuration;
    private SerializedProperty finalLaunchTime;
    private SerializedProperty isFixedPosition;
    private SerializedProperty isPartialPermanentTargetWhileCasting;
    private SerializedProperty isPermanentTarget;
    private SerializedProperty shouldMarkAtCast;
    private SerializedProperty _stateAvailability;
    private SerializedProperty effects;
    private SerializedProperty fakeInstancerApplyEffects;
    private SerializedProperty particle;
    private SerializedProperty particleShouldChangeSize;
    private SerializedProperty subParticleShouldChangeSize;
    private SerializedProperty casterReceivePenalties;
    private SerializedProperty casterReceivesBeneffitsBuffsEvenFromFarAway;
    private SerializedProperty multiAreaEffect;
    private SerializedProperty canBeHitMoreThanOnce;
    private SerializedProperty hasMaxHitPerCollider;
    private SerializedProperty doesAbilityNeedsConfirmationClickToLaunch;
    private SerializedProperty isAbilityTargetSpawnDecidedUponMouseClick;

    private void OnEnable()
    {
        // Basic properties
        nameProp = serializedObject.FindProperty("name");
        singleTargetSelfTarget = serializedObject.FindProperty("singleTargetSelfTarget");
        numberOfTargets = serializedObject.FindProperty("numberOfTargets");

        // Launchable Ability
        shouldLaunch = serializedObject.FindProperty("shouldLaunch");
        speed = serializedObject.FindProperty("speed");
        lifeSpan = serializedObject.FindProperty("lifeSpan");
        isGroundFixedPosition = serializedObject.FindProperty("isGroundFixedPosition");

        // Ability Times
        duration = serializedObject.FindProperty("duration");
        coolDown = serializedObject.FindProperty("coolDown");
        castDuration = serializedObject.FindProperty("castDuration");
        finalLaunchTime = serializedObject.FindProperty("finalLaunchTime");

        // Cast Positions
        isFixedPosition = serializedObject.FindProperty("isFixedPosition");
        isPartialPermanentTargetWhileCasting = serializedObject.FindProperty("isPartialPermanentTargetWhileCasting");
        isPermanentTarget = serializedObject.FindProperty("isPermanentTarget");
        shouldMarkAtCast = serializedObject.FindProperty("shouldMarkAtCast");

        // Effects
        effects = serializedObject.FindProperty("effects");
        fakeInstancerApplyEffects = serializedObject.FindProperty("fakeInstancerApplyEffects");
        particle = serializedObject.FindProperty("particle");
        particleShouldChangeSize = serializedObject.FindProperty("particleShouldChangeSize");
        subParticleShouldChangeSize = serializedObject.FindProperty("subParticleShouldChangeSize");

        // Behavior Flags
        casterReceivePenalties = serializedObject.FindProperty("casterReceivePenalties");
        casterReceivesBeneffitsBuffsEvenFromFarAway = serializedObject.FindProperty("casterReceivesBeneffitsBuffsEvenFromFarAway");
        multiAreaEffect = serializedObject.FindProperty("multiAreaEffect");
        canBeHitMoreThanOnce = serializedObject.FindProperty("canBeHitMoreThanOnce");
        hasMaxHitPerCollider = serializedObject.FindProperty("hasMaxHitPerCollider");
        doesAbilityNeedsConfirmationClickToLaunch = serializedObject.FindProperty("doesAbilityNeedsConfirmationClickToLaunch");
        isAbilityTargetSpawnDecidedUponMouseClick = serializedObject.FindProperty("isAbilityTargetSpawnDecidedUponMouseClick");

        // State Availability
        _stateAvailability = serializedObject.FindProperty("_stateAvailability");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // New system.
        EditorGUILayout.HelpBox("Legacy ability. It still works (converted automatically at runtime), but converting it to an " +
            "Ability Definition unlocks hit shapes, projectiles patterns, ground surges, walls/cages, summons, dashes, mob AI hints and absorption variants.", MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Convert → Ability Definition (Player)"))
            Selection.activeObject = AbilityToolsMenuBridge.Convert((AbilityEffectSO)target, false);
        if (GUILayout.Button("Convert → Ability Definition (Mob)"))
            Selection.activeObject = AbilityToolsMenuBridge.Convert((AbilityEffectSO)target, true);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        // Basic Properties
        if (nameProp != null)
            EditorGUILayout.PropertyField(nameProp);
        EditorGUILayout.PropertyField(singleTargetSelfTarget);
        EditorGUILayout.PropertyField(numberOfTargets);

        // Launchable Ability
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Launchable Ability", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(shouldLaunch);
        if (shouldLaunch.boolValue)
        {
            EditorGUILayout.PropertyField(speed);
            EditorGUILayout.PropertyField(lifeSpan);
            EditorGUILayout.PropertyField(isGroundFixedPosition);
        }

        // Ability Times
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ability Times", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(duration);
        EditorGUILayout.PropertyField(coolDown);
        EditorGUILayout.PropertyField(castDuration);
        EditorGUILayout.PropertyField(finalLaunchTime);

        // Cast Positions Section - Radio Group
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Cast Positions", EditorStyles.boldLabel);

        int selected = -1;
        if (isFixedPosition.boolValue) selected = 0;
        else if (isPartialPermanentTargetWhileCasting.boolValue) selected = 1;
        else if (shouldMarkAtCast.boolValue) selected = 2;

        EditorGUI.BeginChangeCheck();
        selected = GUILayout.SelectionGrid(selected, new[] {
            "Fixed Position",
            "Partial Permanent While Casting",
            "Mark At Cast"
        }, 1);

        if (EditorGUI.EndChangeCheck())
        {
            isFixedPosition.boolValue = (selected == 0);
            isPartialPermanentTargetWhileCasting.boolValue = (selected == 1);
            shouldMarkAtCast.boolValue = (selected == 2);
        }

        EditorGUILayout.PropertyField(isPermanentTarget);

        // Effects Section
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Effects", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(effects, true);
        EditorGUILayout.PropertyField(fakeInstancerApplyEffects);
        EditorGUILayout.PropertyField(particle);
        EditorGUILayout.PropertyField(particleShouldChangeSize);
        EditorGUILayout.PropertyField(subParticleShouldChangeSize);

        // Behavior Flags
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Behavior Flags", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(casterReceivePenalties);
        EditorGUILayout.PropertyField(casterReceivesBeneffitsBuffsEvenFromFarAway);
        EditorGUILayout.PropertyField(multiAreaEffect);
        EditorGUILayout.PropertyField(canBeHitMoreThanOnce);
        EditorGUILayout.PropertyField(hasMaxHitPerCollider);
        EditorGUILayout.PropertyField(doesAbilityNeedsConfirmationClickToLaunch);
        EditorGUILayout.PropertyField(isAbilityTargetSpawnDecidedUponMouseClick);

        // State Availability Section
        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(_stateAvailability, true);
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            ((AbilityEffectSO)target).UpdateStateAvailabilityDict();
        }

        if (GUILayout.Button("Sync States"))
        {
            ((AbilityEffectSO)target).PopulateStateAvailabilityList();
            serializedObject.Update();
        }

        serializedObject.ApplyModifiedProperties();
    }
}

/// <summary>Saves a converted copy of a legacy ability next to it (usable from runtime-assembly editors).</summary>
public static class AbilityToolsMenuBridge
{
    public static AbilityDefinition Convert(AbilityEffectSO legacy, bool forMob)
    {
        string src = AssetDatabase.GetAssetPath(legacy);
        string folder = string.IsNullOrEmpty(src) ? "Assets" : System.IO.Path.GetDirectoryName(src).Replace('\\', '/');
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{legacy.name} ({(forMob ? "Mob" : "Player")}).asset");
        AbilityDefinition def = LegacyAbilityConverter.CreateDefinition(legacy, forMob);
        AssetDatabase.CreateAsset(def, path);
        def.SetId(AssetDatabase.AssetPathToGUID(path));
        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(def);
        return def;
    }
}
#endif
