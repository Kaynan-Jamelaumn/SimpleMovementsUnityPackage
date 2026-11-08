/// <summary>
/// The housing windows that need no NPC, for a key binding or a menu button of the game: the player's own housing
/// (<see cref="MyHousingWindow"/>: their leases, renewals, who they let in, their account and messages) and the marker
/// that shows the way to a house (<see cref="HousingMarker"/>). The innkeeper's and the realtor's windows open from
/// their NPCs (<see cref="Innkeeper"/>, <see cref="Realtor"/>).
/// </summary>
public static class HousingWindows
{
    /// <summary>Opens the player's own housing (null: the local player, <see cref="PlayerLocator.Local"/>).</summary>
    public static void OpenMine(CombatEntity player)
    {
        CombatEntity p = player != null ? player : PlayerLocator.Local;
        if (p != null) MyHousingWindow.Show(p);
    }

    /// <summary>Opens the local player's own housing.</summary>
    public static void OpenMine() => OpenMine(null);

    /// <summary>Closes the player's own housing.</summary>
    public static void CloseMine() => MyHousingWindow.Hide();

    /// <summary>Opens the player's own housing, or closes it when it is open (one key for both).</summary>
    public static void ToggleMine(CombatEntity player = null)
    {
        if (MyHousingWindow.IsOpen) CloseMine();
        else OpenMine(player);
    }

    /// <summary>The player's own housing is shown.</summary>
    public static bool IsMineOpen => MyHousingWindow.IsOpen;

    /// <summary>Marks a house in the world (its land and a beam of light) until the player gets there or two minutes pass.</summary>
    public static void ShowTheWay(Property property, CombatEntity player) => HousingMarker.Show(property, player);
}
