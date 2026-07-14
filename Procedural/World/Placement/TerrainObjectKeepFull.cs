using UnityEngine;

/// <summary>
/// Put on the root of a placed object's prefab to keep the object fully working when its chunk is far from the
/// viewer: nothing of it is switched off by Far Objects (see <see cref="FarObjectSwitcher"/>). For objects that
/// must always collide or run - a quest giver, a beacon, a machine that keeps working.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Terrain/Terrain Object Keep Full")]
public class TerrainObjectKeepFull : MonoBehaviour
{
}
