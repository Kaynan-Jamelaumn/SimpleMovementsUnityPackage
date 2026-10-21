#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Menus that build card tables: a complete Truco table, the Truco game asset, networking a table.</summary>
public static class CardTableTools
{
    private const string Menu = SMMenu.Root + "Minigames/";

    [MenuItem(Menu + "Create Truco Table", false, SMMenu.Minigames)]
    private static void CreateTrucoTableMenu() => CreateTrucoTable(true);

    [MenuItem(Menu + "Create Blackjack Table", false, SMMenu.Minigames + 1)]
    private static void CreateBlackjackMenu() =>
        CreateRoundTable("Blackjack Table", Asset<BlackjackDefinition>("Blackjack"), 4, 0.85f, arc: true, small: false);

    [MenuItem(Menu + "Create Poker Table", false, SMMenu.Minigames + 2)]
    private static void CreatePokerMenu() =>
        CreateRoundTable("Poker Table", Asset<PokerDefinition>("Poker"), 6, 1f, arc: false, small: false);

    [MenuItem(Menu + "Create Arm Wrestling Table", false, SMMenu.Minigames + 3)]
    private static void CreateArmWrestlingMenu() =>
        CreateRoundTable("Arm Wrestling Table", Asset<ArmWrestlingDefinition>("ArmWrestling"), 2, 0.45f, arc: false, small: true);

    [MenuItem(Menu + "Create Drinking Contest Table", false, SMMenu.Minigames + 4)]
    private static void CreateDrinkingMenu() =>
        CreateRoundTable("Drinking Contest Table", Asset<DrinkingContestDefinition>("DrinkingContest"), 4, 0.8f, arc: false, small: false);

    [MenuItem(Menu + "Create Truco Game Asset", false, SMMenu.Minigames + 20)]
    private static void CreateTrucoAssetMenu() => TrucoAsset(true);

    [MenuItem(Menu + "Make Selected Card Table Networked", false, SMMenu.Minigames + 21)]
    private static void MakeNetworkedMenu()
    {
        CardTable t = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<CardTable>() : null;
        if (t == null)
        {
            EditorUtility.DisplayDialog("Card table", "Select a card table first.", "OK");
            return;
        }
        if (!MakeNetworked(t.gameObject))
            EditorUtility.DisplayDialog("Card table", "Fish-Net is not in the project (no NetworkObject type): the table works offline only.", "OK");
    }

    [MenuItem(Menu + "Make Selected Card Table Networked", true)]
    private static bool MakeNetworkedValidate() => Selection.activeGameObject != null;

    /// <summary>A game asset in Resources/SimpleMovements/Minigames (made with its defaults when missing).</summary>
    public static T Asset<T>(string assetName) where T : CardGameDefinition => SMAssetsEditor.FindOrCreate<T>(SMAssets.Minigames, assetName, null, false);

    /// <summary>
    /// A round table (or a small square one) with stools: the seats go clockwise from the near side; <paramref name="arc"/>
    /// puts them in an arc on one side (blackjack: the dealer is across). The game asset, networking when Fish-Net is in
    /// the project.
    /// </summary>
    public static CardTable CreateRoundTable(string title, CardGameDefinition game, int seatCount, float radius, bool arc, bool small)
    {
        Vector3 at = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        var root = new GameObject(title);
        Undo.RegisterCreatedObjectUndo(root, "Create " + title);
        root.transform.position = at;
        Color wood = new Color(0.32f, 0.2f, 0.12f), felt = new Color(0.08f, 0.32f, 0.18f);
        if (small)
        {
            Block("Table Top", root.transform, new Vector3(0f, 0.76f, 0f), new Vector3(0.7f, 0.06f, 0.55f), wood);
        }
        else
        {
            GameObject top = Shape(PrimitiveType.Cylinder, "Table Top", root.transform, new Vector3(0f, 0.76f, 0f), new Vector3(radius * 1.7f, 0.03f, radius * 1.7f), wood);
            GameObject cloth = Shape(PrimitiveType.Cylinder, "Felt", top.transform, new Vector3(0f, 1.05f, 0f), new Vector3(0.88f, 0.1f, 0.88f), felt);
            UnityEngine.Object.DestroyImmediate(cloth.GetComponent<Collider>());
        }
        Block("Leg", root.transform, new Vector3(0f, 0.37f, 0f), new Vector3(0.16f, 0.74f, 0.16f), wood);
        var center = new GameObject("Center");
        center.transform.SetParent(root.transform, false);
        center.transform.localPosition = new Vector3(0f, small ? 0.795f : 0.8f, 0f);

        var table = root.AddComponent<CardTable>();
        var so = new SerializedObject(table);
        so.FindProperty("game").objectReferenceValue = game;
        so.FindProperty("center").objectReferenceValue = center.transform;
        so.FindProperty("botsFillEmptySeats").boolValue = !(game is BlackjackDefinition); // blackjack: play alone against the dealer
        SerializedProperty seats = so.FindProperty("seats");
        seats.ClearArray();
        float distance = small ? 0.75f : radius + 0.55f;
        for (int i = 0; i < seatCount; i++)
        {
            // Clockwise seen from above, starting at the near (south) side: dir = (-sin a, 0, -cos a).
            float a = arc ? Mathf.Lerp(-65f, 65f, seatCount == 1 ? 0.5f : i / (float)(seatCount - 1)) : i * 360f / seatCount;
            float rad = a * Mathf.Deg2Rad;
            Vector3 d = new Vector3(-Mathf.Sin(rad), 0f, -Mathf.Cos(rad));
            GameObject stool = Shape(PrimitiveType.Cylinder, $"Stool {i + 1}", root.transform, d * distance + Vector3.up * 0.22f, new Vector3(0.42f, 0.22f, 0.42f), wood);
            var sit = new GameObject("Sit Point");
            sit.transform.SetParent(root.transform, false);
            sit.transform.localPosition = d * (distance - 0.1f); // the feet, on the floor: the Sit animation puts the hips on the stool
            sit.transform.localRotation = Quaternion.LookRotation(-d, Vector3.up);
            sit.transform.SetParent(stool.transform, true);
            var seat = stool.AddComponent<CardSeat>();
            var sso = new SerializedObject(seat);
            sso.FindProperty("seatIndex").intValue = i;
            sso.FindProperty("sitPoint").objectReferenceValue = sit.transform;
            sso.ApplyModifiedPropertiesWithoutUndo();
            seats.InsertArrayElementAtIndex(i);
            seats.GetArrayElementAtIndex(i).objectReferenceValue = seat;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        MakeNetworked(root);
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log($"[Card Table] Created a {game.DisplayName} table: walk to a stool and interact to sit. Stakes and bots are on the Card Table component.", root);
        return table;
    }

    private static GameObject Shape(PrimitiveType type, string n, Transform parent, Vector3 localPos, Vector3 size, Color color)
    {
        GameObject go = Block(n, parent, localPos, size, color);
        if (type == PrimitiveType.Cube)
            return go;
        GameObject shaped = GameObject.CreatePrimitive(type);
        shaped.name = n;
        shaped.transform.SetParent(parent, false);
        shaped.transform.localPosition = localPos;
        shaped.transform.localScale = size;
        Renderer src = go.GetComponent<Renderer>(), dst = shaped.GetComponent<Renderer>();
        if (src != null && dst != null) dst.sharedMaterial = src.sharedMaterial;
        UnityEngine.Object.DestroyImmediate(go);
        return shaped;
    }

    /// <summary>The project's Truco asset (Resources/SimpleMovements/Minigames/Truco), made when missing.</summary>
    public static TrucoGameDefinition TrucoAsset(bool select) =>
        SMAssetsEditor.FindOrCreate<TrucoGameDefinition>(SMAssets.Minigames, "Truco", null, select);

    /// <summary>
    /// A ready Truco table in the open scene, at the Scene view's pivot: a table top with its centre marker, four benches
    /// (seats 1-4 clockwise; teammates opposite), the Truco asset, and networking when Fish-Net is in the project.
    /// </summary>
    public static CardTable CreateTrucoTable(bool select)
    {
        Vector3 at = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        var root = new GameObject("Truco Table");
        Undo.RegisterCreatedObjectUndo(root, "Create Truco Table");
        root.transform.position = new Vector3(at.x, at.y, at.z);

        GameObject top = Block("Table Top", root.transform, new Vector3(0f, 0.76f, 0f), new Vector3(1.3f, 0.06f, 1.3f), new Color(0.32f, 0.2f, 0.12f));
        GameObject felt = Block("Felt", top.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.9f, 0.1f, 0.9f), new Color(0.08f, 0.32f, 0.18f));
        UnityEngine.Object.DestroyImmediate(felt.GetComponent<Collider>());
        Block("Leg", root.transform, new Vector3(0f, 0.37f, 0f), new Vector3(0.18f, 0.74f, 0.18f), new Color(0.25f, 0.15f, 0.09f));
        var center = new GameObject("Center");
        center.transform.SetParent(root.transform, false);
        center.transform.localPosition = new Vector3(0f, 0.795f, 0f);

        var table = root.AddComponent<CardTable>();
        var so = new SerializedObject(table);
        so.FindProperty("game").objectReferenceValue = TrucoAsset(false);
        so.FindProperty("center").objectReferenceValue = center.transform;
        SerializedProperty seats = so.FindProperty("seats");
        seats.ClearArray();

        // Clockwise seen from above: south, west, north, east. Teammates (1 + 3, 2 + 4) face each other.
        Vector3[] dirs = { Vector3.back, Vector3.left, Vector3.forward, Vector3.right };
        for (int i = 0; i < 4; i++)
        {
            Vector3 d = dirs[i];
            GameObject bench = Block($"Bench {i + 1}", root.transform, d * 1.05f + Vector3.up * 0.22f,
                Mathf.Abs(d.x) > 0.5f ? new Vector3(0.45f, 0.44f, 1.1f) : new Vector3(1.1f, 0.44f, 0.45f), new Color(0.36f, 0.24f, 0.14f));
            var sit = new GameObject("Sit Point");
            sit.transform.SetParent(root.transform, false);
            sit.transform.localPosition = d * 0.95f; // the feet, on the floor: the Sit animation puts the hips on the bench
            sit.transform.localRotation = Quaternion.LookRotation(-d, Vector3.up);
            sit.transform.SetParent(bench.transform, true);
            var seat = bench.AddComponent<CardSeat>();
            var sso = new SerializedObject(seat);
            sso.FindProperty("seatIndex").intValue = i;
            sso.FindProperty("sitPoint").objectReferenceValue = sit.transform;
            sso.ApplyModifiedPropertiesWithoutUndo();
            seats.InsertArrayElementAtIndex(i);
            seats.GetArrayElementAtIndex(i).objectReferenceValue = seat;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        MakeNetworked(root);
        if (select)
        {
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
        }
        Debug.Log("[Card Table] Created a Truco table: walk to a bench and interact to sit. Stakes, bots and the game asset are on " +
                  "the Card Table component.", root);
        return table;
    }

    private static GameObject Block(string n, Transform parent, Vector3 localPos, Vector3 size, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = n;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader != null)
            {
                var m = new Material(shader) { color = color };
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
                r.sharedMaterial = m;
            }
        }
        return go;
    }

    /// <summary>Adds a NetworkObject and a NetCardTable (Fish-Net). False when Fish-Net is not in the project.</summary>
    public static bool MakeNetworked(GameObject root)
    {
        Type nob = SimpleMovements.Setup.SetupUtil.TypeByName("FishNet.Object.NetworkObject");
        Type net = SimpleMovements.Setup.SetupUtil.TypeByName("GameNet.NetCardTable");
        if (nob == null || net == null)
            return false;
        if (root.GetComponent(nob) == null)
            Undo.AddComponent(root, nob);
        if (root.GetComponent(net) == null)
            Undo.AddComponent(root, net);
        return true;
    }
}

[CustomEditor(typeof(CardTable))]
public class CardTableEditor : Editor
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();

    public override void OnInspectorGUI()
    {
        var t = (CardTable)target;
        errors.Clear();
        warnings.Clear();
        t.Validate(errors, warnings);
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
        if (!t.GetComponent<Collider>() && t.GetComponentInChildren<Collider>() == null)
            EditorGUILayout.HelpBox("No collider on the table: fine, but the seats need colliders for the Player Interactor to find them.", MessageType.Info);
        if (SimpleMovements.Setup.SetupUtil.TypeByName("GameNet.NetCardTable") is Type net && t.GetComponent(net) == null)
        {
            EditorGUILayout.HelpBox("Multiplayer: the table has no NetCardTable (and NetworkObject); in a network session each machine would play its own game.", MessageType.Info);
            if (GUILayout.Button("Make Networked"))
                CardTableTools.MakeNetworked(t.gameObject);
        }

        DrawDefaultInspector();

        if (!Application.isPlaying)
            return;
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
        CardTableView v = t.RulesHere ? t.BuildView(-1) : t.LocalView;
        if (v == null)
            return;
        EditorGUILayout.LabelField("Phase", ((CardTable.TablePhase)v.tablePhase).ToString());
        EditorGUILayout.LabelField("Status", v.status, EditorStyles.wordWrappedLabel);
        for (int i = 0; i < v.teamNames.Count; i++)
            EditorGUILayout.LabelField(v.teamNames[i], i < v.teamScores.Count ? v.teamScores[i].ToString() : "0");
        for (int i = 0; i < v.seats.Count; i++)
        {
            CardSeatView s = v.seats[i];
            EditorGUILayout.LabelField($"Seat {i + 1}", s.occupied ? $"{s.name}{(s.bot ? " (bot)" : "")}{(s.ready ? ", ready" : "")}, {s.cardCount} card(s)" : "empty");
        }
        using (new EditorGUI.DisabledScope(!t.RulesHere || t.Phase != CardTable.TablePhase.Lobby))
        {
            if (GUILayout.Button(new GUIContent("Watch Bots Play", "Fills the free seats with bots and starts a game without stakes (testing).")))
                t.StartBotsOnlyGame();
        }
        int from = Mathf.Max(0, v.log.Count - 8);
        for (int i = from; i < v.log.Count; i++)
            EditorGUILayout.LabelField(v.log[i], EditorStyles.miniLabel);
        Repaint();
    }
}

[CustomEditor(typeof(TrucoGameDefinition))]
public class TrucoGameDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var d = (TrucoGameDefinition)target;
        var errors = new List<string>();
        var warnings = new List<string>();
        d.Validate(errors, warnings);
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
        EditorGUILayout.HelpBox("Card strength, strongest first: the four manilhas (by Manilha Suits), then the Rank Sequence from the end " +
                                "(3, 2, A, K, J, Q, 10 ... 4). The manilha is the rank after the vira's in the Rank Sequence.", MessageType.None);
        DrawDefaultInspector();
        if (GUILayout.Button("Reset Rules To Defaults"))
        {
            Undo.RecordObject(d, "Reset Truco rules");
            ResetRules(d);
            EditorUtility.SetDirty(d);
        }
    }

    private static void ResetRules(TrucoGameDefinition d)
    {
        TrucoSettings fresh = new TrucoSettings();
        TrucoSettings r = d.Rules;
        r.pointsToWin = fresh.pointsToWin;
        r.handValues = fresh.handValues;
        r.callNames = fresh.callNames;
        r.rankSequence = fresh.rankSequence;
        r.fortyCardDeck = fresh.fortyCardDeck;
        r.manilhaSuits = fresh.manilhaSuits;
        r.trickPauseSeconds = fresh.trickPauseSeconds;
        r.handPauseSeconds = fresh.handPauseSeconds;
        r.botBoldness = fresh.botBoldness;
    }
}
#endif
