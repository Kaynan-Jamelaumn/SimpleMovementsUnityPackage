using System.Collections.Generic;
using UnityEngine;

// How the legs let the character move (Injury Settings ▸ Movement): a limp from a hurt leg or foot, hopping on one leg
// when the other cannot carry weight, crawling with the arms when no leg can; the strain of moving like that; and the
// body's shape without legs (a shorter collider, the model lowered to the waist, the hitboxes of lost parts off).
// Everything here is worked out from the parts' state, so a client's copy (which only receives that state) matches.
public partial class InjuryController
{
    private BodyLocomotion locomotion = BodyLocomotion.Normal;
    private float legsRemaining = 1f;   // how much of the longest leg is left: 1 whole, 0.5 to the knee, 0 none
    private readonly object strainKey = new object();
    private StatusConsequence strainRunning;
    private Vector3 strainLastPosition;
    private bool strainHasPosition;
    private bool strainMoving;
    private float movingFor, stillFor;

    // The body's shape.
    private float shapeHeight = 1f, shapeDrop;
    private CharacterBody shapeBody;
    private float nextBodySearch;
    private CharacterController shapeController;
    private float controllerHeight;
    private Vector3 controllerCenter;
    private bool controllerStored;
    private Transform shapeHips, shapeModel;
    private Vector3 shapeHipsWritten, shapeModelBase;
    private bool shapeHipsShifted, shapeModelMoved;
    private float legShare = -1f;
    private readonly List<Collider> disabledHitboxes = new List<Collider>();
    private BodyPartHitbox[] hitboxCache;

    /// <summary>How the legs let the character move right now (normal while the system is off).</summary>
    public BodyLocomotion Locomotion => Settings.enabled ? locomotion : BodyLocomotion.Normal;

    // ------------------------------------------------------------------ the mode
    private BodyLocomotion ComputeLocomotion(InjurySettings s)
    {
        BodyLocomotion l = BodyLocomotion.Normal;
        legsRemaining = 1f;
        LocomotionRules r = s.locomotion;
        if (!s.enabled)
            return l;
        l.missingArms = MissingArms();
        if (r == null || !r.enabled)
            return l;

        int legs = 0, unusable = 0, missing = 0, missingFeet = 0;
        float remaining = 0f, worstLimp = 0f, limpSide = 0f, badSide = 0f;
        string limpWhy = "", badWhy = "";
        for (int k = 0; k < 2; k++)
        {
            BodyPartSide side = k == 0 ? BodyPartSide.Left : BodyPartSide.Right;
            float sign = k == 0 ? -1f : 1f;
            bool any = false, upperLost = false, lowerLost = false, footLost = false;
            float legImpairment = 0f, footImpairment = 0f;
            InjuryPartState worstLeg = null, foot = null;
            for (int i = 0; i < parts.Count; i++)
            {
                InjuryPartState p = parts[i];
                if (p.Def.side != side || !p.Def.CarriesWeight)
                    continue;
                any = true;
                if (p.Def.kind == BodyPartKind.Foot)
                {
                    foot = p;
                    if (p.Lost) footLost = true;
                    else footImpairment = Mathf.Max(footImpairment, p.Impairment);
                    continue;
                }
                if (p.Lost)
                {
                    if (IsUpperLeg(p)) upperLost = true;
                    else lowerLost = true;
                }
                else if (worstLeg == null || p.Impairment > legImpairment)
                {
                    legImpairment = p.Impairment;
                    worstLeg = p;
                }
            }
            if (!any)
                continue;
            legs++;
            string sideName = side == BodyPartSide.Left ? "Left" : "Right";
            bool gone = upperLost || lowerLost;
            remaining = Mathf.Max(remaining, upperLost ? 0f : lowerLost ? 0.5f : 1f);
            if (gone || legImpairment + 1e-4f >= r.legUselessFromImpairment)
            {
                unusable++;
                if (gone) missing++;
                badSide = sign;
                badWhy = gone ? $"{sideName} leg is missing" : $"{worstLeg.Name} cannot carry weight";
                continue;
            }
            float worst = Mathf.Max(legImpairment, footImpairment);
            float limp = 0f;
            if (worst + 1e-4f >= r.limpFromImpairment)
                limp = Mathf.Lerp(0.15f, 1f, Mathf.InverseLerp(r.limpFromImpairment, Mathf.Max(r.limpFromImpairment + 0.01f, r.legUselessFromImpairment), worst));
            string why = worstLeg != null && legImpairment >= footImpairment ? $"{worstLeg.Name} is hurt" : foot != null ? $"{foot.Name} is hurt" : "";
            if (footLost)
            {
                missingFeet++;
                if (r.missingFootLimp >= limp)
                {
                    limp = r.missingFootLimp;
                    why = $"{sideName} foot is missing";
                }
            }
            if (limp > worstLimp)
            {
                worstLimp = limp;
                limpSide = sign;
                limpWhy = why;
            }
        }
        if (legs == 0)
            return l; // a body without legs in its settings (a snake, a turret) moves as it always does

        int arms = l.missingArms;
        l = BodyLocomotion.Normal;
        l.missingArms = arms;
        l.unusableLegs = unusable;
        l.missingLegs = missing;
        l.missingFeet = missingFeet;
        if (unusable >= legs)
        {
            l.mode = LocomotionMode.Crawling;
            l.limp = 1f;
            l.speedMultiplier = r.crawlSpeed * CrawlArms();
            l.reason = missing >= legs ? "No legs" : badWhy;
        }
        else if (unusable > 0)
        {
            l.mode = LocomotionMode.Hopping;
            l.limp = 1f;
            l.limpSide = badSide;
            l.speedMultiplier = r.hopSpeed;
            l.hopPeriod = Mathf.Max(0.1f, r.hopSeconds);
            l.hopMoveShare = Mathf.Clamp(r.hopMoveShare, 0.1f, 1f);
            l.reason = badWhy;
        }
        else if (worstLimp > 0f)
        {
            l.mode = LocomotionMode.Limping;
            l.limp = worstLimp;
            l.limpSide = limpSide;
            l.painful = worstLimp + 1e-4f >= r.painFromLimp || missingFeet > 0;
            l.speedMultiplier = missingFeet > 0 ? r.missingFootSpeed : 1f;
            l.reason = limpWhy;
        }
        // Without legs the body is only as tall as what is left of the longest leg (none: from the waist up).
        if (missing >= legs)
        {
            legsRemaining = remaining;
            l.heightFactor = Mathf.Lerp(r.leglessHeight, 1f, remaining);
        }
        return l;
    }

    /// <summary>A leg part attached to something that is not a leg (the thigh, on the groin) is the top of the leg.</summary>
    private bool IsUpperLeg(InjuryPartState p)
    {
        InjuryPartState parent = GetPart(p.Def.attachedTo);
        return parent == null || parent.Def.kind != BodyPartKind.Leg;
    }

    /// <summary>Sides whose arm (upper arm or forearm) is missing.</summary>
    private int MissingArms()
    {
        int n = 0;
        for (int k = 0; k < 2; k++)
        {
            BodyPartSide side = k == 0 ? BodyPartSide.Left : BodyPartSide.Right;
            for (int i = 0; i < parts.Count; i++)
                if (parts[i].Def.side == side && parts[i].Def.kind == BodyPartKind.Arm && parts[i].Lost)
                {
                    n++;
                    break;
                }
        }
        return n;
    }

    /// <summary>Crawling drags the body with the arms: slower with one arm, very slow with none.</summary>
    private float CrawlArms()
    {
        int arms = 0, usable = 0;
        for (int k = 0; k < 2; k++)
        {
            BodyPartSide side = k == 0 ? BodyPartSide.Left : BodyPartSide.Right;
            bool any = false, works = true;
            for (int i = 0; i < parts.Count; i++)
            {
                InjuryPartState p = parts[i];
                if (p.Def.side != side || p.Def.kind != BodyPartKind.Arm)
                    continue;
                any = true;
                if (p.Lost || p.Impairment >= 0.95f)
                    works = false;
            }
            if (!any) continue;
            arms++;
            if (works) usable++;
        }
        if (arms == 0 || usable >= arms)
            return 1f;
        return usable == 0 ? 0.3f : 0.65f;
    }

    private static BodyAction LocomotionBlocks(InjurySettings s, BodyLocomotion l)
    {
        LocomotionRules r = s.locomotion;
        if (r == null || !r.enabled)
            return BodyAction.None;
        switch (l.mode)
        {
            case LocomotionMode.Hopping: return r.hopBlocks;
            case LocomotionMode.Crawling: return r.crawlBlocks;
            case LocomotionMode.Limping: return l.missingFeet > 0 ? r.missingFootBlocks : BodyAction.None;
            default: return BodyAction.None;
        }
    }

    // ------------------------------------------------------------------ strain while moving
    /// <summary>Runs Hopping, Crawling or Painful Walking while the character moves like that (rules side).</summary>
    private void TickStrain(float dt, InjurySettings s)
    {
        LocomotionRules r = s.locomotion;
        StatusConsequence want = null;
        bool moving = IsMoving(dt);
        if (r != null && r.enabled && moving)
        {
            switch (locomotion.mode)
            {
                case LocomotionMode.Crawling: want = r.crawling; break;
                case LocomotionMode.Hopping: want = r.hopping; break;
                case LocomotionMode.Limping: want = locomotion.painful ? r.painfulWalking : null; break;
            }
        }
        if (want != null && want.HasAnyEffect)
        {
            Runner(strainKey).Set(want, 1f);
            strainRunning = want;
        }
        else if (strainRunning != null)
        {
            RemoveRunner(strainKey);
            strainRunning = null;
        }
    }

    /// <summary>Is the character moving over the ground (with a little patience either way)?</summary>
    private bool IsMoving(float dt)
    {
        CombatEntity e = Entity;
        if (e == null || dt <= 0f)
            return false;
        Vector3 pos = e.Position;
        if (!strainHasPosition)
        {
            strainHasPosition = true;
            strainLastPosition = pos;
            return false;
        }
        Vector3 d = pos - strainLastPosition;
        strainLastPosition = pos;
        d.y = 0f;
        float speed = d.magnitude / dt;
        if (speed > 0.25f && speed < 30f) // faster than that is a teleport
        {
            stillFor = 0f;
            movingFor += dt;
            if (movingFor > 0.15f) strainMoving = true;
        }
        else
        {
            movingFor = 0f;
            stillFor += dt;
            if (stillFor > 0.4f) strainMoving = false;
        }
        return strainMoving;
    }

    // ------------------------------------------------------------------ the body's shape
    /// <summary>The collider height and model drop for the legs that are left, and the hitboxes of lost parts.</summary>
    private void ApplyBodyShape(InjurySettings s)
    {
        LocomotionRules r = s.locomotion;
        bool on = s.enabled && r != null && r.enabled;
        float height = on ? Mathf.Clamp(locomotion.heightFactor, 0.1f, 1f) : 1f;
        float drop = on && height < 0.999f && r.lowerModelWhenLegless ? LegLength(r) * (1f - legsRemaining) : 0f;
        if (Mathf.Abs(height - shapeHeight) >= 0.001f || Mathf.Abs(drop - shapeDrop) >= 0.001f)
            SetShape(height, drop);
        UpdateHitboxes(on && r.disableLostHitboxes);
    }

    private void SetShape(float height, float drop)
    {
        bool wasLowered = shapeDrop > 0f;
        shapeHeight = height;
        shapeDrop = drop;
        CharacterBody body = Body();
        if (body != null)
        {
            body.SetPosture(height, drop); // it resizes every collider and lowers the hips itself
        }
        else
        {
            SetControllerHeight(height);
            if (wasLowered && drop <= 0f && shapeModelMoved && shapeModel != null)
            {
                shapeModel.localPosition = shapeModelBase;
                shapeModelMoved = false;
            }
        }
        CombatEntity e = Entity;
        if (e != null)
            e.RemeasureBody();
    }

    /// <summary>Back to standing with every hitbox on (the system turned off, the component removed).</summary>
    private void RestoreBodyShape()
    {
        if (shapeHeight < 0.999f || shapeDrop > 0f)
            SetShape(1f, 0f);
        UpdateHitboxes(false);
    }

    private CharacterBody Body()
    {
        // Searched again now and then: character creation adds a Character Body at runtime.
        if (shapeBody == null && Time.realtimeSinceStartup >= nextBodySearch)
        {
            nextBodySearch = Time.realtimeSinceStartup + 2f;
            CombatEntity e = Entity;
            Transform root = e != null ? e.transform : transform;
            shapeBody = root.GetComponentInChildren<CharacterBody>(true);
            if (shapeBody == null)
                shapeBody = root.GetComponentInParent<CharacterBody>();
        }
        return shapeBody;
    }

    /// <summary>Resizes the CharacterController (characters without a Character Body), its bottom staying where it is.</summary>
    private void SetControllerHeight(float height)
    {
        if (shapeController == null)
        {
            CombatEntity e = Entity;
            shapeController = e != null ? e.GetComponentInChildren<CharacterController>() : GetComponentInParent<CharacterController>();
            if (shapeController == null)
                return;
        }
        if (!controllerStored)
        {
            controllerStored = true;
            controllerHeight = shapeController.height;
            controllerCenter = shapeController.center;
        }
        float bottom = controllerCenter.y - controllerHeight * 0.5f;
        shapeController.height = Mathf.Max(controllerHeight * height, shapeController.radius * 2f);
        shapeController.center = new Vector3(controllerCenter.x, bottom + shapeController.height * 0.5f, controllerCenter.z);
    }

    /// <summary>
    /// How far the model is lowered with no legs (metres): Legless Model Drop × the body's height, else the Humanoid hips'
    /// height above the feet, measured once.
    /// </summary>
    private float LegLength(LocomotionRules r)
    {
        CombatEntity e = Entity;
        // The standing height (the measured body is already shorter while the posture is applied).
        float height = e != null && e.Height > 0.1f ? e.Height / Mathf.Max(0.1f, shapeHeight) : 1.8f;
        if (r.leglessModelDrop >= 0f)
            return r.leglessModelDrop * height;
        if (legShare < 0f)
        {
            legShare = 0.48f;
            Animator a = GetComponentInChildren<Animator>();
            if (a == null && e != null) a = e.GetComponentInChildren<Animator>();
            Transform hips = a != null && a.isHuman ? a.GetBoneTransform(HumanBodyBones.Hips) : null;
            if (hips != null && e != null)
                legShare = Mathf.Clamp((hips.position.y - e.BasePosition.y) / height, 0.3f, 0.6f);
        }
        return legShare * height;
    }

    /// <summary>
    /// Characters without a Character Body: lowers the hips after the animation wrote them (or the whole model on a generic
    /// rig) by the drop. A Character Body does this itself.
    /// </summary>
    private void KeepPosture()
    {
        if (shapeDrop <= 0f || Body() != null)
            return;
        CombatEntity e = Entity;
        Transform root = e != null ? e.transform : transform;
        Vector3 down = -root.up * shapeDrop;
        if (shapeHips == null)
        {
            Animator a = root.GetComponentInChildren<Animator>();
            shapeHips = a != null && a.isHuman ? a.GetBoneTransform(HumanBodyBones.Hips) : null;
            if (shapeHips == null && shapeModel == null && a != null && a.transform != root)
            {
                shapeModel = a.transform;
                shapeModelBase = shapeModel.localPosition;
            }
        }
        if (shapeHips != null)
        {
            // The animation writes the hips every frame; a frame it did not (culled) keeps the shift already made.
            if (shapeHipsShifted && (shapeHips.position - shapeHipsWritten).sqrMagnitude < 1e-10f)
                return;
            shapeHips.position += down;
            shapeHipsWritten = shapeHips.position;
            shapeHipsShifted = true;
        }
        else if (shapeModel != null && shapeModel.parent != null)
        {
            shapeModel.localPosition = shapeModelBase + shapeModel.parent.InverseTransformVector(down);
            shapeModelMoved = true;
        }
    }

    /// <summary>Turns off the hitbox colliders of lost parts (and back on when they return).</summary>
    private void UpdateHitboxes(bool disableLost)
    {
        if (!disableLost)
        {
            for (int i = 0; i < disabledHitboxes.Count; i++)
                if (disabledHitboxes[i] != null)
                    disabledHitboxes[i].enabled = true;
            disabledHitboxes.Clear();
            return;
        }
        if (hitboxCache == null)
        {
            CombatEntity e = Entity;
            hitboxCache = (e != null ? e.gameObject : gameObject).GetComponentsInChildren<BodyPartHitbox>(true);
        }
        for (int i = 0; i < hitboxCache.Length; i++)
        {
            BodyPartHitbox hb = hitboxCache[i];
            Collider c = hb != null ? hb.Collider : null;
            if (c == null)
                continue;
            bool lost = HitboxPartLost(hb.part);
            if (lost && c.enabled)
            {
                c.enabled = false;
                disabledHitboxes.Add(c);
            }
            else if (!lost && disabledHitboxes.Remove(c))
            {
                c.enabled = true;
            }
        }
    }

    /// <summary>
    /// Are the parts a hitbox stands for all lost? Its part name is matched to the parts named after it (or listing it in
    /// their Hit Parts by that exact name).
    /// </summary>
    private bool HitboxPartLost(string partName)
    {
        if (string.IsNullOrWhiteSpace(partName))
            return false;
        bool any = false;
        string n = partName.Trim();
        for (int i = 0; i < parts.Count; i++)
        {
            InjuryBodyPart d = parts[i].Def;
            bool match = d.IsNamed(n);
            if (!match && d.hitParts != null)
                for (int h = 0; h < d.hitParts.Count && !match; h++)
                    match = string.Equals(d.hitParts[h], n, System.StringComparison.OrdinalIgnoreCase);
            if (!match)
                continue;
            if (!parts[i].Lost)
                return false;
            any = true;
        }
        return any;
    }
}
