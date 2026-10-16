using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// A character made on the creation screen: its name, race, class, the traits picked, height and appearance. Races,
/// classes and traits are stored by asset name (the same on every machine running the same build, see
/// <see cref="CharacterCatalog"/>). Saved as one JSON file per character (<see cref="CharacterSaveStore"/>) and sent to
/// the server when the player joins (<see cref="Encode"/>), which checks it before applying it.
/// </summary>
[Serializable]
public class CharacterSave
{
    public int version = 1;
    public string id;
    public string characterName;
    public string race;
    public string playerClass;
    public List<string> traits = new List<string>();
    public float height;
    public CharacterAppearanceData appearance = new CharacterAppearanceData();
    public long createdUtc;
    public long lastPlayedUtc;

    /// <summary>Longest text accepted from the network.</summary>
    public const int MaxEncodedLength = 4096;

    public static CharacterSave New(string name)
    {
        return new CharacterSave
        {
            id = Guid.NewGuid().ToString("N"),
            characterName = name,
            createdUtc = DateTime.UtcNow.Ticks,
            lastPlayedUtc = DateTime.UtcNow.Ticks,
        };
    }

    public CharacterSave Clone()
    {
        CharacterSave c = Decode(Encode());
        return c ?? new CharacterSave();
    }

    /// <summary>"Elf · Ranger" (what is set).</summary>
    public string Summary(CharacterCatalog catalog = null)
    {
        catalog = catalog != null ? catalog : CharacterCatalog.Instance;
        CharacterArchetype r = catalog != null ? catalog.FindRace(race) : null;
        PlayerClass c = catalog != null ? catalog.FindClass(playerClass) : null;
        var parts = new List<string>();
        parts.Add(r != null ? r.Name : !string.IsNullOrEmpty(race) ? race : "No race");
        parts.Add(c != null ? c.GetClassName() : !string.IsNullOrEmpty(playerClass) ? playerClass : "No class");
        return string.Join("  ·  ", parts);
    }

    public string Encode() => JsonUtility.ToJson(this);

    /// <summary>Reads <see cref="Encode"/>'s text (null when it is not a character).</summary>
    public static CharacterSave Decode(string json)
    {
        if (string.IsNullOrEmpty(json) || json.Length > MaxEncodedLength)
            return null;
        try
        {
            CharacterSave s = JsonUtility.FromJson<CharacterSave>(json);
            if (s == null)
                return null;
            if (s.traits == null) s.traits = new List<string>();
            if (s.appearance == null) s.appearance = new CharacterAppearanceData();
            if (s.traits.Count > 64) s.traits.RemoveRange(64, s.traits.Count - 64);
            if (float.IsNaN(s.height) || float.IsInfinity(s.height)) s.height = 0f;
            return s;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>
/// The characters of this machine: one JSON file each in <c>persistentDataPath/Characters</c>. Several game windows
/// on the same machine (host and clients) share them, so each one can pick a different character.
/// </summary>
public static class CharacterSaveStore
{
    public static string Folder => Path.Combine(Application.persistentDataPath, "Characters");

    /// <summary>Every saved character, the most recently played first.</summary>
    public static List<CharacterSave> LoadAll()
    {
        var list = new List<CharacterSave>();
        try
        {
            if (!Directory.Exists(Folder))
                return list;
            foreach (string file in Directory.GetFiles(Folder, "*.json"))
            {
                try
                {
                    CharacterSave s = CharacterSave.Decode(File.ReadAllText(file));
                    if (s == null)
                        continue;
                    if (string.IsNullOrEmpty(s.id))
                        s.id = Path.GetFileNameWithoutExtension(file);
                    list.Add(s);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Characters] Could not read {file}: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Characters] Could not list the saved characters: {e.Message}");
        }
        list.Sort((a, b) => b.lastPlayedUtc.CompareTo(a.lastPlayedUtc));
        return list;
    }

    /// <summary>The character played last (null when there is none).</summary>
    public static CharacterSave LastPlayed()
    {
        List<CharacterSave> all = LoadAll();
        return all.Count > 0 ? all[0] : null;
    }

    /// <summary>The saved character with that name (case-insensitive), or null.</summary>
    public static CharacterSave FindByName(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
            return null;
        foreach (CharacterSave s in LoadAll())
            if (string.Equals(s.characterName, characterName.Trim(), StringComparison.OrdinalIgnoreCase))
                return s;
        return null;
    }

    public static bool NameTaken(string characterName, string exceptId = null)
    {
        CharacterSave s = FindByName(characterName);
        return s != null && s.id != exceptId;
    }

    public static bool Save(CharacterSave save)
    {
        if (save == null)
            return false;
        if (string.IsNullOrEmpty(save.id))
            save.id = Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(PathOf(save), JsonUtility.ToJson(save, true));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Characters] Could not save '{save.characterName}': {e.Message}");
            return false;
        }
    }

    public static void Delete(CharacterSave save)
    {
        if (save == null || string.IsNullOrEmpty(save.id))
            return;
        try
        {
            string path = PathOf(save);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Characters] Could not delete '{save.characterName}': {e.Message}");
        }
    }

    /// <summary>Marks the character as played now (it is listed first next time) and saves it.</summary>
    public static void MarkPlayed(CharacterSave save)
    {
        if (save == null)
            return;
        save.lastPlayedUtc = DateTime.UtcNow.Ticks;
        Save(save);
    }

    private static string PathOf(CharacterSave save)
    {
        string safe = save.id;
        foreach (char c in Path.GetInvalidFileNameChars())
            safe = safe.Replace(c, '_');
        return Path.Combine(Folder, safe + ".json");
    }
}

/// <summary>The character this machine plays in the current session (picked on the start screens).</summary>
public static class CharacterSession
{
    /// <summary>Set before the game starts; applied to the local player when it appears (offline or networked).</summary>
    public static CharacterSave Current { get; set; }

    /// <summary>Raised when <see cref="Current"/> changes.</summary>
    public static event Action<CharacterSave> Changed;

    public static void Set(CharacterSave save)
    {
        Current = save;
        Changed?.Invoke(save);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Current = null;
        Changed = null;
    }
}
