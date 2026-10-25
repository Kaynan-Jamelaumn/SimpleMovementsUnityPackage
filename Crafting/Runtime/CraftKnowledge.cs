using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// A player's smithing and leatherworking journal (<see cref="CraftJournalData"/>): the metals they can name, the ores
/// they know, the heat colours they can read, how their quenches went, the insights they have had, what they have made
/// and how. Added to the player the first time they use a crafting station; saved as one JSON file per character in
/// <c>persistentDataPath/Crafting/Journal</c>, so knowledge survives sessions.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Crafting/Craft Knowledge")]
public class CraftKnowledge : MonoBehaviour
{
    [Tooltip("Save the journal to disk (persistentDataPath/Crafting/Journal/<character>.json) and load it back.")]
    [SerializeField] private bool saveToDisk = true;
    [Tooltip("File name used instead of the character's name (empty = the character's name).")]
    [SerializeField] private string saveKey = "";
    [Tooltip("Show what the player learns in the inventory's message line.")]
    [SerializeField] private bool showMessages = true;

    private CraftJournalData data;
    private bool dirty;
    private float saveTimer;

    /// <summary>The journal changed.</summary>
    public event Action Changed;
    /// <summary>The player learnt something.</summary>
    public event Action<string> Learnt;

    public CraftJournalData Data
    {
        get
        {
            if (data == null)
                Load();
            return data;
        }
    }

    // ------------------------------------------------------------------ finding it
    private static CraftKnowledge local;

    /// <summary>The knowledge of this machine's player (null when there is none yet).</summary>
    public static CraftKnowledge Local
    {
        get
        {
            if (local != null && local.isActiveAndEnabled)
                return local;
            IReadOnlyList<CombatEntity> players = CombatEntity.Players;
            for (int i = 0; i < players.Count; i++)
                if (players[i] != null && CardTable.IsLocalPlayer(players[i]))
                {
                    local = players[i].GetComponent<CraftKnowledge>();
                    if (local != null) return local;
                }
            return null;
        }
    }

    /// <summary>The knowledge of the player <paramref name="anyOnPlayer"/> belongs to (added when missing).</summary>
    public static CraftKnowledge For(Component anyOnPlayer)
    {
        if (anyOnPlayer == null)
            return null;
        CraftKnowledge k = anyOnPlayer.GetComponentInParent<CraftKnowledge>();
        if (k != null)
            return k;
        CombatEntity e = CombatEntity.Resolve(anyOnPlayer.gameObject);
        GameObject root = e != null ? e.gameObject : anyOnPlayer.transform.root.gameObject;
        k = root.GetComponent<CraftKnowledge>();
        return k != null ? k : root.AddComponent<CraftKnowledge>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => local = null;

    // ------------------------------------------------------------------ learning
    /// <summary>Something happened at the station the player is working: learn what it teaches.</summary>
    public void Observe(CraftEvent e, CraftPiece piece)
    {
        string insight = CraftJournal.InsightOf(e, piece);
        if (insight != null)
            Report(CraftJournal.Learn(Data, insight));
    }

    public void LearnInsight(string id) => Report(CraftJournal.Learn(Data, id));

    /// <summary>The player worked a metal (struck, ground, filed it): after enough work it is recognised.</summary>
    public void Worked(CraftRules rules, CraftAlloyProfile p) => Report(CraftJournal.Worked(Data, rules, p), true);

    public void Assayed(CraftRules rules, CraftAlloyProfile p) => Report(CraftJournal.Assayed(Data, rules, p), true);

    public void HandledOre(CraftRules rules, string ore, bool smelted, int deposit = 0, string result = null) =>
        Report(CraftJournal.HandledOre(Data, rules, ore, smelted, deposit, result), true);

    /// <summary>The player read the thermometer while the metal showed a colour.</summary>
    public void ReadHeat(CraftRules rules, string color, double celsius) => Report(CraftJournal.ReadHeat(Data, rules, color, celsius), true);

    public void Quenched(CraftAlloyProfile p, string medium, bool hardened, bool cracked) =>
        Report(CraftJournal.Quenched(Data, p, medium, hardened, cracked), true);

    public void UsedAgent(string agent)
    {
        if (string.IsNullOrEmpty(agent) || Data.agents.Contains(agent))
            return;
        Data.agents.Add(agent);
        MarkDirty();
    }

    /// <summary>A finished item, with the steps that made it.</summary>
    public void Made(CraftItemReport report, List<string> procedure)
    {
        foreach (string line in CraftJournal.Made(Data, report, procedure, DateTime.UtcNow.Ticks))
            Report(line);
        MarkDirty();
    }

    private void Report(string learnt, bool changed = false)
    {
        if (string.IsNullOrEmpty(learnt))
        {
            if (changed) MarkDirty();
            return;
        }
        MarkDirty();
        Learnt?.Invoke(learnt);
        if (showMessages)
            InventoryManager.For(this)?.ShowMessage(learnt, InventoryFeedback.Kind.Info);
    }

    public void MarkDirty()
    {
        dirty = true;
        Changed?.Invoke();
    }

    /// <summary>Forgets everything (debug, a new character).</summary>
    public void ResetKnowledge()
    {
        data = new CraftJournalData();
        MarkDirty();
    }

    /// <summary>Replaces the journal with <paramref name="json"/> (debug tools, a network snapshot).</summary>
    public bool Import(string json)
    {
        if (string.IsNullOrEmpty(json))
            return false;
        data = CraftJournalData.FromJson(json);
        MarkDirty();
        return true;
    }

    public string Export() => Data.ToJson();

    // ------------------------------------------------------------------ saving
    /// <summary>
    /// A client of a network session: the journal is the server's (it is kept with the character there and sent while a
    /// station is worked), never read from or written to this machine's disk - a client cannot teach itself.
    /// </summary>
    public static bool FollowsServer => GameNet.NetAuthority.IsNetworked && !GameNet.NetAuthority.IsServer;

    /// <summary>A network client: the journal as the server has it.</summary>
    public void ReceiveFromServer(string json)
    {
        if (string.IsNullOrEmpty(json))
            return;
        data = CraftJournalData.FromJson(json) ?? new CraftJournalData();
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
            return Path.Combine(Application.persistentDataPath, "Crafting", "Journal", key + ".json");
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
            Debug.LogWarning($"[Crafting] Could not save the journal: {e.Message}", this);
        }
    }

    private void Load()
    {
        data = new CraftJournalData();
        if (!saveToDisk || FollowsServer)
            return;
        try
        {
            string path = SavePath;
            if (File.Exists(path))
                data = CraftJournalData.FromJson(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Crafting] Could not read the journal: {e.Message}", this);
        }
    }
}
