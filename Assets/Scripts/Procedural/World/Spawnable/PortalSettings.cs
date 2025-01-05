
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Portal settings configuration that inherits from BaseSettings.
/// </summary>
[System.Serializable]
public class PortalSettings : BaseSettings
{
    [Tooltip("List of spawnable portal prefabs.")]
    public List<SpawnablePortal> prefabs;

    [Tooltip("Maximum number of portals allowed.")]
    public int maxNumberOfPortals;
}