using UnityEngine;

[CreateAssetMenu(fileName = "Item", menuName = "Scriptable Objects/Item")]
public class ItemSO : ScriptableObject
{
    public string name;
    public Sprite icon;
    public GameObject prefab;
    public int stackMax;
    [SerializeField] public PlayerStatusController statusController;

    [Header("Item Hand Position")]
    [Header("Position")]
    public Vector3 position;

    [Header("Rotation")]
    public Quaternion rotation;

    [Header("Scale")]
    public Vector3 scale;


    [Header("Animation")]
    public AnimationClip useAnimation;

    [Header("Audio")]
    public AudioClip useAudioClip;

    public virtual void UseItem()
    {

        // Play animation if available
        if (useAnimation != null)
        {
            Animation animation = prefab.GetComponent<Animation>();
            if (animation != null)
            {
                animation.Play(useAnimation.name);
            }
        }

        // Play audio if available
        if (useAudioClip != null)
        {
            AudioSource audioSource = prefab.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                // If AudioSource component is not present, add one
                audioSource = prefab.AddComponent<AudioSource>();
            }

            audioSource.clip = useAudioClip;
            audioSource.Play();
        }
    }
}
