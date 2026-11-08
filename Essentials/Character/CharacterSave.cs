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

    /// <summary>
    /// Is the id a strong one (32 hexadecimal digits, as <see cref="New"/> makes)? Only such ids make lasting player keys in
    /// a network session: a short or chosen id could be guessed.
    /// </summary>
    public static bool IsStrongId(string id)
    {
        if (id == null || id.Length != 32) return false;
        foreach (char c in id)
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
        return true;
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
/// The characters of this machine: one JSON file each in <c>persistentDataPath/Characters</c>, named after the
/// character's id. Several game windows on the same machine (host and clients) share them, so each one can pick a
/// different character. An old or hand-made character (a weak id) is given its strong id once, under a lock the windows
/// share, so it never becomes two characters with different keys.
/// </summary>
public static class CharacterSaveStore
{
    public static string Folder => Path.Combine(Application.persistentDataPath, "Characters");

    /// <summary>Every saved character, the most recently played first.</summary>
    public static List<CharacterSave> LoadAll()
    {
        List<CharacterSave> list = Load(out bool moved);
        // Another window moved a file while this one listed: listed again, so no character is missing.
        return moved ? Load(out _) : list;
    }

    private static List<CharacterSave> Load(out bool moved)
    {
        moved = false;
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
                    if (!CharacterSave.IsStrongId(s.id))
                        s = GiveStrongId(file, s);
                    else
                        MoveToItsName(file, s); // a move a crash (or another window reading it) stopped
                    if (s == null)
                    {
                        moved = true;
                        continue;
                    }
                    list.Add(s);
                }
                catch (FileNotFoundException)
                {
                    moved = true; // moved or deleted by another window meanwhile
                }
                catch (DirectoryNotFoundException)
                {
                    moved = true;
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
        // One entry per character: a copy a move left behind is not listed twice (the newest one counts).
        var seen = new HashSet<string>();
        list.RemoveAll(c => CharacterSave.IsStrongId(c.id) && !seen.Add(c.id));
        return list;
    }

    /// <summary>
    /// Gives an old or hand-made character (a weak id) a strong one - once, for every game window of this machine. Under a
    /// lock the windows share, its file is read again; the new id is written into that same file (aside first, then
    /// swapped in), and the file then takes its new name. A window that comes second finds the strong id already there, so
    /// a character is never split into two with different keys. When it cannot be done now (another window keeps the lock,
    /// the disk refuses), the character keeps its old id for now - which makes no key in a network session - and a later
    /// listing tries again: no new id is drawn on every listing. Null: the file went meanwhile (another window moved it).
    /// </summary>
    private static CharacterSave GiveStrongId(string file, CharacterSave read)
    {
        FileStream held = TakeLock();
        try
        {
            if (!File.Exists(file))
                return null;
            CharacterSave s = CharacterSave.Decode(File.ReadAllText(file)) ?? read;
            if (CharacterSave.IsStrongId(s.id))
            {
                MoveToItsName(file, s); // done by another window that could not rename it
                return s;
            }
            if (held == null)
                return Unmigrated(file, s);
            string weak = s.id;
            // Without an id it went by its file's name (as before): a name that is a strong id stays its id.
            string name = Path.GetFileNameWithoutExtension(file);
            s.id = string.IsNullOrEmpty(weak) && CharacterSave.IsStrongId(name) ? name : Guid.NewGuid().ToString("N");
            if (!WriteOver(file, s))
            {
                s.id = weak;
                return Unmigrated(file, s);
            }
            MoveToItsName(file, s);
            return s;
        }
        finally
        {
            ReleaseLock(held);
        }
    }

    /// <summary>
    /// A character whose id could not be made strong now: without an id it goes by its file's name (as before), and its
    /// file takes the name of its id - so saving it writes over that same file, never a second one.
    /// </summary>
    private static CharacterSave Unmigrated(string file, CharacterSave s)
    {
        if (string.IsNullOrEmpty(s.id))
            s.id = Path.GetFileNameWithoutExtension(file);
        MoveToItsName(file, s);
        return s;
    }

    /// <summary>Writes a character over its file: aside first, then swapped in (the file is never half written).</summary>
    private static bool WriteOver(string file, CharacterSave s)
    {
        string temp = file + ".tmp";
        try
        {
            using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var w = new StreamWriter(fs))
            {
                w.Write(JsonUtility.ToJson(s, true));
                w.Flush();
                fs.Flush(true);
            }
            File.Replace(temp, file, null);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Characters] Could not give '{s.characterName}' its lasting id ({e.Message}): tried again next time.");
            try { if (File.Exists(temp)) File.Delete(temp); } catch (Exception) { }
            return false;
        }
    }

    /// <summary>A character file takes the name of its id (a file already there under that name is left alone).</summary>
    private static void MoveToItsName(string file, CharacterSave s)
    {
        string target = PathOf(s);
        if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(target), StringComparison.Ordinal) || File.Exists(target))
            return;
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(file, target);
                return;
            }
            catch (Exception)
            {
                // Another window may be reading it (Windows): a moment later, else at the next listing.
                if (attempt >= 2 || !File.Exists(file)) return;
                System.Threading.Thread.Sleep(20);
            }
        }
    }

    private static DateTime lockRefusedUntil;

    /// <summary>
    /// The lock the game windows of this machine share while one gives a character its strong id: a lock of the operating
    /// system on a file in the folder (released when the window ends, even by a crash). Null when another window keeps it
    /// for half a second (then not asked again for a while, so listing never stalls).
    /// </summary>
    private static FileStream TakeLock()
    {
        if (DateTime.UtcNow < lockRefusedUntil)
            return null;
        string path = Path.Combine(Folder, "ids.lock");
        for (int attempt = 0; attempt < 10; attempt++)
        {
            FileStream fs = null;
            try
            {
                fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                try
                {
                    fs.Lock(0, 1);
                    return fs;
                }
                catch (IOException)
                {
                    fs.Dispose(); // another process holds it
                }
                catch (Exception)
                {
                    return fs; // no such locks on this platform: the file's sharing is all there is
                }
            }
            catch (Exception)
            {
                fs?.Dispose(); // open in another window (the file's sharing)
            }
            System.Threading.Thread.Sleep(50);
        }
        lockRefusedUntil = DateTime.UtcNow.AddSeconds(30);
        return null;
    }

    private static void ReleaseLock(FileStream held)
    {
        if (held == null) return;
        try { held.Unlock(0, 1); } catch (Exception) { }
        try { held.Dispose(); } catch (Exception) { }
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

    /// <summary>Deletes a character: its file, and any other file of the same character (a copy a move left behind).</summary>
    public static void Delete(CharacterSave save)
    {
        if (save == null || string.IsNullOrEmpty(save.id))
            return;
        try
        {
            string path = PathOf(save);
            if (File.Exists(path))
                File.Delete(path);
            if (!CharacterSave.IsStrongId(save.id) || !Directory.Exists(Folder))
                return;
            foreach (string file in Directory.GetFiles(Folder, "*.json"))
            {
                try
                {
                    CharacterSave other = CharacterSave.Decode(File.ReadAllText(file));
                    if (other != null && other.id == save.id)
                        File.Delete(file);
                }
                catch (Exception) { } // gone already, or not readable: not this character's
            }
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
