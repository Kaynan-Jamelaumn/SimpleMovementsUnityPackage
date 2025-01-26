//using UnityEngine;
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;

//public class WeaponController : MonoBehaviour
//{
//    // Method to start collision detection
//    WeaponSO equippedWeapon;
//    [SerializeField] public GameObject handGameObject;
//    private Coroutine comboCoroutine;
//    private int currentComboIndex = 0;
//    private float comboResetTimer = 0f;
//    private HashSet<Collider> detectedColliders = new HashSet<Collider>();




//    private Dictionary<AttackType, int> comboIndices = new Dictionary<AttackType, int>();
//    private Dictionary<AttackType, float> comboResetTimers = new Dictionary<AttackType, float>();
//    private Coroutine comboResetCoroutine;




//    public void EquipWeapon(WeaponSO weaponSO)
//    {
//        equippedWeapon = weaponSO;
//    }






//    private void TriggerAttackEffects(GameObject player, AttackPattern pattern)
//    {
//        // Play sound
//        var audioSource = player.GetComponent<Player>().PlayerAudioSource;
//        if (equippedWeapon.AttackSound != null) audioSource.PlayOneShot(equippedWeapon.AttackSound);

//        // Trigger animation
//        var animController = player.GetComponent<PlayerAnimationController>();
//        if (animController != null)
//        {
//            animController.TriggerAttackAnimation(pattern.AnimationTrigger);
//        }
//    }


//    public void PerformAttack(GameObject player, AttackType attackType)
//    {
//        if (equippedWeapon == null) return;

//        // Reset combos for other attack types only if their reset timer has expired
//        foreach (var type in comboIndices.Keys.ToList())
//        {
//            if (type != attackType && comboResetTimers[type] <= 0f)
//            {
//                ResetCombo(type);
//            }
//        }

//        // Get the current attack pattern for the selected attack type
//        var attackPattern = GetComboAttackPattern(attackType);
//        if (attackPattern == null) return;

//        // Trigger attack effects
//        TriggerAttackEffects(player, attackPattern);

//        // Start collision detection
//        StartCollisionDetection(player);

//        // Update/reset the timer and increment combo index
//        comboResetTimers[attackType] = attackPattern.ComboResetTime;
//        IncrementComboIndex(attackType);

//        // Manage the reset combo coroutine for the current attack type
//        if (comboResetCoroutine != null)
//        {
//            StopCoroutine(comboResetCoroutine);
//        }
//        comboResetCoroutine = StartCoroutine(ResetComboTimer(attackType));
//    }

//    private AttackPattern GetComboAttackPattern(AttackType attackType)
//    {
//        if (equippedWeapon == null || equippedWeapon.AttackPatterns.Count == 0) return null;

//        // Garante que o índice e o timer existem para o tipo de ataque atual
//        if (!comboIndices.ContainsKey(attackType)) comboIndices[attackType] = 0;
//        if (!comboResetTimers.ContainsKey(attackType)) comboResetTimers[attackType] = 0f;

//        // Filtra os padrões de ataque pelo tipo e retorna o padrão atual baseado no índice
//        var patternsForType = equippedWeapon.AttackPatterns.Where(ap => ap.Type == attackType).ToList();
//        if (patternsForType.Count == 0) return null;

//        return patternsForType[comboIndices[attackType] % patternsForType.Count];
//    }

//    private void IncrementComboIndex(AttackType attackType)
//    {
//        if (!comboIndices.ContainsKey(attackType)) comboIndices[attackType] = 0;

//        // Incrementa o índice do combo para o tipo de ataque selecionado
//        comboIndices[attackType]++;
//    }

//    private void ResetCombo(AttackType attackType)
//    {
//        comboIndices[attackType] = 0;
//        comboResetTimers[attackType] = 0f;
//    }


//    private IEnumerator ResetComboTimer(AttackType attackType)
//    {
//        while (comboResetTimers[attackType] > 0f)
//        {
//            comboResetTimers[attackType] -= Time.deltaTime;
//            yield return null;
//        }

//        // Reset the combo for the specific attack type
//        ResetCombo(attackType);
//    }



//    public float CalculateDamage(AttackType attackType)
//    {
//        var pattern = equippedWeapon.AttackPatterns.Find(ap => ap.Type == attackType);
//        if (pattern == null)
//        {
//            float baseDamage = Random.Range(equippedWeapon.MinDamage, equippedWeapon.MaxDamage);
//            return baseDamage;
//        }

//        float damage = Random.Range(pattern.MinDamage, pattern.MaxDamage);
//        if (Random.value <= pattern.CriticalChange)
//        {
//            damage *= equippedWeapon.CriticalDamageMultiplier;
//        }

//        return damage;
//    }

//    public void StartCollisionDetection(GameObject playerObject)
//    {
//        StartCoroutine(PerformCollisionDetectionCoroutine(playerObject));
//    }

//    private IEnumerator PerformCollisionDetectionCoroutine(GameObject playerObject)
//    {
//        while (playerObject.GetComponent<PlayerAnimationModel>().IsAttacking)
//        {

//            if (equippedWeapon == null) yield break;
//            // Call the collision detection function of the WeaponSO

//            PerformCollisionDetection(handGameObject.transform, playerObject);
//            yield return null;
//        }
//        equippedWeapon = null;
//    }


//    public void PerformCollisionDetection(Transform handTransform, GameObject playerObject)
//    {
//        detectedColliders.Clear();
//        if (equippedWeapon == null) return;

//        Collider[] colliders = equippedWeapon.attackCast.DetectObjects(handTransform);
//        if (colliders.Length > 0 && equippedWeapon.AttackSound)
//        {
//            playerObject.GetComponent<Player>().PlayerAudioSource.PlayOneShot(equippedWeapon.AttackSound);
//        }

//        foreach (Collider collider in colliders)
//        {
//            if (collider == null || collider.gameObject == playerObject || detectedColliders.Contains(collider)) continue;

//            detectedColliders.Add(collider);
//            equippedWeapon.ApplyEffectsToTarget(collider.gameObject, playerObject);
//        }
//    }


//    private void OnDrawGizmos()
//    {
//        if (equippedWeapon != null && handGameObject != null)
//        {
//            Transform currentObject = handGameObject.GetComponentInChildren<Transform>();
//            if (currentObject != null)
//            {
//                equippedWeapon.attackCast.DrawGizmos(currentObject);
//            }
//        }
//    }


//}


////private void PerformAttack(GameObject playerObject)
////{
////    PlayerAnimationModel playerAnimationModel = playerObject.GetComponent<PlayerAnimationModel>();
////    if (playerAnimationModel.IsAttacking == true) return;
////    playerAnimationModel.IsAttacking = true;


////    // Clear the list of detected colliders before starting a new check
////    detectedColliders.Clear();

////    // Start collision detection
////    StartCollisionDetection(playerObject);
////}

////private void TriggerAttackEffects(GameObject player, AttackPattern pattern)
////{
////    // Play audio
////    var audioSource = player.GetComponent<Player>().PlayerAudioSource;
////    if (equippedWeapon.AttackSound != null) audioSource.PlayOneShot(equippedWeapon.AttackSound);

////    // Trigger animation
////    var animController = player.GetComponent<PlayerAnimationController>();
////    if (animController != null)
////    {
////        animController.TriggerAttackAnimation(pattern.AnimationTrigger);
////    }
////}