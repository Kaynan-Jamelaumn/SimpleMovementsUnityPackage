using System;

/// <summary>Where <see cref="AutoAssignAttribute"/> looks for the object of a field.</summary>
[Flags]
public enum AutoAssignFrom
{
    /// <summary>The same GameObject.</summary>
    Self = 1,
    /// <summary>The children (and their children) of the GameObject.</summary>
    Children = 2,
    /// <summary>The parents of the GameObject, up to the root.</summary>
    Parents = 4,
    /// <summary>Anywhere under the root of the hierarchy (prefab root, or the scene root object).</summary>
    Hierarchy = 8,
    /// <summary>Anywhere in the open scenes (last resort; only when exactly one candidate exists).</summary>
    Scene = 16,
    /// <summary>Self, children, parents, then the rest of the hierarchy (the default).</summary>
    Nearby = Self | Children | Parents | Hierarchy,
}

/// <summary>
/// Lets the editor fill this field by itself (Tools ▸ SimpleMovements ▸ Auto-Assign, the
/// "Auto-Assign Missing References" context menu of every component, and the Project Validator window). A field is only
/// filled when it is empty, and only when one object clearly fits: the single candidate, or the one whose name matches
/// the field name (an <c>Animator animator</c> takes the Animator, a <c>Transform weaponSocket</c> takes "WeaponSocket").
/// </summary>
/// <remarks>
/// The attribute is optional: the auto-assigner also fills plain fields when the match is unambiguous. Use it to look
/// somewhere else than the default (<see cref="AutoAssignFrom.Nearby"/>), to point at a child by name
/// (<see cref="ChildName"/>), or to mark a field the auto-assigner must leave alone (<see cref="Never"/>).
/// It does nothing at runtime.
/// </remarks>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class AutoAssignAttribute : Attribute
{
    /// <summary>Where to look.</summary>
    public AutoAssignFrom From { get; }

    /// <summary>Name of the GameObject to take (case and spaces ignored); empty = decided by the field name.</summary>
    public string ChildName { get; set; }

    /// <summary>Never fill this field automatically (for fields that must stay empty or are set by code).</summary>
    public bool Never { get; set; }

    public AutoAssignAttribute() : this(AutoAssignFrom.Nearby) { }

    public AutoAssignAttribute(AutoAssignFrom from) => From = from;
}

/// <summary>
/// The validators report this field when it is empty (Tools ▸ SimpleMovements ▸ Validate, the Project Validator window).
/// Use it on references a component cannot work without and does not find by itself at runtime.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class RequiredReferenceAttribute : Attribute
{
    /// <summary>Optional explanation shown with the issue ("needed to aim projectiles").</summary>
    public string Reason { get; }

    public RequiredReferenceAttribute() { }

    public RequiredReferenceAttribute(string reason) => Reason = reason;
}
