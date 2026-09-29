using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Optional scene component that oversees every chunk spawner of the endless terrain: overrides the world-wide mob
/// limit, slows spawning down while the frame rate is low, and shows a debug overlay (F3) with the totals and the
/// state of the nearest chunks' spawners. The spawners work without it. Disabled automatically while the player is
/// in a dungeon (see DungeonWorldPause).
/// </summary>
public class SpawnerManager : MonoBehaviour
{
    [Header("Global Limits")]
    [Tooltip("World-wide mob limit, overriding Mob Settings' Max Mobs In World (0 = use Mob Settings).")]
    [Min(0)] public int globalEntityLimit = 0;

    [Header("Performance")]
    [Tooltip("Slow spawning down while the frame rate is below 80% of Target Frame Rate.")]
    public bool enablePerformanceMonitoring = true;

    [Tooltip("The frame rate the game aims for.")]
    [Range(20f, 240f)] public float targetFrameRate = 60f;

    [Tooltip("While the frame rate is low, spawners tick this many times less often.")]
    [Range(1f, 10f)] public float throttledIntervalMultiplier = 3f;

    [Header("Debug and Visualization")]
    [Tooltip("Show the overlay at start (toggle it in Play mode with F3).")]
    public bool enableDebugUI = false;

    [Tooltip("How many of the nearest chunks the overlay lists.")]
    [Range(0, 40)] public int chunksShown = 12;

    [Tooltip("Tag of the player (to list the chunks nearest to them).")]
    public string playerTag = "Player";

    private float smoothedDeltaTime = 1f / 60f;
    private bool throttled;
    private Rect window = new Rect(10f, 10f, 460f, 360f);
    private Vector2 scroll;
    private readonly List<ChunkSpawnerBase> sorted = new List<ChunkSpawnerBase>();

    /// <summary>True while spawning is slowed down for performance.</summary>
    public bool IsThrottled => throttled;

    private void OnEnable()
    {
        WorldSpawnRegistry.MobLimit = globalEntityLimit;
    }

    private void OnDisable()
    {
        WorldSpawnRegistry.MobLimit = 0;
        WorldSpawnRegistry.IntervalMultiplier = 1f;
        throttled = false;
    }

    private void OnValidate()
    {
        if (Application.isPlaying && isActiveAndEnabled)
            WorldSpawnRegistry.MobLimit = globalEntityLimit;
    }

    private void Update()
    {
        if (TogglePressed())
            enableDebugUI = !enableDebugUI;

        if (!enablePerformanceMonitoring)
        {
            WorldSpawnRegistry.IntervalMultiplier = 1f;
            return;
        }
        smoothedDeltaTime = Mathf.Lerp(smoothedDeltaTime, Time.unscaledDeltaTime, 0.05f);
        float fps = 1f / Mathf.Max(0.0001f, smoothedDeltaTime);
        bool slow = fps < targetFrameRate * 0.8f;
        if (slow != throttled)
        {
            throttled = slow;
            WorldSpawnRegistry.IntervalMultiplier = slow ? throttledIntervalMultiplier : 1f;
        }
    }

    private static bool TogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        return keyboard != null && keyboard.f3Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.F3);
#else
        return false;
#endif
    }

    /// <summary>Totals and the nearest chunks' spawners, as text.</summary>
    public string GetComprehensiveDebugInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Mobs: {WorldSpawnRegistry.MobCount}   Portals: {WorldSpawnRegistry.PortalCount}   Chunk spawners: {Count()}");
        int limit = WorldSpawnRegistry.MobLimit;
        sb.AppendLine($"Mob limit: {(limit > 0 ? limit.ToString() : "from Mob Settings")}   Throttled: {throttled}   Paused: {WorldSpawnRegistry.Paused}");

        sorted.Clear();
        sorted.AddRange(WorldSpawnRegistry.All);
        Transform player = null;
        foreach (Transform p in WorldSpawnRegistry.GetPlayers(playerTag, null))
        {
            player = p;
            break;
        }
        if (player != null)
        {
            Vector3 at = player.position;
            sorted.Sort((a, b) => a.DistanceTo(at).CompareTo(b.DistanceTo(at)));
        }
        int shown = 0;
        foreach (ChunkSpawnerBase s in sorted)
        {
            if (s == null || s.ActiveCount == 0 && !(s is MobSpawner m && m.IsActive))
                continue;
            if (shown++ >= chunksShown)
                break;
            string distance = player != null ? $" {s.DistanceTo(player.position),5:0} m" : "";
            sb.AppendLine($"[{s.Coord.x,4},{s.Coord.y,4}]{distance}  {s.Describe()}");
        }
        return sb.ToString();
    }

    private static int Count()
    {
        int n = 0;
        foreach (ChunkSpawnerBase unused in WorldSpawnRegistry.All)
            n++;
        return n;
    }

    /// <summary>Makes every chunk forget its killed mobs (areas refill) and reopens every closed portal site.</summary>
    [ContextMenu("Reset Killed Mobs And Closed Portals")]
    public void ResetAllSpawnerFailures()
    {
        WorldSpawnRegistry.ClearMobMemory();
        WorldSpawnRegistry.SetClosedPortalSites(null);
    }

    private void OnGUI()
    {
        if (!enableDebugUI)
            return;
        window = GUILayout.Window(0x5A77, window, DrawWindow, "World Spawners (F3)");
    }

    private void DrawWindow(int id)
    {
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label(GetComprehensiveDebugInfo());
        GUILayout.EndScrollView();
        if (GUILayout.Button("Reset killed mobs and closed portals"))
            ResetAllSpawnerFailures();
        GUI.DragWindow();
    }
}
