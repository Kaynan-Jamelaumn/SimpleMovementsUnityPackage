using UnityEngine;

/// <summary>
/// Whether a character stands on the streamed open-world terrain (<see cref="EndlessTerrain"/>), and whether that
/// ground is ready under a position. The terrain is generated around the players a little ahead of them; a fast
/// move (a long dash, a fall from a cliff into a fresh area) can reach ground whose chunk has no collider yet and
/// fall through it. The movement asks this after each step and stops at the edge of the ready ground instead.
/// </summary>
public static class StreamedGround
{
    /// <summary>
    /// True when <paramref name="who"/> is on the streamed terrain: an active EndlessTerrain lives in its scene and it is
    /// not inside a dungeon run (dungeons stand at their own coordinates, often over unloaded world chunks).
    /// </summary>
    public static bool Applies(GameObject who)
    {
        if (who == null || LoadedTerrain.ChunkSpan <= 0)
            return false;
        bool sameScene = false;
        var terrains = EndlessTerrain.Active;
        for (int i = 0; i < terrains.Count; i++)
        {
            EndlessTerrain t = terrains[i];
            if (t != null && t.isActiveAndEnabled && t.gameObject.scene == who.scene)
            {
                sameScene = true;
                break;
            }
        }
        return sameScene && ProceduralDungeon.DungeonSession.RunOf(who) == null;
    }

    /// <summary>True when the ground under (x, z) can be stood on (see <see cref="LoadedTerrain.IsGroundReady"/>).</summary>
    public static bool IsReady(Vector3 position) => LoadedTerrain.IsGroundReady(position.x, position.z);
}
