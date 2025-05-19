using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Interface defining properties required for entities spawnable by a spawner.
/// Ensures consistency across spawnable types.
/// </summary>
public interface ISpawbleBySpawner
{
    /// <summary>
    /// The maximum number of instances allowed for the spawnable entity.
    /// </summary>
    int MaxInstances { get; set; }

    /// <summary>
    /// The current number of instances spawned for the entity.
    /// </summary>
    int CurrentInstances { get; set; }
}
