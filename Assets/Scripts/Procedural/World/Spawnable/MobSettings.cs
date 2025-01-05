using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


/// <summary>
/// Mob settings configuration that inherits from BaseSettings.
/// </summary>
[System.Serializable]
public class MobSettings : BaseSettings
{
    [Tooltip("List of spawnable mob prefabs.")]
    public List<SpawnableMob> prefabs;

    [Tooltip("Maximum number of mobs allowed.")]
    public int maxNumberOfMobs;
}
