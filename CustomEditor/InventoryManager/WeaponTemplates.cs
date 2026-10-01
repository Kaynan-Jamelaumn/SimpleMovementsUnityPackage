#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ready-made movesets for weapons (Apply Template in the weapon inspector, or Assets ▸ Create ▸ Scriptable Objects ▸ Item ▸ Weapon
/// From Template). They only use data - attacks, chains, charge, hit shapes, on-hit effects and behaviours - so
/// everything can be tuned afterwards. Animations, sounds and abilities are left for you to assign.
/// </summary>
public static class WeaponTemplates
{
    public enum Template
    {
        Sword,
        Greatsword,
        Dagger,
        Spear,
        Hammer,
        Axe,
        Bow,
        Staff,
    }

    public static void Apply(WeaponSO weapon, Template t)
    {
        Undo.RecordObject(weapon, $"Apply {t} template");
        switch (t)
        {
            case Template.Sword:
                weapon.SetStats(WeaponCategory.Sword, 10f, 14f, 0.08f, 1.6f, 0.5f, WeaponScaling.Strength);
                weapon.SetAction(AttackType.Normal, Chain("Slash", 3, 0.10f, 0.15f, 0.28f, 1f, 8f, Cone(2.2f, 110f)));
                weapon.SetAction(AttackType.Heavy, Charged(Attack("Charged Slash", 0.18f, 0.2f, 0.45f, 1.6f, 15f, Cone(2.6f, 150f)), 0.3f, 1.2f, 2.2f, 1.2f));
                weapon.SetAction(AttackType.Alternate, ShieldBash());
                weapon.SetAction(AttackType.Special, Lunge(Attack("Lunging Thrust", 0.12f, 0.18f, 0.4f, 1.4f, 12f, Line(3.2f, 1f)), 2.5f));
                break;
            case Template.Greatsword:
                weapon.SetStats(WeaponCategory.Greatsword, 18f, 26f, 0.05f, 1.8f, 1.2f, WeaponScaling.Strength);
                weapon.SetAction(AttackType.Normal, Chain("Cleave", 2, 0.22f, 0.2f, 0.45f, 1.3f, 14f, Cone(2.8f, 160f)));
                var slam = Charged(Attack("Ground Slam", 0.3f, 0.12f, 0.6f, 1.8f, 22f, Circle(1.5f)), 0.4f, 1.6f, 2.5f, 1.6f);
                slam.behaviours.Add(new AreaBurstBehaviour { when = AttackMoment.ActiveStart, shape = HitShape.CircleShape(3.5f), weaponDamageFraction = 0.6f,
                    effects = new List<AbilityEffect> { new StunEffect { duration = 0.8f } } });
                weapon.SetAction(AttackType.Heavy, slam);
                break;
            case Template.Dagger:
                weapon.SetStats(WeaponCategory.Dagger, 6f, 9f, 0.18f, 2f, 0f, WeaponScaling.Agility);
                var stab = Chain("Stab", 4, 0.05f, 0.1f, 0.18f, 0.7f, 5f, Cone(1.8f, 70f));
                stab.cancelPoint = 0.35f;
                weapon.SetAction(AttackType.Normal, stab);
                var poison = Attack("Poisoned Strike", 0.08f, 0.12f, 0.3f, 0.9f, 10f, Cone(1.9f, 80f));
                poison.onHitEffects.Add(new DamageOverTimeEffect { damagePerTick = 2f, tickInterval = 0.5f, duration = 4f, element = ElementType.Poison });
                weapon.SetAction(AttackType.Special, poison);
                weapon.SetAction(AttackType.Alternate, Lunge(Attack("Backstep", 0.02f, 0f, 0.25f, 0f, 6f, null), -2.5f, invulnerable: 0.25f));
                break;
            case Template.Spear:
                weapon.SetStats(WeaponCategory.Spear, 11f, 15f, 0.07f, 1.7f, 0.8f, WeaponScaling.Agility);
                weapon.SetAction(AttackType.Normal, Chain("Thrust", 2, 0.12f, 0.12f, 0.3f, 1f, 8f, Line(3.4f, 0.9f)));
                weapon.SetAction(AttackType.Heavy, Lunge(Charged(Attack("Impaling Charge", 0.15f, 0.25f, 0.45f, 1.7f, 16f, Line(3.8f, 1.1f)), 0.3f, 1.2f, 2f, 1.3f), 3.5f));
                var sweep = Attack("Sweep", 0.15f, 0.15f, 0.35f, 0.8f, 10f, Circle(3f));
                sweep.onHitEffects.Add(new KnockbackEffect { distance = 2.5f });
                weapon.SetAction(AttackType.Alternate, sweep);
                break;
            case Template.Hammer:
                weapon.SetStats(WeaponCategory.Hammer, 16f, 22f, 0.05f, 1.7f, 1.5f, WeaponScaling.Strength);
                var smash = Chain("Smash", 2, 0.25f, 0.15f, 0.45f, 1.2f, 12f, Cone(2.4f, 100f));
                smash.onHitEffects.Add(new StunEffect { duration = 0.5f, chance = 0.25f });
                weapon.SetAction(AttackType.Normal, smash);
                var quake = Charged(Attack("Earthquake", 0.35f, 0.1f, 0.7f, 1.4f, 24f, Circle(4f)), 0.5f, 1.8f, 2.4f, 1.8f);
                quake.onHitEffects.Add(new SlowEffect { duration = 2.5f, slowAmount = 0.5f });
                quake.elementOverride = ElementType.Earth;
                weapon.SetAction(AttackType.Heavy, quake);
                break;
            case Template.Axe:
                weapon.SetStats(WeaponCategory.Axe, 12f, 17f, 0.1f, 1.8f, 0.6f, WeaponScaling.Strength);
                var chop = Chain("Chop", 2, 0.15f, 0.15f, 0.35f, 1f, 9f, Cone(2.1f, 100f));
                chop.onHitEffects.Add(new DamageOverTimeEffect { damagePerTick = 1.5f, tickInterval = 0.5f, duration = 3f, chance = 0.3f });
                weapon.SetAction(AttackType.Normal, chop);
                weapon.SetAction(AttackType.Heavy, Charged(Attack("Overhead Chop", 0.25f, 0.15f, 0.5f, 1.7f, 16f, Cone(2.3f, 60f)), 0.3f, 1.1f, 2.2f, 1f));
                break;
            case Template.Bow:
            {
                weapon.SetStats(WeaponCategory.Bow, 12f, 16f, 0.1f, 2f, 0f, WeaponScaling.Agility);
                var shot = Charged(Attack("Draw And Release", 0.05f, 0.02f, 0.3f, 1f, 6f, null), 0.2f, 1.2f, 2.5f, 1f);
                shot.hitDetection = HitDetectionMode.None;
                shot.charge.moveSpeedWhileCharging = 0.45f;
                shot.charge.earlyRelease = ChargeSettings.EarlyRelease.NormalAttack;
                shot.behaviours.Add(new CastAbilityBehaviour { when = AttackMoment.ActiveStart, aim = CastAbilityBehaviour.Aim.Forward, forwardDistance = 20f });
                weapon.SetAction(AttackType.Normal, shot);
                var bash = Attack("Bow Bash", 0.08f, 0.12f, 0.3f, 0.5f, 5f, Cone(1.8f, 90f));
                bash.onHitEffects.Add(new KnockbackEffect { distance = 2f });
                weapon.SetAction(AttackType.Alternate, bash);
                break;
            }
            case Template.Staff:
            {
                weapon.SetStats(WeaponCategory.Staff, 8f, 11f, 0.05f, 1.6f, 0f, WeaponScaling.Intelligence);
                var bolt = Chain("Arcane Bolt", 3, 0.12f, 0.02f, 0.3f, 1f, 4f, null);
                bolt.hitDetection = HitDetectionMode.None;
                bolt.damageType = DamageType.Magical;
                bolt.behaviours.Add(new CastAbilityBehaviour { when = AttackMoment.ActiveStart, aim = CastAbilityBehaviour.Aim.Target, forwardDistance = 15f });
                foreach (AttackVariation v in bolt.variations)
                {
                    v.hitDetection = HitDetectionMode.None;
                    v.damageType = DamageType.Magical;
                    v.behaviours.Add(new CastAbilityBehaviour { when = AttackMoment.ActiveStart, aim = CastAbilityBehaviour.Aim.Target, forwardDistance = 15f });
                }
                weapon.SetAction(AttackType.Normal, bolt);
                var nova = Charged(Attack("Nova", 0.2f, 0.1f, 0.5f, 1.3f, 18f, Circle(3.5f)), 0.4f, 1.5f, 2.2f, 1.5f);
                nova.damageType = DamageType.Magical;
                nova.onHitEffects.Add(new KnockbackEffect { distance = 3f });
                weapon.SetAction(AttackType.Heavy, nova);
                break;
            }
        }
        EditorUtility.SetDirty(weapon);
        Debug.Log($"[Weapons] Applied the {t} template to '{weapon.name}'. Assign animations and sounds{(t == Template.Bow || t == Template.Staff ? ", and the ability of the Cast Ability behaviours" : "")}.", weapon);
    }

    [MenuItem("Assets/Create/Scriptable Objects/Item/Weapon From Template/Sword", priority = 100)] private static void CreateSword() => Create(Template.Sword);
    [MenuItem("Assets/Create/Scriptable Objects/Item/Weapon From Template/Greatsword", priority = 101)] private static void CreateGreatsword() => Create(Template.Greatsword);
    [MenuItem("Assets/Create/Scriptable Objects/Item/Weapon From Template/Dagger", priority = 102)] private static void CreateDagger() => Create(Template.Dagger);
    [MenuItem("Assets/Create/Scriptable Objects/Item/Weapon From Template/Spear", priority = 103)] private static void CreateSpear() => Create(Template.Spear);
    [MenuItem("Assets/Create/Scriptable Objects/Item/Weapon From Template/Hammer", priority = 104)] private static void CreateHammer() => Create(Template.Hammer);
    [MenuItem("Assets/Create/Scriptable Objects/Item/Weapon From Template/Axe", priority = 105)] private static void CreateAxe() => Create(Template.Axe);
    [MenuItem("Assets/Create/Scriptable Objects/Item/Weapon From Template/Bow", priority = 106)] private static void CreateBow() => Create(Template.Bow);
    [MenuItem("Assets/Create/Scriptable Objects/Item/Weapon From Template/Staff", priority = 107)] private static void CreateStaff() => Create(Template.Staff);

    private static void Create(Template t)
    {
        string folder = "Assets";
        if (Selection.activeObject != null)
        {
            string p = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!string.IsNullOrEmpty(p))
                folder = AssetDatabase.IsValidFolder(p) ? p : System.IO.Path.GetDirectoryName(p).Replace('\\', '/');
        }
        var weapon = ScriptableObject.CreateInstance<WeaponSO>();
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{t}.asset");
        AssetDatabase.CreateAsset(weapon, path);
        var so = new SerializedObject(weapon);
        so.FindProperty("name").stringValue = t.ToString();
        so.FindProperty("stackMax").intValue = 1;
        so.FindProperty("maxDurability").intValue = 200;
        so.FindProperty("durability").intValue = 200;
        so.ApplyModifiedPropertiesWithoutUndo();
        Apply(weapon, t);
        AssetDatabase.SaveAssets();
        Selection.activeObject = weapon;
        EditorGUIUtility.PingObject(weapon);
    }

    // ------------------------------------------------------------------ builders
    private static HitShape Cone(float radius, float angle) => HitShape.ConeShape(radius, angle);
    private static HitShape Circle(float radius) => HitShape.CircleShape(radius);
    private static HitShape Line(float length, float width) => HitShape.LineShape(length, width);

    private static void Fill(AttackComponent a, float startup, float active, float recovery, float damage, float stamina, HitShape shape)
    {
        a.SetTiming(startup, active, recovery);
        a.damageMultiplier = damage;
        a.damageMode = damage > 0f ? WeaponDamageMode.WeaponDamage : WeaponDamageMode.EffectsOnly;
        a.staminaCost = stamina;
        a.cancelPoint = 0.5f;
        if (shape != null)
        {
            a.hitShape = shape;
            a.hitDetection = HitDetectionMode.HitShape;
        }
        else
        {
            a.hitDetection = HitDetectionMode.None;
        }
    }

    private static AttackAction Attack(string name, float startup, float active, float recovery, float damage, float stamina, HitShape shape)
    {
        var a = new AttackAction { actionName = name, variantTime = 0.9f };
        Fill(a, startup, active, recovery, damage, stamina, shape);
        return a;
    }

    /// <summary>A chain of <paramref name="length"/> attacks; each step is a little stronger and the last one hits harder.</summary>
    private static AttackAction Chain(string name, int length, float startup, float active, float recovery, float damage, float stamina, HitShape shape)
    {
        AttackAction a = Attack(name + " 1", startup, active, recovery, damage, stamina, shape?.Clone());
        a.actionName = name;
        for (int i = 1; i < length; i++)
        {
            bool last = i == length - 1;
            var v = new AttackVariation { variationName = $"{name} {i + 1}" };
            Fill(v, startup * (last ? 1.3f : 1f), active, recovery * (last ? 1.4f : 1f), damage * (last ? 1.5f : 1.1f), stamina, shape?.Clone());
            a.variations.Add(v);
        }
        return a;
    }

    private static AttackAction Charged(AttackAction a, float minCharge, float maxCharge, float damageAtFull, float areaAtFull)
    {
        a.charge = new ChargeSettings
        {
            enabled = true,
            minChargeTime = minCharge,
            maxChargeTime = maxCharge,
            damageAtFullCharge = damageAtFull,
            areaAtFullCharge = areaAtFull,
            earlyRelease = ChargeSettings.EarlyRelease.NormalAttack,
            moveSpeedWhileCharging = 0.4f,
        };
        return a;
    }

    private static AttackAction Lunge(AttackAction a, float distance, float invulnerable = 0f)
    {
        a.behaviours.Add(new LungeBehaviour { when = AttackMoment.Start, distance = distance, duration = 0.18f });
        if (invulnerable > 0f)
            a.behaviours.Add(new InvulnerabilityBehaviour { when = AttackMoment.Start, duration = invulnerable });
        return a;
    }

    private static AttackAction ShieldBash()
    {
        AttackAction a = Attack("Shield Bash", 0.08f, 0.1f, 0.35f, 0.4f, 10f, Cone(1.8f, 90f));
        a.onHitEffects.Add(new KnockbackEffect { distance = 2.5f });
        a.onHitEffects.Add(new StunEffect { duration = 0.4f, chance = 0.35f });
        a.behaviours.Add(new InvulnerabilityBehaviour { when = AttackMoment.Start, duration = 0.2f });
        return a;
    }
}
#endif
