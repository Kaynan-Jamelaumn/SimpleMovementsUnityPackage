using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Marks the template scene that network sessions load once per dungeon run (see
    /// <see cref="ProceduralDungeon.DungeonSceneSettings.networkSceneName"/>). The scene stays empty in the editor: the
    /// server builds the dungeon inside its copy and the clients of the run rebuild each floor from the layout the
    /// server sends them. Created by Tools ▸ SimpleMovements ▸ Network ▸ Create Dungeon Instance Scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class DungeonInstanceSceneMarker : MonoBehaviour
    {
    }
}
