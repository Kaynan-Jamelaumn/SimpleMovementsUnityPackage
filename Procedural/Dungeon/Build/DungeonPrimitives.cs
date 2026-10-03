using UnityEngine;
using UnityEngine.Rendering;

namespace ProceduralDungeon
{
    /// <summary>
    /// Builds the stand-in objects used when a table entry or theme slot has no prefab: chests, barrels, crates,
    /// torches, braziers, crystals, altars, pillars, rubble, spike traps, fountains, bookshelves, bones, mushrooms, the
    /// special rooms' furniture (sarcophagi, weapon racks, armor stands, cages, alchemy tables, cauldrons, thrones,
    /// banners, statues, tables, candles, vines, pedestals, herbs, cobwebs), traps (fire jets, blade pendulums, dart
    /// walls), lava pools, spore vents, ice crystals, puzzle plates, keys, gates and doors, placeholder mobs and the two
    /// portals - from Unity primitives, with colliders, lights where they glow and the mechanic components they need
    /// (hazards, shrine, rest point, chest, herbs, plate, key). The newer rooms' and floor styles' furniture is in
    /// DungeonPrimitivesMore.cs. Every object's pivot is at its base, facing +z.
    /// </summary>
    public sealed partial class DungeonPrimitives
    {
        private readonly DungeonMaterials materials;
        private readonly DungeonTheme theme;

        public DungeonPrimitives(DungeonMaterials materials, DungeonTheme theme)
        {
            this.materials = materials;
            this.theme = theme;
        }

        private static readonly Color Wood = new Color(0.36f, 0.22f, 0.12f);
        private static readonly Color DarkWood = new Color(0.22f, 0.13f, 0.07f);
        private static readonly Color Iron = new Color(0.25f, 0.25f, 0.27f);
        private static readonly Color Gold = new Color(0.95f, 0.75f, 0.2f);
        private static readonly Color Stone = new Color(0.5f, 0.48f, 0.45f);
        private static readonly Color DarkStone = new Color(0.32f, 0.3f, 0.29f);
        private static readonly Color Bone = new Color(0.88f, 0.85f, 0.76f);

        public GameObject Create(DungeonPrimitive kind, string name, Color lightColor, float lightRange, int variant)
        {
            var root = new GameObject(string.IsNullOrEmpty(name) ? kind.ToString() : name);
            switch (kind)
            {
                case DungeonPrimitive.Chest: Chest(root, variant); break;
                case DungeonPrimitive.Barrel:
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.45f, 0), new Vector3(0.6f, 0.45f, 0.6f), Wood);
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.2f, 0), new Vector3(0.63f, 0.03f, 0.63f), Iron);
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.7f, 0), new Vector3(0.63f, 0.03f, 0.63f), Iron);
                    Collider(root, new Vector3(0, 0.45f, 0), new Vector3(0.6f, 0.9f, 0.6f));
                    break;
                case DungeonPrimitive.Crate:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.4f, 0), new Vector3(0.8f, 0.8f, 0.8f), Wood);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.4f, 0), new Vector3(0.84f, 0.12f, 0.84f), DarkWood);
                    Collider(root, new Vector3(0, 0.4f, 0), new Vector3(0.8f, 0.8f, 0.8f));
                    break;
                case DungeonPrimitive.Torch:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0, -0.08f), new Vector3(0.12f, 0.25f, 0.06f), Iron);
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.12f, 0.05f), new Vector3(0.06f, 0.2f, 0.06f), DarkWood, new Vector3(20f, 0, 0));
                    Glow(root, PrimitiveType.Sphere, new Vector3(0, 0.36f, 0.12f), new Vector3(0.14f, 0.2f, 0.14f), lightColor, 4f);
                    Light(root, new Vector3(0, 0.4f, 0.25f), lightColor, lightRange, theme != null ? theme.torchIntensity : 2.2f, true);
                    break;
                case DungeonPrimitive.Brazier:
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.45f, 0), new Vector3(0.12f, 0.45f, 0.12f), Iron);
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.95f, 0), new Vector3(0.7f, 0.08f, 0.7f), Iron);
                    Glow(root, PrimitiveType.Sphere, new Vector3(0, 1.1f, 0), new Vector3(0.45f, 0.3f, 0.45f), lightColor, 4f);
                    Light(root, new Vector3(0, 1.4f, 0), lightColor, lightRange, (theme != null ? theme.torchIntensity : 2.2f) * 1.3f, true);
                    Collider(root, new Vector3(0, 0.55f, 0), new Vector3(0.7f, 1.1f, 0.7f));
                    break;
                case DungeonPrimitive.Crystal:
                    Glow(root, PrimitiveType.Cube, new Vector3(0, 0.5f, 0), new Vector3(0.28f, 1f, 0.28f), lightColor, 2.5f, new Vector3(0, 45f, 12f));
                    Glow(root, PrimitiveType.Cube, new Vector3(0.22f, 0.3f, 0.05f), new Vector3(0.18f, 0.6f, 0.18f), lightColor, 2.5f, new Vector3(0, 30f, -25f));
                    Glow(root, PrimitiveType.Cube, new Vector3(-0.18f, 0.25f, -0.08f), new Vector3(0.15f, 0.5f, 0.15f), lightColor, 2.5f, new Vector3(10f, 60f, 20f));
                    Light(root, new Vector3(0, 0.8f, 0.2f), lightColor, lightRange, 1.6f, false);
                    Collider(root, new Vector3(0, 0.5f, 0), new Vector3(0.6f, 1f, 0.6f));
                    break;
                case DungeonPrimitive.Altar:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.45f, 0), new Vector3(1.6f, 0.9f, 0.9f), Stone);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.95f, 0), new Vector3(1.8f, 0.12f, 1.05f), DarkStone);
                    Glow(root, PrimitiveType.Sphere, new Vector3(0, 1.2f, 0), new Vector3(0.3f, 0.3f, 0.3f), new Color(0.7f, 0.5f, 1f), 3f);
                    Light(root, new Vector3(0, 1.5f, 0), new Color(0.7f, 0.5f, 1f), 7f, 1.5f, false);
                    Collider(root, new Vector3(0, 0.5f, 0), new Vector3(1.8f, 1f, 1.05f));
                    break;
                case DungeonPrimitive.Pillar:
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 1.6f, 0), new Vector3(0.6f, 1.6f, 0.6f), Stone);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.1f, 0), new Vector3(0.8f, 0.2f, 0.8f), DarkStone);
                    Collider(root, new Vector3(0, 1.6f, 0), new Vector3(0.7f, 3.2f, 0.7f));
                    break;
                case DungeonPrimitive.Rubble: Rubble(root, variant); break;
                case DungeonPrimitive.SpikeTrap:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.03f, 0), new Vector3(1.2f, 0.06f, 1.2f), DarkStone);
                    for (int i = 0; i < 9; i++)
                        Part(root, PrimitiveType.Cube, new Vector3((i % 3 - 1) * 0.35f, 0.12f, (i / 3 - 1) * 0.35f), new Vector3(0.06f, 0.2f, 0.06f), Iron, new Vector3(0, 45f, 0));
                    var trigger = root.AddComponent<BoxCollider>();
                    trigger.isTrigger = true;
                    trigger.center = new Vector3(0, 0.5f, 0);
                    trigger.size = new Vector3(1.2f, 1f, 1.2f);
                    root.AddComponent<DungeonHazard>();
                    break;
                case DungeonPrimitive.Fountain:
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.3f, 0), new Vector3(2f, 0.3f, 2f), Stone);
                    Glow(root, PrimitiveType.Cylinder, new Vector3(0, 0.58f, 0), new Vector3(1.7f, 0.02f, 1.7f), new Color(0.25f, 0.55f, 0.9f), 1.2f);
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.9f, 0), new Vector3(0.3f, 0.6f, 0.3f), Stone);
                    Light(root, new Vector3(0, 1.4f, 0), new Color(0.4f, 0.7f, 1f), 6f, 1.2f, false);
                    Collider(root, new Vector3(0, 0.3f, 0), new Vector3(2f, 0.6f, 2f));
                    break;
                case DungeonPrimitive.Bookshelf:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 1.1f, 0), new Vector3(1.4f, 2.2f, 0.45f), DarkWood);
                    for (int i = 0; i < 4; i++)
                        Part(root, PrimitiveType.Cube, new Vector3(0, 0.35f + i * 0.5f, 0.12f), new Vector3(1.25f, 0.32f, 0.2f), new Color(0.4f + 0.1f * (i % 2), 0.15f, 0.12f + 0.08f * (i % 3)));
                    Collider(root, new Vector3(0, 1.1f, 0), new Vector3(1.4f, 2.2f, 0.45f));
                    break;
                case DungeonPrimitive.Bones:
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72f + variant * 17f;
                        Part(root, PrimitiveType.Cylinder, new Vector3(Mathf.Sin(a) * 0.3f, 0.05f, Mathf.Cos(a) * 0.3f), new Vector3(0.06f, 0.25f, 0.06f), Bone, new Vector3(90f, a, 0));
                    }
                    Part(root, PrimitiveType.Sphere, new Vector3(0.1f, 0.12f, -0.1f), new Vector3(0.24f, 0.22f, 0.26f), Bone);
                    break;
                case DungeonPrimitive.Mushroom:
                    for (int i = 0; i < 3; i++)
                    {
                        var p = new Vector3((i - 1) * 0.25f, 0f, (i % 2) * 0.2f);
                        float h = 0.25f + 0.12f * i;
                        Part(root, PrimitiveType.Cylinder, p + new Vector3(0, h * 0.5f, 0), new Vector3(0.06f, h * 0.5f, 0.06f), Bone);
                        Glow(root, PrimitiveType.Sphere, p + new Vector3(0, h, 0), new Vector3(0.28f, 0.1f, 0.28f), new Color(0.3f, 0.9f, 0.6f), 1.5f);
                    }
                    break;
                case DungeonPrimitive.PlaceholderMob:
                case DungeonPrimitive.PlaceholderBoss:
                {
                    bool boss = kind == DungeonPrimitive.PlaceholderBoss;
                    float s = boss ? 1.7f : 1f;
                    Part(root, PrimitiveType.Capsule, new Vector3(0, s, 0), new Vector3(0.8f * s, s, 0.8f * s), boss ? new Color(0.5f, 0.05f, 0.08f) : new Color(0.8f, 0.15f, 0.15f));
                    Glow(root, PrimitiveType.Sphere, new Vector3(0, 1.55f * s, 0.32f * s), new Vector3(0.35f * s, 0.12f * s, 0.12f * s), new Color(1f, 0.85f, 0.2f), 3f);
                    Collider(root, new Vector3(0, s, 0), new Vector3(0.8f * s, 2f * s, 0.8f * s));
                    break;
                }
                case DungeonPrimitive.EntrancePortal:
                case DungeonPrimitive.ExitPortal:
                    Portal(root, kind == DungeonPrimitive.EntrancePortal);
                    break;
                default:
                    if (!Special(root, kind, lightColor, lightRange, variant) && !More(root, kind, lightColor, lightRange, variant))
                    {
                        Part(root, PrimitiveType.Cube, new Vector3(0, 0.25f, 0), new Vector3(0.5f, 0.5f, 0.5f), Stone);
                        Collider(root, new Vector3(0, 0.25f, 0), new Vector3(0.5f, 0.5f, 0.5f));
                    }
                    break;
            }
            Mechanics(root, kind);
            MoreMechanics(root, kind);
            return root;
        }

        /// <summary>The components that make a stand-in work (the theme's / tables' prefabs bring their own).</summary>
        private static void Mechanics(GameObject root, DungeonPrimitive kind)
        {
            switch (kind)
            {
                case DungeonPrimitive.Chest: root.AddComponent<DungeonChest>(); break;
                case DungeonPrimitive.Altar: root.AddComponent<DungeonShrine>(); break;
                case DungeonPrimitive.Fountain:
                {
                    var rest = root.AddComponent<DungeonRestPoint>();
                    rest.uses = 0;
                    rest.cooldown = 60f;
                    break;
                }
                case DungeonPrimitive.HerbPatch: root.AddComponent<DungeonHerb>(); break;
                case DungeonPrimitive.PressurePlate: root.AddComponent<DungeonPressurePlate>(); break;
                case DungeonPrimitive.Key: root.AddComponent<DungeonKey>(); break;
            }
        }

        private static readonly Color Brass = new Color(0.8f, 0.6f, 0.25f);
        private static readonly Color Cloth = new Color(0.45f, 0.08f, 0.1f);
        private static readonly Color Leaf = new Color(0.18f, 0.4f, 0.15f);
        private static readonly Color Lava = new Color(1f, 0.38f, 0.05f);
        private static readonly Color Ice = new Color(0.65f, 0.85f, 1f);
        private static readonly Color Spore = new Color(0.55f, 0.85f, 0.25f);

        /// <summary>The special rooms' stand-ins. False for kinds it doesn't build.</summary>
        private bool Special(GameObject root, DungeonPrimitive kind, Color lightColor, float lightRange, int variant)
        {
            switch (kind)
            {
                case DungeonPrimitive.Sarcophagus:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.35f, 0), new Vector3(1f, 0.7f, 2.1f), Stone);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.76f, 0), new Vector3(1.08f, 0.14f, 2.2f), DarkStone);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.86f, -0.55f), new Vector3(0.45f, 0.08f, 0.45f), Stone);
                    Collider(root, new Vector3(0, 0.42f, 0), new Vector3(1.08f, 0.84f, 2.2f));
                    return true;
                case DungeonPrimitive.WeaponRack:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.9f, -0.1f), new Vector3(1.4f, 0.08f, 0.12f), DarkWood);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.25f, -0.1f), new Vector3(1.4f, 0.08f, 0.12f), DarkWood);
                    for (int i = 0; i < 4; i++)
                    {
                        float x = -0.5f + i * 0.33f;
                        Part(root, PrimitiveType.Cylinder, new Vector3(x, 0.8f, 0f), new Vector3(0.04f, 0.75f, 0.04f), DarkWood);
                        Part(root, PrimitiveType.Cube, new Vector3(x, 1.55f, 0f), new Vector3(0.12f, 0.22f, 0.03f), Iron);
                    }
                    Collider(root, new Vector3(0, 0.85f, -0.05f), new Vector3(1.4f, 1.7f, 0.3f));
                    return true;
                case DungeonPrimitive.ArmorStand:
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.05f, 0), new Vector3(0.5f, 0.05f, 0.5f), DarkWood);
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.7f, 0), new Vector3(0.06f, 0.65f, 0.06f), DarkWood);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 1.2f, 0), new Vector3(0.55f, 0.6f, 0.3f), Iron);
                    Part(root, PrimitiveType.Sphere, new Vector3(0, 1.65f, 0), new Vector3(0.28f, 0.3f, 0.28f), Iron);
                    Collider(root, new Vector3(0, 0.9f, 0), new Vector3(0.6f, 1.8f, 0.4f));
                    return true;
                case DungeonPrimitive.Cage:
                {
                    const float s = 1.1f, h = 2f;
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.03f, 0), new Vector3(s, 0.06f, s), Iron);
                    Part(root, PrimitiveType.Cube, new Vector3(0, h, 0), new Vector3(s, 0.06f, s), Iron);
                    for (int i = 0; i < 12; i++)
                    {
                        float t = (i % 3 - 1) * 0.35f;
                        Vector3 p = (i / 3) switch
                        {
                            0 => new Vector3(t, h * 0.5f, s * 0.5f),
                            1 => new Vector3(t, h * 0.5f, -s * 0.5f),
                            2 => new Vector3(s * 0.5f, h * 0.5f, t),
                            _ => new Vector3(-s * 0.5f, h * 0.5f, t),
                        };
                        Part(root, PrimitiveType.Cylinder, p, new Vector3(0.04f, h * 0.5f, 0.04f), Iron);
                    }
                    Part(root, PrimitiveType.Sphere, new Vector3(0.1f, 0.12f, 0.05f), new Vector3(0.22f, 0.2f, 0.24f), Bone);
                    Collider(root, new Vector3(0, h * 0.5f, 0), new Vector3(s, h, s));
                    return true;
                }
                case DungeonPrimitive.AlchemyTable:
                    Table(root, 1.4f, 0.7f, DarkWood);
                    for (int i = 0; i < 4; i++)
                        Glow(root, PrimitiveType.Cylinder, new Vector3(-0.5f + i * 0.32f, 1.0f, 0.05f), new Vector3(0.1f, 0.12f, 0.1f),
                            i % 2 == 0 ? new Color(0.4f, 1f, 0.4f) : new Color(0.7f, 0.4f, 1f), 2f);
                    return true;
                case DungeonPrimitive.Cauldron:
                    Part(root, PrimitiveType.Sphere, new Vector3(0, 0.5f, 0), new Vector3(1.1f, 0.85f, 1.1f), Iron);
                    Glow(root, PrimitiveType.Cylinder, new Vector3(0, 0.82f, 0), new Vector3(0.85f, 0.02f, 0.85f), lightColor, 3f);
                    Light(root, new Vector3(0, 1.3f, 0), lightColor, lightRange > 0f ? lightRange : 6f, 1.6f, true);
                    Collider(root, new Vector3(0, 0.45f, 0), new Vector3(1.1f, 0.9f, 1.1f));
                    return true;
                case DungeonPrimitive.Throne:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.15f, 0), new Vector3(2.2f, 0.3f, 1.8f), DarkStone);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.75f, 0.05f), new Vector3(1.1f, 0.9f, 0.9f), Stone);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 2f, -0.4f), new Vector3(1.2f, 2.6f, 0.2f), Stone);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 1.1f, 0.05f), new Vector3(0.9f, 0.08f, 0.8f), Cloth);
                    Glow(root, PrimitiveType.Sphere, new Vector3(0, 3.35f, -0.4f), new Vector3(0.3f, 0.3f, 0.3f), Gold, 2f);
                    Collider(root, new Vector3(0, 1.4f, -0.1f), new Vector3(2.2f, 2.8f, 1.8f));
                    return true;
                case DungeonPrimitive.Banner:
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 3.1f, -0.05f), new Vector3(0.05f, 0.6f, 0.05f), Brass, new Vector3(0, 0, 90f));
                    Part(root, PrimitiveType.Cube, new Vector3(0, 2.1f, -0.05f), new Vector3(0.9f, 2f, 0.03f), variant % 2 == 0 ? Cloth : new Color(0.12f, 0.18f, 0.45f));
                    Part(root, PrimitiveType.Cube, new Vector3(0, 2.4f, -0.03f), new Vector3(0.35f, 0.35f, 0.02f), Gold, new Vector3(0, 0, 45f));
                    return true;
                case DungeonPrimitive.Statue:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.3f, 0), new Vector3(0.9f, 0.6f, 0.9f), DarkStone);
                    Part(root, PrimitiveType.Capsule, new Vector3(0, 1.45f, 0), new Vector3(0.6f, 0.85f, 0.5f), Stone);
                    Part(root, PrimitiveType.Sphere, new Vector3(0, 2.45f, 0), new Vector3(0.38f, 0.42f, 0.38f), Stone);
                    Collider(root, new Vector3(0, 1.3f, 0), new Vector3(0.9f, 2.6f, 0.9f));
                    return true;
                case DungeonPrimitive.Table:
                    Table(root, 1.6f, 0.9f, Wood);
                    Part(root, PrimitiveType.Cube, new Vector3(0.3f, 0.9f, 0f), new Vector3(0.35f, 0.08f, 0.28f), new Color(0.75f, 0.7f, 0.55f));
                    return true;
                case DungeonPrimitive.Candles:
                    for (int i = 0; i < 3; i++)
                    {
                        var p = new Vector3((i - 1) * 0.15f, 0f, (i % 2) * 0.12f);
                        float h = 0.18f + 0.08f * i;
                        Part(root, PrimitiveType.Cylinder, p + new Vector3(0, h * 0.5f, 0), new Vector3(0.06f, h * 0.5f, 0.06f), new Color(0.92f, 0.88f, 0.75f));
                        Glow(root, PrimitiveType.Sphere, p + new Vector3(0, h + 0.04f, 0), new Vector3(0.05f, 0.08f, 0.05f), lightColor, 4f);
                    }
                    Light(root, new Vector3(0, 0.5f, 0), lightColor, lightRange > 0f ? lightRange : 4.5f, 1.1f, true);
                    return true;
                case DungeonPrimitive.Vines:
                    for (int i = 0; i < 4; i++)
                    {
                        float x = (i - 1.5f) * 0.25f, h = 1.2f + ((variant + i) % 3) * 0.5f;
                        Part(root, PrimitiveType.Cylinder, new Vector3(x, h * 0.5f, -0.2f), new Vector3(0.07f, h * 0.5f, 0.07f), Leaf, new Vector3(0, 0, (i - 1.5f) * 6f));
                        Part(root, PrimitiveType.Sphere, new Vector3(x, h, -0.15f), new Vector3(0.3f, 0.22f, 0.2f), Leaf);
                    }
                    return true;
                case DungeonPrimitive.Pedestal:
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.5f, 0), new Vector3(0.6f, 0.5f, 0.6f), Stone);
                    Glow(root, PrimitiveType.Cube, new Vector3(0, 1.15f, 0), new Vector3(0.35f, 0.3f, 0.35f), Gold, 2.5f, new Vector3(0, 45f, 0));
                    Light(root, new Vector3(0, 1.6f, 0), Gold, 5f, 1.2f, false);
                    Collider(root, new Vector3(0, 0.6f, 0), new Vector3(0.6f, 1.2f, 0.6f));
                    return true;
                case DungeonPrimitive.HerbPatch:
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72f * Mathf.Deg2Rad;
                        Part(root, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.25f, 0.12f, Mathf.Sin(a) * 0.25f), new Vector3(0.28f, 0.2f, 0.28f), Leaf);
                    }
                    Glow(root, PrimitiveType.Sphere, new Vector3(0, 0.25f, 0), new Vector3(0.18f, 0.18f, 0.18f), new Color(0.9f, 0.3f, 0.4f), 2f);
                    Collider(root, new Vector3(0, 0.2f, 0), new Vector3(0.8f, 0.4f, 0.8f));
                    return true;
                case DungeonPrimitive.Cobweb:
                    for (int i = 0; i < 3; i++)
                        Part(root, PrimitiveType.Quad, new Vector3(0, 0, -0.1f * i), new Vector3(0.9f - i * 0.2f, 0.9f - i * 0.2f, 1f), new Color(0.85f, 0.85f, 0.85f), new Vector3(0, 45f + i * 25f, 45f));
                    return true;
                case DungeonPrimitive.IceSpikes:
                    for (int i = 0; i < 4; i++)
                        Glow(root, PrimitiveType.Cube, new Vector3((i - 1.5f) * 0.22f, 0.4f + (i % 2) * 0.15f, (i % 2) * 0.12f),
                            new Vector3(0.16f, 0.8f + (i % 2) * 0.3f, 0.16f), Ice, 1.2f, new Vector3((i - 1.5f) * 10f, 45f, (i % 2) * 15f));
                    Light(root, new Vector3(0, 0.9f, 0.2f), lightColor, lightRange > 0f ? lightRange : 5f, 1.1f, false);
                    Collider(root, new Vector3(0, 0.5f, 0), new Vector3(0.9f, 1f, 0.5f));
                    return true;
                case DungeonPrimitive.LavaPool:
                {
                    float r = 1.1f + (variant % 3) * 0.3f;
                    Glow(root, PrimitiveType.Cylinder, new Vector3(0, 0.03f, 0), new Vector3(r * 2f, 0.03f, r * 1.6f), Lava, 5f);
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.02f, 0), new Vector3(r * 2.3f, 0.02f, r * 1.9f), DarkStone);
                    Light(root, new Vector3(0, 0.8f, 0), lightColor, lightRange > 0f ? lightRange : 7f, 1.8f, true);
                    Hazard(root, new Vector3(0, 0.5f, 0), new Vector3(r * 1.9f, 1f, r * 1.5f), DungeonHazard.Timing.Constant, ElementType.Fire, DamageType.Magical, 7f, 0.5f, variant);
                    return true;
                }
                case DungeonPrimitive.SporeVent:
                {
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.08f, 0), new Vector3(0.7f, 0.08f, 0.7f), new Color(0.3f, 0.25f, 0.15f));
                    var cloud = new GameObject("Active");
                    cloud.transform.SetParent(root.transform, false);
                    Glow(cloud, PrimitiveType.Sphere, new Vector3(0, 0.9f, 0), new Vector3(2.2f, 1.6f, 2.2f), Spore, 0.8f);
                    Hazard(root, new Vector3(0, 0.9f, 0), new Vector3(2.2f, 1.8f, 2.2f), DungeonHazard.Timing.Cycle, ElementType.Poison, DamageType.Magical, 6f, 0.6f, variant);
                    return true;
                }
                case DungeonPrimitive.FireTrap:
                {
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.03f, 0), new Vector3(1.2f, 0.06f, 1.2f), DarkStone);
                    for (int i = 0; i < 4; i++)
                        Part(root, PrimitiveType.Cylinder, new Vector3((i % 2 - 0.5f) * 0.5f, 0.06f, (i / 2 - 0.5f) * 0.5f), new Vector3(0.14f, 0.03f, 0.14f), Iron);
                    var flame = new GameObject("Active");
                    flame.transform.SetParent(root.transform, false);
                    Glow(flame, PrimitiveType.Cylinder, new Vector3(0, 1.1f, 0), new Vector3(1f, 1.1f, 1f), new Color(1f, 0.5f, 0.1f), 4f);
                    Light(flame, new Vector3(0, 1.2f, 0), new Color(1f, 0.55f, 0.2f), 6f, 2.5f, true);
                    Hazard(root, new Vector3(0, 1.1f, 0), new Vector3(1.2f, 2.2f, 1.2f), DungeonHazard.Timing.Cycle, ElementType.Fire, DamageType.Magical, 14f, 0.5f, variant);
                    return true;
                }
                case DungeonPrimitive.BladeTrap:
                {
                    float top = Mathf.Max(3.2f, lightRange);   // the builder passes the ceiling height in lightRange
                    Part(root, PrimitiveType.Cube, new Vector3(0, top - 0.1f, 0), new Vector3(0.5f, 0.2f, 0.5f), Iron);
                    var pivot = new GameObject("Pivot");
                    pivot.transform.SetParent(root.transform, false);
                    pivot.transform.localPosition = new Vector3(0, top - 0.2f, 0);
                    float arm = top - 1.2f;
                    Part(pivot, PrimitiveType.Cylinder, new Vector3(0, -arm * 0.5f, 0), new Vector3(0.06f, arm * 0.5f, 0.06f), Iron);
                    var blade = new GameObject("Blade");
                    blade.transform.SetParent(pivot.transform, false);
                    blade.transform.localPosition = new Vector3(0, -arm, 0);
                    Part(blade, PrimitiveType.Cube, Vector3.zero, new Vector3(1.6f, 0.35f, 0.06f), new Color(0.75f, 0.75f, 0.78f));
                    var trap = root.AddComponent<DungeonBladeTrap>();
                    trap.phaseOffset = (variant % 7) * 0.37f;
                    return true;
                }
                case DungeonPrimitive.DartTrap:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 1.2f, -0.12f), new Vector3(0.6f, 0.6f, 0.12f), DarkStone);
                    for (int i = 0; i < 3; i++)
                        Part(root, PrimitiveType.Cylinder, new Vector3((i - 1) * 0.15f, 1.2f, -0.05f), new Vector3(0.06f, 0.03f, 0.06f), Iron, new Vector3(90f, 0, 0));
                    root.AddComponent<DungeonDartTrap>();
                    return true;
                case DungeonPrimitive.PressurePlate:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.04f, 0), new Vector3(1.1f, 0.08f, 1.1f), Stone);
                    Glow(root, PrimitiveType.Cylinder, new Vector3(0, 0.085f, 0), new Vector3(0.6f, 0.01f, 0.6f), new Color(0.4f, 0.6f, 1f), 1.5f);
                    Light(root, new Vector3(0, 0.6f, 0), new Color(0.45f, 0.7f, 1f), 4f, 3f, false);
                    return true;
                case DungeonPrimitive.Key:
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.55f, 0), new Vector3(0.05f, 0.22f, 0.05f), Gold);
                    Part(root, PrimitiveType.Sphere, new Vector3(0, 0.82f, 0), new Vector3(0.18f, 0.18f, 0.05f), Gold);
                    Part(root, PrimitiveType.Cube, new Vector3(0.06f, 0.38f, 0), new Vector3(0.1f, 0.05f, 0.04f), Gold);
                    Light(root, new Vector3(0, 0.7f, 0), Gold, 4f, 2f, false);
                    return true;
                default:
                    return false;
            }
        }

        private void Table(GameObject root, float w, float d, Color color)
        {
            Part(root, PrimitiveType.Cube, new Vector3(0, 0.82f, 0), new Vector3(w, 0.08f, d), color);
            for (int i = 0; i < 4; i++)
                Part(root, PrimitiveType.Cube, new Vector3((i % 2 - 0.5f) * (w - 0.15f), 0.4f, (i / 2 - 0.5f) * (d - 0.15f)), new Vector3(0.08f, 0.8f, 0.08f), color);
            Collider(root, new Vector3(0, 0.45f, 0), new Vector3(w, 0.9f, d));
        }

        private static void Hazard(GameObject root, Vector3 center, Vector3 size, DungeonHazard.Timing timing, ElementType element, DamageType type,
            float damage, float interval, int variant)
        {
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = center;
            trigger.size = size;
            var hazard = root.AddComponent<DungeonHazard>();
            hazard.timing = timing;
            hazard.element = element;
            hazard.damageType = type;
            hazard.damage = damage;
            hazard.interval = interval;
            hazard.spikeRise = 0f;
            hazard.phaseOffset = (variant % 11) * 0.29f;
        }

        /// <summary>
        /// A gate (iron bars), a locked vault door (studded), a shifting wall (stone, kind Debris) or a shortcut door
        /// (wooden), <paramref name="width"/> across
        /// (along X) and <paramref name="height"/> tall, with a blocking collider. The builder adds its component.
        /// </summary>
        public GameObject Barrier(DungeonPrimitive kind, string name, float width, float height)
        {
            var root = new GameObject(name);
            switch (kind)
            {
                case DungeonPrimitive.Gate:
                {
                    int bars = Mathf.Max(3, Mathf.RoundToInt(width / 0.22f));
                    for (int i = 0; i < bars; i++)
                        Part(root, PrimitiveType.Cylinder, new Vector3(-width * 0.5f + (i + 0.5f) * width / bars, height * 0.5f, 0), new Vector3(0.06f, height * 0.5f, 0.06f), Iron);
                    Part(root, PrimitiveType.Cube, new Vector3(0, height * 0.35f, 0), new Vector3(width, 0.08f, 0.1f), Iron);
                    Part(root, PrimitiveType.Cube, new Vector3(0, height - 0.05f, 0), new Vector3(width, 0.1f, 0.12f), Iron);
                    break;
                }
                case DungeonPrimitive.LockedDoor:
                    Part(root, PrimitiveType.Cube, new Vector3(0, height * 0.5f, 0), new Vector3(width, height, 0.25f), new Color(0.3f, 0.2f, 0.12f));
                    for (int i = 0; i < 3; i++)
                        Part(root, PrimitiveType.Cube, new Vector3(0, height * (0.2f + i * 0.3f), 0), new Vector3(width + 0.02f, 0.1f, 0.28f), Iron);
                    Part(root, PrimitiveType.Cube, new Vector3(0, height * 0.45f, 0.14f), new Vector3(0.25f, 0.32f, 0.06f), Gold);
                    break;
                case DungeonPrimitive.Debris:
                    // A shifting wall: a slab of fitted stone blocks.
                    Part(root, PrimitiveType.Cube, new Vector3(0, height * 0.5f, 0), new Vector3(width, height, 0.5f), Stone);
                    for (int i = 0; i < Mathf.CeilToInt(height / 0.6f); i++)
                        Part(root, PrimitiveType.Cube, new Vector3((i % 2) * 0.3f - 0.15f, 0.3f + i * 0.6f, 0), new Vector3(width - 0.3f, 0.05f, 0.52f), DarkStone);
                    break;
                default:
                    Part(root, PrimitiveType.Cube, new Vector3(0, height * 0.5f, 0), new Vector3(width, height, 0.2f), Wood);
                    Part(root, PrimitiveType.Cube, new Vector3(0, height * 0.5f, 0.11f), new Vector3(0.18f, 0.6f, 0.04f), Iron);
                    break;
            }
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0, height * 0.5f, 0);
            box.size = new Vector3(width, height, kind == DungeonPrimitive.Debris ? 0.5f : 0.4f);
            return root;
        }

        private void Chest(GameObject root, int variant)
        {
            bool ornate = (variant & 1) == 1;
            Part(root, PrimitiveType.Cube, new Vector3(0, 0.28f, 0), new Vector3(0.95f, 0.56f, 0.6f), ornate ? new Color(0.45f, 0.1f, 0.1f) : Wood);
            // The lid hinges on its back edge (DungeonChest opens it).
            var lid = new GameObject("Lid");
            lid.transform.SetParent(root.transform, false);
            lid.transform.localPosition = new Vector3(0, 0.56f, -0.3f);
            Part(lid, PrimitiveType.Cube, new Vector3(0, 0.04f, 0.31f), new Vector3(0.97f, 0.12f, 0.62f), ornate ? Gold : DarkWood);
            Part(lid, PrimitiveType.Cube, new Vector3(0, -0.11f, 0.61f), new Vector3(0.12f, 0.16f, 0.04f), Gold);
            Collider(root, new Vector3(0, 0.33f, 0), new Vector3(0.97f, 0.66f, 0.62f));
        }

        private void Rubble(GameObject root, int variant)
        {
            var rng = new DungeonRandom((ulong)(variant * 2654435761L + 12345));
            for (int i = 0; i < 6; i++)
            {
                float s = rng.Range(0.15f, 0.45f);
                var p = new Vector3(rng.Range(-0.5f, 0.5f), s * 0.35f, rng.Range(-0.5f, 0.5f));
                Part(root, rng.Chance(0.5f) ? PrimitiveType.Cube : PrimitiveType.Sphere, p, new Vector3(s, s * 0.7f, s * 1.2f), rng.Chance(0.5f) ? Stone : DarkStone,
                    new Vector3(rng.Range(0f, 40f), rng.Range(0f, 360f), rng.Range(0f, 40f)));
            }
        }

        private void Portal(GameObject root, bool entrance)
        {
            Color color = entrance ? (theme != null ? theme.portalColor : new Color(0.45f, 0.35f, 1f)) : (theme != null ? theme.exitPortalColor : new Color(0.3f, 1f, 0.55f));
            const int segments = 16;
            const float radius = 1.3f;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var p = new Vector3(Mathf.Cos(a) * radius, 1.45f + Mathf.Sin(a) * radius, 0f);
                Part(root, PrimitiveType.Cube, p, new Vector3(0.28f, 0.55f, 0.28f), DarkStone, new Vector3(0, 0, a * Mathf.Rad2Deg));
            }
            Glow(root, PrimitiveType.Cylinder, new Vector3(0, 1.45f, 0), new Vector3(2.3f, 0.02f, 2.3f), color, 3f, new Vector3(90f, 0, 0));
            Part(root, PrimitiveType.Cube, new Vector3(0, 0.08f, 0), new Vector3(3.2f, 0.16f, 1f), DarkStone);
            Light(root, new Vector3(0, 1.5f, 0.6f), color, 9f, 2.5f, false);
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0, 1.4f, 0);
            trigger.size = new Vector3(2.2f, 2.6f, 0.8f);
        }

        // ------------------------------------------------------------------ helpers

        private GameObject Part(GameObject root, PrimitiveType type, Vector3 position, Vector3 scale, Color color, Vector3 euler = default)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = type.ToString();
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = materials.Flat(color);
            return go;
        }

        private void Glow(GameObject root, PrimitiveType type, Vector3 position, Vector3 scale, Color color, float intensity, Vector3 euler = default)
        {
            GameObject go = Part(root, type, position, scale, color, euler);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = materials.Flat(color, intensity);
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void Collider(GameObject root, Vector3 center, Vector3 size)
        {
            var box = root.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        private static void Light(GameObject root, Vector3 position, Color color, float range, float intensity, bool flicker)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            if (flicker)
                go.AddComponent<DungeonFlicker>();
        }
    }
}
