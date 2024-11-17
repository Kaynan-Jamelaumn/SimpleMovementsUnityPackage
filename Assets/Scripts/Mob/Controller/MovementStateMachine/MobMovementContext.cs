using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class MobMovementContext
{
    private MobActionsController actionsController;
    private Mob mob;
    private Animator animator;
    private MobStatusController statusController;
    private NavMeshAgent navMeshAgent;

    public MobMovementContext(Mob mob, MobActionsController actionsController, Animator animator, MobStatusController statusController, NavMeshAgent navMeshAgent) {
        this.actionsController = actionsController;
        this.mob = mob;
        this.animator = animator;
        this.statusController = statusController;
        this.navMeshAgent = navMeshAgent;

    }
    public MobActionsController ActionsController => actionsController;
    public Mob MobReference => mob;
    public Animator Anim => animator;
    public MobStatusController StatusController => statusController;
    public NavMeshAgent NavMeshAgentReference => navMeshAgent;


}

