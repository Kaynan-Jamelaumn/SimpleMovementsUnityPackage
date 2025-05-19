using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

////Debug.Log(collider.name + " a" + this.name);
public class WallCollision : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, .01f);

        foreach (Collider collider in colliders)
        {
            if (collider.tag == "RoomRemovable" && collider.gameObject != this.gameObject)
            {
                if (this.gameObject.name == "doorPivot")
                    Destroy(this.transform.parent.gameObject);
                //else if //(collider.gameObject.name.Contains("Wall") && gameObject.name.Contains("Wall"))
                //{
                    // Check if the positions are close enough to be considered overlapping
                    else if (Vector3.Distance(transform.position, collider.transform.position) < 0.1f) // Use a small threshold instead of ==
                    {
                        // Determine the rule for which wall to destroy
                        // As an example, we can use the instance ID to decide
                        if (gameObject.GetInstanceID() < collider.gameObject.GetInstanceID())
                        {
                            Destroy(gameObject);
                        }
                        // Otherwise, the other wall's script will destroy it
                    }
               // }
            }
        }

        if (GetComponent<Collider>()) GetComponent<Collider>().enabled = true;
    }
}