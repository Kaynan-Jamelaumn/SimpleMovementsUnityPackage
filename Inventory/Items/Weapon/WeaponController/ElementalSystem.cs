using System.Collections.Generic;
using UnityEngine;

public enum ElementType
{
    None,
    Fire,
    Ice,
    Lightning,
    Poison,
    Holy,
    Dark,
    Wind,
    Earth,
    Water
}

[System.Serializable]
public class ElementalReaction
{
    public ElementType element1;
    public ElementType element2;
    public string reactionName;
    public float damageMultiplier = 1.5f;
    public AttackEffect reactionEffect;
    public ParticleSystem reactionVisual;
    public AudioClip reactionSound;

    [TextArea(2, 3)]
    public string reactionDescription;

    public bool MatchesElements(ElementType e1, ElementType e2)
    {
        return (element1 == e1 && element2 == e2) || (element1 == e2 && element2 == e1);
    }
}

[CreateAssetMenu(fileName = "ElementalSystem", menuName = "Scriptable Objects/ElementalSystem")]
public class ElementalSystem : ScriptableObject
{
    [Header("Elemental Reactions")]
    public List<ElementalReaction> reactions = new List<ElementalReaction>();

    [Header("Elemental Relationships")]
    [SerializeField] private List<ElementalRelationship> elementalRelationships = new List<ElementalRelationship>();

    [Header("Visual Effects")]
    [SerializeField] private ElementalVisuals elementalVisuals;

    // Static instance for easy access
    private static ElementalSystem instance;
    public static ElementalSystem Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<ElementalSystem>("ElementalSystem");
                if (instance == null)
                {
                    Debug.LogWarning("ElementalSystem not found in Resources folder!");
                }
            }
            return instance;
        }
    }

    // Cached data structures for performance
    private Dictionary<ElementType, List<ElementType>> resistanceMap;
    private Dictionary<ElementType, List<ElementType>> weaknessMap;
    private Dictionary<string, ElementalReaction> reactionMap;

    private void OnEnable()
    {
        BuildCaches();
    }

    private void BuildCaches()
    {
        // Build resistance and weakness maps
        resistanceMap = new Dictionary<ElementType, List<ElementType>>();
        weaknessMap = new Dictionary<ElementType, List<ElementType>>();

        foreach (var relationship in elementalRelationships)
        {
            // Add to weakness map
            if (!weaknessMap.ContainsKey(relationship.targetElement))
                weaknessMap[relationship.targetElement] = new List<ElementType>();

            if (!resistanceMap.ContainsKey(relationship.targetElement))
                resistanceMap[relationship.targetElement] = new List<ElementType>();

            if (relationship.damageMultiplier > 1f)
            {
                weaknessMap[relationship.targetElement].Add(relationship.attackElement);
            }
            else if (relationship.damageMultiplier < 1f)
            {
                resistanceMap[relationship.targetElement].Add(relationship.attackElement);
            }
        }

        // Build reaction map
        reactionMap = new Dictionary<string, ElementalReaction>();
        foreach (var reaction in reactions)
        {
            string key1 = GenerateReactionKey(reaction.element1, reaction.element2);
            string key2 = GenerateReactionKey(reaction.element2, reaction.element1);
            reactionMap[key1] = reaction;
            reactionMap[key2] = reaction;
        }
    }

    private string GenerateReactionKey(ElementType e1, ElementType e2)
    {
        return $"{e1}_{e2}";
    }

    public ElementalReaction GetReaction(ElementType element1, ElementType element2)
    {
        if (reactionMap == null) BuildCaches();

        string key = GenerateReactionKey(element1, element2);
        reactionMap.TryGetValue(key, out var reaction);
        return reaction;
    }

    public float GetElementalDamageMultiplier(ElementType attackElement, ElementType targetElement)
    {
        foreach (var relationship in elementalRelationships)
        {
            if (relationship.attackElement == attackElement && relationship.targetElement == targetElement)
            {
                return relationship.damageMultiplier;
            }
        }
        return 1.0f;
    }

    public bool IsWeakTo(ElementType targetElement, ElementType attackElement)
    {
        if (weaknessMap == null) BuildCaches();

        if (weaknessMap.TryGetValue(targetElement, out var weaknesses))
        {
            return weaknesses.Contains(attackElement);
        }
        return false;
    }

    public bool IsResistantTo(ElementType targetElement, ElementType attackElement)
    {
        if (resistanceMap == null) BuildCaches();

        if (resistanceMap.TryGetValue(targetElement, out var resistances))
        {
            return resistances.Contains(attackElement);
        }
        return false;
    }

    public void TriggerElementalReaction(ElementType element1, ElementType element2, Vector3 position, GameObject source, GameObject target)
    {
        var reaction = GetReaction(element1, element2);
        if (reaction == null) return;

        // Play visual effects
        if (reaction.reactionVisual != null)
        {
            var particles = Instantiate(reaction.reactionVisual, position, Quaternion.identity);
            particles.Play();
            Destroy(particles.gameObject, particles.main.duration + 1f);
        }

        // Play sound
        if (reaction.reactionSound != null && source != null)
        {
            var audioSource = source.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = source.GetComponent<Player>()?.PlayerAudioSource;
            }
            audioSource?.PlayOneShot(reaction.reactionSound);
        }

        // Apply reaction effect
        if (reaction.reactionEffect != null && target != null)
        {
            var targetController = target.GetComponent<BaseStatusController>();
            if (targetController != null)
            {
                float amount = reaction.reactionEffect.amount * reaction.damageMultiplier;
                targetController.ApplyEffect(reaction.reactionEffect, amount, reaction.reactionEffect.timeBuffEffect, reaction.reactionEffect.tickCooldown);
            }
        }

        Debug.Log($"Elemental Reaction: {reaction.reactionName} ({element1} + {element2})");
    }

    public Color GetElementColor(ElementType element)
    {
        if (elementalVisuals == null) return Color.white;

        switch (element)
        {
            case ElementType.Fire: return elementalVisuals.fireColor;
            case ElementType.Ice: return elementalVisuals.iceColor;
            case ElementType.Lightning: return elementalVisuals.lightningColor;
            case ElementType.Poison: return elementalVisuals.poisonColor;
            case ElementType.Holy: return elementalVisuals.holyColor;
            case ElementType.Dark: return elementalVisuals.darkColor;
            case ElementType.Wind: return elementalVisuals.windColor;
            case ElementType.Earth: return elementalVisuals.earthColor;
            case ElementType.Water: return elementalVisuals.waterColor;
            default: return Color.white;
        }
    }

    public ParticleSystem GetElementParticles(ElementType element)
    {
        if (elementalVisuals == null) return null;

        switch (element)
        {
            case ElementType.Fire: return elementalVisuals.fireParticles;
            case ElementType.Ice: return elementalVisuals.iceParticles;
            case ElementType.Lightning: return elementalVisuals.lightningParticles;
            case ElementType.Poison: return elementalVisuals.poisonParticles;
            case ElementType.Holy: return elementalVisuals.holyParticles;
            case ElementType.Dark: return elementalVisuals.darkParticles;
            case ElementType.Wind: return elementalVisuals.windParticles;
            case ElementType.Earth: return elementalVisuals.earthParticles;
            case ElementType.Water: return elementalVisuals.waterParticles;
            default: return null;
        }
    }
}

[System.Serializable]
public class ElementalRelationship
{
    public ElementType attackElement;
    public ElementType targetElement;
    [Range(0f, 2f)]
    public float damageMultiplier = 1f;
}

[System.Serializable]
public class ElementalVisuals
{
    [Header("Element Colors")]
    public Color fireColor = new Color(1f, 0.5f, 0f);
    public Color iceColor = new Color(0.5f, 0.8f, 1f);
    public Color lightningColor = new Color(1f, 1f, 0f);
    public Color poisonColor = new Color(0.5f, 0f, 0.5f);
    public Color holyColor = new Color(1f, 1f, 0.8f);
    public Color darkColor = new Color(0.2f, 0f, 0.3f);
    public Color windColor = new Color(0.8f, 1f, 0.8f);
    public Color earthColor = new Color(0.5f, 0.3f, 0f);
    public Color waterColor = new Color(0f, 0.5f, 1f);

    [Header("Element Particles")]
    public ParticleSystem fireParticles;
    public ParticleSystem iceParticles;
    public ParticleSystem lightningParticles;
    public ParticleSystem poisonParticles;
    public ParticleSystem holyParticles;
    public ParticleSystem darkParticles;
    public ParticleSystem windParticles;
    public ParticleSystem earthParticles;
    public ParticleSystem waterParticles;
}