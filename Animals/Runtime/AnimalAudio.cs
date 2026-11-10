using System;
using UnityEngine;

/// <summary>
/// What animal husbandry sounds like (on the <see cref="AnimalDatabase"/>): a clip for each family of care (brushing,
/// washing, petting, hoof care, a species' own care), for the work (mucking out, a dose of medicine, the veterinarian's
/// visit, slaughter, butchering) and the animals' own calls (content, protesting, sick). An empty slot is silent.
/// </summary>
[Serializable]
public class AnimalSounds
{
    [Header("Care")]
    public AudioClip brush;
    public AudioClip wash;
    public AudioClip pet;
    public AudioClip cleanHooves;
    [Tooltip("A species' own care action (one of no family above).")]
    public AudioClip otherCare;

    [Header("Work")]
    public AudioClip muckOut;
    public AudioClip treat;
    public AudioClip vet;
    public AudioClip slaughter;
    public AudioClip butcher;

    [Header("Products")]
    [Tooltip("Collecting what an animal made by hand (eggs).")]
    public AudioClip collect;
    [Tooltip("Milking (empty: the collect sound).")]
    public AudioClip milk;
    [Tooltip("Shearing (empty: the collect sound).")]
    public AudioClip shear;

    [Header("Trading and the pen")]
    [Tooltip("Crating an animal (a crate's lid, a protesting call).")]
    public AudioClip crate;
    [Tooltip("Improving a pen or fitting a machine (hammering, a bolt driven home).")]
    public AudioClip build;

    [Header("The animals")]
    [Tooltip("A contented animal (a soft call).")]
    public AudioClip content;
    [Tooltip("An animal that does not like what was done (too much of it, or care it should not have).")]
    public AudioClip protest;
    [Tooltip("A sick animal (a cough, a groan).")]
    public AudioClip sick;

    [Tooltip("How loud they are played.")]
    [Range(0f, 1f)] public float volume = 0.8f;
    [Tooltip("Metres within which they are heard.")]
    [Min(1f)] public float distance = 25f;

    /// <summary>
    /// The clip for a sound by name: a care family ("brush", "wash", "pet", "clean_hooves", "care"), the work ("muck_out",
    /// "treat", "vet", "slaughter", "butcher"), collecting a product ("collect", "milk", "shear"), trading and the pen
    /// ("crate", "build") or a call ("content", "protest", "sick"); case, spaces and underscores do not matter. Null: none
    /// (silent).
    /// </summary>
    public AudioClip Clip(string what)
    {
        switch (Normalise(what))
        {
            case "brush": return brush;
            case "wash": return wash;
            case "pet": return pet;
            case "cleanhooves":
            case "hooves": return cleanHooves;
            case "care":
            case "custom":
            case "othercare": return otherCare;
            case "muckout":
            case "cleanpen": return muckOut;
            case "treat": return treat;
            case "vet":
            case "callvet": return vet;
            case "crate": return crate;
            case "build":
            case "upgrade": return build;
            case "slaughter": return slaughter;
            case "butcher": return butcher;
            case "collect": return collect;
            case "milk": return milk != null ? milk : collect;
            case "shear": return shear != null ? shear : collect;
            case "content": return content;
            case "protest": return protest;
            case "sick": return sick;
            default: return null;
        }
    }

    private static string Normalise(string what)
    {
        if (string.IsNullOrWhiteSpace(what)) return "";
        var chars = new char[what.Length];
        int n = 0;
        foreach (char c in what)
            if (c != '_' && c != ' ' && c != '-') chars[n++] = char.ToLowerInvariant(c);
        return new string(chars, 0, n);
    }
}

/// <summary>
/// Plays the sounds of animal husbandry where they happen (on the machine that shows them: never the simulation's
/// business). Through the project's <see cref="SoundManager"/> when there is one, else a one-shot at the spot.
/// </summary>
public static class AnimalAudio
{
    /// <summary>
    /// Plays a sound at a point: <paramref name="what"/> is a care family name ("brush", "wash", "pet", "clean_hooves",
    /// "care"), an action name ("muck_out", "treat", "vet", "slaughter", "butcher", "collect", "milk", "shear") or a call
    /// ("content", "protest", "sick")
    /// - see <see cref="AnimalSounds.Clip"/>. Nothing when its slot is empty or there is nobody to hear (a server without a
    /// screen).
    /// </summary>
    public static void Play(string what, Vector3 at)
    {
        if (Application.isBatchMode || !Application.isPlaying) return;
        AnimalSounds s = AnimalDatabase.Instance.sounds;
        AudioClip clip = s?.Clip(what);
        if (clip == null) return;
        float volume = Mathf.Clamp01(s.volume);
        if (volume <= 0f) return;
        SoundManager manager = SoundManager.Instance;
        if (manager != null)
        {
            manager.PlaySFX(new SoundEffect { name = "Animals " + what, clip = clip, volume = volume, maxDistance = Mathf.Max(1f, s.distance) }, at);
            return;
        }
        AudioSource.PlayClipAtPoint(clip, at, volume);
    }
}
