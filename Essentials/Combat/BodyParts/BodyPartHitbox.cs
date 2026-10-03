using UnityEngine;

/// <summary>
/// Marks a collider as a body part (a sphere on the head bone, capsules on the limbs) for exact hit locations: arrows
/// and blades that touch it hit that part. Optional - without hitboxes the part comes from the hit's height and side.
/// Put it on a child collider of the character (a bone); the collider is made a trigger so it never blocks movement.
/// Its layer must be one of Combat Settings ▸ Character Layers for projectiles to find it.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BodyPartHitbox : MonoBehaviour
{
    [Tooltip("Name of the body part (as in the character's Body Part Profile: Head, Torso, Arms, Legs...).")]
    public string part = "Head";
    [Tooltip("Make the collider a trigger (recommended: hitboxes must not push the character or its CharacterController).")]
    public bool forceTrigger = true;

    private Collider col;
    private BodyPartController owner;

    public Collider Collider => col != null ? col : (col = GetComponent<Collider>());

    private void Awake()
    {
        col = GetComponent<Collider>();
        if (forceTrigger && col != null)
            col.isTrigger = true;
    }

    private void OnEnable()
    {
        owner = GetComponentInParent<BodyPartController>();
        if (owner != null)
            owner.RegisterHitbox(this);
    }

    private void OnDisable()
    {
        if (owner != null)
            owner.UnregisterHitbox(this);
        owner = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Collider c = Collider;
        if (c == null)
            return;
        BodyPartController owner = GetComponentInParent<BodyPartController>();
        BodyPartController.DrawHitbox(this, owner != null ? BodyPartController.PartColor(owner.Profile, part) : new Color(1f, 0.4f, 0.2f));
    }
#endif
}
