using UnityEngine;

/// <summary>
/// Marks a model the hotbar put in the hand, so only these are removed when the hand changes (the hand bone's own
/// children - fingers, sockets - are never touched).
/// </summary>
[DisallowMultipleComponent]
public class HeldItemModel : MonoBehaviour
{
}
