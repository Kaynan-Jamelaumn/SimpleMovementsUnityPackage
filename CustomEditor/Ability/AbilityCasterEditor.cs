#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for ability casters (mobs, player, custom): slot summaries and validation, live phases and cooldowns in
/// Play mode, and a Scene view preview of a slot's hit areas around the character.
/// </summary>
[CustomEditor(typeof(AbilityCaster), true)]
public class AbilityCasterEditor : Editor
{
    private int previewSlot = -1;
    private readonly List<ResolvedShape> shapes = new List<ResolvedShape>(8);
    private readonly List<Vector3> outer = new List<Vector3>(80);
    private readonly List<Vector3> inner = new List<Vector3>(80);
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    // Slot checks are cached and refreshed at most twice per second (validating every redraw made the inspector slow).
    private readonly AbilityEditorUI.Throttle slotCheckThrottle = new AbilityEditorUI.Throttle();
    private readonly List<string> slotProblems = new List<string>();
    private readonly List<MessageType> slotProblemTypes = new List<MessageType>();

    public override void OnInspectorGUI()
    {
        var caster = (AbilityCaster)target;
        DrawSlotsOverview(caster);
        EditorGUILayout.Space();
        DrawDefaultInspector();
        AbilityEditorUI.KeepRepainting(this);
    }

    /// <summary>Per-slot summary: name, timings, validation, live phase/cooldown and the Scene preview toggle.</summary>
    protected void DrawSlotsOverview(AbilityCaster caster)
    {
        IReadOnlyList<AbilitySlot> slots = caster.Slots;
        EditorGUILayout.LabelField(Application.isPlaying ? "Abilities (live)" : "Abilities", EditorStyles.boldLabel);
        if (slots.Count == 0)
        {
            EditorGUILayout.HelpBox(caster is PlayerAbilityController
                ? "Player slots are created at runtime from the AbilityStateMachines on the player (one per input binding)."
                : "No ability slots. Add Ability Definitions below (mobs also get a basic attack from their bite settings).", MessageType.Info);
            return;
        }

        if (!Application.isPlaying && slotCheckThrottle.Due)
            RefreshSlotChecks(slots);

        for (int i = 0; i < slots.Count; i++)
        {
            AbilitySlot s = slots[i];
            if (s == null)
                continue;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{i}. {s.DisplayName}", EditorStyles.boldLabel);
            bool preview = previewSlot == i;
            bool newPreview = GUILayout.Toggle(preview, new GUIContent("Preview", "Draw this ability's areas in the Scene view"), "Button", GUILayout.Width(70));
            if (newPreview != preview)
            {
                previewSlot = newPreview ? i : -1;
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            if (s.ability != null)
            {
                AbilityStats st = s.Stats;
                EditorGUILayout.LabelField($"Cast {s.ability.CastTime(st):0.##}s · CD {s.ability.Cooldown(st):0.#}s · reach {s.ability.MaxReach(st):0.#}m · ~{s.ability.EstimateDamage(st):0.#} dmg", EditorStyles.miniLabel);
                if (Application.isPlaying)
                {
                    Rect r = GUILayoutUtility.GetRect(18, 16, GUILayout.ExpandWidth(true));
                    float fill = s.Phase == AbilityPhase.Ready ? 1f - s.CooldownFraction : s.PhaseProgress;
                    EditorGUI.ProgressBar(r, fill, $"{s.Phase}  charges {s.Charges}/{s.MaxCharges}  cd {s.CooldownRemaining:0.0}s");
                }
                else if (i < slotProblems.Count && !string.IsNullOrEmpty(slotProblems[i]))
                {
                    if (slotProblemTypes[i] == MessageType.Error)
                        EditorGUILayout.HelpBox(slotProblems[i], MessageType.Error);
                    else
                        EditorGUILayout.LabelField(slotProblems[i], EditorStyles.miniLabel);
                }
            }
            else if (!(caster is PlayerAbilityController))
            {
                EditorGUILayout.HelpBox("Empty slot.", MessageType.Warning);
            }

            if (s.ability != null)
            {
                bool runtimeOnly = !EditorUtility.IsPersistent(s.ability);
                if (runtimeOnly)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(s.Grant != null ? "Absorbed at runtime (saved with the player's save data)." : "Created at runtime (converted legacy ability or automatic basic attack).",
                        EditorStyles.wordWrappedMiniLabel);
                    if (GUILayout.Button(new GUIContent("Save As Asset", "Save a copy of this runtime ability as an Ability Definition asset you can edit and assign."),
                            EditorStyles.miniButton, GUILayout.Width(92)))
                        AbilityEditorUI.SaveCopy(s.ability, AbilityEditorUI.FolderFor(caster));
                    EditorGUILayout.EndHorizontal();
                }
                AbilityEditorUI.InlineAssetEditor(s.ability, System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(caster) * 31 + i, "ability",
                    runtimeOnly ? null : "Shared asset: changes apply to every character that uses this ability. Use the slot's Modifiers for per-character differences.");
            }
            EditorGUILayout.EndVertical();
        }
    }

    private void RefreshSlotChecks(IReadOnlyList<AbilitySlot> slots)
    {
        slotProblems.Clear();
        slotProblemTypes.Clear();
        for (int i = 0; i < slots.Count; i++)
        {
            string text = null;
            MessageType type = MessageType.None;
            AbilitySlot s = slots[i];
            if (s != null && s.ability != null)
            {
                errors.Clear();
                warnings.Clear();
                s.ability.Validate(errors, warnings);
                if (errors.Count > 0)
                {
                    text = errors[0] + (errors.Count > 1 ? $" (+{errors.Count - 1} more - open the ability)" : "");
                    type = MessageType.Error;
                }
                else if (warnings.Count > 0)
                {
                    text = $"⚠ {warnings.Count} warning(s) - open the ability to see them";
                    type = MessageType.Warning;
                }
            }
            slotProblems.Add(text);
            slotProblemTypes.Add(type);
        }
    }

    protected virtual void OnSceneGUI()
    {
        var caster = (AbilityCaster)target;
        AbilitySlot slot = caster.GetSlot(previewSlot);
        if (slot == null || slot.ability == null)
            return;
        AbilityDefinition def = slot.ability;
        Transform t = caster.transform;
        AbilityStats st = slot.Stats;

        var cast = new AbilityCastInstance(null, null, slot, def, slot.modifiers);
        cast.SetOrigin(t.position, t.position + Vector3.up * 1.3f + t.forward * 0.4f, t.rotation);
        Vector3 fwd = t.forward;
        fwd.y = 0f;
        cast.AimDirection = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
        float range = def.targeting.mode == AbilityTargetingMode.Self ? 0f : def.Range(st);
        cast.AimPoint = t.position + cast.AimDirection * Mathf.Max(1f, range * 0.7f);

        shapes.Clear();
        foreach (CastAction a in def.actions)
            a?.GetTelegraphShapes(cast, shapes);

        Handles.color = new Color(1f, 0.35f, 0.2f, 0.9f);
        foreach (ResolvedShape s in shapes)
        {
            s.GetOutline(outer, inner, 40, 0.05f);
            if (outer.Count > 1) Handles.DrawAAPolyLine(3f, outer.ToArray());
            if (inner.Count > 1) Handles.DrawAAPolyLine(2f, inner.ToArray());
        }
        if (range > 0f)
        {
            Handles.color = new Color(1f, 1f, 1f, 0.35f);
            Handles.DrawWireDisc(t.position, Vector3.up, range);
            Handles.Label(t.position + cast.AimDirection * range, $"{def.DisplayName} range {range:0.#}m");
        }
    }
}
#endif
