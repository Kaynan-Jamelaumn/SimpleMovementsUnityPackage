using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// One player's agriculture, on the player: their skills (<see cref="AgriSkillData"/>), the pollen on their brush and
/// their field notebook. Added by itself the first time it is needed. Where the rules run (offline, the server) the
/// skills are saved to persistentDataPath/Agriculture/Skills/&lt;character&gt;.json (3 s after a change and on quit); a
/// network client takes them from the server. The notebook is the player's own: kept on their own machine
/// (Agriculture/Notebook), never written by the game.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Agriculture/Agri Knowledge")]
public class AgriKnowledge : MonoBehaviour
{
    [SerializeField] private bool saveToDisk = true;
    [Tooltip("The file name (empty = the character's name).")]
    [SerializeField] private string saveKey = "";
    [Tooltip("Show \"Botany rises to 3.\" above the hotbar.")]
    [SerializeField] private bool showMessages = true;

    private AgriSkillData data;
    private AgriNotebook notebook;
    private bool loaded, notebookLoaded, dirty, notebookDirty;
    private float saveTimer;
    private CombatEntity entity;

    public event Action Changed;
    public event Action<string> Learnt;

    /// <summary>The pollen on the player's brush (for a day after it was collected).</summary>
    public PollenState Pollen { get; set; }

    public AgriSkillData Data
    {
        get
        {
            if (!loaded) Load();
            return data ??= new AgriSkillData();
        }
    }

    public AgriNotebook Notebook
    {
        get
        {
            if (!notebookLoaded) LoadNotebook();
            return notebook ??= new AgriNotebook();
        }
    }

    private static AgriKnowledge local;

    /// <summary>The local player's knowledge (null on a dedicated server).</summary>
    public static AgriKnowledge Local
    {
        get
        {
            if (local != null) return local;
            foreach (CombatEntity e in CombatEntity.Players)
                if (CardTable.IsLocalPlayer(e)) return local = For(e);
            return null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => local = null;

    public static AgriKnowledge For(Component anyOnPlayer)
    {
        if (anyOnPlayer == null) return null;
        AgriKnowledge k = anyOnPlayer.GetComponentInParent<AgriKnowledge>();
        if (k != null) return k;
        CombatEntity e = CombatEntity.Resolve(anyOnPlayer.gameObject);
        GameObject root = e != null ? e.gameObject : anyOnPlayer.transform.root.gameObject;
        k = root.GetComponent<AgriKnowledge>();
        return k != null ? k : root.AddComponent<AgriKnowledge>();
    }

    private AgriSettings Settings => AgricultureDatabase.Instance.Settings;

    /// <summary>Adds experience; returns the lines to show ("Botany rises to 3.").</summary>
    public List<string> Gain(AgriSkill skill, float xp)
    {
        List<string> lines = AgriSkills.Gain(Settings, Data, skill, xp);
        MarkDirty();
        foreach (string s in lines)
        {
            Learnt?.Invoke(s);
            if (showMessages) InventoryManager.For(this)?.ShowMessage(s, InventoryFeedback.Kind.Info);
        }
        return lines;
    }

    public int Level(AgriSkill skill) => AgriSkills.Level(Settings, Data, skill);

    public void MarkDirty()
    {
        dirty = true;
        saveTimer = 0f;
        Changed?.Invoke();
    }

    /// <summary>Writes a line in the notebook (only the player decides what goes in).</summary>
    public void Note(string field, string text)
    {
        Notebook.Note(FarmClock.Now, field, text);
        notebookDirty = true;
        saveTimer = 0f;
        Changed?.Invoke();
    }

    public void RemoveNote(int index)
    {
        if (index < 0 || index >= Notebook.notes.Count) return;
        Notebook.notes.RemoveAt(index);
        notebookDirty = true;
        Changed?.Invoke();
    }

    public string Export() => Data.ToJson();

    public bool Import(string json)
    {
        AgriSkillData d = AgriSkillData.FromJson(json);
        if (d == null) return false;
        data = d;
        loaded = true;
        MarkDirty();
        return true;
    }

    /// <summary>On a network client: skills come from the server, and are never saved here.</summary>
    public static bool FollowsServer => GameNet.NetAuthority.IsNetworked && !GameNet.NetAuthority.IsServer;

    public void ReceiveFromServer(string json)
    {
        data = AgriSkillData.FromJson(json);
        loaded = true;
        Changed?.Invoke();
    }

    private void Update()
    {
        if (!dirty && !notebookDirty) return;
        saveTimer += Time.unscaledDeltaTime;
        if (saveTimer >= 3f) Save();
    }

    private void OnDisable()
    {
        if (dirty || notebookDirty) Save();
    }

    private void OnApplicationQuit()
    {
        if (dirty || notebookDirty) Save();
    }

    // ------------------------------------------------------------------ files
    private string Key
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(saveKey)) return saveKey;
            entity ??= CombatEntity.Resolve(gameObject);
            string n = entity != null ? PartySystem.NameOf(entity) : "";
            if (string.IsNullOrWhiteSpace(n)) n = "Player";
            foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n;
        }
    }

    public string SavePath => Path.Combine(Application.persistentDataPath, "Agriculture", "Skills", Key + ".json");
    public string NotebookPath => Path.Combine(Application.persistentDataPath, "Agriculture", "Notebook", Key + ".json");

    public void Save()
    {
        saveTimer = 0f;
        if (!saveToDisk || !Application.isPlaying) { dirty = notebookDirty = false; return; }
        try
        {
            if (dirty && !FollowsServer)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                File.WriteAllText(SavePath, Data.ToJson());
            }
            if (notebookDirty)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(NotebookPath));
                File.WriteAllText(NotebookPath, Notebook.ToJson());
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Agriculture: could not save {Key}'s skills or notebook ({e.Message}).", this);
        }
        dirty = notebookDirty = false;
    }

    private void Load()
    {
        loaded = true;
        if (!saveToDisk || !Application.isPlaying || FollowsServer) return;
        try
        {
            if (File.Exists(SavePath)) data = AgriSkillData.FromJson(File.ReadAllText(SavePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Agriculture: could not read {Key}'s skills ({e.Message}).", this);
        }
    }

    private void LoadNotebook()
    {
        notebookLoaded = true;
        if (!saveToDisk || !Application.isPlaying) return;
        try
        {
            if (File.Exists(NotebookPath)) notebook = AgriNotebook.FromJson(File.ReadAllText(NotebookPath));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Agriculture: could not read {Key}'s notebook ({e.Message}).", this);
        }
    }
}
