using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Configuration settings for managing spawnable mobs.
/// Inherits common settings from BaseSettings.
/// </summary>
[System.Serializable]
public class MobSettings : BaseSettings
{
    /// <summary>
    /// List of spawnable mob prefabs.
    /// </summary>
    [Tooltip("List of spawnable mob prefabs.")]
    public List<SpawnableMob> prefabs;

    /// <summary>
    /// The maximum number of mobs allowed to spawn.
    /// </summary>
    [Tooltip("Maximum number of mobs allowed.")]
    public int maxNumberOfMobs;
}
