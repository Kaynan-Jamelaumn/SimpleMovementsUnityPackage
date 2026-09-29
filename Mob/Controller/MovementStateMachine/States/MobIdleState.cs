using UnityEngine;

/// <summary>
/// Standing still: waits a random time (or the patrol wait after reaching a patrol point), looking around now and
/// then, and staring at anything suspicious it heard.
/// </summary>
public class MobIdleState : MobMovementState
{
    private float duration;
    private float nextLook;
    private float lookUntil;
    private Vector3 lookPoint;

    public MobIdleState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate) { }

    protected override void OnEnter()
    {
        Motor.Stop();
        bool afterPatrol = Context.Machine.PreviousStateKey == MobMovementStateMachine.EMobMovementState.Patrol;
        duration = afterPatrol ? Profile.patrolWaitTime : Profile.RandomIdleTime();
        nextLook = Time.time + Random.Range(1f, 3f);
        lookUntil = 0f;
    }

    protected override void OnUpdate(float dt)
    {
        float now = Time.time;
        if (Context.Memory.HasInvestigatePoint)
        {
            Motor.FaceTowards(Context.Memory.InvestigatePoint);
        }
        else
        {
            if (now >= nextLook)
            {
                Vector2 r = Random.insideUnitCircle.normalized * 3f;
                lookPoint = Position + new Vector3(r.x, 0f, r.y);
                lookUntil = now + Random.Range(0.8f, 1.6f);
                nextLook = now + Random.Range(2.5f, 5f);
            }
            if (now < lookUntil)
                Motor.FaceTowards(lookPoint);
        }

        if (TimeInState >= duration)
            Finish();
    }
}
