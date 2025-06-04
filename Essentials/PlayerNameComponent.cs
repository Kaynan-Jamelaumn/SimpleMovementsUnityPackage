using UnityEngine;

/// <summary>
/// Simple component to store and manage player name
/// </summary>
public class PlayerNameComponent : MonoBehaviour
{
    [Header("Player Identity")]
    [SerializeField] private string playerName = "";

    // Events
    public System.Action<string> OnNameChanged;

    // Properties
    public string PlayerName => playerName;

    /// <summary>
    /// Set the player's name
    /// </summary>
    public void SetPlayerName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogWarning("Attempted to set empty or null player name");
            return;
        }

        string oldName = playerName;
        playerName = name.Trim();

        if (oldName != playerName)
        {
            OnNameChanged?.Invoke(playerName);
            Debug.Log($"Player name set to: {playerName}");
        }
    }

    /// <summary>
    /// Get the player's display name (handles empty names)
    /// </summary>
    public string GetDisplayName()
    {
        return string.IsNullOrEmpty(playerName) ? "Unnamed Player" : playerName;
    }

    /// <summary>
    /// Check if player has a valid name
    /// </summary>
    public bool HasValidName()
    {
        return !string.IsNullOrEmpty(playerName);
    }
}