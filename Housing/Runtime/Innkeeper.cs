using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The "Rent a room" option of an NPC: the rooms of an inn by the night - which are free and what a night costs (it
/// follows how full the inn has been lately) - the player's own stay (staying longer, giving it up, who they let in),
/// their messages and anything held for them. Everything is asked of the server (offline: done at once).
/// </summary>
[AddComponentMenu("SimpleMovements/Housing/Innkeeper")]
public class Innkeeper : NPCBehaviour
{
    [Header("Innkeeper")]
    [Tooltip("The inn this innkeeper lets. Empty: the inn the NPC stands in (else the nearest).")]
    public Inn inn;

    private void Reset() => optionLabel = "Rent a room";

    /// <summary>The inn served: the one set, else the one the NPC stands in or is nearest to.</summary>
    public Inn InnServed
    {
        get
        {
            if (inn != null) return inn;
            Inn parent = GetComponentInParent<Inn>();
            if (parent != null) return parent;
            Inn best = null;
            float bestD = float.MaxValue;
            // Playing: the inns that woke up; in the editor (validation), the inns of the scene.
            IEnumerable<Inn> inns = Application.isPlaying
                ? (IEnumerable<Inn>)HousingDirector.Inns
                : FindObjectsByType<Inn>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Inn i in inns)
            {
                if (i == null || i.gameObject.scene != gameObject.scene) continue;
                float d = (i.transform.position - transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }
    }

    public override bool IsAvailable(NPCInteractionSession session, out string reason)
    {
        reason = "";
        if (!isActiveAndEnabled) return false;
        if (InnServed == null) { reason = "No inn"; return false; }
        return true;
    }

    public override void Begin(NPCInteractionSession session) => InnWindow.Open(this, session);

    public override void End(NPCInteractionSession session) => InnWindow.CloseFor(this, session);

    public override void Validate(List<string> errors, List<string> warnings)
    {
        Inn served = InnServed;
        if (served == null) errors.Add("No inn to let: set Inn, or put the NPC in an inn.");
        else if (served.rooms.Count == 0) warnings.Add($"'{served.DisplayName}' has no rooms.");
        NPC npc = GetComponent<NPC>();
        if (npc != null && npc.MaxPlayersAtOnce > 0) warnings.Add("An innkeeper should serve everyone at once (Max Players At Once 0): one player could keep others waiting.");
    }
}
