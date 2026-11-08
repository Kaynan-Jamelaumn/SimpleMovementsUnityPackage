using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The "Property" option of an NPC: the city's houses to rent - where each is, what it asks and for which terms, its
/// house and land, its garden, whether it is free - the player's sealed order (up to a few houses in order of
/// preference: it wins at most one, at what they offer), their leases (renewal, protection, who they let in), their rent
/// account, their messages and anything held for them. Everything is asked of the server (offline: done at once).
/// </summary>
[AddComponentMenu("SimpleMovements/Housing/Realtor")]
public class Realtor : NPCBehaviour
{
    [Header("Realtor")]
    [Tooltip("The properties this realtor lets. Empty: every property of the city.")]
    public List<Property> listings = new List<Property>();
    [Tooltip("Only the properties of this district (empty: all).")]
    public string district = "";

    private void Reset() => optionLabel = "Property";

    public override void Begin(NPCInteractionSession session) => RealtorWindow.Open(this, session);

    public override void End(NPCInteractionSession session) => RealtorWindow.CloseFor(this, session);

    public override void Validate(List<string> errors, List<string> warnings)
    {
        if (listings.Exists(p => p == null)) warnings.Add("A listed property is missing (an empty slot in Listings).");
        NPC npc = GetComponent<NPC>();
        if (npc != null && npc.MaxPlayersAtOnce > 0) warnings.Add("A realtor should serve everyone at once (Max Players At Once 0): one player could keep others from bidding.");
    }
}
