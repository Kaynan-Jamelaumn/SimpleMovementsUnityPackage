using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optional interface for a script attached by <see cref="AttachScriptTrait"/>: it is told when the trait (or the item
/// running it) attaches it, when the strength changes (armor set enhancements) and when it is about to be removed.
/// </summary>
public interface ITraitAttachedScript
{
    void OnTraitAttached(TraitContext ctx, float strength);
    void OnTraitStrengthChanged(float strength);
    void OnTraitDetached();
}

/// <summary>What <see cref="AttachScriptTrait"/> puts on the character.</summary>
public enum AttachScriptMode
{
    /// <summary>Adds the script as a component (default values) and removes it afterwards.</summary>
    AddScript,
    /// <summary>Spawns a prefab under the character (its scripts keep the values set on the prefab) and destroys it afterwards.</summary>
    SpawnPrefab,
    /// <summary>Turns on a script the character already has (disabled) and turns it off again afterwards.</summary>
    EnableExisting,
}

/// <summary>
/// Puts one of your own scripts on the character while the trait is owned (or while the item that runs it is worn,
/// through Equipment ▸ Passive Behaviour): add a component, spawn a prefab with configured scripts, or switch on a
/// script the character already carries. Two sources asking for the same script share one copy; it goes away when
/// the last one ends.
/// </summary>
[Serializable, AbilityMenu("Custom/Attach Script", "Adds one of your scripts (or a prefab with scripts) to the character while the trait is owned, and removes it afterwards.", 0)]
public class AttachScriptTrait : TraitBehaviour
{
    [Tooltip("Add a script, spawn a prefab with scripts, or turn on a script the character already has.")]
    public AttachScriptMode mode = AttachScriptMode.AddScript;
    [Tooltip("The script (a MonoBehaviour) to add or turn on. Drag the script file here or pick it from the list.")]
    [ComponentTypeName] public string script = "";
    [Tooltip("Spawn Prefab: the prefab spawned under the character (its scripts keep the values set on it).")]
    public GameObject prefab;
    [Tooltip("Name of a child (a bone such as 'Hand_R', or 'Body') to put it on. Empty = the character's root.")]
    public string attachTo = "";
    [Tooltip("Add Script: when the character already has this script, turn it on instead of adding a second one (and turn it off afterwards).")]
    public bool useExistingIfPresent = true;

    [NonSerialized] private Holder holder;

    public override string Describe()
    {
        if (mode == AttachScriptMode.SpawnPrefab)
            return prefab != null ? $"Gives {prefab.name}." : "";
        Type t = ScriptType;
        return t != null ? $"Gives {AbilityTypeNames.Nice(t)}." : "";
    }

    public override string LiveStatus => holder == null ? "Not attached" : holder.Status;

    /// <summary>The script's type (null when empty or missing).</summary>
    public Type ScriptType => string.IsNullOrEmpty(script) ? null : Type.GetType(script, false);

    protected override bool ReadsStrengthLive => true;

    public override void OnAdded(TraitContext ctx)
    {
        Transform target = Target(ctx);
        if (target == null)
            return;
        holder = Holder.Acquire(this, target);
        holder?.Attached(ctx, multiplier);
    }

    public override void OnRemoved(TraitContext ctx)
    {
        holder?.Release();
        holder = null;
    }

    public override bool OnStrengthChanged(TraitContext ctx, float newMultiplier)
    {
        multiplier = newMultiplier;
        holder?.StrengthChanged(newMultiplier);
        return true;
    }

    public override void Validate(List<string> errors, List<string> warnings)
    {
        if (mode == AttachScriptMode.SpawnPrefab)
        {
            if (prefab == null)
                errors.Add("Attach Script: no prefab.");
            return;
        }
        if (string.IsNullOrEmpty(script))
            errors.Add("Attach Script: no script selected.");
        else if (ScriptType == null)
            errors.Add($"Attach Script: the script '{script}' no longer exists (renamed or deleted?).");
        else if (!typeof(MonoBehaviour).IsAssignableFrom(ScriptType) || ScriptType.IsAbstract)
            errors.Add($"Attach Script: '{ScriptType.Name}' is not a script that can be added to an object.");
    }

    private Transform Target(TraitContext ctx)
    {
        Transform root = ctx != null ? ctx.Root : null;
        if (root == null || string.IsNullOrEmpty(attachTo))
            return root;
        Transform found = FindDeep(root, attachTo);
        return found != null ? found : root;
    }

    private static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name)
            return t;
        for (int i = 0; i < t.childCount; i++)
        {
            Transform r = FindDeep(t.GetChild(i), name);
            if (r != null)
                return r;
        }
        return null;
    }

    // ------------------------------------------------------------------ shared copies
    /// <summary>One attached script (or spawned prefab) on one object, shared by every source that asked for it.</summary>
    private sealed class Holder
    {
        private static readonly Dictionary<(Transform, object), Holder> live = new Dictionary<(Transform, object), Holder>();

        private (Transform, object) key;
        private int users;
        private Behaviour component;
        private GameObject spawned;
        private bool enabledByUs, addedByUs, wasEnabled;

        public string Status =>
            spawned != null ? $"{spawned.name} spawned" + (users > 1 ? $" ({users} sources)" : "") :
            component != null ? $"{component.GetType().Name} {(addedByUs ? "attached" : "turned on")}" + (users > 1 ? $" ({users} sources)" : "") :
            "Not attached";

        public static Holder Acquire(AttachScriptTrait t, Transform target)
        {
            // The same prefab or the same script on the same object is one shared copy.
            object id = t.mode == AttachScriptMode.SpawnPrefab ? t.prefab : (object)(string.IsNullOrEmpty(t.script) ? null : t.script);
            if (id == null || (id is GameObject g && g == null))
                return null;
            (Transform, object) key = (target, id);
            if (live.TryGetValue(key, out Holder h) && h.Alive)
            {
                h.users++;
                return h;
            }
            h = new Holder { key = key, users = 1 };
            if (!h.Create(t, target))
                return null;
            live[key] = h;
            return h;
        }

        private bool Alive => spawned != null || component != null;

        private bool Create(AttachScriptTrait t, Transform target)
        {
            if (t.mode == AttachScriptMode.SpawnPrefab)
            {
                if (t.prefab == null)
                    return false;
                spawned = UnityEngine.Object.Instantiate(t.prefab, target, false);
                return true;
            }
            Type type = t.ScriptType;
            if (type == null || !typeof(Behaviour).IsAssignableFrom(type) || type.IsAbstract)
            {
                Debug.LogWarning($"[Traits] Attach Script: '{t.script}' is not a script that can be added.", target);
                return false;
            }
            var existing = target.GetComponent(type) as Behaviour;
            if (existing != null && (t.mode == AttachScriptMode.EnableExisting || t.useExistingIfPresent))
            {
                component = existing;
                wasEnabled = existing.enabled;
                existing.enabled = true;
                enabledByUs = true;
                return true;
            }
            if (t.mode == AttachScriptMode.EnableExisting)
            {
                Debug.LogWarning($"[Traits] Attach Script: {target.name} has no {type.Name} to turn on.", target);
                return false;
            }
            component = target.gameObject.AddComponent(type) as Behaviour;
            addedByUs = component != null;
            return addedByUs;
        }

        public void Attached(TraitContext ctx, float strength)
        {
            foreach (ITraitAttachedScript s in Scripts())
                s.OnTraitAttached(ctx, strength);
        }

        public void StrengthChanged(float strength)
        {
            foreach (ITraitAttachedScript s in Scripts())
                s.OnTraitStrengthChanged(strength);
        }

        public void Release()
        {
            if (--users > 0)
                return;
            live.Remove(key);
            foreach (ITraitAttachedScript s in Scripts())
            {
                try { s.OnTraitDetached(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            if (spawned != null)
                UnityEngine.Object.Destroy(spawned);
            else if (component != null && addedByUs)
                UnityEngine.Object.Destroy(component);
            else if (component != null && enabledByUs)
                component.enabled = wasEnabled; // back to how it was before
            spawned = null;
            component = null;
        }

        private IEnumerable<ITraitAttachedScript> Scripts()
        {
            if (spawned != null)
            {
                foreach (ITraitAttachedScript s in spawned.GetComponentsInChildren<ITraitAttachedScript>(true))
                    yield return s;
            }
            else if (component is ITraitAttachedScript s)
                yield return s;
        }
    }
}

/// <summary>A string field holding a script's type name, drawn as a script picker.</summary>
public sealed class ComponentTypeNameAttribute : PropertyAttribute { }
