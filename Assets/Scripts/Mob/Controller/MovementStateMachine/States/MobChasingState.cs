using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class MobChasingState : MobMovementState
{
    public MobChasingState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate)
    {
        MobMovementContext Context = context;
    }
    public override void EnterState()
    {
        Context.MobReference.StopAllCoroutines();
        Context.Anim?.CrossFadeInFixedTime(StateKey.ToString(), 0.5f);
        HandleChaseState();
    }
    public override void ExitState()
    {


    }
    public override void UpdateState()
    {
        // GetNextState();
    }
    public override
    MobMovementStateMachine.EMobMovementState GetNextState()
    {
        if (shouldChangeToIdleState)
        {
            shouldChangeToIdleState = false;
            return MobMovementStateMachine.EMobMovementState.Idle;
        }
        if (shouldChangeToPatrolState)
        {
            shouldChangeToPatrolState = false;
            return MobMovementStateMachine.EMobMovementState.Patrol;
        }
        if (shouldChangeToMovingState)
        {
            shouldChangeToMovingState = false;
            return MobMovementStateMachine.EMobMovementState.Moving;
        }
        return StateKey;
    }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }





    // Coroutine to manage the chase behavior.
    private IEnumerator ChasePrey()
    {
        float startTime = Time.time;

        // Continue chasing until the max chase time is reached or the prey is caught or the predator is being Preyd.
        while (Context.MobReference.CurrentChaseTarget != null && Vector3.Distance(Context.MobReference.TransformReference.position, Context.MobReference.CurrentChaseTarget.transform.position) > Context.NavMeshAgentReference.stoppingDistance)
        {
            // If max chase time is exceeded or prey is lost, stop the chase.
            if (Time.time - startTime >= Context.MobReference.MaxChaseTime || Context.MobReference.CurrentChaseTarget == null)
            {
                StopChase();
                yield break;
            }

            shouldChangeToChasingState = true;  // Update the state to Chase.
            Context.NavMeshAgentReference.SetDestination(Context.MobReference.CurrentChaseTarget.transform.position);  // Set the destination to the prey.
            if (Context.MobReference.CurrentChaseTarget != null && Context.MobReference.CurrentChaseTarget.isActiveAndEnabled)
            {
                float distance = Vector3.Distance(Context.MobReference.TransformReference.position, Context.MobReference.CurrentChaseTarget.transform.position);
                // Adicione esta verificação para morder apenas quando o predador estiver perto o suficiente.
                if (distance <= Context.MobReference.AttackDistance)
                {
                    // Mordida
                    Context.MobReference.CurrentChaseTarget.ReceiveDamage(Context.MobReference.BiteDamage);
                    yield return new WaitForSeconds(Context.MobReference.BiteCooldown);

                    // Resetar o alvo da presa, retomar a perseguição ou voltar ao estado Idle.
                    Context.MobReference.CurrentChaseTarget = null;
                    HandleChaseState();
                    CheckChaseConditions();
                }
                // Se não estiver perto o suficiente, continue perseguindo.
                else Context.NavMeshAgentReference.SetDestination(Context.MobReference.CurrentChaseTarget.transform.position);

            }
            yield return null;
        }
    }



    public void ChasePlayer(PlayerStatusController player)
    {
        shouldChangeToChasingState = true;        // Set the state to Chase.
        Context.MobReference.CurrentPlayerTarget = player;       // Set the current player target.
        Context.ActionsController.StartCoroutine(ChasePlayerCoroutine());  // Start the coroutine to handle chasing the player.
    }

    // Coroutine to manage the chase behavior with the player.
    private IEnumerator ChasePlayerCoroutine()
    {
        float startTime = Time.time;
        float timeSinceLastBit = Time.time;
        // Continue chasing until the max chase time is reached or the player is caught.
        while (Context.MobReference.CurrentPlayerTarget != null && Vector3.Distance(Context.MobReference.TransformReference.position, Context.MobReference.CurrentPlayerTarget.transform.position) > Context.NavMeshAgentReference.stoppingDistance)
        {
            if (Context.MobReference.PlayerHasMaxChaseTime)
            {
                // If max chase time is exceeded, stop the chase.
                if (Time.time - startTime >= Context.MobReference.MaxChaseTime)
                {
                    StopChase();
                    yield break;
                }
            }

            shouldChangeToChasingState = true;  // Update the state to Chase.
            Context.NavMeshAgentReference.SetDestination(Context.MobReference.CurrentPlayerTarget.transform.position);  // Set the destination to the player.

            // Check if the predator is close enough to bite the player.
            if (Time.time - timeSinceLastBit >= Context.MobReference.BiteCooldown)
            {
                // Bite logic
                Vector3 boxPosition = Context.ActionsController.MobTransform.position + Context.ActionsController.OffSetDetectionDistance;


                Vector3 size = new Vector3(Context.ActionsController.DetectionDistance.x, Context.ActionsController.DetectionDistance.y, Context.ActionsController.DetectionDistance.z); // Tamanho da caixa de detecção
                Collider[] hits = Physics.OverlapBox(boxPosition, size, Context.ActionsController.MobTransform.rotation, LayerMask.NameToLayer("player")); ; // Verificar colisões

                // Loop através de todas as colisões
                foreach (Collider hit in hits)
                {

                    // Se o jogador está dentro da caixa de detecção
                    if (hit.gameObject == Context.MobReference.CurrentPlayerTarget.gameObject)
                    {
                        // Atacar o jogador
                        timeSinceLastBit = Time.time;
                        Context.MobReference.CurrentPlayerTarget.HpManager.ConsumeHP(Context.MobReference.BiteDamage);
                        if (!Context.MobReference.IsPartialWait) yield return new WaitForSeconds(Context.MobReference.BiteCooldown);

                        // Reset the player target, resume chasing or return to the Idle state.
                        Context.MobReference.CurrentPlayerTarget = null;
                        CheckChaseConditions();
                        //HandleChaseState();
                        break; // Saia do loop
                    }
                }

            }

            yield return null;
        }
        if (!Context.MobReference.CurrentPlayerTarget) StopChase();

        // Player caught or out of range, stop the chase and return to Idle state.
    }

    // Method to stop the chase and return to Idle state.
    private void StopChase()
    {
        Context.NavMeshAgentReference.ResetPath();           // Reset the path to stop the chase movement.
        if (Context.MobReference.CurrentChaseTarget != null)
        {

            Context.MobReference.CurrentChaseTarget.CurrentPredator = null;
            Context.MobReference.CurrentChaseTarget = null;          // Reset the prey target.
        }
        else Context.MobReference.CurrentPlayerTarget = null;
        shouldChangeToIdleState = true;         // Set the state back to Idle.
    }


    public void HandleChaseState()
    {
        // If there is a current prey target, alert the prey and start chasing.
        if (Context.MobReference.CurrentPlayerTarget != null)
        {
            Context.MobReference.StartCoroutine(ChasePlayerCoroutine());
            return;
        }
        if (Context.MobReference.CurrentChaseTarget != null)
        {

            AlertPrey(Context.ActionsController);    // Alert the prey about the chase.
            Context.MobReference.StartCoroutine(ChasePrey());           // Start the coroutine to handle the chase.
        }

    }

    public void AlertPrey(MobActionsController predator)
    {
        shouldChangeToChasingState = true;  // Set the state to Chase.
        Context.MobReference.CurrentChaseTarget = null;
        Context.MobReference.CurrentPredator = predator;      // Store the reference to the current predator.
        Context.MobReference.StartCoroutine(RunFromPredator());  // Start the coroutine to handle running away from the predator.
    }

    // Coroutine that manages the prey's behavior while being chased by a predator.
    private IEnumerator RunFromPredator()
    {
        // Wait until the predator is within detection range.
        while (Context.MobReference.CurrentPredator == null || Vector3.Distance(Context.MobReference.TransformReference.position, Context.MobReference.CurrentPredator.transform.position) > Context.MobReference.DetectionRange)
        {
            yield return null;
        }

        // Predator detected, so we should run away.
        while (Context.MobReference.CurrentPredator != null && Vector3.Distance(Context.MobReference.TransformReference.position, Context.MobReference.CurrentPredator.transform.position) <= Context.MobReference.DetectionRange)
        {
            RunAwayFromPredator();   // Execute the method to run away from the predator.

            yield return null;
        }

        // Predator out of range, run to our final location and go back to idle.
        if (!Context.NavMeshAgentReference.pathPending && Context.NavMeshAgentReference.remainingDistance > Context.NavMeshAgentReference.stoppingDistance)
        {
            yield return null;
        }
        shouldChangeToIdleState = true;  // Set the state back to Idle.

    }

    // Method responsible for determining the escape destination and updating the NavMeshAgent.
    private void RunAwayFromPredator()
    {
        if (Context.NavMeshAgentReference != null && Context.NavMeshAgentReference.isActiveAndEnabled)
        {
            if (!Context.NavMeshAgentReference.pathPending && Context.NavMeshAgentReference.remainingDistance < Context.NavMeshAgentReference.stoppingDistance)
            {
                // Calculate the direction opposite to the predator and set the escape destination.
                Vector3 runDirection = Context.MobReference.TransformReference.position - Context.MobReference.CurrentPredator.transform.position;
                Vector3 escapeDestination = Context.MobReference.TransformReference.position + runDirection.normalized * (Context.MobReference.EscapeMaxDistance * 2);
                Context.NavMeshAgentReference.SetDestination(Context.MobReference.GetRandomNavMeshPosition(escapeDestination, Context.MobReference.EscapeMaxDistance));
            }
            if (Context.MobReference.CurrentPredator != null && Vector3.Distance(Context.MobReference.TransformReference.position, Context.MobReference.CurrentPredator.transform.position) > Context.MobReference.DetectionRange)
            {
                StopChase();
            }
        }
    }


}