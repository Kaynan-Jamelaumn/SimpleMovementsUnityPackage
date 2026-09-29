using System;
using UnityEngine;

/// <summary>
/// Shows a type dropdown for a <c>[SerializeReference]</c> field or list so the designer can pick which kind of
/// action/effect to add (Area Hit, Projectile, Damage, Stun...). The drawer lives in the Editor folder.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class SubclassSelectorAttribute : PropertyAttribute
{
}

/// <summary>Shows an int field as a single physics layer dropdown.</summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class LayerIndexAttribute : PropertyAttribute
{
}

/// <summary>Display name and menu path used by the type dropdown of <see cref="SubclassSelectorAttribute"/>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AbilityMenuAttribute : Attribute
{
    /// <summary>Path in the dropdown, e.g. "Damage/Damage Over Time".</summary>
    public readonly string path;
    /// <summary>Short explanation shown as the tooltip of the dropdown entry.</summary>
    public readonly string tooltip;
    /// <summary>Sort order inside its group (lower first).</summary>
    public readonly int order;

    public AbilityMenuAttribute(string path, string tooltip = "", int order = 0)
    {
        this.path = path;
        this.tooltip = tooltip;
        this.order = order;
    }
}
