using UnityEngine;
using UnityEngine.SceneManagement;

namespace HubWorld
{
    /// <summary>The temporary duel arena: a glowing ring on the ground around the two fighters (visual only).</summary>
    public class DuelArena : MonoBehaviour
    {
        private LineRenderer line;
        private float radius;

        public static DuelArena Create(Vector3 center, float radius, Scene scene)
        {
            var go = new GameObject("Duel Arena");
            if (scene.IsValid() && scene.isLoaded)
                SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = CombatQuery.SnapToGround(center + Vector3.up, 3f, 10f) + Vector3.up * 0.08f;
            var arena = go.AddComponent<DuelArena>();
            arena.radius = radius;
            arena.Build();
            return arena;
        }

        private void Build()
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 72;
            line.widthMultiplier = 0.12f;
            line.material = CombatSettings.Instance.TelegraphMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            for (int i = 0; i < line.positionCount; i++)
            {
                float a = i / (float)line.positionCount * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
        }

        private void Update()
        {
            if (line == null)
                return;
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 3f);
            Color c = new Color(1f, 0.78f, 0.25f, pulse);
            line.startColor = line.endColor = c;
        }
    }
}
