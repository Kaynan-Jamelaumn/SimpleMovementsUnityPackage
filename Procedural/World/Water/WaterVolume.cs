using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attached to each generated water body's trigger volume (see
/// <see cref="EndlessTerrain.TerrainChunk"/>). Drives <c>OxygenManager.SetUnderwater</c> automatically
/// for anything with one that enters/exits the water, so swimming/drowning already works with zero
/// extra wiring - <c>OxygenManager</c> already had <c>SetUnderwater</c>, nothing was calling it before
/// this. Not gated by tag/layer: it reacts to whatever has an <c>OxygenManager</c> in its hierarchy
/// (<c>GetComponentInParent</c>), so it works for the player, mobs, or anything else using the same
/// status system.
///
/// This is a coarse, per-chunk trigger volume (a box spanning this chunk's water cells' bounds - see
/// how it's sized in <see cref="EndlessTerrain.TerrainChunk"/>), not an exact match to the water body's
/// true shape. Good enough for oxygen/swim purposes; swap in your own detection if you need
/// pixel-perfect submersion. Toggle <see cref="TerrainGenerator.EnableSwimDetection"/> to skip creating
/// this entirely - water still renders/generates identically either way.
/// </summary>
public class WaterVolume : MonoBehaviour
{
    // Per-collider-hierarchy tracking (not just a bool) so a character with several colliders overlapping
    // the same trigger doesn't get a spurious SetUnderwater(false) when only one of them exits, and so a
    // repeat OnTriggerEnter for the same target doesn't uselessly restart OxygenManager's coroutines.
    private readonly HashSet<OxygenManager> submerged = new HashSet<OxygenManager>();

    private void OnTriggerEnter(Collider other)
    {
        OxygenManager oxygenManager = other.GetComponentInParent<OxygenManager>();
        if (oxygenManager == null)
            return;

        if (submerged.Add(oxygenManager))
        {
            oxygenManager.SetUnderwater(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        OxygenManager oxygenManager = other.GetComponentInParent<OxygenManager>();
        if (oxygenManager == null)
            return;

        if (submerged.Remove(oxygenManager))
        {
            oxygenManager.SetUnderwater(false);
        }
    }

    /// <summary>
    /// Chunks stream out by deactivating their GameObject (see <c>TerrainChunk.SetVisible</c>), which
    /// does NOT fire OnTriggerExit for anything still inside - without this, swimming out of view range
    /// while submerged would leave OxygenManager permanently stuck thinking it's underwater. Note: if the
    /// same connected body of water also overlaps a DIFFERENT (still-active) chunk's water volume at the
    /// same spot, this briefly clears isUnderwater too eagerly until that other volume's own state
    /// catches up on the next trigger event - an acceptable rare edge case given chunks only go invisible
    /// well outside normal swimming range.
    /// </summary>
    private void OnDisable()
    {
        foreach (OxygenManager oxygenManager in submerged)
        {
            if (oxygenManager != null)
                oxygenManager.SetUnderwater(false);
        }
        submerged.Clear();
    }
}
