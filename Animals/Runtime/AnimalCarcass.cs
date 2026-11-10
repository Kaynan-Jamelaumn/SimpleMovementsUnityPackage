using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Butchering a farm animal through Harvesting. The remains of a dead animal (<see cref="AnimalRemainsReport"/>, planned
/// by the Butcher command) are laid out as a Harvesting carcass just outside its pen's fence, its body made from how it
/// lived and died: how it was raised (x the quality of every piece; on a tannery hide, its grain), lean meat from
/// starving, poison, the taint of an infection or worms, a gut soiled by a digestive illness, a hide worn by a filthy life
/// and pocked by parasites, and the hours it has lain dead. Its owner then butchers it like any carcass (the carcass
/// window) - and only its owner, whatever the land: every carcass that carries an <see cref="AnimalCarcassTag"/> refuses
/// anyone else (<see cref="HarvestableCreature.AccessCheck"/>, installed here for every machine).
/// <list type="bullet">
/// <item>Offline a new object is made for it. In a network session (host or server) the network prefab
/// Resources/SimpleMovements/Networking/AnimalCarcass (Tools ▸ SimpleMovements ▸ Network ▸ Create Animal Carcass Prefab)
/// is instantiated, moved into the pen's scene and spawned, so every player sees it; clients harvest it through the
/// server like a mob's carcass.</item>
/// <item>It lies at a fixed spot: the corner of the fence farthest from the feeder and the trough, 0.8 m outside, so it
/// blocks neither and is the same on every machine.</item>
/// <item>Carcasses are not saved (as Harvesting's are not): the animal's record is removed once its carcass lies there,
/// so nothing is butchered twice. A carcass left unworked rots away after the rules' Carcass Minutes; a restart loses one
/// not yet harvested.</item>
/// </list>
/// </summary>
public static class AnimalCarcass
{
    /// <summary>The network prefab's name (in Resources/SimpleMovements/Networking).</summary>
    public const string PrefabName = "AnimalCarcass";
    /// <summary>What anyone but the owner is told.</summary>
    public const string NotYours = "This carcass is not yours.";
    /// <summary>The answer in a network session without the network prefab.</summary>
    public const string NotSetUp = "The butcher's table is not set up (Network ▸ Create Animal Carcass Prefab).";
    /// <summary>Metres outside the fence the carcass lies.</summary>
    public const float FenceGap = 0.8f;

    /// <summary>Damage a lifetime's wear leaves at most on a hide cell (from 0.5 the skinning shows a cell as holed in the fight).</summary>
    private const float MostWear = 0.45f;
    /// <summary>The damage of a parasite's flaw on a hide cell (over 0.3 the tannery counts it a flaw).</summary>
    private const float ParasiteFlaw = 0.35f;

    private static GameObject prefab;
    private static float searchedAt = -100f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        prefab = null;
        searchedAt = -100f;
    }

    /// <summary>
    /// Puts the owner-only rule into every carcass's access check, on every machine (only where the rules run does a
    /// carcass carry its tag). Chained: a rule installed before it still runs first.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallAccess()
    {
        Func<HarvestableCreature, CombatEntity, string> previous = HarvestableCreature.AccessCheck;
        HarvestableCreature.AccessCheck = (creature, player) =>
        {
            string why = previous != null ? previous(creature, player) : null;
            return !string.IsNullOrEmpty(why) ? why : Access(creature, player);
        };
    }

    // ------------------------------------------------------------------ who may work it
    /// <summary>The owner-only rule: a carcass laid out from an animal is its owner's to work. Null when they may (or it is no animal's).</summary>
    public static string Access(HarvestableCreature creature, CombatEntity player)
    {
        AnimalCarcassTag tag = creature != null ? creature.GetComponent<AnimalCarcassTag>() : null;
        return tag == null ? null : Refusal(tag.ownerKey, AnimalDirector.KeyOf(player));
    }

    /// <summary>The refusal for the player known as <paramref name="playerKey"/> on a carcass of <paramref name="ownerKey"/>'s: null when it is theirs (or nobody's).</summary>
    public static string Refusal(string ownerKey, string playerKey) =>
        string.IsNullOrEmpty(ownerKey) || string.Equals(ownerKey, playerKey, StringComparison.Ordinal) ? null : NotYours;

    // ------------------------------------------------------------------ the network prefab
    /// <summary>The network prefab (Resources/SimpleMovements/Networking/AnimalCarcass), or null (looked for again now and then while missing).</summary>
    public static GameObject Prefab
    {
        get
        {
            if (prefab == null && (!Application.isPlaying || Time.unscaledTime - searchedAt > 5f))
            {
                searchedAt = Application.isPlaying ? Time.unscaledTime : -100f;
                prefab = SMAssets.Load<GameObject>(SMAssets.Networking, PrefabName);
            }
            return prefab;
        }
    }

    /// <summary>
    /// Is the network prefab there (with a Harvestable Creature) and, where the network layer can tell (a server in a
    /// session), a registered network prefab? A network session needs it to butcher; offline nothing is needed.
    /// </summary>
    public static bool PrefabReady
    {
        get
        {
            GameObject p = Prefab;
            if (p == null || p.GetComponent<HarvestableCreature>() == null)
                return false;
            IWorldItemNet net = WorldItems.Net;
            return net == null || net.IsNetworkPrefab(p);
        }
    }

    // ------------------------------------------------------------------ laying it out
    /// <summary>
    /// Where the rules run: lays the remains <paramref name="r"/> out as a Harvesting carcass at <paramref name="pen"/>'s
    /// fence (<paramref name="player"/> asked; the carcass is its owner's, from the report). False, and why, when it cannot
    /// be done here: nothing is left behind then (the object is destroyed on any failure, a failed network spawn included),
    /// so the caller keeps the animal's record and may try again.
    /// </summary>
    public static bool LayOut(AnimalRemainsReport r, AnimalPen pen, CombatEntity player, out string why)
    {
        why = "";
        if (r == null)
        {
            why = "There is nothing to butcher.";
            return false;
        }
        if (pen == null)
        {
            why = "There is no pen to lay it out by.";
            return false;
        }
        if (!AnimalPen.RulesHere)
        {
            why = "Only the server lays out a carcass.";
            return false;
        }
        HarvestAnatomyData a = string.IsNullOrWhiteSpace(r.anatomy) ? null : HarvestDatabase.Instance.Anatomy(r.anatomy);
        HarvestCondition body = Body(a, r);
        if (a == null || body == null)
        {
            why = "It cannot be butchered here.";
            return false;
        }
        bool networked = GameNet.NetAuthority.IsNetworked;
        if (networked && !PrefabReady)
        {
            why = NotSetUp;
            return false;
        }
        Vector3 at = Place(pen, out Quaternion facing);
        float minutes = AnimalDatabase.Instance.Rules.carcassMinutes;
        float spoiled = SpoiledMinutes(r.hoursDead, TemperatureAt(at));

        GameObject go = null;
        bool spawned = false;
        try
        {
            go = networked ? UnityEngine.Object.Instantiate(Prefab, at, facing) : new GameObject();
            go.name = string.IsNullOrWhiteSpace(r.name) ? "Animal carcass" : $"Carcass of {r.name}";
            go.transform.SetPositionAndRotation(at, facing);
            // Interest and physics are per scene: the pen's (the provider spawns it in the scene it is in).
            Scene scene = pen.gameObject.scene;
            if (go.transform.parent == null && scene.IsValid() && scene.isLoaded && go.scene != scene)
                SceneManager.MoveGameObjectToScene(go, scene);

            HarvestableCreature creature = go.GetComponent<HarvestableCreature>();
            if (creature == null)
                creature = go.AddComponent<HarvestableCreature>();
            AnimalCarcassTag tag = go.GetComponent<AnimalCarcassTag>();
            if (tag == null)
                tag = go.AddComponent<AnimalCarcassTag>();
            tag.animal = r.animal ?? "";
            tag.species = r.species ?? "";
            tag.ownerKey = r.owner ?? "";
            tag.property = AnimalHousing.PropertyIdOf(pen);
            tag.pen = pen.PenId;
            if (go.GetComponent<AnimalCarcassLook>() == null)
                go.AddComponent<AnimalCarcassLook>(); // the network prefab holds its own

            if (!creature.LayOut(a, body, minutes, spoiled))
            {
                why = "The carcass could not be laid out.";
                Discard(go, false);
                return false;
            }
            if (networked)
            {
                // Laid out first: the spawn then sends every player the carcass at once.
                spawned = GameNet.NetSpawn.Spawn(go);
                if (!spawned)
                {
                    why = "The carcass could not be shown to the other players.";
                    Discard(go, false);
                    return false;
                }
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (go != null)
                Discard(go, spawned);
            why = "The carcass could not be laid out.";
            return false;
        }
    }

    private static void Discard(GameObject go, bool spawned)
    {
        if (go == null)
            return;
        if (spawned)
            GameNet.NetSpawn.Destroy(go);
        else if (Application.isPlaying)
            UnityEngine.Object.Destroy(go);
        else
            UnityEngine.Object.DestroyImmediate(go);
    }

    // ------------------------------------------------------------------ its body
    /// <summary>
    /// The animal's body as Harvesting judges it (pure: no scene): its anatomy's fresh body for its seed, size and age, then
    /// how it lived and died.
    /// <list type="bullet">
    /// <item>Rearing and the cause of death travel with it (<see cref="HarvestCondition.rearing"/>, <see cref="HarvestCondition.cause"/>).</item>
    /// <item>Every region's meat is at most its leanness (never under 0.12: below that a cut reads as destroyed); its poison
    /// and taint at least the remains'; the regions that hold the gut are soiled by a digestive illness.</item>
    /// <item>Every hide cell (feathers, on a bird) is worn by a filthy life (coat damage) and - on a hide that goes to the
    /// tannery, which reads only its cells - by poor rearing, 0.5 x (1 - rearing); at most 0.45. Parasites leave flaws
    /// (0.35) on a seeded share of the cells, about severity / 200.</item>
    /// </list>
    /// Null without an anatomy or remains.
    /// </summary>
    public static HarvestCondition Body(HarvestAnatomyData a, AnimalRemainsReport r)
    {
        if (a == null || r == null)
            return null;
        HarvestAge age = r.age <= 0 ? HarvestAge.Young : r.age >= 2 ? HarvestAge.Old : HarvestAge.Adult;
        HarvestCondition k = HarvestCondition.Create(a, r.seed, Mathf.Clamp(r.sizeScale, 0.2f, 2f), age, false, 1f);
        k.rearing = r.rearing > 0f ? Mathf.Clamp(r.rearing, 0.3f, 1.5f) : 1f;
        k.cause = r.cause ?? "";

        float lean = Mathf.Clamp(r.lean, 0.12f, 1f);
        float poison = Mathf.Clamp01(r.poison), taint = Mathf.Clamp01(r.taint), soiled = Mathf.Clamp01(r.contamination);
        for (int i = 0; i < k.regions.Count; i++)
        {
            HarvestRegionState s = k.regions[i];
            s.meat = Mathf.Min(s.meat, lean);
            s.poison = Mathf.Max(s.poison, poison);
            s.taint = Mathf.Max(s.taint, taint);
            // The gut lies where the body's organs are (as a spill while working soils them).
            if (soiled > 0f && i < a.regions.Count && a.regions[i] != null && a.regions[i].organs >= 0.3f)
                s.contamination = Mathf.Max(s.contamination, soiled);
        }

        float worn = Mathf.Clamp01(r.coatDamage);
        if (TanneryHide(a))
            worn = Mathf.Max(worn, 0.5f * (1f - Mathf.Min(1f, k.rearing)));
        worn = Mathf.Min(worn, MostWear);
        float flawed = Mathf.Clamp01(r.parasites) * 0.5f;
        for (int i = 0; i < k.pelt.Count; i++)
        {
            HarvestPeltCell cell = k.pelt[i];
            if (cell == null || !cell.present)
                continue;
            if (worn > 0f)
                cell.damage = Mathf.Max(cell.damage, worn);
            if (flawed > 0f && HarvestMath.Noise(k.seed, i, 331) < flawed)
                cell.damage = Mathf.Max(cell.damage, ParasiteFlaw);
        }
        return k;
    }

    /// <summary>Does the anatomy's hide go to the tannery (a hide with a Crafting hide, judged by its cells, not by its quality)?</summary>
    public static bool TanneryHide(HarvestAnatomyData a)
    {
        if (a?.materials == null)
            return false;
        foreach (HarvestMaterial m in a.materials)
            if (m != null && m.kind == HarvestMaterialKind.Hide && HarvestCarcass.IsHide(m) && !string.IsNullOrWhiteSpace(m.craftHide))
                return true;
        return false;
    }

    // ------------------------------------------------------------------ where, and how long dead
    /// <summary>Where the carcass lies by <paramref name="pen"/> (on the ground; see <see cref="FencePoint"/>), lying along the fence.</summary>
    public static Vector3 Place(AnimalPen pen, out Quaternion facing)
    {
        facing = pen != null ? pen.transform.rotation : Quaternion.identity;
        if (pen == null)
            return Vector3.zero;
        Vector2 local = FencePoint(pen.outline, pen.feederPoint, pen.troughPoint, FenceGap, out Vector2 along);
        Vector3 p = pen.World(local);
        p.y = pen.GroundY(local);
        Vector3 dir = pen.transform.rotation * new Vector3(along.x, 0f, along.y);
        if (dir.sqrMagnitude > 1e-6f)
            facing = Quaternion.LookRotation(dir.normalized, Vector3.up);
        return p;
    }

    /// <summary>
    /// The point <paramref name="gap"/> metres outside <paramref name="outline"/> (in its own space) at the corner farthest
    /// from both <paramref name="feeder"/> and <paramref name="trough"/> (the nearer of the two counts; the first such corner
    /// on a tie), and the direction along the fence there (<paramref name="along"/>). Pure: the same pen gives the same
    /// point on every machine.
    /// </summary>
    public static Vector2 FencePoint(IList<Vector2> outline, Vector2 feeder, Vector2 trough, float gap, out Vector2 along)
    {
        along = Vector2.right;
        if (outline == null || outline.Count < 3)
            return new Vector2(0f, -gap);
        int n = outline.Count, best = 0;
        float bestScore = float.MinValue;
        for (int i = 0; i < n; i++)
        {
            float score = Mathf.Min((outline[i] - feeder).sqrMagnitude, (outline[i] - trough).sqrMagnitude);
            if (score > bestScore + 1e-6f)
            {
                bestScore = score;
                best = i;
            }
        }
        Vector2 v = outline[best], prev = outline[(best + n - 1) % n], next = outline[(best + 1) % n];
        // Outward normals of the two sides meeting there (counter-clockwise outline: the outside is on the right).
        float sign = HousingGeometry.SignedArea(outline) >= 0f ? 1f : -1f;
        Vector2 e1 = v - prev, e2 = next - v;
        Vector2 n1 = new Vector2(e1.y, -e1.x) * sign, n2 = new Vector2(e2.y, -e2.x) * sign;
        Vector2 outward = (n1.sqrMagnitude > 1e-12f ? n1.normalized : Vector2.zero) + (n2.sqrMagnitude > 1e-12f ? n2.normalized : Vector2.zero);
        if (outward.sqrMagnitude < 1e-8f)
        {
            Rect box = HousingGeometry.Bounds(outline);
            outward = v - box.center;
        }
        outward = outward.sqrMagnitude > 1e-12f ? outward.normalized : Vector2.down;
        Vector2 p = v + outward * gap;
        if (HousingGeometry.Contains(outline, p))
            p = v - outward * gap; // a corner turned inwards: the other way is out
        along = new Vector2(-outward.y, outward.x);
        return p;
    }

    /// <summary>Minutes at 20 °C that <paramref name="hoursDead"/> game hours dead at <paramref name="temperature"/> °C come to (Harvesting's spoiling clock).</summary>
    public static float SpoiledMinutes(double hoursDead, float temperature)
    {
        if (hoursDead <= 0.0)
            return 0f;
        double rate = HarvestCarcass.SpoilRate(HarvestDatabase.Instance.Settings, temperature);
        return (float)Math.Min(1e6, hoursDead * RealMinutesPerGameHour() * rate);
    }

    /// <summary>Real minutes a game hour lasts: the day-night cycle's day (else the farm's) over 24.</summary>
    public static double RealMinutesPerGameHour()
    {
        DayNightCycle c = DayNightCycle.Instance;
        float day = c != null ? c.dayLengthMinutes : FarmClock.Settings.dayLengthMinutes;
        return Math.Max(0.5f, day) / 24.0;
    }

    /// <summary>The air temperature at a point (the Weather System's, else Harvesting's default).</summary>
    private static float TemperatureAt(Vector3 at)
    {
        WeatherSystem w = WeatherSystem.Instance;
        return w != null ? w.WeatherAt(at).Temperature : HarvestDatabase.Instance.Settings.defaultTemperature;
    }
}
