using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

public class WeaponController : MonoBehaviour, IAssignmentsValidator
{
    private WeaponSO equippedWeapon;
    [SerializeField] public GameObject handGameObject;
    private Coroutine comboResetCoroutine;

    private class ComboState
    {
        public int CurrentIndex;
        public float ResetTimer;

        public ComboState()
        {
            CurrentIndex = 0;
            ResetTimer = 0f;
        }
    }

    private Dictionary<AttackType, ComboState> comboStates = new Dictionary<AttackType, ComboState>();
    private Dictionary<AttackType, List<AttackPattern>> attackPatternsByType;
    private List<Collider> collisionBuffer = new List<Collider>();
    [SerializeField]PlayerAnimationController animController;

    public PlayerAnimationController AnimController { get => animController; set => animController = value; }

    private void Awake()
    {
        ValidateAssignments();
    }

    public  void ValidateAssignments()
    {
        animController = GetComponentOrLogError(ref animController, "HealthManager");
    }
    /// <summary>
    /// Gets a component of type <typeparamref name="T"/> and logs an error if not found.
    /// </summary>
    /// <typeparam name="T">Type of the component.</typeparam>
    /// <param name="field">Reference to the field to assign the component to.</param>
    /// <param name="fieldName">Name of the field for error logging.</param>
    /// <returns>The found component, or null if not found.</returns>
    private T GetComponentOrLogError<T>(ref T field, string fieldName) where T : Component
    {
        field = GetComponent<T>();
        Assert.IsNotNull(field, $"{fieldName} is not assigned.");
        return field;
    }


    public void EquipWeapon(WeaponSO weaponSO)
    {
        equippedWeapon = weaponSO;

        // Precompute attack patterns by type
        attackPatternsByType = equippedWeapon.AttackPatterns
            .GroupBy(p => p.Type)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public void PerformAttack(GameObject player, AttackType attackType)
    {
        if (equippedWeapon == null || !attackPatternsByType.ContainsKey(attackType)) return;

        // Get current animation state
        PlayerAnimationController animController = player.GetComponent<PlayerAnimationController>();
        if (animController.model.IsAttacking) return; // Prevent new attacks during animation


        ResetOtherCombos(attackType);
        var attackPattern = GetComboAttackPattern(attackType);
        if (attackPattern == null) return;
        TriggerAttackEffects(player, attackPattern);
        StartCollisionDetection(player);
        UpdateComboState(attackType, attackPattern.ComboResetTime);

        // Lock inputs until animation ends
      //  StartCoroutine(UnlockInputAfterAnimation(animController, attackPattern));
    }

    private IEnumerator UnlockInputAfterAnimation(PlayerAnimationController animController, AttackPattern attackPattern)
    {
        float animationLength = GetAnimationClipLength(animController.model.Anim, attackPattern.AnimationTrigger);
        if (animationLength > 0f)
        {
            yield return new WaitForSeconds(animationLength);
        }
        animController.EndAttackAnimation();
    }

    private float GetAnimationClipLength(Animator animator, string animationTrigger)
    {
        if (animator.runtimeAnimatorController == null || string.IsNullOrEmpty(animationTrigger))
        {
            return 0f;
        }

        // Find animation clip by name
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == animationTrigger)
            {
                return clip.length;
            }
        }

        Debug.LogWarning($"Animation clip for trigger '{animationTrigger}' not found!");
        return 0f;
    }

    private void ResetOtherCombos(AttackType currentType)
    {
        foreach (var type in comboStates.Keys.ToList())
        {
            if (type != currentType && comboStates[type].ResetTimer <= 0f)
            {
                ResetCombo(type);
            }
        }
    }

    private void UpdateComboState(AttackType attackType, float resetTime)
    {
        if (!comboStates.ContainsKey(attackType))
        {
            comboStates[attackType] = new ComboState();
        }

        comboStates[attackType].ResetTimer = resetTime;
        IncrementComboIndex(attackType);

        if (comboResetCoroutine != null)
        {
            StopCoroutine(comboResetCoroutine);
        }
        comboResetCoroutine = StartCoroutine(ResetComboTimer(attackType));
    }

    private AttackPattern GetComboAttackPattern(AttackType attackType)
    {
        if (!attackPatternsByType.ContainsKey(attackType)) return null;

        var patterns = attackPatternsByType[attackType];
        int index = comboStates.ContainsKey(attackType) ? comboStates[attackType].CurrentIndex : 0;
        return patterns[index % patterns.Count];
    }

    private void IncrementComboIndex(AttackType attackType)
    {
        if (!comboStates.ContainsKey(attackType))
        {
            comboStates[attackType] = new ComboState();
        }

        comboStates[attackType].CurrentIndex++;
    }

    private void ResetCombo(AttackType attackType)
    {
        if (comboStates.ContainsKey(attackType))
        {
            comboStates[attackType].CurrentIndex = 0;
            comboStates[attackType].ResetTimer = 0f;
        }
    }

    private IEnumerator ResetComboTimer(AttackType attackType)
    {
        if (!comboStates.ContainsKey(attackType)) yield break;

        while (comboStates[attackType].ResetTimer > 0f)
        {
            comboStates[attackType].ResetTimer -= Time.deltaTime;
            yield return null;
        }
        ResetCombo(attackType);
    }

    private void TriggerAttackEffects(GameObject player, AttackPattern pattern)
    {
        // Play sound
        var audioSource = player.GetComponent<Player>().PlayerAudioSource;
        if (equippedWeapon.AttackSound != null) audioSource.PlayOneShot(equippedWeapon.AttackSound);

        
        /*
            PlayerAnimationModel playerAnimationModel = player.GetComponent<PlayerAnimationModel>();
            if (playerAnimationModel.IsAttacking == true) return;
            playerAnimationModel.IsAttacking = true;*/
            // Trigger animation
            var animController = player.GetComponent<PlayerAnimationController>();
       
        if (animController != null)
        {
            animController.TriggerAttackAnimation(pattern.AnimationTrigger);
        }
    }

    public void StartCollisionDetection(GameObject playerObject)
    {
        StartCoroutine(PerformCollisionDetectionCoroutine(playerObject));
    }

    private IEnumerator PerformCollisionDetectionCoroutine(GameObject playerObject)
    {
        while (playerObject.GetComponent<PlayerAnimationModel>().IsAttacking)
        {
            PerformCollisionDetection(handGameObject.transform, playerObject);
            yield return null;
        }
    }

    public void PerformCollisionDetection(Transform handTransform, GameObject playerObject)
    {
        collisionBuffer.Clear();
        if (equippedWeapon == null) return;

        Collider[] colliders = equippedWeapon.attackCast.DetectObjects(handTransform);
        foreach (Collider collider in colliders)
        {
            if (collider != null && collider.gameObject != playerObject && !collisionBuffer.Contains(collider))
            {
                collisionBuffer.Add(collider);
                equippedWeapon.ApplyEffectsToTarget(collider.gameObject, playerObject);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (equippedWeapon != null && handGameObject != null)
        {
            Transform currentObject = handGameObject.GetComponentInChildren<Transform>();
            if (currentObject != null)
            {
                equippedWeapon.attackCast.DrawGizmos(currentObject);
            }
        }
    }
}
