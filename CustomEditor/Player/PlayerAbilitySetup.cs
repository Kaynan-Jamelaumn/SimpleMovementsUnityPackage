#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Editor helpers for the player's ABILITY KEYS (press an input ➜ cast an ability). A key is stored in two places:
/// a line in the AbilitiesStateMachine list (the input) and an AbilityStateMachine component (the ability). These
/// helpers create, edit, check and repair both at once, so the inspectors can show a key as one thing.
/// </summary>
public static class PlayerAbilitySetup
{
    public const string ContainerName = "Ability Keys";
    private const string OldContainerName = "Ability Slots";

    /// <summary>The player's AbilitiesStateMachine (bindings) or null.</summary>
    public static AbilitiesStateMachine FindBindings(Component anyOnPlayer)
    {
        AbilitiesStateMachine a = anyOnPlayer.GetComponent<AbilitiesStateMachine>();
        if (a == null) a = anyOnPlayer.GetComponentInParent<AbilitiesStateMachine>();
        if (a == null) a = AbilitiesStateMachine.PlayerRoot(anyOnPlayer).GetComponentInChildren<AbilitiesStateMachine>(true);
        return a;
    }

    /// <summary>
    /// Creates the object that stores an ability key: a child under "Ability Keys" with an AbilityStateMachine holding
    /// <paramref name="ability"/> (no binding is added; see <see cref="CreateSlot"/>).
    /// </summary>
    public static AbilityStateMachine CreateSlotObject(Component anyOnPlayer, AbilityDefinition ability, InputActionReference input)
    {
        Transform root = AbilitiesStateMachine.PlayerRoot(anyOnPlayer);
        PlayerAbilityController controller = root.GetComponentInChildren<PlayerAbilityController>(true);
        GameObject owner = controller != null ? controller.gameObject : root.gameObject;

        Transform container = owner.transform.Find(ContainerName);
        if (container == null)
            container = owner.transform.Find(OldContainerName);
        if (container == null)
        {
            var c = new GameObject(ContainerName);
            Undo.RegisterCreatedObjectUndo(c, "Create Ability Keys");
            Undo.SetTransformParent(c.transform, owner.transform, "Create Ability Keys");
            c.transform.localPosition = Vector3.zero;
            container = c.transform;
        }

        int n = container.childCount + 1;
        string label = ability != null ? ability.DisplayName : (input != null && input.action != null ? input.action.name : "Empty");
        var go = new GameObject($"Key {n} - {label}");
        Undo.RegisterCreatedObjectUndo(go, "Create Ability Key");
        Undo.SetTransformParent(go.transform, container, "Create Ability Key");
        go.transform.localPosition = Vector3.zero;

        AbilityStateMachine m = Undo.AddComponent<AbilityStateMachine>(go);
        var so = new SerializedObject(m);
        SerializedProperty holder = so.FindProperty("abilityHolder");
        holder.FindPropertyRelative("ability").objectReferenceValue = ability;
        holder.FindPropertyRelative("abilityActionReference").objectReferenceValue = input;
        so.FindProperty("abilityController").objectReferenceValue = controller;
        so.FindProperty("animationModel").objectReferenceValue = root.GetComponentInChildren<PlayerAnimationModel>(true);
        so.ApplyModifiedProperties();
        return m;
    }

    /// <summary>Creates a whole ability key: the object that stores the ability plus its input line.</summary>
    public static AbilityStateMachine CreateSlot(Component anyOnPlayer, InputActionReference input, AbilityDefinition ability)
    {
        AbilitiesStateMachine bindings = FindBindings(anyOnPlayer);
        if (bindings == null)
        {
            PlayerAbilityController pc = AbilitiesStateMachine.PlayerRoot(anyOnPlayer).GetComponentInChildren<PlayerAbilityController>(true);
            bindings = Undo.AddComponent<AbilitiesStateMachine>(pc != null ? pc.gameObject : AbilitiesStateMachine.PlayerRoot(anyOnPlayer).gameObject);
        }
        AbilityStateMachine m = CreateSlotObject(anyOnPlayer, ability, input);
        AddBinding(bindings, m, input);
        return m;
    }

    /// <summary>Adds a binding (input ➜ slot) to the AbilitiesStateMachine.</summary>
    public static void AddBinding(AbilitiesStateMachine bindings, AbilityStateMachine machine, InputActionReference input)
    {
        var so = new SerializedObject(bindings);
        SerializedProperty list = so.FindProperty("abilityAction");
        list.arraySize++;
        SerializedProperty el = list.GetArrayElementAtIndex(list.arraySize - 1);
        el.FindPropertyRelative("abilityStateMachine").objectReferenceValue = machine;
        el.FindPropertyRelative("abilityActionReference").objectReferenceValue = input;
        so.ApplyModifiedProperties();
    }

    /// <summary>Removes binding <paramref name="index"/>; optionally deletes the slot's object too.</summary>
    public static void RemoveBinding(AbilitiesStateMachine bindings, int index, bool deleteSlotObject)
    {
        var so = new SerializedObject(bindings);
        SerializedProperty list = so.FindProperty("abilityAction");
        if (index < 0 || index >= list.arraySize)
            return;
        var machine = list.GetArrayElementAtIndex(index).FindPropertyRelative("abilityStateMachine").objectReferenceValue as AbilityStateMachine;
        list.DeleteArrayElementAtIndex(index);
        so.ApplyModifiedProperties();
        if (deleteSlotObject && machine != null)
        {
            if (machine.gameObject.GetComponents<Component>().Length <= 2 && machine.transform.childCount == 0)
                Undo.DestroyObjectImmediate(machine.gameObject);
            else
                Undo.DestroyObjectImmediate(machine);
        }
    }

    // ------------------------------------------------------------------ inline key editing
    private static readonly HashSet<int> openModifiers = new HashSet<int>();
    private static readonly HashSet<int> openDetails = new HashSet<int>();

    /// <summary>"Q (Ability1)" style label for an input, or a note about the AbilityN fallback.</summary>
    public static string InputLabel(InputActionReference r, int index)
    {
        if (r == null)
            return $"(auto: {AbilitiesStateMachine.DefaultActionName(index)})";
        return r.action != null ? r.action.name : r.name;
    }

    /// <summary>What an AbilityStateMachine casts, as a short label.</summary>
    public static string AbilityLabel(AbilityStateMachine machine)
    {
        if (machine == null)
            return "(not connected)";
        PlayerAbilityHolder h = machine.AbilityHolder;
        return h == null ? "(empty)" : h.ability != null ? h.ability.DisplayName : h.abilityEffect != null ? h.abilityEffect.name + " (legacy)" : "(empty - filled by absorption)";
    }

    /// <summary>
    /// Draws one ABILITY KEY with everything editable inline: the key (input), the ability it casts (with preset
    /// menu) and its modifiers. Behind the scenes a key is one line of the AbilitiesStateMachine list plus the
    /// AbilityStateMachine component it points to; both are edited here. Returns true if the user asked to remove it.
    /// </summary>
    public static bool DrawBindingEditor(AbilitiesStateMachine bindings, int index, Component folderOwner, out bool deleteObject)
    {
        deleteObject = false;
        SerializedObject so = AbilityEditorUI.Serialized(bindings);
        SerializedProperty list = so.FindProperty("abilityAction");
        if (index < 0 || index >= list.arraySize)
            return false;
        SerializedProperty el = list.GetArrayElementAtIndex(index);
        SerializedProperty inputP = el.FindPropertyRelative("abilityActionReference");
        SerializedProperty machineP = el.FindPropertyRelative("abilityStateMachine");
        var machine = machineP.objectReferenceValue as AbilityStateMachine;
        bool remove = false;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(new GUIContent($"Key {index + 1}:  {InputLabel(inputP.objectReferenceValue as InputActionReference, index)}  ➜  {AbilityLabel(machine)}",
            "One ability key: press the input, cast the ability."), EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(machine == null))
            if (GUILayout.Button(new GUIContent("Select", "Select the object that stores this key (its AbilityStateMachine component)."), EditorStyles.miniButton, GUILayout.Width(50)))
                Selection.activeObject = machine;
        if (GUILayout.Button(new GUIContent("✕", "Remove this ability key (and the object that stores it)."), EditorStyles.miniButton, GUILayout.Width(24)))
        {
            remove = true;
            deleteObject = true;
        }
        EditorGUILayout.EndHorizontal();

        // 1. The key.
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(inputP, new GUIContent("Input", "The input action (key / button) that casts this ability."));
        if (GUILayout.Button(new GUIContent("▾", "Pick an input action from the project's Input Actions assets."), EditorStyles.miniButton, GUILayout.Width(22)))
            InputMenu(r => SetInput(bindings, index, r));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.PropertyField(el.FindPropertyRelative("holdToRepeat"), new GUIContent("Hold To Repeat",
            "Keep casting while the key is held (every time the ability is ready again). Off = one cast per press."));
        if (inputP.objectReferenceValue == null)
        {
            string def = AbilitiesStateMachine.DefaultActionName(index);
            InputActionReference found = FindInputReference(def);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Empty = uses the '{def}' action of the player's input actions (if it exists).", EditorStyles.wordWrappedMiniLabel);
            if (found != null && GUILayout.Button(new GUIContent($"Use {def}", $"Assign {AssetDatabase.GetAssetPath(found)} ▸ {found.name}."), EditorStyles.miniButton, GUILayout.Width(110)))
                inputP.objectReferenceValue = found;
            EditorGUILayout.EndHorizontal();
        }

        // Not connected to a component: offer to create it.
        if (machine == null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("This key has nowhere to store its ability yet, so it casts nothing.", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button(new GUIContent("Fix", "Create the object that stores this key's ability (a child with an AbilityStateMachine under 'Ability Keys')."), EditorStyles.miniButton, GUILayout.Width(40)))
            {
                so.ApplyModifiedProperties();
                AbilityStateMachine created = CreateSlotObject(bindings, null, inputP.objectReferenceValue as InputActionReference);
                var s4 = new SerializedObject(bindings);
                s4.FindProperty("abilityAction").GetArrayElementAtIndex(index).FindPropertyRelative("abilityStateMachine").objectReferenceValue = created;
                s4.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(machineP, new GUIContent("Stored On", "The AbilityStateMachine component that stores this key's ability. Drag an existing one here, or press Fix."));
            so.ApplyModifiedProperties();
            EditorGUILayout.EndVertical();
            return remove;
        }
        so.ApplyModifiedProperties();

        // 2. The ability.
        SerializedObject mso = AbilityEditorUI.Serialized(machine);
        SerializedProperty holder = mso.FindProperty("abilityHolder");
        SerializedProperty abilityP = holder.FindPropertyRelative("ability");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(abilityP, new GUIContent("Ability", "What this key casts (an Ability Definition asset). Empty = the legacy Ability Effect, or nothing until the player absorbs one."));
        if (GUILayout.Button(new GUIContent("Preset ▾", "Create an Ability Definition from a preset and put it on this key."), EditorStyles.miniButton, GUILayout.Width(62)))
        {
            AbilityStateMachine captured = machine;
            AbilityEditorUI.PresetMenu(AbilityEditorUI.FolderFor(folderOwner), def =>
            {
                var s3 = new SerializedObject(captured);
                s3.FindProperty("abilityHolder").FindPropertyRelative("ability").objectReferenceValue = def;
                s3.ApplyModifiedProperties();
            });
        }
        EditorGUILayout.EndHorizontal();
        PlayerAbilityHolder h = machine.AbilityHolder;
        if (abilityP.objectReferenceValue is AbilityDefinition def0)
            EditorGUILayout.LabelField(AbilityEditorUI.AbilityLine(def0, h != null ? h.modifiers : null), EditorStyles.miniLabel);
        else if (holder.FindPropertyRelative("abilityEffect").objectReferenceValue != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Uses a legacy Ability Effect (converted at runtime).", EditorStyles.miniLabel);
            if (GUILayout.Button(new GUIContent("Convert", "Save it as an Ability Definition and assign it."), EditorStyles.miniButton, GUILayout.Width(60)))
            {
                mso.ApplyModifiedProperties();
                AbilityStateMachineEditor.ConvertHolder(machine);
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }

        // 3. Modifiers (collapsed).
        int key = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(machine);
        bool open = openModifiers.Contains(key);
        bool now = EditorGUILayout.Foldout(open, new GUIContent("Modifiers" + (h != null && h.modifiers != null && !h.modifiers.IsIdentity ? ": " + h.modifiers.Describe() : ""),
            "Player-only changes to this key's ability (stronger, weaker, 1 projectile...). The ability asset is not changed."), true);
        if (now != open) { if (now) openModifiers.Add(key); else openModifiers.Remove(key); }
        if (now)
            EditorGUILayout.PropertyField(holder.FindPropertyRelative("modifiers"), GUIContent.none, true);
        mso.ApplyModifiedProperties();

        if (abilityP.objectReferenceValue is AbilityDefinition def1)
            AbilityEditorUI.InlineAssetEditor(def1, key * 7 + 1, "ability",
                "Shared asset: changes apply to every character that uses this ability. Use Modifiers for player-only changes.");

        // Where it is stored (only for the curious).
        bool detailsOpen = openDetails.Contains(key);
        bool details = EditorGUILayout.Foldout(detailsOpen, new GUIContent("Where this key is stored", "Only needed if you want to move things around by hand."), true);
        if (details != detailsOpen) { if (details) openDetails.Add(key); else openDetails.Remove(key); }
        if (details)
        {
            EditorGUILayout.LabelField($"Input: line {index + 1} of the AbilitiesStateMachine list on '{bindings.name}'.   Ability: the AbilityStateMachine component on '{machine.name}'.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.PropertyField(machineP, new GUIContent("Stored On", "The AbilityStateMachine component that stores this key's ability."));
            so.ApplyModifiedProperties();
        }
        EditorGUILayout.EndVertical();
        return remove;
    }

    // ------------------------------------------------------------------ repair
    /// <summary>
    /// Every InputActionReference in the project: standalone reference assets and the ones inside .inputactions assets.
    /// </summary>
    private static List<InputActionReference> cachedRefs;
    private static readonly Dictionary<string, InputActionReference> refByName = new Dictionary<string, InputActionReference>();

    [InitializeOnLoadMethod]
    private static void HookCache()
    {
        AbilityEditorUI.CachesCleared -= ClearInputCache;
        AbilityEditorUI.CachesCleared += ClearInputCache;
    }

    private static void ClearInputCache()
    {
        cachedRefs = null;
        refByName.Clear();
    }

    /// <summary>Every InputActionReference in the project (searched once, refreshed when assets change).</summary>
    public static List<InputActionReference> AllInputReferences()
    {
        if (cachedRefs != null)
        {
            cachedRefs.RemoveAll(r => r == null);
            return cachedRefs;
        }
        var list = new List<InputActionReference>();
        var seen = new HashSet<InputActionReference>();
        foreach (string type in new[] { "t:InputActionReference", "t:InputActionAsset" })
        {
            foreach (string guid in AssetDatabase.FindAssets(type))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (InputActionReference r in AssetDatabase.LoadAllAssetsAtPath(path).OfType<InputActionReference>())
                    if (r != null && seen.Add(r))
                        list.Add(r);
            }
        }
        cachedRefs = list;
        return list;
    }

    /// <summary>The project's InputActionReference for an action name ("Ability1"), or null.</summary>
    public static InputActionReference FindInputReference(string actionName)
    {
        if (string.IsNullOrEmpty(actionName))
            return null;
        if (refByName.TryGetValue(actionName, out InputActionReference cached))
            return cached; // null = not found (the cache is cleared whenever assets change)
        InputActionReference found = null;
        foreach (InputActionReference r in AllInputReferences())
        {
            string n = r.action != null ? r.action.name : r.name;
            if (n == actionName || r.name == actionName || r.name.EndsWith("/" + actionName))
            {
                found = r;
                break;
            }
        }
        refByName[actionName] = found;
        return found;
    }

    /// <summary>
    /// Fixes the binding list in one go: empty bindings get an unbound slot (or are removed when there is none),
    /// duplicates are removed, unbound slots get a binding, and empty inputs get the slot's own input or the
    /// project's "AbilityN" action. Returns a summary of what changed.
    /// </summary>
    public static string RepairBindings(AbilitiesStateMachine bindings)
    {
        Transform root = AbilitiesStateMachine.PlayerRoot(bindings);
        var machines = root.GetComponentsInChildren<AbilityStateMachine>(true).ToList();
        var so = new SerializedObject(bindings);
        SerializedProperty list = so.FindProperty("abilityAction");
        int assigned = 0, removed = 0, added = 0, inputs = 0;

        var used = new HashSet<AbilityStateMachine>();
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("abilityStateMachine").objectReferenceValue is AbilityStateMachine m)
                used.Add(m);
        var unbound = machines.Where(m => !used.Contains(m)).ToList();

        // Empty or duplicate bindings.
        var seen = new HashSet<AbilityStateMachine>();
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty machineP = list.GetArrayElementAtIndex(i).FindPropertyRelative("abilityStateMachine");
            var m = machineP.objectReferenceValue as AbilityStateMachine;
            if (m != null && seen.Add(m))
                continue;
            if (unbound.Count > 0)
            {
                machineP.objectReferenceValue = unbound[0];
                seen.Add(unbound[0]);
                unbound.RemoveAt(0);
                assigned++;
            }
            else
            {
                machineP.objectReferenceValue = null;
                list.DeleteArrayElementAtIndex(i);
                i--;
                removed++;
            }
        }
        // Slots without a binding.
        foreach (AbilityStateMachine m in unbound)
        {
            list.arraySize++;
            SerializedProperty el = list.GetArrayElementAtIndex(list.arraySize - 1);
            el.FindPropertyRelative("abilityStateMachine").objectReferenceValue = m;
            el.FindPropertyRelative("abilityActionReference").objectReferenceValue = null;
            added++;
        }
        // Inputs.
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty el = list.GetArrayElementAtIndex(i);
            SerializedProperty inputP = el.FindPropertyRelative("abilityActionReference");
            if (inputP.objectReferenceValue != null)
                continue;
            var m = el.FindPropertyRelative("abilityStateMachine").objectReferenceValue as AbilityStateMachine;
            InputActionReference r = m != null && m.AbilityHolder != null ? m.AbilityHolder.AbilityActionReference : null;
            if (r == null)
                r = FindInputReference(AbilitiesStateMachine.DefaultActionName(i));
            if (r != null)
            {
                inputP.objectReferenceValue = r;
                inputs++;
            }
        }
        so.ApplyModifiedProperties();

        // Slots should point at the player's controller.
        PlayerAbilityController controller = root.GetComponentInChildren<PlayerAbilityController>(true);
        foreach (AbilityStateMachine m in machines)
        {
            var mso = new SerializedObject(m);
            SerializedProperty c = mso.FindProperty("abilityController");
            if (c.objectReferenceValue == null && controller != null)
            {
                c.objectReferenceValue = controller;
                mso.ApplyModifiedProperties();
            }
        }

        string summary = $"Ability keys repaired: {assigned} key(s) connected to an unused AbilityStateMachine, {removed} broken key(s) removed, {added} key(s) added for unused AbilityStateMachines, {inputs} input(s) assigned.";
        Debug.Log($"[{bindings.name}] {summary}", bindings);
        return summary;
    }

    /// <summary>
    /// Checks the binding list and adds issues with one-click fixes (Repair, Create Slot, Use AbilityN, Copy Input).
    /// </summary>
    public static void ValidateBindings(AbilitiesStateMachine bindings, List<AbilityEditorUI.Issue> issues)
    {
        Transform root = AbilitiesStateMachine.PlayerRoot(bindings);
        List<AbilityStateMachine> machines = root.GetComponentsInChildren<AbilityStateMachine>(true).ToList();
        var bound = new HashSet<AbilityStateMachine>();
        foreach (AbilityAction b in bindings.AbilityActions)
            if (b != null && b.AbilityStateMachine != null) bound.Add(b.AbilityStateMachine);
        int unbound = machines.Count(m => !bound.Contains(m));
        System.Action repair = () => RepairBindings(bindings);

        var seenMachines = new HashSet<AbilityStateMachine>();
        var seenInputs = new HashSet<object>();
        for (int i = 0; i < bindings.AbilityActions.Count; i++)
        {
            AbilityAction b = bindings.AbilityActions[i];
            if (b == null)
                continue;
            int index = i;
            if (b.AbilityStateMachine == null)
            {
                if (unbound > 0)
                    AbilityEditorUI.Add(issues, MessageType.Error, $"Key {i + 1} is not connected to an AbilityStateMachine, so it casts nothing. {unbound} AbilityStateMachine(s) on the player are not used by any key: Repair connects them.", "Repair", repair);
                else
                    AbilityEditorUI.Add(issues, MessageType.Error, $"Key {i + 1} has nowhere to store its ability (no AbilityStateMachine), so it casts nothing. Fix creates one; ✕ removes the key.", "Fix",
                        () =>
                        {
                            AbilityStateMachine created = CreateSlotObject(bindings, null, b.abilityActionReference);
                            var so = new SerializedObject(bindings);
                            so.FindProperty("abilityAction").GetArrayElementAtIndex(index).FindPropertyRelative("abilityStateMachine").objectReferenceValue = created;
                            so.ApplyModifiedProperties();
                        });
            }
            else if (!seenMachines.Add(b.AbilityStateMachine))
                AbilityEditorUI.Add(issues, MessageType.Warning, $"Key {i + 1} points to the same AbilityStateMachine as another key (both would cast the same ability).", "Repair", repair);

            if (b.abilityActionReference == null)
            {
                PlayerAbilityHolder h = b.AbilityStateMachine != null ? b.AbilityStateMachine.AbilityHolder : null;
                string def = AbilitiesStateMachine.DefaultActionName(i);
                if (h != null && h.AbilityActionReference != null)
                    AbilityEditorUI.Add(issues, MessageType.Warning, $"Key {i + 1} has no input, but its AbilityStateMachine has a copy ({h.AbilityActionReference.name}).", "Copy Input",
                        () => SetInput(bindings, index, h.AbilityActionReference));
                else
                {
                    InputActionReference found = FindInputReference(def);
                    if (found != null)
                        AbilityEditorUI.Add(issues, MessageType.Warning, $"Key {i + 1} has no input. The project has an '{def}' action for it.", $"Use {def}",
                            () => SetInput(bindings, index, found));
                    else
                        AbilityEditorUI.Add(issues, MessageType.Info, $"Key {i + 1} has no input: at runtime it uses the '{def}' action of the player's input actions if there is one. Pick an input with ▾ to be sure.");
                }
            }
            else if (b.abilityActionReference.action != null && !seenInputs.Add(b.abilityActionReference.action))
                AbilityEditorUI.Add(issues, MessageType.Warning, $"Key {i + 1} uses the same input ({b.abilityActionReference.name}) as another key: both cast together.");
        }
        foreach (AbilityStateMachine m in machines)
            if (!bound.Contains(m))
                AbilityEditorUI.Add(issues, MessageType.Warning, $"'{m.name}' has an AbilityStateMachine that no key uses, so nothing casts it.", "Repair", repair);
        if (bindings.AbilityActions.Count == 0 && machines.Count == 0)
            AbilityEditorUI.Add(issues, MessageType.Info, "No ability keys yet: use '＋ New Ability Key'.");
    }

    private static void SetInput(AbilitiesStateMachine bindings, int index, InputActionReference input)
    {
        var so = new SerializedObject(bindings);
        so.FindProperty("abilityAction").GetArrayElementAtIndex(index).FindPropertyRelative("abilityActionReference").objectReferenceValue = input;
        so.ApplyModifiedProperties();
    }

    /// <summary>A menu of every InputActionReference in the project's Input Actions assets.</summary>
    public static void InputMenu(System.Action<InputActionReference> onPicked)
    {
        var menu = new GenericMenu();
        int count = 0;
        foreach (InputActionReference r in AllInputReferences().OrderBy(x => AssetDatabase.GetAssetPath(x)).ThenBy(x => x.name))
        {
            InputActionReference captured = r;
            string asset = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(r));
            string label = AssetDatabase.IsSubAsset(r) ? $"{asset}/{r.name}" : r.name;
            menu.AddItem(new GUIContent(label), false, () => onPicked(captured));
            count++;
        }
        if (count == 0)
            menu.AddDisabledItem(new GUIContent("No Input Action references found (open your .inputactions asset once)"));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("None"), false, () => onPicked(null));
        menu.ShowAsContext();
    }

    // ------------------------------------------------------------------ "new slot" panel
    private static InputActionReference newInput;
    private static AbilityDefinition newAbility;

    /// <summary>Draws the "New Ability Key" panel (input + ability + Create).</summary>
    public static void DrawNewSlotPanel(Component anyOnPlayer)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("＋ New Ability Key", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Pick an input and an ability, then Create. Everything behind it (child object, AbilityStateMachine, list line) is set up for you.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.BeginHorizontal();
        newInput = (InputActionReference)EditorGUILayout.ObjectField(new GUIContent("Input", "The input action (key / button) that casts the new ability."), newInput, typeof(InputActionReference), false);
        if (GUILayout.Button(new GUIContent("▾", "Pick an input action."), EditorStyles.miniButton, GUILayout.Width(22)))
            InputMenu(r => newInput = r);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        newAbility = (AbilityDefinition)EditorGUILayout.ObjectField(new GUIContent("Ability", "What it casts. Empty = an empty key (filled when the player absorbs an ability)."), newAbility, typeof(AbilityDefinition), false);
        if (GUILayout.Button(new GUIContent("Preset ▾", "Create an Ability Definition from a preset for the new key."), EditorStyles.miniButton, GUILayout.Width(62)))
            AbilityEditorUI.PresetMenu(AbilityEditorUI.FolderFor(anyOnPlayer), d => newAbility = d);
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button(new GUIContent("Create Ability Key", "Create the key now (Undo removes it).")))
        {
            AbilityStateMachine m = CreateSlot(anyOnPlayer, newInput, newAbility);
            EditorGUIUtility.PingObject(m);
            newInput = null;
            newAbility = null;
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndVertical();
    }
}
#endif
