using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Context class that provides references to various components used in the mob's movement state machine.
/// </summary>
public class MobMovementContext
{
    private MobActionsController actionsController;
    private Mob mob;
    private Animator animator;
    private MobStatusController statusController;
    private NavMeshAgent navMeshAgent;

    /// <summary>
    /// Constructor for the MobMovementContext class.
    /// Initializes the context with the necessary component references.
    /// </summary>
    /// <param name="mob">The mob reference.</param>
    /// <param name="actionsController">The actions controller reference.</param>
    /// <param name="animator">The animator reference.</param>
    /// <param name="statusController">The status controller reference.</param>
    /// <param name="navMeshAgent">The NavMeshAgent reference.</param>
    public MobMovementContext(Mob mob, MobActionsController actionsController, Animator animator, MobStatusController statusController, NavMeshAgent navMeshAgent)
    {
        this.actionsController = actionsController;
        this.mob = mob;
        this.animator = animator;
        this.statusController = statusController;
        this.navMeshAgent = navMeshAgent;
    }

    /// <summary>
    /// Gets the actions controller reference.
    /// </summary>
    public MobActionsController ActionsController => actionsController;

    /// <summary>
    /// Gets the mob reference.
    /// </summary>
    public Mob MobReference => mob;

    /// <summary>
    /// Gets the animator reference.
    /// </summary>
    public Animator Anim => animator;

    /// <summary>
    /// Gets the status controller reference.
    /// </summary>
    public MobStatusController StatusController => statusController;

    /// <summary>
    /// Gets the NavMeshAgent reference.
    /// </summary>
    public NavMeshAgent NavMeshAgentReference => navMeshAgent;
}
