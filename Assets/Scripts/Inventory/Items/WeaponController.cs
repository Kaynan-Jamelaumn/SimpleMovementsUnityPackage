using UnityEngine;
using System.Collections;

public class WeaponController : MonoBehaviour
{
    // Method to start collision detection
    WeaponSO weaponSx;
    [SerializeField] public GameObject handGameObject;
    public void StartCollisionDetection(WeaponSO weaponSO, GameObject playerObject)
    {
        StartCoroutine(PerformCollisionDetectionCoroutine(weaponSO, playerObject));
    }

    // Coroutine to perform collision detection
    private IEnumerator PerformCollisionDetectionCoroutine(WeaponSO weaponSO, GameObject playerObject)
    {
        while (playerObject.GetComponent<PlayerAnimationModel>().IsAttacking)
        {
            
            if (weaponSx == null) weaponSx = weaponSO;
            // Call the collision detection function of the WeaponSO
            weaponSO.PerformCollisionDetection(handGameObject.transform, playerObject);

            // Wait for one frame before checking again
            yield return null;

            weaponSx = null;
        }
    }
    private void OnDrawGizmos()
    {
        if (weaponSx != null && handGameObject != null)
        {
            Transform currentObject = handGameObject.GetComponentInChildren<Transform>();
            if (currentObject != null)
            {
                weaponSx.attackCast.DrawGizmos(currentObject);
            }
        }
    }
}
