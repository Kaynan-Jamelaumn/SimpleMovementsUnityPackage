using System.Collections.Generic;
using GameNet;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Remembers what a character from the creation screen gave a player (so applying another one removes it exactly).
/// Added to the player by <see cref="CharacterProfileApplier"/>.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("")]
public class AppliedCharacter : MonoBehaviour
{
    /// <summary>The character shown on this player.</summary>
    public CharacterSave Save { get; internal set; }

    internal readonly List<Trait> grantedTraits = new List<Trait>();
}
