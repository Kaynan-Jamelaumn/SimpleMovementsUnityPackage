
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuration settings for managing spawnable portals.
/// Inherits common settings from BaseSettings.
/// </summary>
[System.Serializable]
public class PortalSettings : BaseSettings
{
    /// <summary>
    /// List of spawnable portal prefabs.
    /// </summary>
    [Tooltip("List of spawnable portal prefabs.")]
    public List<SpawnablePortal> prefabs;

    /// <summary>
    /// The maximum number of portals allowed to spawn.
    /// </summary>
    [Tooltip("Maximum number of portals allowed.")]
    public int maxNumberOfPortals;
}
