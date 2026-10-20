#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Full movesets for the Starter Item Library: for each kind of weapon its hit volumes (the parts of the model that hit:
/// a blade, a tip, a head, a shaft, a pommel, a fist) and every attack - chains whose hits differ, charged attacks,
/// lunges, spins, ground slams with an Impact, guard breakers, bleeding, stuns, knockbacks, pulls - mostly with Weapon
/// Blade hit detection (the weapon itself, following the animation), area shapes where a thrust, a spin or a shockwave
/// needs them, and ranged mechanics for bows, crossbows, slings, blowguns, guns, thrown weapons and mana-fired wands and
/// staves. The volumes are measured from the grip (the model's pivot): put BladeStart / BladeEnd (TipStart, HeadStart...)
/// markers on your model to make them exact. Animations and sounds are left to assign.
/// </summary>
public static class StarterMovesets
{
    public enum Kind
    {
        Sword, Rapier, Katana, Scimitar, Longsword, Greatsword,
        Axe, Greataxe, Mace, Flail, Warhammer, Maul,
        Spear, Halberd, Scythe, Whip,
        Dagger, Knuckles, Claws,
        BattleStaff, MageStaff, Wand,
        Shortbow, Longbow, Crossbow, HeavyCrossbow, RepeatingCrossbow, Sling, Blowgun,
        ThrowingKnife, Javelin, ThrowingAxe, Shuriken,
        Pistol, Musket, Blunderbuss, HandCannon,
    }

    /// <summary>Replaces the weapon's moveset, hit volumes, guard and ranged mechanic with the one of <paramref name="kind"/>.</summary>
    public static void Apply(WeaponSO w, Kind kind)
    {
        Undo.RecordObject(w, $"{kind} moveset");
        w.SetRanged(null);
        ShieldDefense noGuard = ShieldDefense.WeaponGuard();
        noGuard.enabled = false;
        w.SetGuard(noGuard);
        foreach (AttackType t in new[] { AttackType.Normal, AttackType.Light, AttackType.Heavy, AttackType.Special, AttackType.Alternate })
            if (t != AttackType.Normal) w.SetAction(t, null);
        switch (kind)
        {
            case Kind.Sword: Sword(w); break;
            case Kind.Rapier: Rapier(w); break;
            case Kind.Katana: Katana(w); break;
            case Kind.Scimitar: Scimitar(w); break;
            case Kind.Longsword: Longsword(w); break;
            case Kind.Greatsword: Greatsword(w); break;
            case Kind.Axe: Axe(w); break;
            case Kind.Greataxe: Greataxe(w); break;
            case Kind.Mace: Mace(w); break;
            case Kind.Flail: Flail(w); break;
            case Kind.Warhammer: Warhammer(w); break;
            case Kind.Maul: Maul(w); break;
            case Kind.Spear: Spear(w); break;
            case Kind.Halberd: Halberd(w); break;
            case Kind.Scythe: Scythe(w); break;
            case Kind.Whip: Whip(w); break;
            case Kind.Dagger: Dagger(w); break;
            case Kind.Knuckles: Knuckles(w, false); break;
            case Kind.Claws: Knuckles(w, true); break;
            case Kind.BattleStaff: BattleStaff(w); break;
            case Kind.MageStaff: MageStaff(w); break;
            case Kind.Wand: Wand(w); break;
            case Kind.Shortbow: Bow(w, 0.7f, 0.9f, 42f, true); break;
            case Kind.Longbow: Bow(w, 1.3f, 1.8f, 70f, false); break;
            case Kind.Crossbow: Crossbow(w, 1, 1.8f, 75f, 1, false); break;
            case Kind.HeavyCrossbow: Crossbow(w, 1, 2.8f, 95f, 3, false); break;
            case Kind.RepeatingCrossbow: Crossbow(w, 6, 3f, 55f, 0, true); break;
            case Kind.Sling: Sling(w); break;
            case Kind.Blowgun: Blowgun(w); break;
            case Kind.ThrowingKnife: Thrown(w, "Throw", 0.08f, WeaponCategory.Thrown, DamageForm.Pierce, 10, 0.2f); break;
            case Kind.Javelin: Thrown(w, "Hurl Javelin", 0.2f, WeaponCategory.Thrown, DamageForm.Pierce, 5, 0f); break;
            case Kind.ThrowingAxe: Thrown(w, "Hurl Axe", 0.14f, WeaponCategory.Thrown, DamageForm.Slash, 6, 0f); break;
            case Kind.Shuriken: Thrown(w, "Flick", 0.04f, WeaponCategory.Thrown, DamageForm.Slash, 20, 0f); break;
            case Kind.Pistol: Gun(w, 6, 1.5f, 0.3f, 140f, 1, 0.8f, "Bullet", 0f); break;
            case Kind.Musket: Gun(w, 1, 2.6f, 0.5f, 180f, 1, 0.2f, "Bullet", 0f); break;
            case Kind.Blunderbuss: Gun(w, 1, 2.4f, 0.6f, 90f, 7, 9f, "Shell", 0f); break;
            case Kind.HandCannon: Gun(w, 1, 3.2f, 0.8f, 45f, 1, 1f, "Shot", 2.2f); break;
        }
        w.SetGridSize(ItemPresets.SuggestGridSize(w), true);
        EditorUtility.SetDirty(w);
    }

    // ================================================================== swords
    private static void Sword(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Sword, 10f, 14f, 0.08f, 1.6f, 0.5f, WeaponScaling.Strength);
        w.SetHitVolumes(new[] { Capsule("Blade", 0.12f, 0.95f, 0.06f), Sphere("Pommel", new Vector3(0f, -0.1f, 0f), 0.06f) });
        w.SetAction(AttackType.Normal, Chain(Blade(A("Slash", 0.10f, 0.15f, 0.28f, 1f, 8f), "Blade"),
            Blade(V("Backhand", 0.09f, 0.15f, 0.26f, 1.05f, 8f), "Blade"),
            Fx(Blade(V("Rising Slash", 0.13f, 0.17f, 0.38f, 1.4f, 10f), "Blade"), new KnockbackEffect { distance = 1.5f, arcHeight = 0.6f })));
        w.SetAction(AttackType.Heavy, Charge(Fx(Blade(A("Overhead Cleave", 0.2f, 0.18f, 0.45f, 1.6f, 15f), "Blade"), Bleed(2f, 4f)), 0.3f, 1.2f, 2.2f, 1.2f));
        w.SetAction(AttackType.Alternate, Form(Fx(Blade(A("Pommel Strike", 0.06f, 0.1f, 0.25f, 0.6f, 6f), "Pommel"), Stun(0.6f, 0.35f)), DamageForm.Blunt));
        w.SetAction(AttackType.Special, Lunge(Form(Shape(A("Lunging Thrust", 0.12f, 0.16f, 0.4f, 1.4f, 12f), Line(3.2f, 0.8f)), DamageForm.Pierce), 2.5f));
    }

    private static void Rapier(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Sword, 8f, 11f, 0.18f, 2.1f, 0.2f, WeaponScaling.Agility);
        w.SetHandling(WeaponGrip.OneHanded, OffHandUse.Allowed);
        w.SetHitVolumes(new[] { Capsule("Blade", 0.1f, 1.05f, 0.03f), Sphere("Guard", new Vector3(0f, 0.05f, 0f), 0.07f) });
        AttackAction thrusts = Chain(Crit(Form(Blade(A("Thrust", 0.06f, 0.1f, 0.18f, 0.9f, 5f), "Blade"), DamageForm.Pierce), 0.05f),
            Crit(Form(Blade(V("Feint Thrust", 0.05f, 0.1f, 0.16f, 0.85f, 5f), "Blade"), DamageForm.Pierce), 0.05f),
            Crit(Form(Blade(V("Disengage Thrust", 0.07f, 0.1f, 0.2f, 1f, 5f), "Blade"), DamageForm.Pierce), 0.1f),
            Crit(Form(Shape(V("Coup de Grâce", 0.1f, 0.12f, 0.32f, 1.5f, 8f), Line(2.6f, 0.4f)), DamageForm.Pierce), 0.25f));
        thrusts.cancelPoint = 0.3f;
        w.SetAction(AttackType.Normal, thrusts);
        // Flèche: a running lunge, untouchable at the start, hits one enemy with a high critical chance.
        AttackAction fleche = Lunge(Crit(Form(Shape(A("Flèche", 0.1f, 0.18f, 0.4f, 1.8f, 14f), Line(3.6f, 0.5f)), DamageForm.Pierce), 0.25f), 3.5f, 0.15f);
        fleche.maxTargets = 1;
        w.SetAction(AttackType.Heavy, fleche);
        // Flurry: one long active phase that hits the same enemy every 0.12 s.
        AttackAction flurry = Crit(Form(Blade(A("Flurry", 0.08f, 0.6f, 0.3f, 0.45f, 12f), "Blade"), DamageForm.Pierce), 0.1f);
        flurry.rehitInterval = 0.12f;
        w.SetAction(AttackType.Special, flurry);
        w.SetAction(AttackType.Alternate, Fx(Blade(A("Riposte Cut", 0.04f, 0.12f, 0.22f, 0.9f, 6f), "Blade"), Bleed(1.5f, 3f)));
        Guard(w, 0.35f);
    }

    private static void Katana(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Sword, 12f, 16f, 0.12f, 1.9f, 0.4f, WeaponScaling.Agility);
        w.SetHitVolumes(new[] { Capsule("Blade", 0.15f, 1.0f, 0.05f) });
        w.SetAction(AttackType.Normal, Chain(Fx(Blade(A("Kesa Cut", 0.1f, 0.14f, 0.26f, 1f, 8f), "Blade"), Bleed(1.5f, 3f, 0.3f)),
            Fx(Blade(V("Reverse Cut", 0.08f, 0.14f, 0.24f, 1.05f, 8f), "Blade"), Bleed(1.5f, 3f, 0.3f)),
            Fx(Blade(V("Vertical Cut", 0.12f, 0.15f, 0.36f, 1.4f, 10f), "Blade"), Bleed(2f, 4f, 0.5f))));
        // Iaido: hold, then a blinding draw-cut that crosses the distance.
        AttackAction iaido = Lunge(Charge(Fx(Shape(A("Iaido Draw-Cut", 0.05f, 0.12f, 0.45f, 1.9f, 16f), Line(4f, 1.2f)), Bleed(3f, 4f)), 0.4f, 1.4f, 2.4f, 1.2f), 4f, 0.2f);
        w.SetAction(AttackType.Heavy, iaido);
        w.SetAction(AttackType.Alternate, Lunge(Blade(A("Stepping Parry Cut", 0.03f, 0.12f, 0.2f, 0.8f, 6f), "Blade"), -1.5f, 0.2f));
        Guard(w, 0.4f);
    }

    private static void Scimitar(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Sword, 9f, 14f, 0.1f, 1.7f, 0.4f, WeaponScaling.Agility);
        w.SetHandling(WeaponGrip.OneHanded, OffHandUse.Allowed);
        w.SetHitVolumes(new[] { Capsule("Blade", 0.12f, 0.85f, 0.07f) });
        w.SetAction(AttackType.Normal, Chain(Fx(Blade(A("Crescent Slash", 0.08f, 0.16f, 0.24f, 0.9f, 7f), "Blade"), Bleed(1f, 3f, 0.4f)),
            Fx(Blade(V("Crescent Return", 0.08f, 0.16f, 0.24f, 0.95f, 7f), "Blade"), Bleed(1f, 3f, 0.4f)),
            Fx(Blade(V("Whirl", 0.1f, 0.25f, 0.34f, 1.2f, 9f), "Blade"), Bleed(1.5f, 3f, 0.5f))));
        // Sandstorm: a full spin hitting all around.
        AttackAction spin = Shape(A("Sandstorm Spin", 0.12f, 0.3f, 0.4f, 1.3f, 14f), Circle(2.4f));
        spin.behaviours.Add(new InvulnerabilityBehaviour { when = AttackMoment.ActiveStart, duration = 0.2f });
        w.SetAction(AttackType.Heavy, Fx(spin, Bleed(2f, 3f, 0.6f)));
    }

    private static void Longsword(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Sword, 13f, 18f, 0.08f, 1.7f, 0.7f, WeaponScaling.Strength);
        w.SetHandling(WeaponGrip.TwoHanded, OffHandUse.NotAllowed);
        w.SetHitVolumes(new[] { Capsule("Blade", 0.2f, 1.2f, 0.06f), Sphere("Pommel", new Vector3(0f, -0.15f, 0f), 0.06f) });
        w.SetAction(AttackType.Normal, Chain(Blade(A("Oberhau", 0.14f, 0.16f, 0.3f, 1f, 10f), "Blade"),
            Blade(V("Mittelhau", 0.12f, 0.18f, 0.3f, 1.1f, 10f), "Blade"),
            Form(Shape(V("Thrust", 0.12f, 0.12f, 0.36f, 1.3f, 10f), Line(3f, 0.6f)), DamageForm.Pierce)));
        AttackAction zornhau = Charge(Guard(Blade(A("Zornhau", 0.22f, 0.18f, 0.5f, 1.8f, 18f), "Blade"), 2f), 0.4f, 1.4f, 2.3f, 1.2f);
        w.SetAction(AttackType.Heavy, zornhau);
        // Mordhau: the sword reversed, the pommel and guard used as a hammer.
        w.SetAction(AttackType.Special, Form(Fx(Blade(A("Mordhau", 0.18f, 0.14f, 0.45f, 1.3f, 14f), "Pommel"), Stun(1f, 0.5f)), DamageForm.Blunt));
        Guard(w, 0.3f);
    }

    private static void Greatsword(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Greatsword, 18f, 26f, 0.05f, 1.8f, 1.2f, WeaponScaling.Strength);
        w.SetHitVolumes(new[] { Capsule("Blade", 0.25f, 1.55f, 0.09f) });
        w.SetAction(AttackType.Normal, Chain(Blade(A("Cleave", 0.22f, 0.2f, 0.45f, 1.3f, 14f), "Blade"),
            Fx(Blade(V("Reverse Cleave", 0.24f, 0.22f, 0.55f, 1.6f, 16f), "Blade"), new KnockbackEffect { distance = 2f })));
        AttackAction slam = Charge(Blade(A("Ground Slam", 0.3f, 0.14f, 0.6f, 1.8f, 22f), "Blade"), 0.4f, 1.6f, 2.5f, 1.6f);
        Impact(slam, 3.2f, 0.7f, DamageForm.Blunt, Stun(0.8f, 1f));
        w.SetAction(AttackType.Heavy, slam);
        AttackAction whirl = Shape(A("Whirlwind", 0.2f, 0.45f, 0.5f, 1.1f, 20f), Circle(2.8f));
        whirl.rehitInterval = 0.2f;
        w.SetAction(AttackType.Special, whirl);
        Guard(w, 0.3f);
    }

    // ================================================================== axes, maces, hammers
    private static void Axe(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Axe, 12f, 17f, 0.1f, 1.8f, 0.6f, WeaponScaling.Strength);
        w.SetHitVolumes(new[] { Box("Head", new Vector3(0f, 0.6f, 0.08f), new Vector3(0.06f, 0.22f, 0.24f)), Capsule("Handle", 0f, 0.55f, 0.04f) });
        w.SetAction(AttackType.Normal, Chain(Fx(Blade(A("Chop", 0.15f, 0.15f, 0.35f, 1f, 9f), "Head"), Bleed(1.5f, 3f, 0.3f)),
            Fx(Blade(V("Backswing", 0.14f, 0.15f, 0.4f, 1.2f, 9f), "Head", "Handle"), Bleed(1.5f, 3f, 0.3f))));
        // Shield splitter: blocking it costs three times the stamina.
        w.SetAction(AttackType.Heavy, Charge(Guard(Blade(A("Shield Splitter", 0.25f, 0.15f, 0.5f, 1.7f, 16f), "Head"), 3f), 0.3f, 1.1f, 2.2f, 1f));
        // Bearded hook: drags the enemy in.
        w.SetAction(AttackType.Alternate, Fx(Blade(A("Hook", 0.1f, 0.14f, 0.32f, 0.5f, 7f), "Head"), new PullEffect { maxDistance = 3f, stopDistance = 1f, duration = 0.25f }));
    }

    private static void Greataxe(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Axe, 18f, 25f, 0.08f, 2f, 1f, WeaponScaling.Strength);
        w.SetHandling(WeaponGrip.TwoHanded, OffHandUse.NotAllowed);
        w.SetHitVolumes(new[] { Box("Head", new Vector3(0f, 1.05f, 0.1f), new Vector3(0.08f, 0.35f, 0.4f)), Capsule("Haft", 0f, 1f, 0.045f) });
        w.SetAction(AttackType.Normal, Chain(Fx(Blade(A("Heavy Chop", 0.25f, 0.18f, 0.5f, 1.3f, 14f), "Head"), Bleed(2f, 4f, 0.4f)),
            Fx(Blade(V("Rising Reaver", 0.22f, 0.2f, 0.55f, 1.5f, 15f), "Head", "Haft"), new KnockbackEffect { distance = 2.5f, arcHeight = 1f })));
        AttackAction execute = Charge(Fx(Guard(Blade(A("Executioner", 0.35f, 0.15f, 0.7f, 2.2f, 24f), "Head"), 2.5f),
            new InflictInjuryEffect { injuryType = InjuryDefaults.DeepWound, severChance = 0.1f, chance = 0.5f }), 0.5f, 1.8f, 2.6f, 1f);
        w.SetAction(AttackType.Heavy, execute);
        AttackAction spin = Shape(A("Berserker Spin", 0.2f, 0.5f, 0.5f, 1f, 22f), Circle(2.6f));
        spin.rehitInterval = 0.25f;
        w.SetAction(AttackType.Special, Fx(spin, Bleed(1.5f, 3f, 0.5f)));
    }

    private static void Mace(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Mace, 12f, 16f, 0.05f, 1.6f, 0.9f, WeaponScaling.Strength);
        w.SetHitVolumes(new[] { Sphere("Head", new Vector3(0f, 0.55f, 0f), 0.1f), Capsule("Handle", 0f, 0.48f, 0.035f) });
        w.SetAction(AttackType.Normal, Chain(Fx(Blade(A("Bash", 0.14f, 0.14f, 0.32f, 1f, 9f), "Head"), Stun(0.4f, 0.15f)),
            Fx(Blade(V("Backhand Bash", 0.14f, 0.14f, 0.34f, 1.05f, 9f), "Head"), Stun(0.4f, 0.15f)),
            Fx(Guard(Blade(V("Skull Cracker", 0.2f, 0.14f, 0.45f, 1.4f, 12f), "Head"), 2f), Stun(0.8f, 0.4f))));
        // Armour breaker: the target's Defense drops for a while.
        w.SetAction(AttackType.Heavy, Charge(Fx(Blade(A("Armour Breaker", 0.24f, 0.14f, 0.5f, 1.6f, 15f), "Head"), ArmourBreak(-8f, 6f)), 0.3f, 1.2f, 2.2f, 1.1f));
    }

    private static void Flail(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Mace, 13f, 19f, 0.07f, 1.7f, 1f, WeaponScaling.Strength);
        w.SetHitVolumes(new[] { Sphere("Ball", new Vector3(0f, 0.85f, 0f), 0.12f), Capsule("Chain", 0.35f, 0.8f, 0.03f) });
        // The chain swings around shields: unblockable, but slow.
        w.SetAction(AttackType.Normal, Chain(Unblockable(Blade(A("Whirling Strike", 0.22f, 0.2f, 0.4f, 1.1f, 11f), "Ball")),
            Unblockable(Fx(Blade(V("Overhead Smash", 0.28f, 0.16f, 0.5f, 1.4f, 13f), "Ball"), Stun(0.6f, 0.3f)))));
        AttackAction orbit = Shape(A("Orbit", 0.2f, 0.6f, 0.45f, 0.8f, 18f), Ring(1.4f, 2.4f));
        orbit.rehitInterval = 0.3f;
        w.SetAction(AttackType.Heavy, Unblockable(orbit));
    }

    private static void Warhammer(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Hammer, 16f, 22f, 0.05f, 1.7f, 1.5f, WeaponScaling.Strength);
        w.SetHitVolumes(new[] { Box("Head", new Vector3(0f, 0.72f, 0f), new Vector3(0.12f, 0.14f, 0.3f)), Box("Spike", new Vector3(0f, 0.72f, -0.18f), new Vector3(0.05f, 0.05f, 0.12f)),
                                Capsule("Handle", 0f, 0.65f, 0.04f) });
        w.SetAction(AttackType.Normal, Chain(Fx(Blade(A("Smash", 0.25f, 0.15f, 0.45f, 1.2f, 12f), "Head"), Stun(0.5f, 0.25f)),
            Form(Fx(Blade(V("Spike Backswing", 0.22f, 0.15f, 0.45f, 1.1f, 12f), "Spike"), Bleed(1.5f, 3f, 0.4f)), DamageForm.Pierce)));
        AttackAction quake = Charge(Blade(A("Earthquake", 0.35f, 0.12f, 0.7f, 1.5f, 24f), "Head"), 0.5f, 1.8f, 2.4f, 1.8f);
        Impact(quake, 4f, 0.8f, DamageForm.Crush, new SlowEffect { duration = 2.5f, slowAmount = 0.5f }, new KnockbackEffect { distance = 1f, arcHeight = 1.4f });
        quake.elementOverride = ElementType.Earth;
        w.SetAction(AttackType.Heavy, quake);
    }

    private static void Maul(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Hammer, 22f, 30f, 0.04f, 1.8f, 2f, WeaponScaling.Strength);
        w.SetHandling(WeaponGrip.TwoHanded, OffHandUse.NotAllowed);
        w.SetHitVolumes(new[] { Box("Head", new Vector3(0f, 1.15f, 0f), new Vector3(0.22f, 0.22f, 0.42f)), Capsule("Haft", 0f, 1.05f, 0.05f) });
        w.SetAction(AttackType.Normal, Chain(Form(Fx(Blade(A("Crushing Swing", 0.32f, 0.18f, 0.6f, 1.4f, 16f), "Head"), Stun(0.6f, 0.3f)), DamageForm.Crush),
            Form(Fx(Guard(Blade(V("Bone Breaker", 0.35f, 0.16f, 0.65f, 1.7f, 18f), "Head"), 2.5f), new InflictInjuryEffect { injuryType = InjuryDefaults.Fracture, chance = 0.35f }), DamageForm.Crush)));
        AttackAction meteor = Charge(Form(Blade(A("Meteor Strike", 0.45f, 0.14f, 0.8f, 2f, 28f), "Head"), DamageForm.Crush), 0.6f, 2f, 2.8f, 2f);
        Impact(meteor, 4.5f, 0.9f, DamageForm.Crush, Stun(1.2f, 1f), new KnockbackEffect { distance = 3f, arcHeight = 1.2f });
        w.SetAction(AttackType.Heavy, meteor);
        Guard(w, 0.25f);
    }

    // ================================================================== polearms, scythe, whip
    private static void Spear(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Spear, 11f, 15f, 0.07f, 1.7f, 0.8f, WeaponScaling.Agility);
        w.SetHitVolumes(new[] { Capsule("Tip", 1.35f, 1.85f, 0.05f), Capsule("Shaft", 0f, 1.35f, 0.04f) });
        w.SetAction(AttackType.Normal, Chain(Form(Blade(A("Thrust", 0.12f, 0.12f, 0.3f, 1f, 8f), "Tip"), DamageForm.Pierce),
            Form(Blade(V("Double Thrust", 0.1f, 0.18f, 0.3f, 1.05f, 8f), "Tip"), DamageForm.Pierce),
            Form(Fx(Shape(V("Piercing Line", 0.15f, 0.12f, 0.4f, 1.4f, 10f), Line(3.8f, 0.7f)), Bleed(1.5f, 3f, 0.4f)), DamageForm.Pierce)));
        w.SetAction(AttackType.Heavy, Lunge(Charge(Form(Shape(A("Impaling Charge", 0.15f, 0.25f, 0.45f, 1.7f, 16f), Line(3.8f, 1f)), DamageForm.Pierce), 0.3f, 1.2f, 2f, 1.3f), 3.5f));
        w.SetAction(AttackType.Alternate, Form(Fx(Blade(A("Shaft Sweep", 0.15f, 0.18f, 0.35f, 0.8f, 10f), "Shaft"), new KnockbackEffect { distance = 2.5f }), DamageForm.Blunt));
        // Leg sweep: aims at the legs and roots the enemy for a moment.
        AttackAction trip = Form(Fx(Blade(A("Leg Sweep", 0.12f, 0.14f, 0.35f, 0.6f, 9f), "Shaft"), new RootEffect { duration = 1f }), DamageForm.Blunt);
        trip.aimedBodyPart = "Legs";
        w.SetAction(AttackType.Special, trip);
    }

    private static void Halberd(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Spear, 15f, 21f, 0.06f, 1.8f, 1f, WeaponScaling.Strength);
        w.SetHandling(WeaponGrip.TwoHanded, OffHandUse.NotAllowed);
        w.SetHitVolumes(new[] { Box("Axe", new Vector3(0f, 1.6f, 0.12f), new Vector3(0.06f, 0.3f, 0.26f)), Capsule("Tip", 1.7f, 2.05f, 0.04f),
                                Box("Hook", new Vector3(0f, 1.6f, -0.1f), new Vector3(0.05f, 0.08f, 0.14f)), Capsule("Shaft", 0f, 1.5f, 0.04f) });
        w.SetAction(AttackType.Normal, Chain(Form(Blade(A("Axe Swing", 0.2f, 0.18f, 0.42f, 1.2f, 12f), "Axe"), DamageForm.Slash),
            Form(Blade(V("Spike Thrust", 0.16f, 0.12f, 0.38f, 1.1f, 10f), "Tip"), DamageForm.Pierce),
            Form(Fx(Blade(V("Overhead Hew", 0.26f, 0.15f, 0.55f, 1.6f, 14f), "Axe"), Bleed(2f, 4f, 0.5f)), DamageForm.Slash)));
        // Dismount: the back hook drags riders and shield-bearers forward and roots them.
        w.SetAction(AttackType.Alternate, Fx(Blade(A("Hook Pull", 0.15f, 0.15f, 0.4f, 0.6f, 10f), "Hook"), new PullEffect { maxDistance = 3.5f, stopDistance = 1.2f }, new RootEffect { duration = 0.8f }));
        w.SetAction(AttackType.Heavy, Charge(Guard(Blade(A("Crushing Hew", 0.3f, 0.15f, 0.6f, 1.8f, 18f), "Axe"), 2.5f), 0.4f, 1.5f, 2.3f, 1.1f));
    }

    private static void Scythe(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Other, 14f, 19f, 0.12f, 1.9f, 0.5f, WeaponScaling.Agility);
        w.SetHandling(WeaponGrip.TwoHanded, OffHandUse.NotAllowed);
        w.SetHitVolumes(new[] { Capsule("Blade", 1.2f, 1.75f, 0.06f), Capsule("Snath", 0f, 1.2f, 0.04f) });
        w.SetAction(AttackType.Normal, Chain(Form(Fx(Blade(A("Reap", 0.18f, 0.22f, 0.4f, 1.1f, 11f), "Blade"), Bleed(2f, 4f, 0.5f)), DamageForm.Slash),
            Form(Fx(Blade(V("Harvest", 0.2f, 0.25f, 0.45f, 1.3f, 12f), "Blade"), Bleed(2f, 4f, 0.5f)), DamageForm.Slash)));
        // Grim pull: hooks everyone in front toward you.
        AttackAction pull = Fx(Shape(A("Grim Pull", 0.22f, 0.15f, 0.45f, 0.8f, 14f), Cone(3.2f, 120f)), new PullEffect { maxDistance = 3f, stopDistance = 1.2f });
        w.SetAction(AttackType.Alternate, pull);
        AttackAction dance = Shape(A("Death Spiral", 0.2f, 0.55f, 0.5f, 1f, 22f), Ring(0.8f, 2.6f));
        dance.rehitInterval = 0.25f;
        w.SetAction(AttackType.Heavy, Fx(dance, Bleed(2f, 3f, 0.6f)));
    }

    private static void Whip(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Other, 7f, 10f, 0.15f, 1.8f, 0.3f, WeaponScaling.Agility);
        w.SetHandling(WeaponGrip.OneHanded, OffHandUse.Allowed);
        w.SetHitVolumes(new[] { Capsule("Lash", 0.3f, 2.6f, 0.05f) });
        w.SetAction(AttackType.Normal, Chain(Form(Blade(A("Lash", 0.12f, 0.12f, 0.28f, 1f, 6f), "Lash"), DamageForm.Slash),
            Form(Fx(Blade(V("Crack", 0.12f, 0.1f, 0.3f, 1.2f, 7f), "Lash"), Stun(0.3f, 0.3f)), DamageForm.Slash)));
        w.SetAction(AttackType.Alternate, Fx(Shape(A("Snare", 0.15f, 0.12f, 0.4f, 0.4f, 9f), Line(4.5f, 0.6f)), new PullEffect { maxDistance = 4f, stopDistance = 1.5f }));
        w.SetAction(AttackType.Heavy, Fx(Shape(A("Disarming Crack", 0.2f, 0.1f, 0.45f, 0.8f, 12f), Line(4.5f, 0.5f)), new SilenceEffect { duration = 1.5f }));
    }

    // ================================================================== daggers and fists
    private static void Dagger(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Dagger, 6f, 9f, 0.18f, 2f, 0f, WeaponScaling.Agility);
        w.SetHitVolumes(new[] { Capsule("Blade", 0.05f, 0.32f, 0.035f) });
        AttackAction stabs = Chain(Form(Blade(A("Stab", 0.05f, 0.1f, 0.16f, 0.7f, 4f), "Blade"), DamageForm.Pierce),
            Form(Blade(V("Slice", 0.05f, 0.1f, 0.16f, 0.75f, 4f), "Blade"), DamageForm.Slash),
            Form(Blade(V("Stab", 0.05f, 0.1f, 0.16f, 0.8f, 4f), "Blade"), DamageForm.Pierce),
            Crit(Form(Fx(Blade(V("Gut Rip", 0.08f, 0.12f, 0.28f, 1.2f, 6f), "Blade"), Bleed(2f, 4f, 0.6f)), DamageForm.Slash), 0.1f));
        stabs.cancelPoint = 0.3f;
        w.SetAction(AttackType.Normal, stabs);
        w.SetAction(AttackType.Heavy, Charge(Crit(Form(Blade(A("Backstab", 0.1f, 0.1f, 0.3f, 1.6f, 10f), "Blade"), DamageForm.Pierce), 0.4f), 0.4f, 1f, 2.5f, 1f));
        AttackAction poison = Fx(Blade(A("Poisoned Strike", 0.08f, 0.12f, 0.3f, 0.9f, 10f), "Blade"),
            new DamageOverTimeEffect { damagePerTick = 2f, tickInterval = 0.5f, duration = 4f, element = ElementType.Poison });
        w.SetAction(AttackType.Special, poison);
        w.SetAction(AttackType.Alternate, Lunge(Shape(A("Backstep", 0.02f, 0f, 0.25f, 0f, 6f), null), -2.5f, 0.25f));
    }

    private static void Knuckles(WeaponSO w, bool claws)
    {
        w.SetStats(WeaponCategory.Fist, 5f, 8f, 0.12f, 1.8f, 0.3f, WeaponScaling.Strength);
        w.SetHitVolumes(claws
            ? new[] { Capsule("Claws", 0.05f, 0.32f, 0.05f) }
            : new[] { Sphere("Fist", new Vector3(0f, 0.04f, 0.05f), 0.08f) });
        string v = claws ? "Claws" : "Fist";
        DamageForm form = claws ? DamageForm.Claw : DamageForm.Blunt;
        AttackAction combo = Chain(Form(Blade(A(claws ? "Rake" : "Jab", 0.05f, 0.08f, 0.16f, 0.8f, 4f), v), form),
            Form(Blade(V(claws ? "Cross Rake" : "Cross", 0.05f, 0.08f, 0.16f, 0.85f, 4f), v), form),
            Form(Blade(V(claws ? "Rending Hook" : "Hook", 0.07f, 0.1f, 0.2f, 1f, 5f), v), form),
            Form(Fx(Blade(V(claws ? "Gutting Uppercut" : "Uppercut", 0.09f, 0.1f, 0.3f, 1.3f, 6f), v), claws
                ? (AbilityEffect)Bleed(2f, 4f, 0.7f) : new KnockbackEffect { distance = 1f, arcHeight = 1.2f }), form));
        combo.cancelPoint = 0.3f;
        w.SetAction(AttackType.Normal, combo);
        AttackAction haymaker = Charge(Form(Fx(Blade(A(claws ? "Savage Swipe" : "Haymaker", 0.15f, 0.1f, 0.35f, 1.6f, 12f), v),
            new KnockbackEffect { distance = 2f }, Stun(0.4f, 0.3f)), form), 0.3f, 1f, 2.2f, 1.1f);
        w.SetAction(AttackType.Heavy, haymaker);
        w.SetAction(AttackType.Alternate, Lunge(Form(Shape(A(claws ? "Pounce" : "Dash Punch", 0.06f, 0.12f, 0.3f, 1.1f, 8f), Line(2.4f, 0.8f)), form), 2f));
        // Flurry of blows: one long active phase that keeps hitting.
        AttackAction flurry = Form(Blade(A(claws ? "Shred" : "Flurry Of Blows", 0.06f, 0.7f, 0.3f, 0.4f, 14f), v), form);
        flurry.rehitInterval = 0.1f;
        w.SetAction(AttackType.Special, flurry);
    }

    // ================================================================== staves and wands
    private static void BattleStaff(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Staff, 9f, 13f, 0.06f, 1.6f, 0.8f, WeaponScaling.Agility);
        w.SetHitVolumes(new[] { Capsule("Upper", 0.75f, 1.45f, 0.045f), Capsule("Lower", -0.55f, 0.05f, 0.045f) });
        w.SetAction(AttackType.Normal, Chain(Form(Blade(A("Strike", 0.1f, 0.14f, 0.24f, 0.9f, 6f), "Upper"), DamageForm.Blunt),
            Form(Blade(V("Butt Strike", 0.08f, 0.14f, 0.22f, 0.9f, 6f), "Lower"), DamageForm.Blunt),
            Form(Blade(V("Spinning Strike", 0.1f, 0.25f, 0.3f, 1f, 8f), "Upper", "Lower"), DamageForm.Blunt),
            Form(Fx(Blade(V("Vault Kick Strike", 0.14f, 0.15f, 0.4f, 1.4f, 10f), "Upper"), Stun(0.6f, 0.4f)), DamageForm.Blunt)));
        AttackAction windmill = Form(Shape(A("Windmill", 0.15f, 0.5f, 0.4f, 0.8f, 16f), Circle(2.2f)), DamageForm.Blunt);
        windmill.rehitInterval = 0.2f;
        windmill.behaviours.Add(new InvulnerabilityBehaviour { when = AttackMoment.ActiveStart, duration = 0.15f });
        w.SetAction(AttackType.Heavy, windmill);
        w.SetAction(AttackType.Alternate, Lunge(Form(Fx(Blade(A("Pole Vault Kick", 0.12f, 0.14f, 0.4f, 1f, 10f), "Lower"), new KnockbackEffect { distance = 3f }), DamageForm.Blunt), 2.5f));
        Guard(w, 0.4f);
    }

    /// <summary>A mage staff fires mana bolts (no ammo; Mana per shot), novas on Heavy, a staff sweep on Alternate.</summary>
    private static void MageStaff(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Staff, 8f, 11f, 0.05f, 1.6f, 0f, WeaponScaling.Intelligence);
        w.SetHitVolumes(new[] { Sphere("Orb", new Vector3(0f, 1.45f, 0f), 0.12f), Capsule("Shaft", 0f, 1.35f, 0.04f) });
        AttackAction bolt = Magic(None(A("Mana Bolt", 0.18f, 0.02f, 0.3f, 1f, 0f)));
        w.SetAction(AttackType.Normal, bolt);
        w.SetRanged(ManaShots(5f, 28f, 0.5f, 0.35f, 0f, 1));
        AttackAction nova = Charge(Magic(Fx(Shape(A("Arcane Nova", 0.25f, 0.12f, 0.5f, 1.3f, 0f), Circle(3.5f)), new KnockbackEffect { distance = 3f })), 0.4f, 1.6f, 2.2f, 1.5f);
        w.SetAction(AttackType.Heavy, nova);
        w.SetAction(AttackType.Alternate, Form(Fx(Blade(A("Staff Sweep", 0.12f, 0.16f, 0.32f, 0.6f, 6f), "Shaft"), new KnockbackEffect { distance = 2f }), DamageForm.Blunt));
        w.SetAction(AttackType.Special, Lunge(None(A("Blink Step", 0.02f, 0.02f, 0.25f, 0f, 0f)), -4f, 0.3f));
        Guard(w, 0.3f);
    }

    private static void Wand(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Wand, 6f, 8f, 0.08f, 1.7f, 0f, WeaponScaling.Intelligence);
        w.SetHandling(WeaponGrip.OneHanded, OffHandUse.Allowed);
        w.SetHitVolumes(new[] { Sphere("Tip", new Vector3(0f, 0.33f, 0f), 0.05f) });
        w.SetAction(AttackType.Normal, Magic(None(A("Spark", 0.08f, 0.02f, 0.18f, 1f, 0f))));
        w.SetAction(AttackType.Heavy, Charge(Magic(None(A("Charged Spark", 0.1f, 0.02f, 0.3f, 1.8f, 0f))), 0.3f, 1.2f, 2f, 1f));
        MagazineMechanic shots = ManaShots(2.5f, 34f, 0.22f, 0.6f, 0f, 1);
        shots.firingAttacks = new List<AttackType> { AttackType.Normal, AttackType.Heavy };
        w.SetRanged(shots);
        w.SetAction(AttackType.Alternate, Magic(Fx(Shape(A("Repulse", 0.12f, 0.1f, 0.35f, 0.4f, 0f), Cone(2.5f, 90f)), new KnockbackEffect { distance = 3.5f })));
    }

    // ================================================================== bows, crossbows, slings
    private static void Bow(WeaponSO w, float drawTime, float maxDamage, float maxVelocity, bool quickShots)
    {
        w.SetStats(WeaponCategory.Bow, 12f, 16f, 0.1f, 2f, 0f, WeaponScaling.Agility);
        w.SetHitVolumes(new[] { Capsule("Limb", -0.6f, 0.6f, 0.04f) });
        w.SetAction(AttackType.Normal, None(A("Draw And Release", 0.05f, 0.02f, 0.3f, 1f, 2f)));
        var draw = new DrawMechanic
        {
            firingAttacks = new List<AttackType> { AttackType.Normal },
            style = quickShots ? DrawStyle.HoldToDraw : DrawStyle.HoldToDraw,
            drawTime = drawTime,
            maxDamage = maxDamage,
            maxVelocity = maxVelocity,
            minVelocity = maxVelocity * 0.3f,
            quickDamage = quickShots ? 0.75f : 0.5f,
            quickFireInterval = quickShots ? 0.3f : 0.5f,
            moveSpeedWhileDrawing = quickShots ? 0.6f : 0.35f,
            steadyHoldTime = quickShots ? 2f : 4f,
        };
        draw.ammo.ammoType = "Arrow";
        w.SetRanged(draw);
        w.SetAction(AttackType.Alternate, Form(Fx(Blade(A("Bow Bash", 0.08f, 0.12f, 0.3f, 0.5f, 5f), "Limb"), new KnockbackEffect { distance = 2f }), DamageForm.Blunt));
    }

    private static void Crossbow(WeaponSO w, int magazine, float reload, float velocity, int pierce, bool repeating)
    {
        w.SetStats(WeaponCategory.Crossbow, repeating ? 10f : 18f, repeating ? 13f : 24f, 0.12f, 2.2f, 0.5f, WeaponScaling.Agility);
        w.SetHitVolumes(new[] { Box("Stock", new Vector3(0f, 0f, -0.25f), new Vector3(0.08f, 0.1f, 0.35f)) });
        w.SetAction(AttackType.Normal, None(A("Shoot", 0.05f, 0.02f, repeating ? 0.15f : 0.35f, 1f, 2f)));
        var bolts = new MagazineMechanic
        {
            magazineSize = magazine,
            reloadTime = reload,
            velocity = velocity,
            damageMultiplier = repeating ? 0.8f : pierce > 1 ? 1.5f : 1.2f,
            spread = repeating ? 1.2f : 0.3f,
            fireInterval = repeating ? 0.28f : 0.25f,
            automatic = repeating,
            firingAttacks = new List<AttackType> { AttackType.Normal },
        };
        bolts.ammo.ammoType = "Bolt";
        bolts.projectile.gravity = 5f;
        bolts.projectile.pierce = pierce;
        w.SetRanged(bolts);
        w.SetAction(AttackType.Alternate, Form(Fx(Blade(A("Stock Strike", 0.1f, 0.12f, 0.35f, 0.5f, 6f), "Stock"), new KnockbackEffect { distance = 1.5f }), DamageForm.Blunt));
    }

    private static void Sling(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Other, 6f, 9f, 0.08f, 1.8f, 0.4f, WeaponScaling.Agility);
        w.SetHandling(WeaponGrip.OneHanded, OffHandUse.Allowed);
        w.SetAction(AttackType.Normal, Form(None(A("Sling Stone", 0.1f, 0.02f, 0.3f, 1f, 3f)), DamageForm.Blunt));
        var draw = new DrawMechanic { firingAttacks = new List<AttackType> { AttackType.Normal }, drawTime = 0.8f, maxDamage = 1.4f, maxVelocity = 38f, minVelocity = 14f };
        draw.ammo.ammoType = "Stone";
        draw.projectile.gravity = 9.81f;
        draw.projectile.stickInSurfaces = false;
        w.SetRanged(draw);
        w.SetHitVolumes(new WeaponBlade[0]);
    }

    private static void Blowgun(WeaponSO w)
    {
        w.SetStats(WeaponCategory.Other, 3f, 4f, 0.2f, 2f, 0f, WeaponScaling.Agility);
        w.SetHandling(WeaponGrip.OneHanded, OffHandUse.Allowed);
        AttackAction puff = Form(Fx(None(A("Dart", 0.08f, 0.02f, 0.4f, 1f, 2f)), new DamageOverTimeEffect { damagePerTick = 2f, tickInterval = 1f, duration = 6f, element = ElementType.Poison },
            new SlowEffect { slowAmount = 0.3f, duration = 3f }), DamageForm.Pierce);
        w.SetAction(AttackType.Normal, puff);
        var gun = new MagazineMechanic { magazineSize = 1, reloadTime = 0.9f, velocity = 45f, spread = 0.4f, firingAttacks = new List<AttackType> { AttackType.Normal } };
        gun.ammo.ammoType = "Dart";
        gun.projectile.gravity = 2f;
        w.SetRanged(gun);
        w.SetHitVolumes(new WeaponBlade[0]);
    }

    // ================================================================== thrown and guns
    private static void Thrown(WeaponSO w, string name, float windUp, WeaponCategory category, DamageForm form, int stack, float pierce)
    {
        w.SetStats(category, 7f, 10f, 0.15f, 2f, 0.2f, WeaponScaling.Agility);
        w.SetHandling(WeaponGrip.OneHanded, OffHandUse.Allowed);
        w.SetHitVolumes(new[] { Capsule("Blade", 0.05f, 0.3f, 0.04f) });
        w.SetAction(AttackType.Normal, Form(None(A(name, windUp, 0.02f, 0.3f, 1f, 4f)), form));
        w.SetAction(AttackType.Alternate, Form(Blade(A("Close Strike", 0.06f, 0.1f, 0.2f, 0.6f, 4f), "Blade"), form));
        var t = new ThrowMechanic();
        t.projectile.pierce = pierce > 0f ? 1 : 0;
        w.SetRanged(t);
        var so = new SerializedObject(w);
        so.FindProperty("stackMax").intValue = stack;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Gun(WeaponSO w, int magazine, float reload, float interval, float velocity, int pellets, float spread, string ammo, float explosion)
    {
        w.SetStats(WeaponCategory.Other, pellets > 1 ? 5f : 14f, pellets > 1 ? 7f : 20f, 0.1f, 2f, 0.4f, WeaponScaling.Agility);
        w.SetHitVolumes(new[] { Box("Butt", new Vector3(0f, -0.05f, -0.2f), new Vector3(0.07f, 0.12f, 0.2f)) });
        w.SetAction(AttackType.Normal, None(A("Fire", 0.02f, 0.02f, interval, 1f, 0f)));
        var gun = new MagazineMechanic
        {
            magazineSize = magazine,
            reloadTime = reload,
            fireInterval = interval,
            velocity = velocity,
            projectilesPerShot = pellets,
            spread = spread,
            bloomPerShot = magazine > 1 ? 1.2f : 0f,
            maxBloom = 5f,
            firingAttacks = new List<AttackType> { AttackType.Normal },
        };
        gun.ammo.ammoType = ammo;
        gun.projectile.gravity = explosion > 0f ? 4f : 0f;
        gun.projectile.stickInSurfaces = false;
        gun.projectile.recoverChance = 0f;
        gun.projectile.damageAtMaxDistance = pellets > 1 ? 0.2f : 0.5f;
        gun.projectile.explosionRadius = explosion;
        w.SetRanged(gun);
        w.SetAction(AttackType.Alternate, Form(Fx(Blade(A("Pistol Whip", 0.08f, 0.1f, 0.3f, 0.5f, 5f), "Butt"), Stun(0.5f, 0.3f)), DamageForm.Blunt));
        if (magazine == 1 && pellets == 1)
            w.SetHandling(WeaponGrip.TwoHanded, OffHandUse.NotAllowed);
        else if (pellets == 1)
            w.SetHandling(WeaponGrip.OneHanded, OffHandUse.Allowed);
    }

    // ================================================================== builders
    private static WeaponBlade Capsule(string name, float start, float end, float radius) => new WeaponBlade
    {
        name = name, shape = WeaponBlade.VolumeShape.Capsule, axis = WeaponBlade.Axis.Y, start = start, end = end, radius = radius,
        startMarker = name + "Start", endMarker = name + "End",
    };

    private static WeaponBlade Sphere(string name, Vector3 center, float radius) => new WeaponBlade
    {
        name = name, shape = WeaponBlade.VolumeShape.Sphere, center = center, radius = radius, startMarker = name + "Center", endMarker = "",
    };

    private static WeaponBlade Box(string name, Vector3 center, Vector3 size) => new WeaponBlade
    {
        name = name, shape = WeaponBlade.VolumeShape.Box, center = center, size = size, startMarker = name + "Center", endMarker = "",
    };

    private static HitShape Cone(float radius, float angle) => HitShape.ConeShape(radius, angle);
    private static HitShape Circle(float radius) => HitShape.CircleShape(radius);
    private static HitShape Line(float length, float width) => HitShape.LineShape(length, width);
    private static HitShape Ring(float inner, float outer) => new HitShape { type = HitShapeType.Ring, radius = outer, innerRadius = inner };

    private static void Fill(AttackComponent a, float startup, float active, float recovery, float damage, float stamina)
    {
        a.SetTiming(startup, active, recovery);
        a.damageMultiplier = damage;
        a.damageMode = damage > 0f ? WeaponDamageMode.WeaponDamage : WeaponDamageMode.EffectsOnly;
        a.staminaCost = stamina;
        a.cancelPoint = 0.5f;
    }

    private static AttackAction A(string name, float startup, float active, float recovery, float damage, float stamina)
    {
        var a = new AttackAction { actionName = name, variantTime = 0.9f };
        Fill(a, startup, active, recovery, damage, stamina);
        return a;
    }

    private static AttackVariation V(string name, float startup, float active, float recovery, float damage, float stamina)
    {
        var v = new AttackVariation { variationName = name };
        Fill(v, startup, active, recovery, damage, stamina);
        return v;
    }

    private static AttackAction Chain(AttackAction first, params AttackVariation[] next)
    {
        first.variations.Clear();
        first.variations.AddRange(next);
        return first;
    }

    /// <summary>Hits with the named volumes of the weapon (Weapon Blade).</summary>
    private static T Blade<T>(T c, params string[] volumes) where T : AttackComponent
    {
        c.hitDetection = HitDetectionMode.WeaponBlade;
        c.bladeVolumes = new List<string>(volumes);
        return c;
    }

    /// <summary>Hits an area in front of the attacker (thrusts, spins, waves); null = no direct hit.</summary>
    private static T Shape<T>(T c, HitShape shape) where T : AttackComponent
    {
        if (shape == null)
            return None(c);
        c.hitDetection = HitDetectionMode.HitShape;
        c.hitShape = shape;
        return c;
    }

    private static T None<T>(T c) where T : AttackComponent
    {
        c.hitDetection = HitDetectionMode.None;
        return c;
    }

    private static T Fx<T>(T c, params AbilityEffect[] effects) where T : AttackComponent
    {
        c.onHitEffects.AddRange(effects);
        return c;
    }

    private static T Form<T>(T c, DamageForm form) where T : AttackComponent
    {
        c.damageForm = form;
        return c;
    }

    private static T Crit<T>(T c, float bonus) where T : AttackComponent
    {
        c.criticalChanceBonus = bonus;
        return c;
    }

    private static T Guard<T>(T c, float guardDamage) where T : AttackComponent
    {
        c.guardDamage = guardDamage;
        return c;
    }

    private static T Unblockable<T>(T c) where T : AttackComponent
    {
        c.unblockable = true;
        return c;
    }

    private static T Magic<T>(T c) where T : AttackComponent
    {
        c.damageType = DamageType.Magical;
        return c;
    }

    private static T Charge<T>(T c, float minCharge, float maxCharge, float damageAtFull, float areaAtFull) where T : AttackComponent
    {
        c.charge = new ChargeSettings
        {
            enabled = true, minChargeTime = minCharge, maxChargeTime = maxCharge, damageAtFullCharge = damageAtFull, areaAtFullCharge = areaAtFull,
            earlyRelease = ChargeSettings.EarlyRelease.NormalAttack, moveSpeedWhileCharging = 0.4f,
        };
        return c;
    }

    private static T Lunge<T>(T c, float distance, float invulnerable = 0f) where T : AttackComponent
    {
        c.behaviours.Add(new LungeBehaviour { when = AttackMoment.Start, distance = distance, duration = 0.18f });
        if (invulnerable > 0f)
            c.behaviours.Add(new InvulnerabilityBehaviour { when = AttackMoment.Start, duration = invulnerable });
        return c;
    }

    /// <summary>A shockwave where the weapon strikes the ground.</summary>
    private static void Impact(AttackComponent c, float radius, float damage, DamageForm form, params AbilityEffect[] effects)
    {
        c.impact = new ImpactSettings
        {
            enabled = true,
            trigger = ImpactSettings.Trigger.GroundContactOrActiveEnd,
            area = HitShape.CircleShape(radius),
            damageMultiplier = damage,
            damageForm = form,
            effects = new List<AbilityEffect>(effects),
        };
    }

    private static void Guard(WeaponSO w, float parryWindow)
    {
        ShieldDefense g = ShieldDefense.WeaponGuard();
        g.enabled = true;
        w.SetGuard(g);
    }

    private static DamageOverTimeEffect Bleed(float perTick, float seconds, float chance = 1f) =>
        new DamageOverTimeEffect { damagePerTick = perTick, tickInterval = 0.5f, duration = seconds, chance = chance };

    private static StunEffect Stun(float seconds, float chance) => new StunEffect { duration = seconds, chance = chance };

    private static StatModifierEffect ArmourBreak(float defense, float seconds)
    {
        var e = new StatModifierEffect { label = "Armour Broken", kind = StatBuffKind.Debuff, duration = seconds };
        e.onlyAffects = TargetFilter.Enemies;
        e.combatStats.Add(new CombatStatModifier(CombatStatType.Defense, defense));
        return e;
    }

    private static MagazineMechanic ManaShots(float manaPerShot, float velocity, float interval, float spread, float explosion, int count)
    {
        var m = new MagazineMechanic
        {
            magazineSize = 1, reloadTime = 0f, autoReloadWhenEmpty = true, fireInterval = interval, velocity = velocity, spread = spread,
            projectilesPerShot = count, firingAttacks = new List<AttackType> { AttackType.Normal },
        };
        m.ammo.infinite = true;
        m.extraCosts.Add(new ShotCost { resource = ShotResource.Mana, amount = manaPerShot });
        m.projectile.gravity = 0f;
        m.projectile.stickInSurfaces = false;
        m.projectile.recoverChance = 0f;
        m.projectile.explosionRadius = explosion;
        return m;
    }
}
#endif
