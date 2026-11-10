using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The body of one animal near a player: it only <b>shows</b> the animal - its life is the <see cref="AnimalDirector"/>'s,
/// so a body can be made and put away freely, and every machine makes its own from the picture it holds (no network
/// object per animal). It is as big as the animal has grown, wears its coat, and lives what the picture says: a hungry
/// animal goes to the feeder, a thirsty one to the trough, grazers crop the grass, all of them wander, rest and sleep at
/// night, the dead lie still. It moves with its species' gait (the mobs' <see cref="MobGait"/>: a rabbit hops) and stays
/// inside its pen. Its choices follow the farm clock and its id, so every machine sees nearly the same herd.
/// <para>How its body looks (<see cref="AnimalView.looks"/>) shows in it: a dirty coat is darker; a sick animal lies down a
/// good deal by day, mopes with its head low and walks at half pace (and now and then coughs or groans); a lame one walks
/// slower with a rolling limp; a stressed one paces with short pauses; a thin one is narrower; a sheep grows bulkier as its
/// fleece grows and is slim once shorn (<see cref="AnimalView.fleece"/>); a contented one (an Excellent life) idles calmly, lies at its ease and now and then calls softly. Care given to it is answered with a short
/// reaction - it leans into the brush, shakes off the water, nuzzles the hand, lifts a hoof - and with a protest (a toss of
/// the head and its protest call) when it was too much of that care or care it should not have had.</para>
/// <para>Its ways follow its species' behaviour (Phase 4): it grooms itself, walks over to its closest companion and keeps
/// with it a while, a herding kind drifts toward the others, and a young keeps to its mother. People near its pen it shies
/// from (a skittish one, a flighty kind) or comes to look at (a curious kind); its owner, as their bond lets it, it greets
/// with a hop and a call and follows along the fence, and it comes to the fence when called. A deep bond shows as a ribbon,
/// and a mother late in carrying her young is heavier. Who is its owner only its owner's machine knows: elsewhere it treats
/// everyone alike.</para>
/// </summary>
public class AnimalBody : MonoBehaviour
{
    private enum Doing { Idle, Walk, Eat, Drink, Graze, Sleep, Dead, Rest, Mope, React, Follow, Flee, Social, Groom }

    private AnimalPen pen;
    private AnimalView view;
    private AnimalSpeciesData species;
    private Transform model;
    private Vector2 at, target;
    private float heading, ground, groundTarget, nextGround, until;
    private Doing doing = Doing.Idle;
    private int seed, choice;
    private MobGaitState gait;
    private MobGaitShape shape;
    private float baseScale = 1f;

    private AnimalLooks looks;
    /// <summary>The model was built with the dirty coat.</summary>
    private bool dirtyShown;
    /// <summary>Metres walked (the rhythm of a limp).</summary>
    private float limp;
    /// <summary>How far its fleece has grown, eased towards the picture's (0-1; -1: it grows none).</summary>
    private float fleece = -1f, fleeceTarget = -1f;

    // Its ways (Phase 4): greeting its owner (when last, and whether it greets on reaching them), being called (until when,
    // and where to), the model built with the ribbon of a deep bond.
    private float greetedAt = -100f, calledUntil;
    private bool greetOnArrival;
    private Vector2 calledTo;
    private bool ribbonShown;
    /// <summary>The reaction that is a greeting (not a care family).</summary>
    private const int GreetReaction = -2;
    /// <summary>Real seconds between two greetings.</summary>
    private const float GreetEvery = 40f;
    /// <summary>How much wider a mother heavy with young is.</summary>
    private const float HeavyWidth = 1.1f;

    // Reactions to care: the last care seen, the family reacted to now, and (to tell too much of it) how much of each
    // family it had lately - counted as the rules count it, from what this machine saw.
    private double seenCareAt = -1.0;
    private int reaction = -1;
    private bool protesting;
    private float reactStart;
    private readonly float[] recent = new float[5];
    private readonly double[] recentAt = { -1.0, -1.0, -1.0, -1.0, -1.0 };

    // Its calls: the clock's slot last judged, and the earliest real time for the next one.
    private int callSlot = int.MinValue;
    private float nextCall;

    /// <summary>How much darker a dirty coat is.</summary>
    private const float DirtyShade = 0.7f;
    /// <summary>How much narrower a thin animal is.</summary>
    private const float ThinWidth = 0.88f;
    /// <summary>How wide and tall a fleeced animal is, freshly shorn and in full fleece (its length does not change).</summary>
    private const float ShornBulk = 0.9f, FullFleeceBulk = 1.14f;
    /// <summary>Care given longer ago than this (game hours) is not reacted to (a body made later, as a player comes near).</summary>
    private const double FreshCareHours = 0.5;
    /// <summary>Seconds a reaction lasts.</summary>
    private const float ReactSeconds = 1.6f;
    /// <summary>The chance, per ten game minutes, that a sick animal coughs or groans (at most every 20 s), or a contented one calls (40 s).</summary>
    private const float SickCallChance = 0.12f, ContentCallChance = 0.05f;
    /// <summary>Degrees a lame animal rolls as it limps.</summary>
    private const float LameRoll = 5f;

    public string AnimalId => view != null ? view.id : "";
    public AnimalPen Pen => pen;

    public void Bind(AnimalPen p, AnimalView v)
    {
        pen = p;
        seed = AnimalMath.Hash(v.id);
        gait = MobGaitState.For(seed);
        species = AnimalDatabase.Instance.Species(v.species);
        at = RandomPoint(1);
        target = at;
        heading = AnimalMath.Noise(seed, 3) * 360f;
        ground = groundTarget = pen.GroundY(at);
        Apply(v, true);
    }

    /// <summary>The picture changed: size, coat, needs, how it looks, care given to it, death.</summary>
    public void Apply(AnimalView v, bool force = false)
    {
        if (v == null) return;
        bool first = view == null;
        bool alive = v.status != AnimalStatus.Dead;
        bool dirty = alive && (v.looks & (int)AnimalLooks.Dirty) != 0;
        bool rebuild = force || model == null || first || view.coat != v.coat || view.species != v.species || dirty != dirtyShown || v.ribbon != ribbonShown;
        view = v;
        looks = alive ? (AnimalLooks)v.looks : AnimalLooks.None;
        if (rebuild)
        {
            dirtyShown = dirty;
            ribbonShown = v.ribbon;
            BuildModel();
        }
        baseScale = Mathf.Clamp(v.size, 0.05f, 3f);
        // Its fleece: shorn at once (a big step down), grown slowly; a body just made shows it as it is.
        fleeceTarget = alive ? v.fleece : -1f;
        if (first || fleeceTarget < 0f || fleece < 0f || fleeceTarget < fleece - 0.25f) fleece = fleeceTarget;
        if (!alive) doing = Doing.Dead;
        else if (doing == Doing.Dead) doing = Doing.Idle;

        // Care given since it was last seen: it reacts (a body just made does not react to care given before it was shown).
        if (first) seenCareAt = v.lastCareAt;
        else if (v.lastCareAt > seenCareAt + 1e-6)
        {
            seenCareAt = v.lastCareAt;
            if (alive && v.lastCare > 0 && FarmClock.Now - v.lastCareAt < FreshCareHours) React((AnimalCareKind)(v.lastCare - 1), v.lastCareAt);
        }
    }

    private void BuildModel()
    {
        if (model != null) Destroy(model.gameObject);
        AnimalBreedData breed = species?.breeds != null ? species.breeds.Find(b => b != null && b.name == view.breedName) : null;
        Color coat = AnimalArt.CoatOf(species, breed, view.coat);
        if (dirtyShown) coat = AnimalArt.Shade(coat, DirtyShade);
        Color accent = species != null ? species.look.accent : Color.white;
        GameObject prefab = AnimalDatabase.Instance.BodyPrefab(view.species);
        GameObject go = prefab != null ? Instantiate(prefab) : AnimalArt.MakeBody(species, coat, accent);
        // A model of the species' own cannot be recoloured as a placeholder is: its materials are darkened instead.
        if (prefab != null && dirtyShown) Darken(go, DirtyShade);
        go.name = "Model";
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        go.transform.SetParent(transform, false);
        model = go.transform;
        float height = species != null ? species.look.height : 1f;
        MobLandGait g = species != null ? species.look.gait : MobLandGait.Walk;
        shape = MobGait.DefaultShape(g, height);
        // A deep bond's ribbon: a bright band at its neck (a cosmetic mark; colliders none).
        if (ribbonShown)
        {
            float length = species != null ? species.look.length : 1f;
            AnimalArt.AddPart(go, PrimitiveType.Cube, new Vector3(0f, height * 0.78f, length * 0.32f),
                new Vector3(Mathf.Max(0.08f, length * 0.28f), Mathf.Max(0.03f, height * 0.06f), Mathf.Max(0.03f, length * 0.06f)), new Color(0.86f, 0.18f, 0.24f));
        }
    }

    /// <summary>Its owner called it (a bond that lets it come when called): it comes to the fence where they stand, for a while.</summary>
    public void Called(Vector3 world)
    {
        if (view == null || doing == Doing.Dead) return;
        calledTo = Inside(pen.Local(world));
        calledUntil = Time.time + 15f;
        doing = Doing.Walk;
        target = calledTo;
        until = calledUntil;
        greetOnArrival = true;
    }

    /// <summary>A point moved toward another by at most <paramref name="step"/>.</summary>
    private static Vector2 Toward(Vector2 from, Vector2 to, float step)
    {
        Vector2 d = to - from;
        float m = d.magnitude;
        return m <= step || m < 1e-6f ? to : from + d / m * step;
    }

    /// <summary>The point inside its pen nearest a point (pulled a little in from the fence).</summary>
    private Vector2 Inside(Vector2 p)
    {
        if (pen.outline == null || pen.outline.Count < 3) return at;
        Vector2 centre = HousingGeometry.Bounds(pen.outline).center;
        if (!HousingGeometry.Contains(pen.outline, centre)) centre = at;
        if (HousingGeometry.Contains(pen.outline, p)) return Toward(p, centre, 0.3f);
        for (int k = 1; k <= 24; k++)
        {
            Vector2 q = Vector2.Lerp(p, centre, k / 24f);
            if (HousingGeometry.Contains(pen.outline, q)) return Toward(q, centre, 0.4f);
        }
        return centre;
    }

    /// <summary>Darkens every material of a model (its main colour, through a property block: the materials stay shared).</summary>
    private static void Darken(GameObject go, float k)
    {
        var block = new MaterialPropertyBlock();
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            Material[] materials = r.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material m = materials[i];
                if (m == null) continue;
                string property = m.HasProperty("_BaseColor") ? "_BaseColor" : m.HasProperty("_Color") ? "_Color" : null;
                if (property == null) continue;
                Color c = m.GetColor(property);
                block.Clear();
                block.SetColor(property, new Color(c.r * k, c.g * k, c.b * k, c.a));
                r.SetPropertyBlock(block, i);
            }
        }
    }

    private void Update()
    {
        if (pen == null || view == null) return;
        float dt = Mathf.Min(Time.deltaTime, 0.1f);
        bool arrived = (target - at).sqrMagnitude < 0.04f;
        if (arrived && greetOnArrival && (doing == Doing.Walk || doing == Doing.Follow)) Greet();
        else if (Time.time >= until || ((doing == Doing.Walk || doing == Doing.Flee) && arrived)) Choose();
        Calls();
        float speed = 0f;
        Vector2 to = target - at;
        bool goes = doing == Doing.Walk || doing == Doing.Follow || doing == Doing.Flee
                    || ((doing == Doing.Eat || doing == Doing.Drink || doing == Doing.Social) && to.sqrMagnitude > 0.09f);
        if (goes && to.sqrMagnitude > 0.0004f)
        {
            float walk = (species != null ? species.look.walkSpeed : 1f) * Mathf.Sqrt(baseScale) * Pace() * (doing == Doing.Flee ? 1.6f : 1f);
            MobGaitStep step = MobGait.Land(species != null ? species.look.gait : MobLandGait.Walk, shape, ref gait, walk, dt);
            speed = walk * step.speedScale;
            Vector2 dir = to.normalized;
            Vector2 next = at + dir * Mathf.Min(to.magnitude, speed * dt);
            if (HousingGeometry.Contains(pen.outline, next))
            {
                limp += (next - at).magnitude;
                at = next;
            }
            else target = at; // the fence: it turns back
            float want = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            heading = Mathf.MoveTowardsAngle(heading, want, 240f * dt);
            Place(step.lift, step.squash, true);
        }
        else
        {
            gait.phase = 0f;
            Place(0f, 1f, false);
        }
        if (Time.time >= nextGround)
        {
            nextGround = Time.time + 0.5f;
            groundTarget = pen.GroundY(at);
        }
        ground = Mathf.MoveTowards(ground, groundTarget, 2f * dt);
    }

    /// <summary>How bulky its fleece makes it (1: it grows none), easing towards the picture's as it grows.</summary>
    private float FleeceBulk()
    {
        if (fleeceTarget < 0f) return 1f;
        if (fleece < 0f) fleece = fleeceTarget;
        fleece = Mathf.MoveTowards(fleece, fleeceTarget, 0.05f * Time.deltaTime);
        return Mathf.Lerp(ShornBulk, FullFleeceBulk, Mathf.Clamp01(fleece));
    }

    /// <summary>How fast it walks for how it looks: a sick animal at half pace, a lame one at 0.6 (both: less still), a stressed one paces a little faster.</summary>
    private float Pace()
    {
        float k = 1f;
        if ((looks & AnimalLooks.Sick) != 0) k *= 0.5f;
        if ((looks & AnimalLooks.Lame) != 0) k *= 0.6f;
        if (k >= 1f && (looks & AnimalLooks.Stressed) != 0) k = 1.2f;
        return k;
    }

    private void Place(float lift, float squash, bool moving)
    {
        Quaternion yaw = pen.transform.rotation * Quaternion.Euler(0f, heading, 0f);
        float s = baseScale;
        // A thin animal is narrower (x), never shorter: its body runs along z.
        float width = ((looks & AnimalLooks.Thin) != 0 ? ThinWidth : 1f) * (view != null && view.heavy ? HeavyWidth : 1f);
        // A fleeced animal grows bulkier (wider and taller) as its fleece grows, and is slim once shorn.
        float bulk = FleeceBulk();
        float spread = 1f / Mathf.Sqrt(Mathf.Max(0.2f, squash));
        Vector3 scale = new Vector3(s * spread * width * bulk, s * squash * bulk, s * spread);
        float pitch = 0f, roll = 0f, raise = 0f;
        switch (doing)
        {
            case Doing.Dead:
                roll = 88f;
                break;
            case Doing.Sleep:
            case Doing.Rest:
                scale.y *= 0.6f;
                break;
            case Doing.Eat:
            case Doing.Drink:
            case Doing.Graze:
            case Doing.Mope:
                pitch = 12f;
                break;
            case Doing.React:
                Reaction(out pitch, out roll, out raise);
                break;
            case Doing.Groom:
            {
                // It grooms itself: head turned to its flank, working at it.
                float t = Time.time * 3.5f + seed;
                roll = 8f + 4f * Mathf.Sin(t);
                pitch = 6f + 5f * Mathf.Sin(t * 1.7f);
                break;
            }
            case Doing.Social:
                // Beside a companion: a nuzzle, a peck, heads together.
                if (!moving) pitch = 9f * Mathf.Abs(Mathf.Sin(Time.time * 2.2f + seed));
                break;
        }
        // A lame animal rolls a little with each step.
        if (moving && (looks & AnimalLooks.Lame) != 0)
        {
            float stride = Mathf.Max(0.3f, (species != null ? species.look.height : 1f) * s * 0.9f);
            roll += LameRoll * Mathf.Sin(limp / stride * Mathf.PI * 2f);
        }
        Vector3 p = pen.World(at);
        p.y = ground + lift + raise;
        transform.position = p;
        transform.rotation = yaw * Quaternion.Euler(pitch, 0f, roll);
        transform.localScale = scale;
    }

    // ------------------------------------------------------------------ choosing
    /// <summary>What to do next: from its needs as they look, how its body looks, the hour, and the farm clock's slot (the same on every machine).</summary>
    private void Choose()
    {
        if (view.status == AnimalStatus.Dead)
        {
            doing = Doing.Dead;
            until = Time.time + 5f;
            return;
        }
        choice++;
        double now = FarmClock.Now;
        int slot = (int)(now * 6.0); // a new choice every ten game minutes at the latest
        float r = AnimalMath.Noise(seed, slot, choice);
        float hour = (float)(now % 24.0);
        bool night = hour < 5f || hour > 21.5f;
        float seconds = 4f + 8f * AnimalMath.Noise(seed, slot, choice + 31);
        bool sick = (looks & AnimalLooks.Sick) != 0;
        bool stressed = (looks & AnimalLooks.Stressed) != 0;
        bool content = (looks & AnimalLooks.Content) != 0;
        // Called by its owner: it goes on coming.
        if (Time.time < calledUntil)
        {
            doing = Doing.Walk;
            target = calledTo;
            until = calledUntil;
            return;
        }
        greetOnArrival = false;
        // People near: its owner it greets and follows (as its bond lets it), strangers it shies from or comes to look at.
        if (!night && !sick && People(slot, seconds)) return;
        if (night && r < 0.85f)
        {
            doing = Doing.Sleep;
            until = Time.time + seconds * 2f;
            return;
        }
        if (view.hunger <= 1 && r < 0.7f)
        {
            doing = Doing.Eat;
            target = Near(pen.feederPoint, 0.6f, slot);
            until = Time.time + seconds + 4f;
            return;
        }
        if (view.thirst <= 1 && r < 0.75f)
        {
            doing = Doing.Drink;
            target = Near(pen.troughPoint, 0.6f, slot);
            until = Time.time + seconds;
            return;
        }
        // By day a sick animal lies down a good deal, and a contented one takes its ease.
        float lie = sick ? 0.4f : content ? 0.2f : 0f;
        if (lie > 0f && AnimalMath.Noise(seed, slot, choice + 57) < lie)
        {
            doing = Doing.Rest;
            until = Time.time + seconds * (sick ? 2.5f : 1.5f);
            return;
        }
        // A young keeps to its mother; it seeks its closest companion; a herding kind keeps with the others.
        if (!sick && Company(slot, seconds)) return;
        // A stressed animal paces (more walking, short pauses); a sick one hardly wanders; a contented one less than most.
        float walk = stressed ? 0.7f : sick ? 0.15f : content ? 0.3f : 0.45f;
        if (r < walk)
        {
            doing = Doing.Walk;
            target = RandomPoint(slot * 7 + choice);
            until = Time.time + (stressed ? 10f : 20f);
        }
        else if (r < walk + 0.3f && pen.kind == AnimalHabitatKind.Pasture && !sick)
        {
            doing = Doing.Graze;
            until = Time.time + seconds;
        }
        else if (!sick && !stressed && species?.behaviour != null && AnimalMath.Noise(seed, slot, choice + 71) < species.behaviour.grooming * 0.6f)
        {
            // It grooms itself (preens, licks, dust-bathes).
            doing = Doing.Groom;
            until = Time.time + seconds * 0.8f;
        }
        else
        {
            // A sick animal stands with its head low; a stressed one only stops for a moment; a contented one idles long.
            doing = sick ? Doing.Mope : Doing.Idle;
            until = Time.time + (stressed ? 1f + 2f * AnimalMath.Noise(seed, slot, choice + 13) : content ? seconds * 1.5f : seconds);
        }
    }

    /// <summary>
    /// People near its pen (this machine's player: whose it is, the picture says). Its owner it greets (now and then, as its
    /// bond lets it) and follows along the fence; a stranger it shies from (by its kind's flightiness and its temper) or comes
    /// to look at (by its kind's curiosity). True: it is doing one of these.
    /// </summary>
    private bool People(int slot, float seconds)
    {
        AnimalBehaviourSpec b = species?.behaviour;
        CombatEntity me = AnimalDirector.LocalPlayer();
        if (b == null || me == null || !me.IsAlive) return false;
        float away = pen.DistanceFromOutline(me.transform.position);
        if (away > b.noticeRange) return false;
        Vector2 them = pen.Local(me.transform.position);
        float close = (them - at).magnitude;
        if (view.mine)
        {
            if (view.greets && Time.time - greetedAt > GreetEvery && away <= 6f)
            {
                doing = Doing.Walk;
                target = Inside(them);
                until = Time.time + 12f;
                greetOnArrival = true;
                return true;
            }
            if (view.follows && AnimalMath.Noise(seed, slot, choice + 81) < 0.85f)
            {
                doing = Doing.Follow;
                target = Inside(them);
                until = Time.time + 2.5f;
                return true;
            }
            // Its own owner is no stranger: it is curious at most.
        }
        else
        {
            // A shy one (a skittish temper) shies readily; a steady one seldom.
            float skittish = b.flightiness * (view.shy ? 1f : 0.3f);
            if (close < 4f && AnimalMath.Noise(seed, slot, choice + 83) < skittish)
            {
                Vector2 off = at - them;
                off = off.sqrMagnitude > 0.0001f ? off.normalized : new Vector2(1f, 0f);
                doing = Doing.Flee;
                target = Inside(at + off * 4f);
                until = Time.time + 6f;
                return true;
            }
        }
        if (AnimalMath.Noise(seed, slot, choice + 85) < b.curiosity * 0.4f)
        {
            doing = Doing.Walk;
            target = Inside(Toward(them, at, 1.2f));
            until = Time.time + seconds;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Its company: a young keeps close to its mother; it walks over to its closest companion and they keep together a
    /// while (as sociable as its kind is); a herding kind drifts toward the others. True: it is doing one of these.
    /// </summary>
    private bool Company(int slot, float seconds)
    {
        AnimalBehaviourSpec b = species?.behaviour;
        if (b == null) return false;
        if (!string.IsNullOrEmpty(view.motherHere) && AnimalMath.Noise(seed, slot, choice + 91) < 0.7f)
        {
            AnimalBody mother = AnimalBodies.Of(pen, view.motherHere);
            if (mother != null)
            {
                doing = Doing.Follow;
                target = Inside(mother.at + new Vector2(AnimalMath.Signed(seed, slot, 92), AnimalMath.Signed(seed, slot, 93)) * 0.6f);
                until = Time.time + 3f;
                return true;
            }
        }
        if (!string.IsNullOrEmpty(view.friend) && AnimalMath.Noise(seed, slot, choice + 95) < b.sociability * 0.35f)
        {
            AnimalBody friend = AnimalBodies.Of(pen, view.friend);
            if (friend != null && friend.doing != Doing.Dead)
            {
                float apart = Mathf.Max(0.5f, (species != null ? species.look.length : 1f) * baseScale);
                Vector2 side = at - friend.at;
                side = side.sqrMagnitude > 0.0001f ? side.normalized : new Vector2(0f, 1f);
                doing = Doing.Social;
                target = Inside(friend.at + side * apart);
                until = Time.time + seconds;
                return true;
            }
        }
        if (b.herds && AnimalMath.Noise(seed, slot, choice + 97) < 0.2f)
        {
            Vector2 centre = Vector2.zero;
            int n = 0;
            foreach (AnimalBody other in AnimalBodies.InPen(pen))
                if (other != null && other != this && other.doing != Doing.Dead && other.species == species)
                {
                    centre += other.at;
                    n++;
                }
            if (n > 0)
            {
                doing = Doing.Walk;
                target = Inside(centre / n + new Vector2(AnimalMath.Signed(seed, slot, 98), AnimalMath.Signed(seed, slot, 99)) * 1.5f);
                until = Time.time + 15f;
                return true;
            }
        }
        return false;
    }

    /// <summary>It greets its owner: a hop and a nuzzle, and a contented call.</summary>
    private void Greet()
    {
        greetOnArrival = false;
        greetedAt = Time.time;
        calledUntil = 0f;
        reaction = GreetReaction;
        protesting = false;
        reactStart = Time.time;
        doing = Doing.React;
        target = at;
        until = Time.time + ReactSeconds;
        CombatEntity me = AnimalDirector.LocalPlayer();
        if (me != null)
        {
            Vector2 d = pen.Local(me.transform.position) - at;
            if (d.sqrMagnitude > 0.0001f) heading = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
        }
        Call("content");
    }

    private Vector2 Near(Vector2 point, float spread, int slot)
    {
        Vector2 p = point + new Vector2(AnimalMath.Signed(seed, slot, 5), AnimalMath.Signed(seed, slot, 6)) * spread;
        return HousingGeometry.Contains(pen.outline, p) ? p : point;
    }

    /// <summary>A point inside the pen, chosen from the animal's seed (rejection sampling in its box).</summary>
    private Vector2 RandomPoint(int salt)
    {
        Rect b = HousingGeometry.Bounds(pen.outline);
        for (int i = 0; i < 12; i++)
        {
            var p = new Vector2(b.xMin + b.width * AnimalMath.Noise(seed, salt, i * 2), b.yMin + b.height * AnimalMath.Noise(seed, salt, i * 2 + 1));
            if (HousingGeometry.Contains(pen.outline, p)) return p;
        }
        return b.center;
    }

    // ------------------------------------------------------------------ reactions and calls
    /// <summary>
    /// Care was given to it (<paramref name="when"/>: the game hour): it stops and reacts for a moment. Too much of that care
    /// lately (its species' Overuse After within Overuse Hours, counted as the rules count it from the care this machine saw),
    /// or care unsuitable for its kind, makes it protest instead.
    /// </summary>
    private void React(AnimalCareKind kind, double when)
    {
        AnimalCareActionSpec spec = species?.care?.Action(kind);
        bool protest = spec != null && spec.inappropriate;
        int f = (int)kind;
        if (spec != null && f >= 0 && f < recent.Length)
        {
            recent[f] = recentAt[f] < 0.0 ? 1f : recent[f] * AnimalMath.Decay(when - recentAt[f], Mathf.Max(0.5f, spec.overuseHours)) + 1f;
            recentAt[f] = when;
            if (recent[f] > Mathf.Max(1, spec.overuseAfter) + 0.001f) protest = true;
        }
        reaction = f;
        protesting = protest;
        reactStart = Time.time;
        doing = Doing.React;
        target = at;
        until = Time.time + ReactSeconds;
        if (protest) Call("protest");
        else if ((looks & AnimalLooks.Content) != 0 && AnimalMath.Noise(seed, (int)(when * 60.0), 41) < 0.5f) Call("content");
    }

    /// <summary>The pose of a reaction now: a lean into the brush, a shake, a nuzzle, a lifted hoof, a hop; or a toss of the head.</summary>
    private void Reaction(out float pitch, out float roll, out float raise)
    {
        pitch = roll = raise = 0f;
        float t = Time.time - reactStart;
        float fade = Mathf.Clamp01(1f - t / ReactSeconds);
        float h = (species != null ? species.look.height : 1f) * baseScale;
        if (protesting)
        {
            pitch = -14f * Mathf.Sin(t * 12f) * fade;
            raise = 0.04f * h * Mathf.Abs(Mathf.Sin(t * 9f)) * fade;
            return;
        }
        if (reaction == GreetReaction)
        {
            // A greeting: a little hop and a nod.
            raise = 0.06f * h * Mathf.Abs(Mathf.Sin(t * 7f)) * fade;
            pitch = 8f * Mathf.Sin(t * 5f) * fade;
            return;
        }
        switch ((AnimalCareKind)reaction)
        {
            case AnimalCareKind.Brush:
                // It leans into the brush.
                roll = (9f * Mathf.Clamp01(t * 4f) + 2f * Mathf.Sin(t * 5f)) * fade;
                break;
            case AnimalCareKind.Wash:
                // It shakes off the water.
                roll = 13f * Mathf.Sin(t * 30f) * fade;
                break;
            case AnimalCareKind.Pet:
                // It nuzzles the hand.
                pitch = 10f * Mathf.Abs(Mathf.Sin(t * 4.5f)) * fade;
                break;
            case AnimalCareKind.CleanHooves:
                // It lifts a hoof, shifting its weight.
                roll = -5f * fade;
                pitch = -4f * fade;
                raise = 0.03f * h * fade;
                break;
            default:
                // A little hop.
                raise = 0.05f * h * Mathf.Abs(Mathf.Sin(t * 8f)) * fade;
                break;
        }
    }

    /// <summary>Now and then a sick animal coughs or groans, and a contented one calls softly (the clock's slot decides, as for its choices).</summary>
    private void Calls()
    {
        if (doing == Doing.Dead) return;
        bool sick = (looks & AnimalLooks.Sick) != 0, content = (looks & AnimalLooks.Content) != 0;
        if (!sick && !content) return;
        int slot = (int)(FarmClock.Now * 6.0);
        if (slot == callSlot) return;
        callSlot = slot;
        if (Time.time < nextCall) return;
        float r = AnimalMath.Noise(seed, slot, 77);
        if (sick && r < SickCallChance)
        {
            Call("sick");
            nextCall = Time.time + 20f;
        }
        else if (!sick && r < ContentCallChance)
        {
            Call("content");
            nextCall = Time.time + 40f;
        }
    }

    /// <summary>Plays one of its calls where it stands (see <see cref="AnimalSounds.Clip"/>), when it can be heard from the camera.</summary>
    private void Call(string what)
    {
        AnimalSounds s = AnimalDatabase.Instance.sounds;
        if (s == null || s.Clip(what) == null) return;
        Camera cam = Camera.main;
        float reach = Mathf.Max(1f, s.distance);
        if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > reach * reach) return;
        AnimalAudio.Play(what, transform.position);
    }
}

/// <summary>
/// The bodies of the animals near this machine's player: made from each pen's picture as it arrives, changed as it
/// changes, put away when the pen is forgotten; and the pen's floor (its dung and straw) as the picture shows it. Nothing
/// here decides anything about an animal.
/// </summary>
public static class AnimalBodies
{
    private static readonly Dictionary<AnimalPen, (int revision, Dictionary<string, AnimalBody> bodies)> shown =
        new Dictionary<AnimalPen, (int, Dictionary<string, AnimalBody>)>();
    private static readonly List<AnimalPen> scratch = new List<AnimalPen>();
    private static readonly HashSet<string> seen = new HashSet<string>();
    private static readonly List<string> gone = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => shown.Clear();

    /// <summary>Follows the pens' pictures (a dedicated server, with nobody to see them, makes none).</summary>
    public static void Tick(float dt)
    {
        if (GameNet.NetAuthority.IsNetworked && !GameNet.NetAuthority.HasLocalPlayer) return;
        foreach (AnimalPen p in AnimalDirector.Pens)
        {
            if (p == null) continue;
            AnimalPenView v = p.View;
            if (v == null)
            {
                if (shown.ContainsKey(p)) Clear(p);
                continue;
            }
            int revision = v.revision * 31 + v.animals.Count + (int)(v.now * 10.0);
            if (shown.TryGetValue(p, out var s) && s.revision == revision) continue;
            Sync(p, v, revision);
            AnimalPenLook.Floor(p, v);
            AnimalPenLook.Machines(p, v);
        }
        // Pens that went away.
        scratch.Clear();
        foreach (AnimalPen p in shown.Keys) if (p == null) scratch.Add(p);
        foreach (AnimalPen p in scratch) shown.Remove(p);
    }

    private static void Sync(AnimalPen p, AnimalPenView v, int revision)
    {
        if (!shown.TryGetValue(p, out var s)) s = (revision, new Dictionary<string, AnimalBody>());
        seen.Clear();
        foreach (AnimalView a in v.animals)
        {
            if (a == null || string.IsNullOrEmpty(a.id)) continue;
            seen.Add(a.id);
            if (!s.bodies.TryGetValue(a.id, out AnimalBody body) || body == null)
            {
                var go = new GameObject("Animal " + a.name);
                go.transform.SetParent(p.transform, false);
                body = go.AddComponent<AnimalBody>();
                body.Bind(p, a);
                s.bodies[a.id] = body;
            }
            else body.Apply(a);
        }
        gone.Clear();
        foreach (KeyValuePair<string, AnimalBody> kv in s.bodies) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (string id in gone)
        {
            if (s.bodies[id] != null) Object.Destroy(s.bodies[id].gameObject);
            s.bodies.Remove(id);
        }
        shown[p] = (revision, s.bodies);
    }

    /// <summary>Puts away a pen's bodies.</summary>
    public static void Clear(AnimalPen p)
    {
        if (p == null || !shown.TryGetValue(p, out var s)) return;
        foreach (AnimalBody b in s.bodies.Values)
            if (b != null) Object.Destroy(b.gameObject);
        shown.Remove(p);
    }

    /// <summary>The body of an animal, if one is shown here.</summary>
    public static AnimalBody Of(AnimalPen p, string animalId)
    {
        if (p == null || string.IsNullOrEmpty(animalId) || !shown.TryGetValue(p, out var s)) return null;
        return s.bodies.TryGetValue(animalId, out AnimalBody b) ? b : null;
    }

    private static readonly List<AnimalBody> none = new List<AnimalBody>();

    /// <summary>The bodies shown of a pen's animals (none: none shown).</summary>
    public static IEnumerable<AnimalBody> InPen(AnimalPen p)
    {
        if (p == null || !shown.TryGetValue(p, out var s)) return none;
        return s.bodies.Values;
    }
}

/// <summary>
/// The simple look of a pen: fence posts and rails along its outline, its feeder and its trough (no colliders); and its
/// floor as its picture shows it - dung patches as it gets soiled, fresh straw in a bedded pen as it is laid.
/// </summary>
public static class AnimalPenLook
{
    /// <summary>Dung patches and straw patches shown at each level (0-4) of how soiled the floor is and how fresh its bedding, for a pen of 24 m².</summary>
    private static readonly int[] DungByLevel = { 0, 2, 5, 9, 14 };
    private static readonly int[] StrawByLevel = { 0, 2, 5, 9, 14 };
    /// <summary>Most patches of each a pen has (a big pen shows more, up to these).</summary>
    private const int MaxDung = 32, MaxStraw = 28;
    private static readonly Color DungColour = new Color(0.32f, 0.23f, 0.13f);
    private static readonly Color StrawColour = new Color(0.86f, 0.76f, 0.42f);

    public static void Build(AnimalPen pen)
    {
        if (pen == null || pen.outline == null || pen.outline.Count < 3 || pen.transform.Find("Pen Look") != null) return;
        var root = new GameObject("Pen Look");
        root.transform.SetParent(pen.transform, false);
        Color wood = new Color(0.5f, 0.36f, 0.22f);
        Material m = AlchemyArt.MaterialFor(wood);
        List<Vector2> o = pen.outline;
        for (int i = 0, j = o.Count - 1; i < o.Count; j = i++)
        {
            Vector2 a = o[j], b = o[i];
            float len = (b - a).magnitude;
            int posts = Mathf.Max(1, Mathf.CeilToInt(len / 2f));
            for (int k = 0; k < posts; k++)
            {
                Vector2 p = Vector2.Lerp(a, b, k / (float)posts);
                Piece(root.transform, pen, PrimitiveType.Cube, p, 0.55f, new Vector3(0.1f, 1.1f, 0.1f), 0f, m);
            }
            float angle = Mathf.Atan2(b.x - a.x, b.y - a.y) * Mathf.Rad2Deg;
            Vector2 mid = (a + b) * 0.5f;
            Piece(root.transform, pen, PrimitiveType.Cube, mid, 0.85f, new Vector3(0.05f, 0.08f, len), angle, m);
            Piece(root.transform, pen, PrimitiveType.Cube, mid, 0.45f, new Vector3(0.05f, 0.08f, len), angle, m);
        }
        Piece(root.transform, pen, PrimitiveType.Cube, pen.feederPoint, 0.3f, new Vector3(0.9f, 0.5f, 0.5f), 0f, AlchemyArt.MaterialFor(new Color(0.6f, 0.45f, 0.28f)));
        Piece(root.transform, pen, PrimitiveType.Cube, pen.troughPoint, 0.2f, new Vector3(1.1f, 0.4f, 0.5f), 0f, AlchemyArt.MaterialFor(new Color(0.35f, 0.4f, 0.45f)));
    }

    private static void Piece(Transform root, AnimalPen pen, PrimitiveType type, Vector2 local, float y, Vector3 size, float yaw, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        Collider c = g.GetComponent<Collider>();
        if (c != null) Object.Destroy(c);
        g.transform.SetParent(root, false);
        float ground = pen.GroundY(local) - pen.transform.position.y;
        g.transform.localPosition = new Vector3(local.x, ground + y, local.y);
        g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        g.transform.localScale = size;
        Renderer r = g.GetComponent<Renderer>();
        if (r != null && m != null) r.sharedMaterial = m;
    }

    /// <summary>
    /// Shows the pen's floor as its picture says: dung patches by how soiled it is (<see cref="AnimalPenView.dirtLevel"/>),
    /// fresh straw by its bedding (<see cref="AnimalPenView.beddingLevel"/>; none in a pen that takes none). The patches are
    /// made once per pen, at spots chosen from its id (the same on every machine), then shown or hidden. A pen without the
    /// simple fence look shows its floor its own way: nothing is made.
    /// </summary>
    public static void Floor(AnimalPen pen, AnimalPenView v)
    {
        if (pen == null || v == null || pen.outline == null || pen.outline.Count < 3) return;
        Transform look = pen.transform.Find("Pen Look");
        if (look == null) return;
        Transform floor = look.Find("Pen Floor");
        if (floor == null) floor = BuildFloor(pen, look);
        float size = Mathf.Clamp(pen.Area / 24f, 0.5f, 2.5f);
        int dung = Mathf.Min(MaxDung, Mathf.RoundToInt(DungByLevel[Mathf.Clamp(v.dirtLevel, 0, 4)] * size));
        int straw = v.beddingLevel < 0 ? 0 : Mathf.Min(MaxStraw, Mathf.RoundToInt(StrawByLevel[Mathf.Clamp(v.beddingLevel, 0, 4)] * size));
        ShowFirst(floor.Find("Dung"), dung);
        ShowFirst(floor.Find("Straw"), straw);
    }

    /// <summary>
    /// Shows the machines a pen is fitted with (Phase 5), as its picture says: a feed hopper standing over the feeder, a
    /// waterer's pipe and tank by the trough, egg trays between them. Made once per pen (hidden), then shown or hidden; a
    /// pen without the simple fence look shows nothing.
    /// </summary>
    public static void Machines(AnimalPen pen, AnimalPenView v)
    {
        if (pen == null || v == null || pen.outline == null || pen.outline.Count < 3) return;
        Transform look = pen.transform.Find("Pen Look");
        if (look == null) return;
        Transform machines = look.Find("Pen Machines");
        if (machines == null)
        {
            machines = new GameObject("Pen Machines").transform;
            machines.SetParent(look, false);
            Material metal = AlchemyArt.MaterialFor(new Color(0.55f, 0.57f, 0.6f));
            Material wood = AlchemyArt.MaterialFor(new Color(0.62f, 0.47f, 0.3f));
            Transform hopper = Group(machines, "Hopper");
            Piece(hopper, pen, PrimitiveType.Cube, pen.feederPoint, 1.0f, new Vector3(0.6f, 0.8f, 0.42f), 0f, metal);
            Piece(hopper, pen, PrimitiveType.Cube, pen.feederPoint, 1.45f, new Vector3(0.7f, 0.08f, 0.5f), 0f, wood);
            Transform waterer = Group(machines, "Waterer");
            Vector2 pipe = pen.troughPoint + new Vector2(0.65f, 0f);
            Piece(waterer, pen, PrimitiveType.Cylinder, pipe, 0.5f, new Vector3(0.08f, 0.5f, 0.08f), 0f, metal);
            Piece(waterer, pen, PrimitiveType.Cube, pipe, 1.05f, new Vector3(0.35f, 0.3f, 0.35f), 0f, metal);
            Transform trays = Group(machines, "Trays");
            Vector2 at = Vector2.Lerp(pen.feederPoint, pen.troughPoint, 0.5f);
            Piece(trays, pen, PrimitiveType.Cube, at, 0.55f, new Vector3(1.0f, 0.12f, 0.36f), 0f, wood);
            Piece(trays, pen, PrimitiveType.Cube, at, 0.27f, new Vector3(0.08f, 0.54f, 0.08f), 0f, wood);
        }
        Show(machines.Find("Hopper"), v.hopper);
        Show(machines.Find("Waterer"), v.waterer);
        Show(machines.Find("Trays"), v.trays);
    }

    private static Transform Group(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.gameObject.SetActive(false);
        return t;
    }

    private static void Show(Transform t, bool on)
    {
        if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
    }

    /// <summary>Makes a pen's floor patches (all hidden): the spots first - the ground below each, while no patch stands there yet - then the patches.</summary>
    private static Transform BuildFloor(AnimalPen pen, Transform look)
    {
        var root = new GameObject("Pen Floor").transform;
        root.SetParent(look, false);
        var strawRoot = new GameObject("Straw").transform;
        strawRoot.SetParent(root, false);
        var dungRoot = new GameObject("Dung").transform;
        dungRoot.SetParent(root, false);
        int seed = AnimalMath.Hash(pen.PenId);
        var spots = new List<Vector3>(MaxStraw + MaxDung);
        for (int i = 0; i < MaxStraw + MaxDung; i++) spots.Add(Spot(pen, seed, i));
        Material strawMaterial = AlchemyArt.MaterialFor(StrawColour), dungMaterial = AlchemyArt.MaterialFor(DungColour);
        for (int i = 0; i < MaxStraw; i++)
        {
            // A loose drift of straw, about a metre across.
            float across = 0.9f + 0.7f * AnimalMath.Noise(seed, i, 3);
            float along = across * (0.6f + 0.4f * AnimalMath.Noise(seed, i, 4));
            Patch(strawRoot, spots[i], new Vector3(across, 0.004f, along), AnimalMath.Noise(seed, i, 5) * 180f, strawMaterial, 0.006f);
        }
        for (int i = 0; i < MaxDung; i++)
        {
            // A dropping: a small dark patch, on top of any straw.
            int k = MaxStraw + i;
            float across = 0.22f + 0.22f * AnimalMath.Noise(seed, k, 3);
            float along = across * (0.7f + 0.3f * AnimalMath.Noise(seed, k, 4));
            Patch(dungRoot, spots[k], new Vector3(across, 0.01f, along), AnimalMath.Noise(seed, k, 5) * 180f, dungMaterial, 0.02f);
        }
        return root;
    }

    /// <summary>A spot on the pen's floor for patch <paramref name="i"/> (inside its outline, clear of the feeder and the trough), on the ground, in the pen's space.</summary>
    private static Vector3 Spot(AnimalPen pen, int seed, int i)
    {
        Rect b = HousingGeometry.Bounds(pen.outline);
        Vector2 p = b.center;
        for (int k = 0; k < 16; k++)
        {
            var q = new Vector2(b.xMin + b.width * AnimalMath.Noise(seed, i, k * 2 + 11), b.yMin + b.height * AnimalMath.Noise(seed, i, k * 2 + 12));
            if (!HousingGeometry.Contains(pen.outline, q)) continue;
            if ((q - pen.feederPoint).sqrMagnitude < 0.8f || (q - pen.troughPoint).sqrMagnitude < 0.8f) continue;
            p = q;
            break;
        }
        return new Vector3(p.x, pen.GroundY(p) - pen.transform.position.y, p.y);
    }

    /// <summary>One flat patch lying on the ground (a squashed disc, no collider, no shadow), hidden until shown.</summary>
    private static void Patch(Transform root, Vector3 at, Vector3 size, float yaw, Material m, float lift)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Collider c = g.GetComponent<Collider>();
        if (c != null) Object.Destroy(c);
        g.transform.SetParent(root, false);
        g.transform.localPosition = new Vector3(at.x, at.y + lift, at.z);
        g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        g.transform.localScale = size;
        Renderer r = g.GetComponent<Renderer>();
        if (r != null)
        {
            if (m != null) r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        g.SetActive(false);
    }

    /// <summary>Shows the first <paramref name="n"/> children of a group and hides the rest.</summary>
    private static void ShowFirst(Transform group, int n)
    {
        if (group == null) return;
        for (int i = 0; i < group.childCount; i++)
        {
            GameObject g = group.GetChild(i).gameObject;
            bool on = i < n;
            if (g.activeSelf != on) g.SetActive(on);
        }
    }
}
