using UnityEngine;

/// <summary>
/// Marks a Harvesting carcass as a farm animal's (<see cref="AnimalCarcass"/>): whose it was and where it was laid out.
/// It exists only where the rules run - added there when the carcass is laid out, never sent to clients (owner keys stay
/// on the server) - and it is what the owner-only access check reads.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("")]
public sealed class AnimalCarcassTag : MonoBehaviour
{
    [Tooltip("The animal's id (its record is removed once its carcass is laid out).")]
    public string animal = "";
    [Tooltip("Its species id (its body is drawn as that species).")]
    public string species = "";
    [Tooltip("Its owner's key: only they may work the carcass.")]
    public string ownerKey = "";
    [Tooltip("The property of the pen it was laid out at (empty: public land).")]
    public string property = "";
    [Tooltip("The id of the pen it was laid out at.")]
    public string pen = "";
}
