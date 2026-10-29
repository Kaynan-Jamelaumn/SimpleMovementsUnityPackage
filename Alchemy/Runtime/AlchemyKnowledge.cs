using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// A player's alchemy journal (<see cref="AlchemyJournalData"/>): discoveries and how they were made, reactions seen
/// and understood, identified substances, ingredients used, the last experiments. Added to the player automatically
/// the first time they use an alchemy station. Saved as one JSON file per character in
/// <c>persistentDataPath/Alchemy</c> (by the character's name), so knowledge survives sessions.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Alchemy/Alchemy Knowledge")]
public class AlchemyKnowledge : MonoBehaviour
{
    [Tooltip("Save the journal to disk (persistentDataPath/Alchemy/<character>.json) and load it back.")]
    [SerializeField] private bool saveToDisk = true;
    [Tooltip("File name used instead of the character's name (empty = the character's name).")]
    [SerializeField] private string saveKey = "";
    [Tooltip("Show what the player learns (\"Discovery: Healing Draught!\") in the inventory's message line.")]
    [SerializeField] private bool showMessages = true;

    private AlchemyJournalData data;
    private bool dirty;
    private float saveTimer;

    /// <summary>The journal changed (the station window and the journal page redraw).</summary>
    public event Action Changed;
    /// <summary>The player learnt something ("You understand Kindling: ...").</summary>
    public event Action<string> Learnt;

    public AlchemyJournalData Data
    {
        get
        {
            if (data == null)
                Load();
            return data;
        }
    }

    // ------------------------------------------------------------------ finding it
    private static AlchemyKnowledge local;

    /// <summary>The knowledge of this machine's player (null when there is none yet).</summary>
    public static AlchemyKnowledge Local
    {
        get
        {
            if (local != null && local.isActiveAndEnabled)
                return local;
            IReadOnlyList<CombatEntity> players = CombatEntity.Players;
            for (int i = 0; i < players.Count; i++)
                if (players[i] != null && CardTable.IsLocalPlayer(players[i]))
                {
                    local = players[i].GetComponent<AlchemyKnowledge>();
                    if (local != null) return local;
                }
            return null;
        }
    }

    /// <summary>The knowledge of the player <paramref name="anyOnPlayer"/> belongs to (added when missing).</summary>
    public static AlchemyKnowledge For(Component anyOnPlayer)
    {
        if (anyOnPlayer == null)
            return null;
        AlchemyKnowledge k = anyOnPlayer.GetComponentInParent<AlchemyKnowledge>();
        if (k != null)
            return k;
        CombatEntity e = CombatEntity.Resolve(anyOnPlayer.gameObject);
        GameObject root = e != null ? e.gameObject : anyOnPlayer.transform.root.gameObject;
        k = root.GetComponent<AlchemyKnowledge>();
        return k != null ? k : root.AddComponent<AlchemyKnowledge>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => local = null;

    // ------------------------------------------------------------------ learning
    /// <summary>What the player sees in <paramref name="brew"/> now (reactions, substances in glass).</summary>
    public void Observe(AlchemyBrew brew) => Report(AlchemyJournal.Observe(Data, brew));

    public void RecordIngredient(string id, string name, int count)
    {
        AlchemyJournal.RecordIngredient(Data, id, name, count);
        MarkDirty();
    }

    /// <summary>A finished result (bottled, collected or poured away).</summary>
    public void RecordResult(AlchemyBrew brew, AlchemyResult result, int bottles) =>
        Report(AlchemyJournal.RecordResult(Data, brew, result, bottles, DateTime.UtcNow.Ticks), true);

    /// <summary>How practised the player is at alchemy (0 novice - 1 complete mastery).</summary>
    public double Mastery => CraftMastery.Of(Data.mastery, CraftDiscipline.Alchemy);

    /// <summary>
    /// Practice at alchemy: <paramref name="xp"/> experience (small work - <paramref name="work"/> - only so fast; a
    /// bottled potion always counts). Tells the player when they reach a new rank.
    /// </summary>
    public void Practised(double xp, bool work, bool finishedOne = false)
    {
        string line = CraftMastery.Practise(Data.mastery, CraftDiscipline.Alchemy, xp, work, Time.unscaledTimeAsDouble, finishedOne);
        Report(line != null ? new List<string> { line } : new List<string>(), true);
    }

    private void Report(List<string> learnt, bool changed = false)
    {
        if (learnt.Count > 0 || changed)
            MarkDirty();
        foreach (string line in learnt)
        {
            Learnt?.Invoke(line);
            if (showMessages)
                InventoryManager.For(this)?.ShowMessage(line, InventoryFeedback.Kind.Info);
        }
    }

    public void MarkDirty()
    {
        dirty = true;
        Changed?.Invoke();
    }

    /// <summary>Forgets everything (debug, a new character).</summary>
    public void ResetKnowledge()
    {
        data = new AlchemyJournalData();
        MarkDirty();
    }

    /// <summary>Replaces the journal with <paramref name="json"/> (debug tools, a network snapshot).</summary>
    public bool Import(string json)
    {
        if (string.IsNullOrEmpty(json))
            return false;
        data = AlchemyJournalData.FromJson(json);
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
        data = AlchemyJournalData.FromJson(json) ?? new AlchemyJournalData();
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

    /// <summary>The file this journal is saved to.</summary>
    public string SavePath
    {
        get
        {
            string key = !string.IsNullOrWhiteSpace(saveKey) ? saveKey : CharacterKey();
            foreach (char c in Path.GetInvalidFileNameChars())
                key = key.Replace(c, '_');
            return Path.Combine(Application.persistentDataPath, "Alchemy", key + ".json");
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
            Debug.LogWarning($"[Alchemy] Could not save the journal: {e.Message}", this);
        }
    }

    private void Load()
    {
        data = new AlchemyJournalData();
        if (!saveToDisk || FollowsServer)
            return;
        try
        {
            string path = SavePath;
            if (File.Exists(path))
                data = AlchemyJournalData.FromJson(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Alchemy] Could not read the journal: {e.Message}", this);
        }
    }
}
