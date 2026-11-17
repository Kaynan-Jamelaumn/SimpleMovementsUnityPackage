#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Editor helpers for the sound side of the project:
/// <list type="bullet">
/// <item><b>Assets ▸ Create ▸ SimpleMovements ▸ Audio ▸ Audio Cue From Selected Clips</b>: one <see cref="AudioCue"/>
/// holding the selected clips as its variations, next to them.</item>
/// <item><b>Tools ▸ SimpleMovements ▸ Validate ▸ Sound Setup</b>: says how the volume sliders reach the sounds in
/// the open scenes - through the Audio Mixer or applied to the sounds directly - and what is missing for the mixer.</item>
/// </list>
/// </summary>
public static class AudioTools
{
    private static readonly string[] Parameters = { "MasterVolume", "MusicVolume", "SFXVolume", "AmbientVolume", "VoiceVolume" };

    // ------------------------------------------------------------------ cue from clips
    [MenuItem("Assets/Create/SimpleMovements/Audio/Audio Cue From Selected Clips", false, 81)]
    private static void CueFromSelectedClips()
    {
        var clips = new List<AudioClip>();
        foreach (Object o in Selection.objects)
        {
            if (o is AudioClip clip)
                clips.Add(clip);
        }
        if (clips.Count == 0)
            return;
        clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        string first = AssetDatabase.GetAssetPath(clips[0]);
        string folder = string.IsNullOrEmpty(first) ? "Assets" : System.IO.Path.GetDirectoryName(first).Replace('\\', '/');
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{CueName(clips)}.asset");

        AudioCue cue = ScriptableObject.CreateInstance<AudioCue>();
        cue.clips = clips.ToArray();
        // Several takes of one sound: a little pitch variation keeps them from sounding like a loop.
        if (clips.Count > 1)
            cue.pitch = new Vector2(0.95f, 1.05f);
        AssetDatabase.CreateAsset(cue, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = cue;
        EditorGUIUtility.PingObject(cue);
    }

    [MenuItem("Assets/Create/SimpleMovements/Audio/Audio Cue From Selected Clips", true)]
    private static bool CueFromSelectedClipsValid()
    {
        foreach (Object o in Selection.objects)
        {
            if (o is AudioClip)
                return true;
        }
        return false;
    }

    /// <summary>"Footstep_01", "Footstep_02" make "Footstep Cue": the clips' common start, without trailing digits and separators.</summary>
    private static string CueName(List<AudioClip> clips)
    {
        string common = clips[0].name;
        for (int i = 1; i < clips.Count && common.Length > 0; i++)
        {
            string n = clips[i].name;
            int k = 0;
            while (k < common.Length && k < n.Length && common[k] == n[k])
                k++;
            common = common.Substring(0, k);
        }
        common = common.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '_', '-', ' ', '.', '(');
        if (common.Length < 2)
            common = clips[0].name;
        return common + " Cue";
    }

    // ------------------------------------------------------------------ check
    [MenuItem(SMMenu.Root + "Validate/Sound Setup", false, SMMenu.Validate + 27)]
    private static void CheckSoundSetup()
    {
        var report = new StringBuilder();
        SoundManager[] managers = Object.FindObjectsByType<SoundManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (managers.Length == 0)
        {
            report.AppendLine("No Sound Manager in the open scenes.");
            report.AppendLine("Sounds still play, and the settings menu's sliders are applied to them directly. " +
                              "Add a Sound Manager for music, ambience, voice lines and the named sound library.");
        }
        else
        {
            if (managers.Length > 1)
                report.AppendLine($"{managers.Length} Sound Managers in the open scenes: the first one to wake up stays, the others remove themselves.");
            SoundManager m = managers[0];
            AudioMixer mixer = m.masterMixerGroup != null ? m.masterMixerGroup.audioMixer : null;
            if (mixer == null)
            {
                report.AppendLine($"'{m.name}' has no Master Mixer Group: no Audio Mixer is used.");
                report.AppendLine("That works - every volume slider is applied to the sounds directly. An Audio Mixer adds effects, " +
                                  "snapshots and ducking: create one (Assets ▸ Create ▸ Audio Mixer) with Music, SFX, Ambient and Voice groups " +
                                  "under Master, expose each group's Volume (right-click it ▸ Expose) and name the exposed parameters " +
                                  string.Join(", ", Parameters) + ", then assign the five groups here.");
            }
            else
            {
                report.AppendLine($"'{m.name}' uses the Audio Mixer '{mixer.name}'.");
                var missing = new List<string>();
                foreach (string p in Parameters)
                {
                    if (!mixer.GetFloat(p, out _))
                        missing.Add(p);
                }
                if (missing.Count == 0)
                    report.AppendLine("All five volume parameters are exposed: the mixer carries every slider.");
                else
                    report.AppendLine("Not exposed in the mixer: " + string.Join(", ", missing) + ". Those sliders are applied to the " +
                                      "sounds directly instead (they still work). To let the mixer carry them, expose the group's Volume " +
                                      "and give the exposed parameter that name.");
                Group(report, "Music", m.musicMixerGroup);
                Group(report, "SFX", m.sfxMixerGroup);
                Group(report, "Ambient", m.ambientMixerGroup);
                Group(report, "Voice", m.voiceMixerGroup);
            }
        }
        int listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        if (listeners == 0)
            report.AppendLine("No Audio Listener in the open scenes (the player's camera usually has one once it is spawned).");
        else if (listeners > 1)
            report.AppendLine($"{listeners} Audio Listeners are active: Unity uses one and warns about the others.");

        string text = report.ToString().TrimEnd();
        Debug.Log("[Sound Setup]\n" + text);
        EditorUtility.DisplayDialog("Sound setup", text, "OK");
    }

    private static void Group(StringBuilder report, string label, AudioMixerGroup group)
    {
        if (group == null)
            report.AppendLine($"No {label} Mixer Group assigned: {label.ToLowerInvariant()} sounds do not go through the mixer (their slider is applied to them directly).");
    }
}
#endif
