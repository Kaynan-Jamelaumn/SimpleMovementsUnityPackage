using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>Makes a light flicker like a flame (torches, braziers).</summary>
    [RequireComponent(typeof(Light))]
    public class DungeonFlicker : MonoBehaviour
    {
        [Tooltip("How much the light's intensity flickers (share of its base intensity).")]
        [Range(0f, 1f)] public float amount = 0.25f;
        [Tooltip("How fast it flickers.")]
        public float speed = 7f;

        private Light target;
        private float baseIntensity;
        private float offset;

        private void Awake()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            Vector3 p = transform.position;
            offset = Mathf.Abs(p.x * 12.9898f + p.z * 78.233f) % 100f;
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(offset, Time.time * speed);
            target.intensity = baseIntensity * (1f - amount + amount * 2f * n);
        }
    }
}
