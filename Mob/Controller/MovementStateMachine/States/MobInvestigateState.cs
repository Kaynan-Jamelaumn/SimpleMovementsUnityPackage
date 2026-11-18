using UnityEngine;

/// <summary>
/// Goes to check a noise, a half-seen character or where a lost target was last seen (running if it is alarming),
/// then looks around for a few seconds before giving up - turning on the spot, or (profile Search Radius) walking to a
/// few places around it, which is how a blind hunter finds the prey that went quiet: by bumping into it.
/// </summary>
public class MobInvestigateState : MobMovementState
{
    private Vector3 point;
    private bool looking;
    private int looks;
    private float nextLook;
    private Vector3 lookPoint;
    private bool prowling;

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
        prowling = false;
        MobMoveMode mode = Context.Memory.InvestigateUrgency > 0.6f ? MobMoveMode.Run : MobMoveMode.Walk;
        if (!Motor.SamplePoint(p, 4f, out Vector3 onMesh) || !Motor.MoveTo(onMesh, mode, 1f, true))
            StartLooking();
    }

    private void StartLooking()
    {
        looking = true;
        prowling = false;
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

        float search = Profile.searchRadius;
        if (Time.time >= nextLook || (prowling && (Motor.HasArrived(0.5f) || Motor.IsStuck)))
        {
            prowling = false;
            looks++;
            if (looks > (search > 0.5f ? 5 : 3))
            {
                Context.Memory.ClearInvestigate();
                Finish();
                return;
            }
            Vector2 r = Random.insideUnitCircle.normalized * 4f;
            lookPoint = Position + new Vector3(r.x, 0f, r.y);
            nextLook = Time.time + Random.Range(0.9f, 1.4f);
            // Searching on foot: a spot near the place it came to check.
            if (search > 0.5f)
            {
                Vector2 s = Random.insideUnitCircle * search;
                if (Motor.SamplePoint(point + new Vector3(s.x, 0f, s.y), 2f, out Vector3 spot) && Motor.MoveTo(spot, MobMoveMode.Walk, 0.6f, true))
                {
                    prowling = true;
                    nextLook = Time.time + Mathf.Clamp(search / Mathf.Max(0.5f, Motor.SpeedFor(MobMoveMode.Walk)) + 1f, 2f, 6f);
                }
            }
        }
        if (!prowling)
            Motor.FaceTowards(lookPoint);
    }
}
