#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using UnityEngine;

namespace SimpleMovements.Validation.Tests
{
    /// <summary>A component with one field per auto-assign rule, used by <see cref="ReferenceAutoAssignerTests"/>.</summary>
    [AddComponentMenu("")]
    public class AutoAssignProbe : MonoBehaviour
    {
        public Animator animator;
        public Rigidbody body;
        public Collider hitbox;
        public Collider ignoreCollider;
        public Transform weaponSocket;
        public Transform muzzle;
        public GameObject projectilePrefab;
        public AudioSource voice;
        public AudioSource existing;

        [AutoAssign(ChildName = "Grip")]
        public Transform handle;

        [AutoAssign(AutoAssignFrom.Parents)]
        public Camera rigCamera;

        [AutoAssign(Never = true)]
        public Animator manualAnimator;

        [RequiredReference("lights the test")]
        public Light requiredLight;

        [SerializeField] private Animator privateAnimator;

        public int autoAssignCalls;

        public Animator PrivateAnimator => privateAnimator;

        /// <summary>Called by the auto-assigner before it fills fields (like the package's controllers).</summary>
        public void AutoAssignReferences() => autoAssignCalls++;
    }
}
#endif
