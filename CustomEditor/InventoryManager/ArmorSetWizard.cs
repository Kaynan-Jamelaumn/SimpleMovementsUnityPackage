#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools ▸ SimpleMovements ▸ Inventory ▸ Armor Set Wizard: creates an armor set and its pieces in one go (already linked both ways),
/// with the usual 2 / 3 / 4-piece bonus tiers ready to fill in.
/// </summary>
public class ArmorSetWizard : EditorWindow
{
    private string setName = "New Armor Set";
    private string folder = "Assets";
    private Color color = Color.white;
    private readonly Dictionary<ArmorSlotType, bool> slots = new Dictionary<ArmorSlotType, bool>
    {
        { ArmorSlotType.Helmet, true }, { ArmorSlotType.Chestplate, true }, { ArmorSlotType.Leggings, true }, { ArmorSlotType.Boots, true },
    };
    private bool useMaterial = true;
    private ItemPresets.ArmorMaterial material = ItemPresets.ArmorMaterial.Iron;
    private float defensePerPiece = 5f;
    private float magicDefensePerPiece = 2f;
    private bool tier2 = true, tier3 = false, tier4 = true;
    private bool requireAll;
    private Vector2 scroll;

    [MenuItem("Tools/SimpleMovements/Inventory/Armor Set Wizard")]
    public static void Open()
    {
        var w = GetWindow<ArmorSetWizard>("Armor Set Wizard");
        w.minSize = new Vector2(340, 420);
        if (Selection.activeObject != null)
        {
            string p = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!string.IsNullOrEmpty(p))
                w.folder = AssetDatabase.IsValidFolder(p) ? p : System.IO.Path.GetDirectoryName(p).Replace('\\', '/');
        }
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Set", EditorStyles.boldLabel);
        setName = EditorGUILayout.TextField("Name", setName);
        color = EditorGUILayout.ColorField("Colour", color);
        EditorGUILayout.BeginHorizontal();
        folder = EditorGUILayout.TextField("Folder", folder);
        if (GUILayout.Button("…", GUILayout.Width(24)))
        {
            string abs = EditorUtility.OpenFolderPanel("Folder for the set", folder, "");
            if (!string.IsNullOrEmpty(abs) && abs.StartsWith(Application.dataPath))
                folder = "Assets" + abs.Substring(Application.dataPath.Length);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Pieces", EditorStyles.boldLabel);
        foreach (ArmorSlotType s in System.Enum.GetValues(typeof(ArmorSlotType)))
        {
            slots.TryGetValue(s, out bool on);
            slots[s] = EditorGUILayout.ToggleLeft(SlotTypeHelper.GetDisplayName(s), on);
        }
        useMaterial = EditorGUILayout.Toggle(new GUIContent("Use material preset", "Defense, magic resistance, weight, durability and value from the material, scaled per slot (a chestplate protects more than boots)."), useMaterial);
        if (useMaterial)
        {
            material = (ItemPresets.ArmorMaterial)EditorGUILayout.EnumPopup(new GUIContent("Material", "Cloth: light, good against magic · Leather · Chainmail · Iron · Steel: heavy, strong · Mythril: strong and light."), material);
        }
        else
        {
            defensePerPiece = EditorGUILayout.FloatField("Defense per piece", defensePerPiece);
            magicDefensePerPiece = EditorGUILayout.FloatField("Magic resistance per piece", magicDefensePerPiece);
        }

        int count = slots.Count(kv => kv.Value);
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Bonus tiers (filled in afterwards)", EditorStyles.boldLabel);
        tier2 = EditorGUILayout.ToggleLeft("2 pieces", tier2);
        using (new EditorGUI.DisabledScope(count < 3)) tier3 = EditorGUILayout.ToggleLeft("3 pieces", tier3);
        using (new EditorGUI.DisabledScope(count < 4)) tier4 = EditorGUILayout.ToggleLeft("4 pieces", tier4);
        requireAll = EditorGUILayout.ToggleLeft(new GUIContent("Complete only when every piece is worn"), requireAll);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(4);
        bool valid = count > 0 && !string.IsNullOrWhiteSpace(setName) && AssetDatabase.IsValidFolder(folder);
        if (!valid)
            EditorGUILayout.HelpBox("Pick at least one piece, a name, and an existing folder inside Assets.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!valid))
            if (GUILayout.Button($"Create set and {count} piece(s)", GUILayout.Height(28)))
                Create(count);
    }

    private void Create(int count)
    {
        string safe = string.Concat(setName.Split(System.IO.Path.GetInvalidFileNameChars())).Trim();
        string dir = folder + "/" + safe;
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder(folder, safe);

        var set = CreateInstance<ArmorSet>();
        AssetDatabase.CreateAsset(set, AssetDatabase.GenerateUniqueAssetPath($"{dir}/{safe} Set.asset"));
        var setSo = new SerializedObject(set);
        setSo.FindProperty("setName").stringValue = setName;
        setSo.FindProperty("setColor").colorValue = color;
        setSo.FindProperty("requiresAllPiecesToComplete").boolValue = requireAll;
        setSo.FindProperty("minimumPiecesForSet").intValue = Mathf.Min(2, count);
        setSo.FindProperty("maximumSetPieces").intValue = Mathf.Max(count, 1);
        SerializedProperty tiers = setSo.FindProperty("setEffects");
        tiers.ClearArray();
        foreach (int n in new[] { 2, 3, 4 })
        {
            bool on = n == 2 ? tier2 : n == 3 ? tier3 : tier4;
            if (!on || n > count) continue;
            tiers.InsertArrayElementAtIndex(tiers.arraySize);
            SerializedProperty t = tiers.GetArrayElementAtIndex(tiers.arraySize - 1);
            t.FindPropertyRelative("piecesRequired").intValue = n;
            t.FindPropertyRelative("effectName").stringValue = $"{setName} ({n})";
            t.FindPropertyRelative("effectDescription").stringValue = "";
        }
        setSo.ApplyModifiedPropertiesWithoutUndo();

        foreach (var kv in slots.Where(kv => kv.Value))
        {
            var piece = CreateInstance<ArmorSO>();
            string pieceName = $"{setName} {SlotTypeHelper.GetDisplayName(kv.Key)}";
            AssetDatabase.CreateAsset(piece, AssetDatabase.GenerateUniqueAssetPath($"{dir}/{pieceName}.asset"));
            var so = new SerializedObject(piece);
            so.FindProperty("name").stringValue = pieceName;
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.Armor;
            so.FindProperty("stackMax").intValue = 1;
            so.FindProperty("maxDurability").intValue = 100;
            so.FindProperty("durability").intValue = 100;
            so.FindProperty("armorSlotType").enumValueIndex = (int)kv.Key;
            so.FindProperty("defenseValue").floatValue = defensePerPiece;
            so.FindProperty("magicDefenseValue").floatValue = magicDefensePerPiece;
            so.FindProperty("belongsToArmorSet").objectReferenceValue = set;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (useMaterial)
                ItemPresets.ApplyArmor(piece, material);
            set.AddPiece(piece);
            if (!set.RequiredSlotTypes.Contains(kv.Key))
                set.RequiredSlotTypes.Add(kv.Key);
        }
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Selection.activeObject = set;
        EditorGUIUtility.PingObject(set);
        Debug.Log($"[Armor Sets] Created '{setName}' with {count} piece(s) in {dir}. Fill in the bonus tiers in the set's inspector.", set);
    }
}
#endif
