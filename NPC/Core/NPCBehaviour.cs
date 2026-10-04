using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One thing an <see cref="NPC"/> can do for the player: trade (<see cref="Merchant"/>), talk (<see cref="NPCDialogue"/>),
/// and anything you add - quests, training, repairs, travel. Add it next to the NPC component; every enabled behaviour
/// becomes an option of the NPC (an NPC with a single option opens it directly).
/// <para>A new kind of NPC is a new behaviour: override <see cref="Begin"/> (open your window, using the session's
/// player, inventory and wallet) and call <see cref="NPCInteractionSession.BehaviourFinished"/> when it is done.</para>
/// </summary>
[RequireComponent(typeof(NPC))]
public abstract class NPCBehaviour : MonoBehaviour
{
    [Header("Option")]
    [Tooltip("Label of the option in the NPC's menu, and the word in the interaction prompt when it is the NPC's only option.")]
    [SerializeField] protected string optionLabel = "Talk";
    [Tooltip("Optional icon on the option button.")]
    [SerializeField] protected Sprite optionIcon;
    [Tooltip("Options are listed by this (lowest first).")]
    [SerializeField] protected int optionOrder;

    private NPC npc;

    public NPC Npc => npc != null ? npc : (npc = GetComponent<NPC>());
    public virtual string OptionLabel => string.IsNullOrWhiteSpace(optionLabel) ? GetType().Name : optionLabel;
    /// <summary>The word of the interaction prompt when this is the NPC's only option ("Trade").</summary>
    public virtual string PromptVerb => OptionLabel;
    public Sprite OptionIcon => optionIcon;
    public int OptionOrder => optionOrder;

    /// <summary>Can the option be used in this session? <paramref name="reason"/> is shown on a disabled option.</summary>
    public virtual bool IsAvailable(NPCInteractionSession session, out string reason)
    {
        reason = "";
        return isActiveAndEnabled;
    }

    /// <summary>The player chose this option: open its window (the session is open and this is its active behaviour).</summary>
    public abstract void Begin(NPCInteractionSession session);

    /// <summary>The session ends or moves to another option: close whatever <see cref="Begin"/> opened.</summary>
    public virtual void End(NPCInteractionSession session) { }

    /// <summary>Configuration problems shown in the NPC inspector.</summary>
    public virtual void Validate(List<string> errors, List<string> warnings) { }

    protected virtual void OnEnable() => Npc?.RefreshBehaviours();

    protected virtual void OnDisable()
    {
        if (npc != null)
            npc.OnBehaviourDisabled(this);
    }
}
