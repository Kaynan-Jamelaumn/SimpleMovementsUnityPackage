using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Between animal husbandry and Housing: which property a pen belongs to, and what a player may do in it.
/// <list type="bullet">
/// <item>A pen belongs to the <see cref="Property"/> that lists it (its children are listed by themselves): it is that
/// house's pen, on that house's land, next to that house's garden.</item>
/// <item>Rights follow the garden's: the tenant and those they let work the garden may tend the pen and keep animals in
/// it (<see cref="AnimalRights.Tend"/>, <see cref="AnimalRights.Keep"/>); anyone may look over the fence. A pen on no
/// property is public.</item>
/// <item>Where the rules run, an owner who can no longer keep animals in the pen (the lease ended, they are no longer let
/// in) has them boarded - checked against the housing books by the owner's key, so it works while they are offline.</item>
/// </list>
/// </summary>
public static class AnimalHousing
{
    private static readonly Dictionary<AnimalPen, Property> owners = new Dictionary<AnimalPen, Property>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => owners.Clear();

    /// <summary>Forget what was found (a property or pen moved, appeared or went).</summary>
    public static void Forget(AnimalPen pen)
    {
        if (pen != null) owners.Remove(pen);
        else owners.Clear();
    }

    /// <summary>The property a pen belongs to (null: public land).</summary>
    public static Property PropertyOf(AnimalPen pen)
    {
        if (pen == null) return null;
        if (owners.TryGetValue(pen, out Property known) && known != null && (known.pens.Contains(pen) || known.includeChildren)) return known;
        Property p = pen.GetComponentInParent<Property>(true);
        // A child pen is its property's (Include Children lists it when the property wakes); otherwise only a listed one.
        if (p != null && !p.pens.Contains(pen) && !p.includeChildren) p = null;
        if (p == null)
            foreach (Property q in HousingDirector.Properties)
                if (q != null && q.pens.Contains(pen))
                {
                    p = q;
                    break;
                }
        if (p != null) owners[pen] = p;
        return p;
    }

    public static AnimalRights FromAccess(HousingAccess a)
    {
        AnimalRights r = AnimalRights.Look;
        if ((a & HousingAccess.Garden) != 0) r |= AnimalRights.Tend | AnimalRights.Keep | AnimalRights.CareForOthers;
        return r;
    }

    /// <summary>
    /// What everyone may do in a pen on public land: look, stock its feeder and trough, keep their own animals there - but
    /// never feed another player's animal by hand or clear its remains (nobody let them).
    /// </summary>
    public const AnimalRights Public = AnimalRights.Look | AnimalRights.Tend | AnimalRights.Keep;

    /// <summary>What a player may do in a pen now (where the rules run: the housing books; on a client: what the server told it).</summary>
    public static AnimalRights RightsOf(AnimalPen pen, CombatEntity player)
    {
        if (pen == null || player == null) return AnimalRights.None;
        Property p = PropertyOf(pen);
        if (p == null) return Public;
        if (p.OutOfOrder) return AnimalRights.Look;
        return FromAccess(HousingGate.AccessOf(new HousingPlace(p), player));
    }

    /// <summary>
    /// What the holder of a key may do on a property, from the housing books (where the rules run; works while they are
    /// offline). False when it cannot be known now (the books are closed or not loaded): nothing should be decided then.
    /// </summary>
    public static bool TryRightsOfKey(string propertyId, string key, out AnimalRights rights)
    {
        rights = AnimalRights.All;
        if (string.IsNullOrEmpty(propertyId)) return true;
        if (string.IsNullOrEmpty(key)) return false;
        HousingBook book = HousingDirector.Book;
        if (book == null || !string.IsNullOrEmpty(HousingDirector.Closed)) return false;
        rights = FromAccess(book.AccessOf(propertyId, null, key, book.Now));
        return true;
    }

    /// <summary>The property's id of a pen ("" on public land).</summary>
    public static string PropertyIdOf(AnimalPen pen)
    {
        Property p = PropertyOf(pen);
        return p != null ? p.PropertyId : "";
    }
}
