using System;
using UnityEngine;

/// <summary>
/// The body of a farm animal's carcass (<see cref="AnimalCarcass"/>): the animal lying on its side, as the dead lie in
/// their pens, built on every machine from what the carcass is - the species whose harvest anatomy it is (where the rules
/// run, the tag's own species), its look (in the coat of the species' first breed), its size - and built again if that
/// changes. It never breaks the carcass: a body that cannot be built is a plain placeholder. On the network prefab it
/// waits for the server's carcass (the creature's <see cref="HarvestableCreature.Changed"/>), and the carcass's trigger
/// is then fitted to it. It marks the creature as a body laid out from code (<see cref="IHarvestLaidOutBody"/>): its
/// inspector asks the prefab for no anatomy and no combat entity.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Animals/Animal Carcass Look")]
public sealed class AnimalCarcassLook : MonoBehaviour, IHarvestLaidOutBody
{
    private HarvestableCreature creature;
    private Transform model;
    private string builtAnatomy, builtSpecies;
    private float builtSize = -1f;

    private void OnEnable()
    {
        if (creature == null)
            creature = GetComponent<HarvestableCreature>();
        if (creature != null)
            creature.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (creature != null)
            creature.Changed -= Refresh;
    }

    /// <summary>The carcass changed: the body is built when there is one to show and it is not the one shown.</summary>
    private void Refresh()
    {
        HarvestCondition k = creature != null && creature.IsCarcass ? creature.Condition : null;
        if (k == null || string.IsNullOrEmpty(k.anatomy))
            return; // not laid out yet (a client waiting for the server's carcass)
        AnimalCarcassTag tag = GetComponent<AnimalCarcassTag>();
        string species = tag != null ? tag.species ?? "" : "";
        float size = Mathf.Clamp(k.sizeScale, 0.05f, 3f);
        if (model != null && builtAnatomy == k.anatomy && builtSpecies == species && Mathf.Abs(builtSize - size) < 1e-4f)
            return;
        // Remembered before building: a body that fails is not tried again on every change.
        builtAnatomy = k.anatomy;
        builtSpecies = species;
        builtSize = size;
        try
        {
            Build(k.anatomy, species, size);
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
            try
            {
                Placeholder(size);
            }
            catch (Exception again)
            {
                Debug.LogException(again, this);
            }
        }
    }

    private void Build(string anatomy, string speciesId, float size)
    {
        Clear();
        AnimalSpeciesData sp = SpeciesOf(anatomy, speciesId);
        GameObject prefab = sp != null ? AnimalDatabase.Instance.BodyPrefab(sp.id) : null;
        HarvestAnatomyData a = creature.Anatomy;
        // The coat of the species' first breed (its first coat), as its living animals wear it: a client knows no breed, so
        // every machine draws the same one.
        Color coat = sp != null ? AnimalArt.CoatOf(sp, sp.BreedOrDefault(null), 0) : a != null ? a.hairColor : new Color(0.6f, 0.5f, 0.4f);
        Color accent = sp != null ? sp.look.accent : a != null ? a.hideColor : Color.white;
        GameObject body = prefab != null ? Instantiate(prefab) : AnimalArt.MakeBody(sp, coat, accent);
        Attach(body, size, Quaternion.Euler(0f, 0f, 88f));
    }

    /// <summary>A plain body in place of one that could not be built.</summary>
    private void Placeholder(float size)
    {
        Clear();
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Renderer r = body.GetComponent<Renderer>();
        Material m = AlchemyArt.MaterialFor(new Color(0.5f, 0.38f, 0.3f));
        if (r != null && m != null)
            r.sharedMaterial = m;
        body.transform.localScale = new Vector3(0.6f, 0.7f, 0.6f);
        var root = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        Attach(root, size, Quaternion.Euler(0f, 0f, 90f));
    }

    /// <summary>Puts a body under the carcass, scaled and turned, with no colliders (the carcass's own trigger takes the clicks), resting on the ground.</summary>
    private void Attach(GameObject body, float size, Quaternion lying)
    {
        body.name = "Carcass Body";
        foreach (Collider c in body.GetComponentsInChildren<Collider>(true))
            c.enabled = false;
        body.transform.SetParent(transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = lying;
        body.transform.localScale = body.transform.localScale * size;
        model = body.transform;
        // Lying on its side, half of it would be under the ground: lifted until its lowest point rests on it.
        bool any = false;
        Bounds b = default;
        foreach (Renderer r in body.GetComponentsInChildren<Renderer>())
        {
            if (!any)
            {
                b = r.bounds;
                any = true;
            }
            else
                b.Encapsulate(r.bounds);
        }
        if (any)
            model.position += Vector3.up * (transform.position.y - b.min.y);
    }

    private void Clear()
    {
        if (model == null)
            return;
        if (Application.isPlaying) Destroy(model.gameObject);
        else DestroyImmediate(model.gameObject);
        model = null;
    }

    /// <summary>
    /// The species a carcass of <paramref name="anatomy"/> is: <paramref name="speciesId"/> when it names one (where the
    /// rules run), else the first species whose harvest anatomy it is. Null: none (a plain body is drawn).
    /// </summary>
    public static AnimalSpeciesData SpeciesOf(string anatomy, string speciesId)
    {
        AnimalDatabase db = AnimalDatabase.Instance;
        if (db == null)
            return null;
        if (!string.IsNullOrEmpty(speciesId))
        {
            AnimalSpeciesData s = db.Species(speciesId);
            if (s != null)
                return s;
        }
        if (string.IsNullOrEmpty(anatomy))
            return null;
        foreach (AnimalSpeciesData s in db.AllSpecies)
            if (s != null && string.Equals(s.harvestAnatomy, anatomy, StringComparison.OrdinalIgnoreCase))
                return s;
        return null;
    }
}
