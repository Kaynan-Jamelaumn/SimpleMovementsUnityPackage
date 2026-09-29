using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Moves the caster: dashes, leaps, blinks, retreats and charges.</summary>
[Serializable, AbilityMenu("Movement/Dash, Leap, Blink or Charge", "Moves the caster: dash to the target, dash along the aim, leap in an arc, teleport, jump back, or charge through enemies. Optional hits along the path and on landing.", 0)]
public class MovementAction : CastAction
{
    [Tooltip("Dash To Target: rush to the target, stopping Stop Distance before it.\nDash Along Aim: Distance along the aim.\nLeap: jump in an arc to the aimed point.\nBlink: teleport to the aimed point.\nRetreat: jump back away from the target.\nCharge: dash through the aim direction hitting everything on the way.")]
    public MovementActionMode mode = MovementActionMode.DashToTarget;
    [Tooltip("Distance for Dash Along Aim, Retreat, Charge, and Blink/Leap without a point (metres, multiplied by the Range modifier). Dash To Target and Leap are limited to the ability's Range.")]
    [Min(0f)] public float distance = 6f;
    [Tooltip("Speed in metres per second for dashes, retreats and charges.")]
    [Min(1f)] public float speed = 20f;
    [Tooltip("Dash To Target / Leap: stop this far from the target's body (metres).")]
    [Min(0f)] public float stopDistance = 0.8f;
    [Tooltip("Leap: height of the arc (metres). Retreat: small hop height.")]
    [Min(0f)] public float arcHeight = 2.5f;
    [Tooltip("Leap: seconds in the air.")]
    [Min(0.1f)] public float leapDuration = 0.7f;
    [Tooltip("The caster cannot be damaged while moving.")]
    public bool invulnerable = false;
    [Tooltip("Stop at walls and stay on the NavMesh (Leap may still jump over low obstacles).")]
    public bool respectObstacles = true;

    [Header("Charge / Path Hit")]
    [Tooltip("Charge (and optionally dashes): effects on characters touched along the way, each once.")]
    public HitSettings pathHit = new HitSettings(TargetFilter.Enemies);
    [Tooltip("Width of the path hit (metres).")]
    [Min(0.2f)] public float pathWidth = 1.6f;
    [Tooltip("Stop the charge at the first enemy hit.")]
    public bool stopOnFirstHit = false;

    [Header("Landing Hit (slam)")]
    [Tooltip("Area hit when the movement ends. Set the shape radius to 0 for none.")]
    public HitShape landingShape = new HitShape { type = HitShapeType.Circle, radius = 0f };
    public HitSettings landingHit = new HitSettings(TargetFilter.Enemies);

    [Header("Feedback")]
    public GameObject startVfx;
    public GameObject landVfx;
    public AudioClip landSound;

    public MovementAction()
    {
        anchor = ActionAnchor.Caster;
    }

    private bool HasPathHit => pathHit != null && pathHit.effects.Count > 0 && (mode == MovementActionMode.Charge || mode == MovementActionMode.DashAlongAim || mode == MovementActionMode.DashToTarget);
    private bool HasLandingHit => landingShape != null && landingHit != null && landingHit.effects.Count > 0 && landingShape.Reach() > 0.01f;

    public override void Execute(AbilityCastInstance cast)
    {
        CombatEntity self = cast.CasterEntity;
        if (self == null || !self.IsAlive)
            return;
        AbilityStats s = cast.Stats;
        Vector3 from = self.Position;
        Vector3 dest = Destination(cast, from, out bool arc);
        if (respectObstacles)
            dest = ClampPath(self, from, dest, arc);

        Vector3 offset = dest - from;
        float flat = new Vector2(offset.x, offset.z).magnitude;
        if (startVfx != null)
            AbilityPool.PlayVfx(startVfx, self.BasePosition, Quaternion.LookRotation(cast.AimDirection));

        if (mode == MovementActionMode.Blink)
        {
            Teleport(self, dest);
            OnArrived(cast);
            return;
        }
        if (flat < 0.1f && Mathf.Abs(offset.y) < 0.1f)
        {
            OnArrived(cast);
            return;
        }

        float seconds = mode == MovementActionMode.Leap ? leapDuration : flat / Mathf.Max(1f, speed);
        float height = mode == MovementActionMode.Leap ? arcHeight : (mode == MovementActionMode.Retreat ? Mathf.Min(arcHeight, 0.8f) : 0f);
        if (invulnerable)
            self.SetInvulnerable(seconds + 0.1f);

        ForcedMovement fm = ForcedMovement.GetOrAdd(self);
        PathHitter hitter = HasPathHit ? new PathHitter(this, cast, fm) : null;
        fm.Begin(offset, seconds, height, mode != MovementActionMode.Retreat, () =>
        {
            if (hitter != null)
                hitter.Finish();
            OnArrived(cast);
        });
        if (hitter != null)
            AbilityRuntime.Add(hitter);
        cast.EmitNoise(from, 0.6f);
    }

    private Vector3 Destination(AbilityCastInstance cast, Vector3 from, out bool arc)
    {
        AbilityStats s = cast.Stats;
        float dist = distance * s.range;
        float range = cast.Definition.Range(s);
        Vector3 aim = cast.AimDirection;
        arc = false;
        CombatEntity target = cast.Target != null && cast.Target.IsAlive ? cast.Target : null;
        float bodies = (cast.CasterEntity != null ? cast.CasterEntity.Radius : 0.5f) + (target != null ? target.Radius : 0f);

        switch (mode)
        {
            case MovementActionMode.DashToTarget:
            {
                Vector3 goal = target != null ? target.Position : cast.AimPoint;
                Vector3 d = goal - from;
                d.y = 0f;
                float len = d.magnitude;
                float travel = Mathf.Clamp(len - stopDistance - bodies, 0f, Mathf.Max(range, dist));
                return from + (len > 1e-3f ? d / len : aim) * travel;
            }
            case MovementActionMode.Leap:
            {
                arc = true;
                Vector3 goal = target != null ? target.Position : cast.AimPoint;
                Vector3 d = goal - from;
                d.y = 0f;
                float len = d.magnitude;
                float travel = Mathf.Clamp(len - (target != null ? stopDistance + bodies : 0f), 0f, Mathf.Max(range, dist));
                Vector3 p = from + (len > 1e-3f ? d / len : aim) * travel;
                return CombatQuery.SnapToGround(p, 4f, 10f);
            }
            case MovementActionMode.Blink:
            {
                Vector3 goal = cast.Definition.targeting.mode == AbilityTargetingMode.Point || cast.Definition.targeting.mode == AbilityTargetingMode.Unit
                    ? cast.AimPoint
                    : from + aim * dist;
                return CombatQuery.SnapToGround(goal, 4f, 10f);
            }
            case MovementActionMode.Retreat:
            {
                Vector3 away = target != null ? CombatQuery.FlatDirection(target.Position, from, -aim) : -aim;
                return from + away * dist;
            }
            default: // DashAlongAim, Charge
                return from + aim * dist;
        }
    }

    /// <summary>Shortens the move at walls; keeps NavMesh agents on the NavMesh.</summary>
    private static Vector3 ClampPath(CombatEntity self, Vector3 from, Vector3 dest, bool arc)
    {
        NavMeshAgent agent = self.Agent;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (arc)
                return CombatQuery.SampleNavMesh(dest, 3f, out Vector3 landing, agent.areaMask) ? landing : from;
            if (NavMesh.Raycast(from, dest, out NavMeshHit hit, agent.areaMask))
                return hit.position;
            return CombatQuery.SampleNavMesh(dest, 1f, out Vector3 onMesh, agent.areaMask) ? onMesh : dest;
        }
        if (arc)
            return dest;
        Vector3 chest = Vector3.up * (self.Height * 0.5f);
        Vector3 d = dest - from;
        float len = d.magnitude;
        if (len > 0.01f && CombatQuery.RaycastObstacle(from + chest, d / len, len + self.Radius, out RaycastHit wall))
            return from + d / len * Mathf.Max(0f, wall.distance - self.Radius - 0.1f);
        return dest;
    }

    private static void Teleport(CombatEntity self, Vector3 dest)
    {
        NavMeshAgent agent = self.Agent;
        if (agent != null && agent.enabled)
        {
            agent.Warp(dest);
            return;
        }
        CharacterController cc = self.Controller;
        if (cc != null && cc.enabled)
        {
            cc.enabled = false;
            self.transform.position = dest;
            cc.enabled = true;
            return;
        }
        if (self.Body != null && !self.Body.isKinematic)
            self.Body.position = dest;
        self.transform.position = dest;
    }

    private void OnArrived(AbilityCastInstance cast)
    {
        CombatEntity self = cast.CasterEntity;
        if (self == null || cast.Interrupted && cast.InterruptReason == CastInterruptReason.Death)
            return;
        Vector3 at = self.BasePosition;
        if (HasLandingHit)
        {
            ResolvedShape r = ResolvedShape.Resolve(landingShape, at, cast.AimRotation, cast.Stats.area);
            cast.HitArea(r, landingHit, cast.AimDirection);
        }
        if (landVfx != null)
            AbilityPool.PlayVfx(landVfx, at, cast.AimRotation, 0f, cast.Stats.area);
        if (landSound != null)
            AbilityPool.PlaySound(landSound, at, cast.Definition.presentation.volume);
        cast.EmitNoise(at, HasLandingHit ? 0.9f : 0.4f);
    }

    // ------------------------------------------------------------------ AI / telegraph
    public override bool GetTelegraphShapes(AbilityCastInstance cast, List<ResolvedShape> shapes)
    {
        AbilityStats s = cast.Stats;
        Vector3 from = cast.CasterPosition;
        bool any = false;
        if (HasPathHit || mode == MovementActionMode.Charge)
        {
            Vector3 dest = Destination(cast, from, out _);
            float len = CombatQuery.FlatDistance(from, dest);
            shapes.Add(ResolvedShape.Resolve(new HitShape { type = HitShapeType.Line, length = Mathf.Max(0.5f, len), width = pathWidth * s.area }, from, cast.AimRotation, 1f));
            any = true;
        }
        if (HasLandingHit)
        {
            Vector3 land = mode == MovementActionMode.Leap || mode == MovementActionMode.Blink ? cast.AimPoint : Destination(cast, from, out _);
            shapes.Add(ResolvedShape.Resolve(landingShape, land, cast.AimRotation, s.area));
            any = true;
        }
        return any;
    }

    public override float HazardDuration(AbilityDefinition def, in AbilityStats s) =>
        (mode == MovementActionMode.Leap ? leapDuration : distance * s.range / Mathf.Max(1f, speed)) + 0.3f;

    public override TargetFilter HazardFilter => HasLandingHit ? landingHit.filter : pathHit.filter;

    public override float LaunchDuration(AbilityDefinition def, in AbilityStats s)
    {
        switch (mode)
        {
            case MovementActionMode.Blink: return 0f;
            case MovementActionMode.Leap: return leapDuration;
            case MovementActionMode.DashToTarget: return Mathf.Min(def.Range(s), distance * s.range) / Mathf.Max(1f, speed);
            default: return distance * s.range / Mathf.Max(1f, speed);
        }
    }

    public override float Reach(AbilityDefinition def, in AbilityStats s)
    {
        float move = mode == MovementActionMode.Retreat ? 0f : Mathf.Max(def.Range(s), distance * s.range);
        return move + (HasLandingHit ? landingShape.Reach(s.area) : pathWidth * 0.5f);
    }

    public override bool WouldHit(in CastPreview p)
    {
        if (mode == MovementActionMode.Retreat)
            return true;
        float d = CombatQuery.FlatDistance(p.casterPosition, p.targetVolume.basePosition);
        return d <= Reach(p.definition, p.stats) + p.targetVolume.radius;
    }

    public override float EstimateDamage(in AbilityStats s) =>
        (HasPathHit ? pathHit.EstimateDamage(s) : 0f) + (HasLandingHit ? landingHit.EstimateDamage(s) : 0f);

    public override float EstimateControl(in AbilityStats s) =>
        Mathf.Max(HasPathHit ? pathHit.EstimateControl(s) : 0f, HasLandingHit ? landingHit.EstimateControl(s) : 0f);

    public override string Describe(AbilityDefinition def, in AbilityStats s)
    {
        string text;
        switch (mode)
        {
            case MovementActionMode.DashToTarget: text = "Dashes to the target"; break;
            case MovementActionMode.DashAlongAim: text = $"Dashes {distance * s.range:0.#}m"; break;
            case MovementActionMode.Leap: text = "Leaps to the target area"; break;
            case MovementActionMode.Blink: text = "Teleports"; break;
            case MovementActionMode.Retreat: text = $"Jumps back {distance * s.range:0.#}m"; break;
            default: text = $"Charges {distance * s.range:0.#}m"; break;
        }
        if (HasPathHit) text += $"; on the way: {pathHit.Describe(s)}";
        if (HasLandingHit) text += $"; landing {landingShape.Describe(s.area)}: {landingHit.Describe(s)}";
        if (invulnerable) text += " (invulnerable)";
        return text;
    }

    public override void Validate(AbilityDefinition def, string owner, List<string> errors, List<string> warnings)
    {
        if (HasPathHit) pathHit.Validate(owner + " › Path Hit", errors, warnings);
        if (HasLandingHit) landingHit.Validate(owner + " › Landing Hit", errors, warnings);
        if (mode == MovementActionMode.Charge && !HasPathHit)
            warnings.Add($"{owner}: Charge has no Path Hit effects; it is just a dash.");
        if ((mode == MovementActionMode.DashToTarget || mode == MovementActionMode.Leap) && def.targeting.mode == AbilityTargetingMode.Self)
            warnings.Add($"{owner}: {mode} needs a target or point; set Targeting Mode to Unit or Point.");
        if (landingHit.effects.Count > 0 && landingShape.Reach() <= 0.01f)
            warnings.Add($"{owner}: Landing Hit has effects but the Landing Shape has no size.");
        if (def.movementWhileCasting != CasterMovementRule.Stop && mode != MovementActionMode.Blink)
            warnings.Add($"{owner}: set Movement While Casting to Stop so the character's own movement does not fight the dash.");
    }

    public override void Prewarm()
    {
        if (startVfx != null) AbilityPool.Prewarm(startVfx, 1);
        if (landVfx != null) AbilityPool.Prewarm(landVfx, 1);
    }

    public override CastAction Clone()
    {
        var c = (MovementAction)base.Clone();
        c.pathHit = pathHit.Clone();
        c.landingHit = landingHit.Clone();
        c.landingShape = landingShape.Clone();
        return c;
    }

    /// <summary>Hits characters touched along a dash/charge, each once.</summary>
    private sealed class PathHitter : IAbilityRuntimeObject
    {
        private readonly MovementAction action;
        private readonly AbilityCastInstance cast;
        private readonly ForcedMovement movement;
        private readonly HashSet<CombatEntity> hitSet = new HashSet<CombatEntity>();
        private bool done;

        public PathHitter(MovementAction action, AbilityCastInstance cast, ForcedMovement movement)
        {
            this.action = action;
            this.cast = cast;
            this.movement = movement;
            if (cast.CasterEntity != null)
                hitSet.Add(cast.CasterEntity);
        }

        public void Finish() => done = true;

        public bool Tick(float dt)
        {
            if (done || cast.CasterEntity == null || movement == null || !movement.IsActive)
                return false;
            var circle = new HitShape { type = HitShapeType.Circle, radius = action.pathWidth * 0.5f, height = 3f, baseOffset = -1f };
            ResolvedShape r = ResolvedShape.Resolve(circle, cast.CasterEntity.BasePosition, cast.AimRotation, cast.Stats.area);
            int n = cast.HitArea(r, action.pathHit, cast.AimDirection, hitSet);
            if (n > 0 && action.stopOnFirstHit)
            {
                done = true;
                movement.Cancel();
                action.OnArrived(cast);
                return false;
            }
            return true;
        }

        public void Dispose() { }
    }
}
