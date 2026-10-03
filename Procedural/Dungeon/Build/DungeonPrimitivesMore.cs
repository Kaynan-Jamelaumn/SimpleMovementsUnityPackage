using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// The stand-ins of the newer rooms and floor styles: undercity streets (lamp posts, market stalls, carts, wells,
    /// debris), barracks beds, the kitchen (long tables, benches, stoves, pots of stew), the gallery's paintings, the
    /// greenhouse (planters, rare herbs, poisonous plants), the wine cellar (racks and barrels), the map table, blood and
    /// cursed altars, nests, a pit fight's spectators, the dragon's hoard (gold piles, bones), hive egg sacs and giant
    /// fungi, giant roots, stalactites, star motes, bridge railings, and the mechanics' pieces (tripwires, swinging logs,
    /// gas vents, levers, teleport pads, moving platforms), plus the giant root of a climb shaft.
    /// </summary>
    public sealed partial class DungeonPrimitives
    {
        private static readonly Color Straw = new Color(0.78f, 0.68f, 0.4f);
        private static readonly Color Linen = new Color(0.82f, 0.78f, 0.68f);
        private static readonly Color Blood = new Color(0.55f, 0.02f, 0.04f);
        private static readonly Color Curse = new Color(0.55f, 0.2f, 0.9f);
        private static readonly Color Bark = new Color(0.3f, 0.2f, 0.12f);
        private static readonly Color Moss = new Color(0.2f, 0.35f, 0.12f);
        private static readonly Color Ooze = new Color(0.5f, 0.85f, 0.3f);
        private static readonly Color Star = new Color(0.7f, 0.8f, 1f);
        private static readonly Color Wine = new Color(0.35f, 0.04f, 0.1f);

        private static Color Pick(int variant, params Color[] colors) => colors[((variant % colors.Length) + colors.Length) % colors.Length];

        /// <summary>The newer stand-ins. False for kinds it doesn't build.</summary>
        private bool More(GameObject root, DungeonPrimitive kind, Color lightColor, float lightRange, int variant)
        {
            switch (kind)
            {
                // ---------------------------------------------------------- undercity
                case DungeonPrimitive.LampPost:
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.1f, 0), new Vector3(0.4f, 0.1f, 0.4f), DarkStone);
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 1.7f, 0), new Vector3(0.1f, 1.6f, 0.1f), Iron);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 3.35f, 0), new Vector3(0.38f, 0.06f, 0.38f), Iron);
                    Glow(root, PrimitiveType.Cube, new Vector3(0, 3.1f, 0), new Vector3(0.26f, 0.4f, 0.26f), lightColor, 4f);
                    Light(root, new Vector3(0, 3f, 0), lightColor, lightRange > 0f ? lightRange : 9f, (theme != null ? theme.torchIntensity : 2.2f) * 1.2f, true);
                    Collider(root, new Vector3(0, 1.7f, 0), new Vector3(0.3f, 3.4f, 0.3f));
                    return true;
                case DungeonPrimitive.MarketStall:
                {
                    Color cloth = Pick(variant, Cloth, new Color(0.15f, 0.3f, 0.5f), new Color(0.6f, 0.5f, 0.15f), new Color(0.2f, 0.4f, 0.2f));
                    for (int i = 0; i < 4; i++)
                        Part(root, PrimitiveType.Cylinder, new Vector3((i % 2 - 0.5f) * 1.8f, 1.15f, (i / 2 - 0.5f) * 1.1f), new Vector3(0.08f, 1.15f, 0.08f), DarkWood);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.85f, 0.3f), new Vector3(1.9f, 0.1f, 0.6f), Wood);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.45f, 0.3f), new Vector3(1.85f, 0.8f, 0.55f), DarkWood);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 2.35f, 0.1f), new Vector3(2.1f, 0.06f, 1.5f), cloth, new Vector3(-12f, 0, 0));
                    for (int i = 0; i < 3; i++)
                        Part(root, PrimitiveType.Sphere, new Vector3(-0.5f + i * 0.5f, 0.98f, 0.3f), new Vector3(0.22f, 0.16f, 0.22f), Pick(variant + i, Gold, Leaf, Cloth));
                    Collider(root, new Vector3(0, 0.5f, 0.3f), new Vector3(1.9f, 1f, 0.6f));
                    return true;
                }
                case DungeonPrimitive.Cart:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.75f, 0), new Vector3(1.2f, 0.5f, 2f), Wood);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.55f, 0), new Vector3(1.25f, 0.08f, 2.05f), DarkWood);
                    for (int i = 0; i < 2; i++)
                        Part(root, PrimitiveType.Cylinder, new Vector3((i - 0.5f) * 1.35f, 0.45f, -0.3f), new Vector3(0.9f, 0.05f, 0.9f), DarkWood, new Vector3(0, 0, 90f));
                    Part(root, PrimitiveType.Cube, new Vector3(0.35f, 0.6f, 1.6f), new Vector3(0.06f, 0.06f, 1.4f), DarkWood, new Vector3(-12f, 0, 0));
                    Part(root, PrimitiveType.Cube, new Vector3(-0.35f, 0.6f, 1.6f), new Vector3(0.06f, 0.06f, 1.4f), DarkWood, new Vector3(-12f, 0, 0));
                    Part(root, PrimitiveType.Sphere, new Vector3(0.1f, 1.05f, -0.3f), new Vector3(0.7f, 0.35f, 0.8f), Straw);
                    Collider(root, new Vector3(0, 0.6f, 0), new Vector3(1.4f, 1.2f, 2.1f));
                    return true;
                case DungeonPrimitive.Well:
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.45f, 0), new Vector3(1.6f, 0.45f, 1.6f), Stone);
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.88f, 0), new Vector3(1.25f, 0.02f, 1.25f), new Color(0.03f, 0.04f, 0.05f));
                    for (int i = 0; i < 2; i++)
                        Part(root, PrimitiveType.Cube, new Vector3((i - 0.5f) * 1.5f, 1.4f, 0), new Vector3(0.12f, 1.9f, 0.12f), DarkWood);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 2.4f, 0), new Vector3(1.9f, 0.08f, 1.1f), Wood, new Vector3(0, 0, 0));
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 1.9f, 0), new Vector3(0.1f, 0.75f, 0.1f), DarkWood, new Vector3(0, 0, 90f));
                    Part(root, PrimitiveType.Cylinder, new Vector3(0.2f, 1.45f, 0), new Vector3(0.25f, 0.15f, 0.25f), Wood);
                    Collider(root, new Vector3(0, 0.5f, 0), new Vector3(1.6f, 1f, 1.6f));
                    return true;
                case DungeonPrimitive.Debris:
                    Rubble(root, variant);
                    Part(root, PrimitiveType.Cube, new Vector3(0.1f, 0.25f, 0.1f), new Vector3(0.18f, 0.18f, 2.2f), DarkWood, new Vector3(12f, 35f + variant * 23f, 8f));
                    for (int i = 0; i < 4; i++)
                        Part(root, PrimitiveType.Cube, new Vector3((i - 1.5f) * 0.3f, 0.1f, -0.4f + (i % 2) * 0.25f), new Vector3(0.28f, 0.14f, 0.18f), Stone, new Vector3(0, i * 27f, 0));
                    Collider(root, new Vector3(0, 0.25f, 0), new Vector3(1.2f, 0.5f, 1.2f));
                    return true;

                // ---------------------------------------------------------- barracks and kitchen
                case DungeonPrimitive.Bed:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.22f, 0), new Vector3(0.95f, 0.3f, 2.05f), DarkWood);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.42f, 0.05f), new Vector3(0.85f, 0.12f, 1.9f), Straw);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.5f, -0.75f), new Vector3(0.6f, 0.1f, 0.35f), Linen);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.49f, 0.35f), new Vector3(0.88f, 0.06f, 1.15f), Pick(variant, Cloth, new Color(0.25f, 0.3f, 0.2f), new Color(0.3f, 0.25f, 0.4f)));
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.5f, -1.02f), new Vector3(0.95f, 0.65f, 0.08f), DarkWood);
                    Collider(root, new Vector3(0, 0.3f, 0), new Vector3(0.95f, 0.6f, 2.05f));
                    return true;
                case DungeonPrimitive.LongTable:
                    Table(root, 3.2f, 1f, Wood);
                    for (int i = 0; i < 5; i++)
                    {
                        float x = -1.2f + i * 0.6f;
                        Part(root, PrimitiveType.Cylinder, new Vector3(x, 0.875f, 0.25f * (i % 2 == 0 ? 1 : -1)), new Vector3(0.28f, 0.01f, 0.28f), Linen);
                        Part(root, PrimitiveType.Cylinder, new Vector3(x + 0.18f, 0.94f, 0.3f * (i % 2 == 0 ? -1 : 1)), new Vector3(0.09f, 0.07f, 0.09f), Pick(i, Wood, Iron));
                    }
                    Part(root, PrimitiveType.Sphere, new Vector3(0.1f, 0.95f, 0), new Vector3(0.4f, 0.18f, 0.3f), new Color(0.6f, 0.35f, 0.15f));
                    return true;
                case DungeonPrimitive.Bench:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.45f, 0), new Vector3(2.4f, 0.08f, 0.38f), Wood);
                    for (int i = 0; i < 2; i++)
                        Part(root, PrimitiveType.Cube, new Vector3((i - 0.5f) * 2f, 0.21f, 0), new Vector3(0.08f, 0.42f, 0.32f), DarkWood);
                    Collider(root, new Vector3(0, 0.25f, 0), new Vector3(2.4f, 0.5f, 0.38f));
                    return true;
                case DungeonPrimitive.Stove:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.6f, -0.1f), new Vector3(1.6f, 1.2f, 1f), DarkStone);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 1.23f, -0.1f), new Vector3(1.7f, 0.08f, 1.08f), Iron);
                    Glow(root, PrimitiveType.Cube, new Vector3(0, 0.45f, 0.38f), new Vector3(0.7f, 0.45f, 0.06f), Lava, 5f);
                    Part(root, PrimitiveType.Cube, new Vector3(0.45f, 2.2f, -0.35f), new Vector3(0.4f, 1.9f, 0.4f), DarkStone);
                    Part(root, PrimitiveType.Cylinder, new Vector3(-0.35f, 1.4f, -0.1f), new Vector3(0.45f, 0.15f, 0.45f), Iron);
                    Light(root, new Vector3(0, 0.6f, 0.8f), lightColor, lightRange > 0f ? lightRange : 6f, 1.6f, true);
                    Collider(root, new Vector3(0, 0.65f, -0.1f), new Vector3(1.6f, 1.3f, 1f));
                    return true;
                case DungeonPrimitive.Pots:
                    for (int i = 0; i < 3; i++)
                    {
                        var p = new Vector3((i - 1) * 0.42f, 0f, (i % 2) * 0.25f);
                        float r = 0.32f + 0.08f * (i % 2);
                        Part(root, PrimitiveType.Sphere, p + new Vector3(0, r * 0.8f, 0), new Vector3(r * 1.3f, r * 1.4f, r * 1.3f), Pick(i + variant, Iron, new Color(0.55f, 0.32f, 0.2f)));
                        Part(root, PrimitiveType.Cylinder, p + new Vector3(0, r * 1.45f, 0), new Vector3(r * 0.95f, 0.01f, r * 0.95f), new Color(0.55f, 0.35f, 0.12f));
                    }
                    Collider(root, new Vector3(0, 0.3f, 0.1f), new Vector3(1.3f, 0.6f, 0.7f));
                    return true;

                // ---------------------------------------------------------- gallery and greenhouse
                case DungeonPrimitive.Painting:
                {
                    bool magic = variant < 0;
                    float w = magic ? 1.6f : 1f + (variant % 3) * 0.3f, h = magic ? 2f : 0.8f + (variant % 2) * 0.4f;
                    float y = magic ? 1.3f : 1.9f;
                    Part(root, PrimitiveType.Cube, new Vector3(0, y, -0.04f), new Vector3(w + 0.16f, h + 0.16f, 0.06f), Gold);
                    if (magic)
                    {
                        Color swirl = theme != null ? theme.portalColor : new Color(0.45f, 0.35f, 1f);
                        Glow(root, PrimitiveType.Cube, new Vector3(0, y, 0f), new Vector3(w, h, 0.03f), swirl, 2.5f);
                        Glow(root, PrimitiveType.Sphere, new Vector3(0, y, 0.02f), new Vector3(w * 0.6f, h * 0.5f, 0.02f), Color.white, 3f);
                        Light(root, new Vector3(0, y, 0.6f), swirl, 6f, 1.6f, false);
                    }
                    else
                    {
                        Part(root, PrimitiveType.Cube, new Vector3(0, y, 0f), new Vector3(w, h, 0.03f), Pick(variant, new Color(0.25f, 0.35f, 0.5f), new Color(0.45f, 0.3f, 0.2f), new Color(0.3f, 0.4f, 0.25f), new Color(0.5f, 0.42f, 0.3f)));
                        Part(root, PrimitiveType.Sphere, new Vector3(-w * 0.15f, y + h * 0.1f, 0.02f), new Vector3(w * 0.35f, h * 0.45f, 0.01f), Pick(variant + 1, Linen, Gold, Cloth));
                    }
                    return true;
                }
                case DungeonPrimitive.Planter:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.3f, 0), new Vector3(1.6f, 0.6f, 0.7f), Stone);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.58f, 0), new Vector3(1.5f, 0.06f, 0.6f), new Color(0.25f, 0.17f, 0.1f));
                    for (int i = 0; i < 4; i++)
                        Part(root, PrimitiveType.Sphere, new Vector3(-0.55f + i * 0.37f, 0.8f, (i % 2) * 0.1f - 0.05f), new Vector3(0.38f, 0.42f + (i % 2) * 0.15f, 0.38f), Pick(i + variant, Leaf, Moss));
                    Collider(root, new Vector3(0, 0.4f, 0), new Vector3(1.6f, 0.8f, 0.7f));
                    return true;
                case DungeonPrimitive.RareHerb:
                {
                    Color bloom = Pick(variant, new Color(0.4f, 0.75f, 1f), new Color(1f, 0.85f, 0.3f), new Color(0.95f, 0.5f, 1f));
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * 90f * Mathf.Deg2Rad;
                        Part(root, PrimitiveType.Cylinder, new Vector3(Mathf.Cos(a) * 0.15f, 0.25f, Mathf.Sin(a) * 0.15f), new Vector3(0.03f, 0.25f, 0.03f), Leaf);
                        Glow(root, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.15f, 0.52f, Mathf.Sin(a) * 0.15f), new Vector3(0.14f, 0.1f, 0.14f), bloom, 3f);
                    }
                    Part(root, PrimitiveType.Sphere, new Vector3(0, 0.08f, 0), new Vector3(0.6f, 0.16f, 0.6f), Leaf);
                    Light(root, new Vector3(0, 0.7f, 0), bloom, 3.5f, 1f, false);
                    Collider(root, new Vector3(0, 0.25f, 0), new Vector3(0.6f, 0.5f, 0.6f));
                    return true;
                }
                case DungeonPrimitive.PoisonPlant:
                    Part(root, PrimitiveType.Sphere, new Vector3(0, 0.35f, 0), new Vector3(0.6f, 0.7f, 0.6f), new Color(0.35f, 0.12f, 0.4f));
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60f;
                        Part(root, PrimitiveType.Cube, new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 0.3f, 0.45f, Mathf.Cos(a * Mathf.Deg2Rad) * 0.3f), new Vector3(0.05f, 0.4f, 0.05f), new Color(0.6f, 0.75f, 0.2f), new Vector3(35f, a, 0));
                    }
                    Glow(root, PrimitiveType.Sphere, new Vector3(0, 0.75f, 0), new Vector3(0.22f, 0.22f, 0.22f), Ooze, 2.5f);
                    Hazard(root, new Vector3(0, 0.6f, 0), new Vector3(1.3f, 1.2f, 1.3f), DungeonHazard.Timing.Constant, ElementType.Poison, DamageType.Magical, 4f, 0.8f, variant);
                    return true;

                // ---------------------------------------------------------- wine cellar and map room
                case DungeonPrimitive.WineRack:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 1f, -0.05f), new Vector3(1.6f, 2f, 0.5f), DarkWood);
                    for (int r = 0; r < 4; r++)
                        for (int c = 0; c < 5; c++)
                            Part(root, PrimitiveType.Cylinder, new Vector3(-0.6f + c * 0.3f, 0.35f + r * 0.45f, 0.18f), new Vector3(0.1f, 0.12f, 0.1f), (r + c + variant) % 3 == 0 ? Wine : new Color(0.12f, 0.25f, 0.12f), new Vector3(90f, 0, 0));
                    Collider(root, new Vector3(0, 1f, 0), new Vector3(1.6f, 2f, 0.55f));
                    return true;
                case DungeonPrimitive.WineBarrel:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.12f, 0), new Vector3(0.9f, 0.24f, 1.2f), DarkWood);
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.62f, 0), new Vector3(0.95f, 0.6f, 0.95f), Wood, new Vector3(90f, 0, 0));
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.62f, 0.32f), new Vector3(0.98f, 0.04f, 0.98f), Iron, new Vector3(90f, 0, 0));
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.62f, -0.32f), new Vector3(0.98f, 0.04f, 0.98f), Iron, new Vector3(90f, 0, 0));
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.45f, 0.62f), new Vector3(0.06f, 0.06f, 0.06f), Brass, new Vector3(90f, 0, 0));
                    Collider(root, new Vector3(0, 0.6f, 0), new Vector3(0.95f, 1.15f, 1.25f));
                    return true;
                case DungeonPrimitive.MapTable:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.45f, 0), new Vector3(1.4f, 0.9f, 1f), Stone);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.95f, 0), new Vector3(2.2f, 0.12f, 1.6f), DarkStone);
                    for (int i = 0; i < 5; i++)
                        Glow(root, PrimitiveType.Cube, new Vector3(-0.8f + i * 0.4f, 1.015f, (i % 2) * 0.3f - 0.15f), new Vector3(0.3f, 0.01f, 0.05f + (i % 3) * 0.2f), new Color(0.6f, 0.85f, 1f), 1.5f, new Vector3(0, i * 37f, 0));
                    Part(root, PrimitiveType.Cylinder, new Vector3(0.7f, 1.05f, 0.5f), new Vector3(0.12f, 0.05f, 0.12f), Brass);
                    Light(root, new Vector3(0, 1.8f, 0), new Color(0.6f, 0.85f, 1f), 5f, 1.2f, false);
                    Collider(root, new Vector3(0, 0.5f, 0), new Vector3(2.2f, 1f, 1.6f));
                    return true;

                // ---------------------------------------------------------- altars, nests, the arena
                case DungeonPrimitive.BloodAltar:
                case DungeonPrimitive.CursedAltar:
                {
                    bool blood = kind == DungeonPrimitive.BloodAltar;
                    Color glow = blood ? Blood : Curse;
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.45f, 0), new Vector3(1.5f, 0.9f, 0.9f), blood ? new Color(0.3f, 0.12f, 0.1f) : new Color(0.12f, 0.1f, 0.14f));
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.95f, 0), new Vector3(1.7f, 0.1f, 1.05f), DarkStone);
                    if (blood)
                        Glow(root, PrimitiveType.Cylinder, new Vector3(0, 1.02f, 0), new Vector3(0.8f, 0.03f, 0.6f), glow, 2f);
                    else
                    {
                        Part(root, PrimitiveType.Sphere, new Vector3(0, 1.18f, -0.15f), new Vector3(0.32f, 0.3f, 0.34f), Bone);
                        Glow(root, PrimitiveType.Sphere, new Vector3(0, 1.6f, 0), new Vector3(0.3f, 0.3f, 0.3f), glow, 3f);
                    }
                    for (int i = 0; i < 2; i++)
                        Glow(root, PrimitiveType.Cylinder, new Vector3((i - 0.5f) * 1.4f, 1.15f, 0.35f), new Vector3(0.08f, 0.12f, 0.08f), glow, 2.5f);
                    Light(root, new Vector3(0, 1.6f, 0.4f), glow, 6f, 1.5f, true);
                    Collider(root, new Vector3(0, 0.5f, 0), new Vector3(1.7f, 1f, 1.05f));
                    return true;
                }
                case DungeonPrimitive.Nest:
                    Part(root, PrimitiveType.Sphere, new Vector3(0, 0.25f, 0), new Vector3(2f, 0.6f, 2f), new Color(0.3f, 0.24f, 0.15f));
                    Part(root, PrimitiveType.Sphere, new Vector3(0, 0.4f, 0), new Vector3(1.5f, 0.4f, 1.5f), Straw);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = (i * 72f + variant * 11f) * Mathf.Deg2Rad;
                        Glow(root, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.35f, 0.65f, Mathf.Sin(a) * 0.35f), new Vector3(0.3f, 0.4f, 0.3f), Ooze, 1.2f);
                    }
                    Light(root, new Vector3(0, 1.2f, 0), Ooze, 5f, 1f, false);
                    Collider(root, new Vector3(0, 0.4f, 0), new Vector3(2f, 0.8f, 2f));
                    return true;
                case DungeonPrimitive.EggSac:
                    for (int i = 0; i < 4; i++)
                    {
                        float a = (i * 90f + variant * 31f) * Mathf.Deg2Rad;
                        float r = 0.25f + 0.08f * (i % 2);
                        Glow(root, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.25f, r, Mathf.Sin(a) * 0.25f), new Vector3(r * 1.6f, r * 2f, r * 1.6f), Ooze, 0.9f);
                    }
                    Part(root, PrimitiveType.Sphere, new Vector3(0, 0.05f, 0), new Vector3(1.1f, 0.12f, 1.1f), new Color(0.25f, 0.3f, 0.12f));
                    Collider(root, new Vector3(0, 0.35f, 0), new Vector3(0.9f, 0.7f, 0.9f));
                    return true;
                case DungeonPrimitive.Fungus:
                {
                    Color cap = Pick(variant, new Color(0.7f, 0.3f, 0.6f), new Color(0.3f, 0.7f, 0.8f), new Color(0.85f, 0.6f, 0.2f));
                    float h = 1.4f + (variant % 3) * 0.6f;
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, h * 0.5f, 0), new Vector3(0.25f, h * 0.5f, 0.25f), Linen);
                    Glow(root, PrimitiveType.Sphere, new Vector3(0, h, 0), new Vector3(1.4f, 0.45f, 1.4f), cap, 1.5f);
                    Glow(root, PrimitiveType.Sphere, new Vector3(0.45f, h * 0.45f, 0.2f), new Vector3(0.5f, 0.2f, 0.5f), cap, 1.5f);
                    Light(root, new Vector3(0, h - 0.3f, 0), cap, lightRange > 0f ? lightRange : 5f, 1f, false);
                    Collider(root, new Vector3(0, h * 0.5f, 0), new Vector3(0.3f, h, 0.3f));
                    return true;
                }
                case DungeonPrimitive.Spectators:
                {
                    // Two tiers of stands with a row of figures on each (DungeonSpectators makes them cheer).
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.3f, -0.2f), new Vector3(3f, 0.6f, 0.8f), DarkStone);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.75f, -0.75f), new Vector3(3f, 1.5f, 0.7f), DarkStone);
                    for (int i = 0; i < 8; i++)
                    {
                        bool back = i >= 4;
                        var fig = new GameObject("Spectator");
                        fig.transform.SetParent(root.transform, false);
                        fig.transform.localPosition = new Vector3(-1.1f + (i % 4) * 0.72f, back ? 1.5f : 0.6f, back ? -0.75f : -0.2f);
                        Color shirt = Pick(i + variant, Cloth, new Color(0.2f, 0.3f, 0.55f), new Color(0.5f, 0.45f, 0.2f), new Color(0.3f, 0.45f, 0.25f), Linen);
                        Part(fig, PrimitiveType.Capsule, new Vector3(0, 0.45f, 0), new Vector3(0.4f, 0.45f, 0.35f), shirt);
                        Part(fig, PrimitiveType.Sphere, new Vector3(0, 1.0f, 0), new Vector3(0.26f, 0.28f, 0.26f), new Color(0.8f, 0.62f, 0.48f));
                    }
                    Collider(root, new Vector3(0, 0.75f, -0.45f), new Vector3(3f, 1.5f, 1.3f));
                    return true;
                }

                // ---------------------------------------------------------- the den, roots and the void
                case DungeonPrimitive.GoldPile:
                {
                    float s = 0.8f + (variant % 3) * 0.35f;
                    Glow(root, PrimitiveType.Sphere, new Vector3(0, 0.1f * s, 0), new Vector3(1.6f * s, 0.5f * s, 1.4f * s), Gold, 0.6f);
                    for (int i = 0; i < 7; i++)
                    {
                        float a = (i * 51f + variant * 13f) * Mathf.Deg2Rad;
                        Part(root, PrimitiveType.Cylinder, new Vector3(Mathf.Cos(a) * 0.7f * s, 0.03f, Mathf.Sin(a) * 0.6f * s), new Vector3(0.12f, 0.01f, 0.12f), Gold, new Vector3(10f * (i % 3), 0, 15f * (i % 2)));
                    }
                    if (variant % 2 == 0)
                        Part(root, PrimitiveType.Cube, new Vector3(0.3f * s, 0.35f * s, -0.2f * s), new Vector3(0.25f, 0.35f, 0.18f), Brass, new Vector3(0, 30f, 10f));
                    Light(root, new Vector3(0, 0.8f, 0), Gold, 4f, 0.8f, false);
                    return true;
                }
                case DungeonPrimitive.DragonBones:
                    for (int i = 0; i < 6; i++)
                    {
                        float z = -1.5f + i * 0.6f;
                        Part(root, PrimitiveType.Cylinder, new Vector3(0.6f, 0.7f, z), new Vector3(0.12f, 0.8f, 0.12f), Bone, new Vector3(0, 0, -30f));
                        Part(root, PrimitiveType.Cylinder, new Vector3(-0.6f, 0.7f, z), new Vector3(0.12f, 0.8f, 0.12f), Bone, new Vector3(0, 0, 30f));
                    }
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.15f, 0), new Vector3(0.2f, 2f, 0.2f), Bone, new Vector3(90f, 0, 0));
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.35f, 2.4f), new Vector3(0.8f, 0.6f, 1.2f), Bone, new Vector3(-10f, 0, 0));
                    Collider(root, new Vector3(0, 0.6f, 0), new Vector3(1.8f, 1.2f, 3.6f));
                    return true;
                case DungeonPrimitive.GiantRoot:
                {
                    float top = Mathf.Max(3f, lightRange);   // the builder passes the ceiling height in lightRange
                    float lean = (variant % 5 - 2) * 4f;
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, top * 0.5f, 0), new Vector3(0.9f, top * 0.5f + 0.1f, 0.9f), Bark, new Vector3(lean, variant * 37f, 0));
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * 90f + variant * 17f;
                        Part(root, PrimitiveType.Cylinder, new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 0.7f, 0.25f, Mathf.Cos(a * Mathf.Deg2Rad) * 0.7f), new Vector3(0.3f, 0.6f, 0.3f), Bark, new Vector3(65f, a, 0));
                        Part(root, PrimitiveType.Cylinder, new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 0.5f, top * (0.3f + 0.15f * i), Mathf.Cos(a * Mathf.Deg2Rad) * 0.5f), new Vector3(0.06f, top * 0.25f, 0.06f), Leaf, new Vector3(4f, a, 0));
                    }
                    Collider(root, new Vector3(0, top * 0.5f, 0), new Vector3(0.9f, top, 0.9f));
                    return true;
                }
                case DungeonPrimitive.Stalactite:
                {
                    float top = Mathf.Max(3f, lightRange);
                    for (int i = 0; i < 3; i++)
                    {
                        var p = new Vector3((i - 1) * 0.45f, 0f, (i % 2) * 0.3f);
                        float len = 0.8f + ((variant + i) % 3) * 0.6f;
                        Part(root, PrimitiveType.Cylinder, p + new Vector3(0, top - len * 0.5f, 0), new Vector3(0.28f, len * 0.5f, 0.28f), DarkStone);
                        Part(root, PrimitiveType.Sphere, p + new Vector3(0, top - len, 0), new Vector3(0.18f, 0.35f, 0.18f), DarkStone);
                    }
                    return true;
                }
                case DungeonPrimitive.StarMote:
                {
                    float h = 1.6f + (variant % 4) * 0.5f;
                    Glow(root, PrimitiveType.Sphere, new Vector3(0, h, 0), new Vector3(0.22f, 0.22f, 0.22f), lightColor.maxColorComponent > 0f ? lightColor : Star, 6f);
                    Glow(root, PrimitiveType.Cube, new Vector3(0, h, 0), new Vector3(0.05f, 0.6f, 0.05f), Star, 3f, new Vector3(0, 0, 45f));
                    Glow(root, PrimitiveType.Cube, new Vector3(0, h, 0), new Vector3(0.05f, 0.6f, 0.05f), Star, 3f, new Vector3(0, 0, -45f));
                    Light(root, new Vector3(0, h, 0), lightColor.maxColorComponent > 0f ? lightColor : Star, lightRange > 0f ? lightRange : 6f, 1.3f, false);
                    return true;
                }
                case DungeonPrimitive.Railing:
                    for (int i = 0; i < 3; i++)
                        Part(root, PrimitiveType.Cylinder, new Vector3((i - 1) * 0.9f, 0.5f, 0), new Vector3(0.08f, 0.5f, 0.08f), DarkWood);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 1f, 0), new Vector3(2f, 0.08f, 0.08f), Wood);
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.55f, 0), new Vector3(2f, 0.05f, 0.05f), Wood);
                    return true;

                // ---------------------------------------------------------- mechanics
                case DungeonPrimitive.Tripwire:
                {
                    float half = Mathf.Max(0.5f, lightRange * 0.5f);   // the builder passes the corridor width in lightRange
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.3f, 0), new Vector3(0.015f, half, 0.015f), Linen, new Vector3(0, 0, 90f));
                    for (int i = 0; i < 2; i++)
                        Part(root, PrimitiveType.Cylinder, new Vector3((i - 0.5f) * 2f * half, 0.18f, 0), new Vector3(0.05f, 0.18f, 0.05f), Iron);
                    return true;
                }
                case DungeonPrimitive.LogTrap:
                {
                    float top = Mathf.Max(3.2f, lightRange);   // the builder passes the ceiling height in lightRange
                    Part(root, PrimitiveType.Cube, new Vector3(0, top - 0.1f, 0), new Vector3(1.8f, 0.2f, 0.3f), DarkWood);
                    var pivot = new GameObject("Pivot");
                    pivot.transform.SetParent(root.transform, false);
                    pivot.transform.localPosition = new Vector3(0, top - 0.2f, 0);
                    float arm = top - 1.1f;
                    for (int i = 0; i < 2; i++)
                        Part(pivot, PrimitiveType.Cylinder, new Vector3((i - 0.5f) * 1.4f, -arm * 0.5f, 0), new Vector3(0.04f, arm * 0.5f, 0.04f), Straw);
                    var blade = new GameObject("Blade");
                    blade.transform.SetParent(pivot.transform, false);
                    blade.transform.localPosition = new Vector3(0, -arm, 0);
                    Part(blade, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.7f, 0.95f, 0.7f), Bark, new Vector3(0, 0, 90f));
                    var trap = root.AddComponent<DungeonBladeTrap>();
                    trap.axis = DungeonBladeTrap.SwingAxis.ForwardBack;
                    trap.damage = 14f;
                    trap.knockback = 6f;
                    trap.amplitude = 50f;
                    trap.period = 3.2f;
                    trap.bladeSize = new Vector3(1.9f, 0.7f, 0.7f);
                    trap.phaseOffset = (variant % 5) * 0.53f;
                    return true;
                }
                case DungeonPrimitive.GasVent:
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.03f, 0), new Vector3(0.8f, 0.03f, 0.8f), Iron);
                    for (int i = 0; i < 3; i++)
                        Part(root, PrimitiveType.Cube, new Vector3(0, 0.065f, (i - 1) * 0.2f), new Vector3(0.6f, 0.02f, 0.06f), DarkStone);
                    Glow(root, PrimitiveType.Cylinder, new Vector3(0, 0.05f, 0), new Vector3(0.55f, 0.01f, 0.55f), Ooze, 1.2f);
                    return true;
                case DungeonPrimitive.Lever:
                {
                    // The plate's middle is the pivot (the population places it at hand height).
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0, -0.06f), new Vector3(0.35f, 0.5f, 0.08f), DarkStone);
                    var handle = new GameObject("Handle");
                    handle.transform.SetParent(root.transform, false);
                    handle.transform.localPosition = new Vector3(0, -0.1f, 0f);
                    handle.transform.localRotation = Quaternion.Euler(40f, 0, 0);
                    Part(handle, PrimitiveType.Cylinder, new Vector3(0, 0.22f, 0), new Vector3(0.05f, 0.22f, 0.05f), Iron);
                    Part(handle, PrimitiveType.Sphere, new Vector3(0, 0.46f, 0), new Vector3(0.1f, 0.1f, 0.1f), variant % 2 == 0 ? Cloth : Brass);
                    Collider(root, new Vector3(0, 0, 0), new Vector3(0.35f, 0.5f, 0.2f));
                    return true;
                }
                case DungeonPrimitive.Teleporter:
                {
                    Color c = theme != null ? theme.portalColor : new Color(0.45f, 0.35f, 1f);
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.05f, 0), new Vector3(1.5f, 0.05f, 1.5f), DarkStone);
                    Glow(root, PrimitiveType.Cylinder, new Vector3(0, 0.1f, 0), new Vector3(1.15f, 0.01f, 1.15f), c, 3f);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * 90f * Mathf.Deg2Rad;
                        Glow(root, PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.68f, 0.35f, Mathf.Sin(a) * 0.68f), new Vector3(0.1f, 0.5f, 0.1f), c, 2f, new Vector3(0, i * 90f, 0));
                    }
                    Light(root, new Vector3(0, 0.8f, 0), c, 5f, 1.6f, false);
                    Collider(root, new Vector3(0, 0.05f, 0), new Vector3(1.5f, 0.1f, 1.5f));
                    return true;
                }
                case DungeonPrimitive.MovingPlatform:
                {
                    // The deck's top is the pivot (level with the ledges it travels between).
                    float s = Mathf.Max(1f, lightRange) * 0.96f;   // the builder passes the cell size in lightRange
                    Part(root, PrimitiveType.Cube, new Vector3(0, -0.18f, 0), new Vector3(s, 0.36f, s), DarkStone);
                    Part(root, PrimitiveType.Cube, new Vector3(0, -0.01f, 0), new Vector3(s * 0.9f, 0.02f, s * 0.9f), Stone);
                    Glow(root, PrimitiveType.Cylinder, new Vector3(0, -0.45f, 0), new Vector3(s * 0.5f, 0.05f, s * 0.5f), theme != null ? theme.portalColor : new Color(0.45f, 0.35f, 1f), 3f);
                    Collider(root, new Vector3(0, -0.18f, 0), new Vector3(s, 0.36f, s));
                    return true;
                }
                default:
                    return false;
            }
        }

        /// <summary>The components the newer stand-ins need.</summary>
        private static void MoreMechanics(GameObject root, DungeonPrimitive kind)
        {
            switch (kind)
            {
                case DungeonPrimitive.Barrel:
                case DungeonPrimitive.Crate:
                case DungeonPrimitive.WineBarrel:
                {
                    var b = root.AddComponent<DungeonBreakable>();
                    b.holdToUse = 0f;   // a weapon or the Interact key, not standing next to it
                    b.uses = 0;
                    b.cooldown = 0.2f;
                    break;
                }
                case DungeonPrimitive.Pots:
                {
                    var food = root.AddComponent<DungeonHerb>();
                    food.heal = 0.25f;
                    food.message = "You eat some of the stew. It's still warm.";
                    break;
                }
                case DungeonPrimitive.RareHerb:
                {
                    var herb = root.AddComponent<DungeonHerb>();
                    herb.heal = 0.6f;
                    herb.message = "The rare herb's bloom restores you.";
                    break;
                }
                case DungeonPrimitive.MapTable:
                {
                    var table = root.AddComponent<DungeonMapTable>();
                    table.uses = 0;
                    table.holdToUse = 1.5f;
                    table.cooldown = 3f;
                    break;
                }
                case DungeonPrimitive.BloodAltar:
                case DungeonPrimitive.CursedAltar:
                {
                    var altar = root.AddComponent<DungeonGamblingAltar>();
                    altar.price = kind == DungeonPrimitive.BloodAltar ? DungeonGamblingAltar.Price.Blood : DungeonGamblingAltar.Price.Curse;
                    altar.holdToUse = 2f;
                    break;
                }
                case DungeonPrimitive.Spectators: root.AddComponent<DungeonSpectators>(); break;
            }
        }

        /// <summary>A giant root up the middle of a climb shaft, wound with vines (no collider: the shaft is climbed round it).</summary>
        public GameObject ClimbingRoot(float height, float width)
        {
            var root = new GameObject("Climbing Root");
            float r = Mathf.Clamp(width * 0.18f, 0.25f, 0.6f);
            int segments = Mathf.Max(1, Mathf.CeilToInt(height / 2.5f));
            for (int i = 0; i < segments; i++)
            {
                float y0 = i * height / segments, y1 = (i + 1) * height / segments;
                Part(root, PrimitiveType.Cylinder, new Vector3(Mathf.Sin(i * 1.7f) * 0.1f, (y0 + y1) * 0.5f, Mathf.Cos(i * 1.3f) * 0.1f),
                    new Vector3(r * 2f, (y1 - y0) * 0.5f + 0.05f, r * 2f), Bark, new Vector3(Mathf.Sin(i) * 4f, i * 40f, Mathf.Cos(i) * 4f));
            }
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a) * (r + 0.08f), height * 0.5f, Mathf.Sin(a) * (r + 0.08f));
                Part(root, PrimitiveType.Cylinder, p, new Vector3(0.05f, height * 0.5f, 0.05f), Leaf, new Vector3(Mathf.Sin(a) * 3f, 0, Mathf.Cos(a) * 3f));
                for (float y = 0.8f + i * 0.3f; y < height; y += 1.6f)
                    Part(root, PrimitiveType.Sphere, new Vector3(p.x * 1.2f, y, p.z * 1.2f), new Vector3(0.28f, 0.2f, 0.18f), Moss);
            }
            return root;
        }
    }
}
