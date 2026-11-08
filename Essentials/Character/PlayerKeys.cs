using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// A player's lasting key: the same for the same character in every session and after restarts, so what a player owns
/// (a lease, a room, held items, money) can be kept for them while they are away. Only where the rules run (offline, the
/// server): keys never travel to other machines - other players are named by their session id or display name.
/// <list type="bullet">
/// <item>From the character (<see cref="CharacterSave.id"/>, made on the player's own machine): "c" and a hash of it, so
/// saved files hold no usable secret. A network session does not show the id to other clients, so nobody else can claim it.</item>
/// <item>Offline without a character (start screens skipped): "local" - the one person playing.</item>
/// <item>Networked without a character the server accepted: null - such a player owns nothing that lasts. In a network
/// session a player is only who the server accepted (a host's own player too): the character picked on this machine's
/// start screens does not count until the server has accepted it.</item>
/// </list>
/// A game with real accounts installs <see cref="Resolver"/> (an account id, hashed the same way with <see cref="FromId"/>).
/// The network layer installs its own only when none is installed, so a game's resolver is never replaced.
/// </summary>
public static class PlayerKeys
{
    /// <summary>Key of a player playing offline without a character.</summary>
    public const string Local = "local";

    /// <summary>
    /// Installed by the network layer (or a game with its own accounts), asked first: the key of a player; an empty
    /// string when the player has no key and nothing else may give them one (a networked player whose character the
    /// server has not accepted); or null to fall back to the character the player shows here.
    /// </summary>
    public static Func<CombatEntity, string> Resolver;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Resolver = null;

    /// <summary>The player's lasting key (null when it has none: a networked player without a character).</summary>
    public static string Of(CombatEntity player)
    {
        if (player == null) return null;
        Func<CombatEntity, string> resolver = Resolver;
        if (resolver != null)
        {
            try
            {
                string key = resolver(player);
                // An empty answer is final: this player has no key, and nothing below may make one for them.
                if (key != null) return key.Length > 0 ? key : null;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }
        CharacterSave save = CharacterOf(player);
        bool networked = GameNet.NetAuthority.IsNetworked;
        // In a network session only a strong id makes a key (a short or chosen one could be guessed by someone else).
        if (save != null && !string.IsNullOrEmpty(save.id) && (!networked || CharacterSave.IsStrongId(save.id))) return FromId(save.id);
        return networked ? null : Local;
    }

    /// <summary>The key made from an id (a character's or an account's): "c" and 24 hex digits of its SHA-256.</summary>
    public static string FromId(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        using (SHA256 sha = SHA256.Create())
        {
            byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes("SimpleMovements.PlayerKey|" + id));
            var b = new StringBuilder("c", 25);
            for (int i = 0; i < 12; i++) b.Append(h[i].ToString("x2"));
            return b.ToString();
        }
    }

    /// <summary>Is this a well-formed key (as <see cref="FromId"/> makes, or <see cref="Local"/>, or a resolver's up to 64 safe characters)?</summary>
    public static bool IsValid(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > 64) return false;
        foreach (char c in key)
            if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ':' || c == '.')) return false;
        return true;
    }

    /// <summary>
    /// The character a player shows on this machine; offline, the local player's own when nothing was applied yet. In a
    /// network session only what was applied counts (the server applies a character once it accepted it): the start
    /// screens' character of a host's own player is no key while the server refuses it (one session per character).
    /// </summary>
    private static CharacterSave CharacterOf(CombatEntity player)
    {
        GameObject root = PlayerLocator.MovableRoot(player);
        AppliedCharacter applied = root != null ? root.GetComponent<AppliedCharacter>() : null;
        if (applied == null) applied = player.GetComponentInParent<AppliedCharacter>();
        if (applied != null && applied.Save != null && !string.IsNullOrEmpty(applied.Save.id)) return applied.Save;
        return !GameNet.NetAuthority.IsNetworked && CardTable.IsLocalPlayer(player) ? CharacterSession.Current : null;
    }
}
