using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// A player's harvesting skills and journal (<see cref="HarvestSkillData"/>): Skinning, Butchering, Anatomy, Bone
/// Harvesting, Tendon Extraction, Organ Harvesting and Trophy Extraction, what they know of each species, their best
/// work and their last harvests. Added to the player the first time they harvest; saved as one JSON file per character
/// in <c>persistentDataPath/Harvesting/Skills</c>. In a network session the server keeps it and sends it to its player.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Harvesting/Harvest Knowledge")]
public class HarvestKnowledge : MonoBehaviour
{
    [Tooltip("Save the skills to disk (persistentDataPath/Harvesting/Skills/<character>.json) and load them back.")]
    [SerializeField] private bool saveToDisk = true;
    [Tooltip("File name used instead of the character's name (empty = the character's name).")]
    [SerializeField] private string saveKey = "";
    [Tooltip("Show level-ups and techniques learnt in the inventory's message line.")]
    [SerializeField] private bool showMessages = true;

    private HarvestSkillData data;
    private bool dirty;
    private float saveTimer;

    /// <summary>The skills changed (experience, the journal).</summary>
    public event Action Changed;
    /// <summary>A level or a technique was gained ("Skinning rises to 10.").</summary>
    public event Action<string> Learnt;

    public HarvestSkillData Data
    {
        get
        {
            if (data == null)
                Load();
            return data;
        }
    }

    // ------------------------------------------------------------------ finding it
    private static HarvestKnowledge local;

    /// <summary>The skills of this machine's player (null when there is none yet).</summary>
    public static HarvestKnowledge Local
    {
        get
        {
            if (local != null && local.isActiveAndEnabled)
                return local;
            IReadOnlyList<CombatEntity> players = CombatEntity.Players;
            for (int i = 0; i < players.Count; i++)
                if (players[i] != null && CardTable.IsLocalPlayer(players[i]))
                {
                    local = For(players[i]);
                    return local;
                }
            return null;
        }
    }

    /// <summary>The skills of the player <paramref name="anyOnPlayer"/> belongs to (added when missing).</summary>
    public static HarvestKnowledge For(Component anyOnPlayer)
    {
        if (anyOnPlayer == null)
            return null;
        HarvestKnowledge k = anyOnPlayer.GetComponentInParent<HarvestKnowledge>();
        if (k != null)
            return k;
        CombatEntity e = CombatEntity.Resolve(anyOnPlayer.gameObject);
        GameObject root = e != null ? e.gameObject : anyOnPlayer.transform.root.gameObject;
        k = root.GetComponent<HarvestKnowledge>();
        return k != null ? k : root.AddComponent<HarvestKnowledge>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => local = null;

    // ------------------------------------------------------------------ learning
    /// <summary>Adds experience to a skill; tells the player of a new level or technique. Returns what was learnt.</summary>
    public List<string> Gain(HarvestSkill skill, float xp)
    {
        List<string> said = HarvestSkills.Gain(HarvestDatabase.Instance.Settings, Data, skill, xp);
        MarkDirty();
        foreach (string s in said)
        {
            Learnt?.Invoke(s);
            if (showMessages)
                InventoryManager.For(this)?.ShowMessage(s, InventoryFeedback.Kind.Info);
        }
        return said;
    }

    /// <summary>A harvest of <paramref name="anatomyId"/> was done: the species note (harvests, pieces, best work, what was found in it).</summary>
    public void Harvested(string anatomyId, IEnumerable<string> materials, int pieces, float bestQuality, string logLine)
    {
        HarvestSpeciesNote n = Data.Species(anatomyId, true);
        if (n != null)
        {
            n.harvests++;
            n.pieces += Math.Max(0, pieces);
            n.bestQuality = Math.Max(n.bestQuality, bestQuality);
            if (materials != null)
                foreach (string m in materials)
                    if (!string.IsNullOrEmpty(m) && !n.known.Contains(m)) n.known.Add(m);
        }
        Data.harvests++;
        Data.pieces += Math.Max(0, pieces);
        Data.Log(logLine);
        MarkDirty();
    }

    public void MarkDirty()
    {
        dirty = true;
        Changed?.Invoke();
    }

    /// <summary>Forgets everything (debug, a new character).</summary>
    public void ResetSkills()
    {
        data = new HarvestSkillData();
        MarkDirty();
    }

    /// <summary>Replaces the skills with <paramref name="json"/> (debug tools).</summary>
    public bool Import(string json)
    {
        if (string.IsNullOrEmpty(json))
            return false;
        data = HarvestSkillData.FromJson(json);
        MarkDirty();
        return true;
    }

    public string Export() => Data.ToJson();

    // ------------------------------------------------------------------ saving
    /// <summary>A client of a network session: the skills are the server's, never read from or written to this machine's disk.</summary>
    public static bool FollowsServer => GameNet.NetAuthority.IsNetworked && !GameNet.NetAuthority.IsServer;

    /// <summary>A network client: the skills as the server has them.</summary>
    public void ReceiveFromServer(string json)
    {
        if (string.IsNullOrEmpty(json))
            return;
        data = HarvestSkillData.FromJson(json);
        dirty = false;
        Changed?.Invoke();
    }

    private void Update()
    {
        if (!dirty || !saveToDisk || FollowsServer)
            return;
        saveTimer += Time.unscaledDeltaTime;
        if (saveTimer >= 3f)
            Save();
    }

    private void OnDisable()
    {
        if (dirty && saveToDisk)
            Save();
    }

    private void OnApplicationQuit()
    {
        if (dirty && saveToDisk)
            Save();
    }

    public string SavePath
    {
        get
        {
            string key = !string.IsNullOrWhiteSpace(saveKey) ? saveKey : CharacterKey();
            foreach (char c in Path.GetInvalidFileNameChars())
                key = key.Replace(c, '_');
            return Path.Combine(Application.persistentDataPath, "Harvesting", "Skills", key + ".json");
        }
    }

    private string CharacterKey()
    {
        CombatEntity e = CombatEntity.Resolve(gameObject);
        string n = e != null ? PartySystem.NameOf(e) : "";
        return string.IsNullOrWhiteSpace(n) ? "Player" : n.Trim();
    }

    public void Save()
    {
        dirty = false;
        saveTimer = 0f;
        if (data == null || !saveToDisk || FollowsServer)
            return;
        try
        {
            string path = SavePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, data.ToJson());
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Harvesting] Could not save the skills: {e.Message}", this);
        }
    }

    private void Load()
    {
        data = new HarvestSkillData();
        if (!saveToDisk || FollowsServer)
            return;
        try
        {
            string path = SavePath;
            if (File.Exists(path))
                data = HarvestSkillData.FromJson(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Harvesting] Could not read the skills: {e.Message}", this);
        }
    }
}
