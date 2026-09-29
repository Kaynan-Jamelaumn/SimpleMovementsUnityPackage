using UnityEngine;
using UnityEngine.Rendering;

namespace ProceduralDungeon
{
    /// <summary>
    /// Builds the stand-in objects used when a table entry or theme slot has no prefab: chests, barrels, crates,
    /// torches, braziers, crystals, altars, pillars, rubble, spike traps, fountains, bookshelves, bones, mushrooms,
    /// placeholder mobs and the two portals - from Unity primitives, with one collider each and lights where they glow.
    /// Every object's pivot is at its base, facing +z. Main thread only.
    /// </summary>
    public sealed class DungeonPrimitives
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
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.25f, 0), new Vector3(0.5f, 0.5f, 0.5f), Stone);
                    Collider(root, new Vector3(0, 0.25f, 0), new Vector3(0.5f, 0.5f, 0.5f));
                    break;
            }
            return root;
        }

        private void Chest(GameObject root, int variant)
        {
            bool ornate = (variant & 1) == 1;
            Part(root, PrimitiveType.Cube, new Vector3(0, 0.28f, 0), new Vector3(0.95f, 0.56f, 0.6f), ornate ? new Color(0.45f, 0.1f, 0.1f) : Wood);
            Part(root, PrimitiveType.Cube, new Vector3(0, 0.6f, 0), new Vector3(0.97f, 0.12f, 0.62f), ornate ? Gold : DarkWood);
            Part(root, PrimitiveType.Cube, new Vector3(0, 0.45f, 0.31f), new Vector3(0.12f, 0.16f, 0.04f), Gold);
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
