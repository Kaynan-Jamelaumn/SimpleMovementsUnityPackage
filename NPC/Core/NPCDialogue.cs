using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The "Talk" option of an NPC: lines the NPC says one after another (or one at random), shown in the dialogue window.
/// Use several on one NPC for different topics ("Ask about the mine", "Rumours"), each with its own label.
/// </summary>
[AddComponentMenu("SimpleMovements/NPC/NPC Dialogue")]
public class NPCDialogue : NPCBehaviour
{
    [Header("Dialogue")]
    [Tooltip("What the NPC says, one page per line.")]
    [SerializeField, TextArea(2, 5)] private List<string> lines = new List<string> { "These roads are not safe after dark." };
    [Tooltip("Say ONE line picked at random each time instead of all of them in order.")]
    [SerializeField] private bool randomLine;
    [Tooltip("Raised when the player has read the dialogue (the player object).")]
    public UnityEvent<GameObject> onFinished = new UnityEvent<GameObject>();

    private readonly List<string> shown = new List<string>();

    public IReadOnlyList<string> Lines => lines;

    private void Reset() => optionLabel = "Talk";

    public override bool IsAvailable(NPCInteractionSession session, out string reason)
    {
        reason = "";
        return isActiveAndEnabled && lines != null && lines.Exists(l => !string.IsNullOrWhiteSpace(l));
    }

    public override void Begin(NPCInteractionSession session)
    {
        shown.Clear();
        var valid = lines.FindAll(l => !string.IsNullOrWhiteSpace(l));
        if (randomLine && valid.Count > 0)
            shown.Add(valid[Random.Range(0, valid.Count)]);
        else
            shown.AddRange(valid);
        NPCDialogueWindow window = NPCDialogueWindow.For(session, Npc.DialogueWindowPrefab);
        window.ShowLines(session, shown, () =>
        {
            if (session.Player != null)
                onFinished?.Invoke(session.Player);
            session.BehaviourFinished(this);
        });
    }

    public override void End(NPCInteractionSession session) => NPCDialogueWindow.HideFor(session);

    public override void Validate(List<string> errors, List<string> warnings)
    {
        if (lines == null || !lines.Exists(l => !string.IsNullOrWhiteSpace(l)))
            warnings.Add($"{OptionLabel} (NPC Dialogue) has no lines: the option is hidden.");
    }
}
