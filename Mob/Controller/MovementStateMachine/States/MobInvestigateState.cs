using UnityEngine;

/// <summary>
/// Goes to check a noise, a half-seen character or where a lost target was last seen (running if it is alarming),
/// then looks around for a few seconds before giving up.
/// </summary>
public class MobInvestigateState : MobMovementState
{
    private Vector3 point;
    private bool looking;
    private int looks;
    private float nextLook;
    private Vector3 lookPoint;

    public MobInvestigateState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate) { }

    protected override void OnEnter()
    {
        looking = false;
        looks = 0;
        if (!Context.Memory.HasInvestigatePoint)
        {
            Finish();
            return;
        }
        GoTo(Context.Memory.InvestigatePoint);
    }

    private void GoTo(Vector3 p)
    {
        point = p;
        looking = false;
        MobMoveMode mode = Context.Memory.InvestigateUrgency > 0.6f ? MobMoveMode.Run : MobMoveMode.Walk;
        if (!Motor.SamplePoint(p, 4f, out Vector3 onMesh) || !Motor.MoveTo(onMesh, mode, 1f, true))
            StartLooking();
    }

    private void StartLooking()
    {
        looking = true;
        looks = 0;
        nextLook = Time.time;
        Motor.Stop();
    }

    protected override void OnUpdate(float dt)
    {
        if (!Context.Memory.HasInvestigatePoint)
        {
            Finish();
            return;
        }
        // Something new to check.
        if ((Context.Memory.InvestigatePoint - point).sqrMagnitude > 9f)
            GoTo(Context.Memory.InvestigatePoint);

        if (!looking)
        {
            if (Motor.HasArrived(0.5f) || Motor.IsStuck || Motor.IsOffMesh || TimeInState > 15f)
                StartLooking();
            return;
        }

        if (Time.time >= nextLook)
        {
            looks++;
            if (looks > 3)
            {
                Context.Memory.ClearInvestigate();
                Finish();
                return;
            }
            Vector2 r = Random.insideUnitCircle.normalized * 4f;
            lookPoint = Position + new Vector3(r.x, 0f, r.y);
            nextLook = Time.time + Random.Range(0.9f, 1.4f);
        }
        Motor.FaceTowards(lookPoint);
    }
}
