using UnityEngine;

/// <summary>
/// Attached to each terrain chunk's water trigger (see <see cref="EndlessTerrain.TerrainChunk"/>). Anything with an
/// <c>OxygenManager</c> inside it is handed to <see cref="Submersion"/>, which decides every frame whether its HEAD is
/// under the actual water surface at its position (<see cref="WaterQuery"/>) and only then calls
/// <c>OxygenManager.SetUnderwater(true)</c>.
/// </summary>
/// <remarks>
/// The trigger is a coarse box: it covers the chunk's whole footprint, from the deepest water bed to the highest water
/// surface of the chunk. It used to switch "underwater" on by itself, so standing on dry land next to a lake (inside
/// that height band anywhere in the chunk) drained oxygen. Now it is only the "near water" filter. Toggle
/// <see cref="TerrainGenerator.EnableSwimDetection"/> to skip creating it (no drowning in the terrain's water then;
/// swimming does not need it).
/// </remarks>
public class WaterVolume : MonoBehaviour
{
    private void OnTriggerEnter(Collider other) => Submersion.Enter(this, other);

    private void OnTriggerExit(Collider other) => Submersion.Exit(this, other);

    /// <summary>
    /// Chunks stream out by deactivating their GameObject, which does NOT fire OnTriggerExit for anything still
    /// inside: forget everyone this trigger was holding (another chunk's trigger keeps holding them if they are in
    /// its box too).
    /// </summary>
    private void OnDisable() => Submersion.ForgetSource(this);
}
