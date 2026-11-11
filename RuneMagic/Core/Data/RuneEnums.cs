namespace RuneMagic
{
    /// <summary>What a rune is made of.</summary>
    public enum RuneKind
    {
        /// <summary>A rune of one alignment, as monsters carry them (Greater Wind, Greater Fire...).</summary>
        Base,
        /// <summary>A rune made by synthesis from other runes (Pyroclastic Resonance...): several alignments.</summary>
        Composite,
        /// <summary>A rare, dangerous rune: synthesis and casting with it strain the soul more and leave deep damage.</summary>
        Primordial,
    }

    /// <summary>How rare a rune or a concept is (drops, discovery difficulty, value).</summary>
    public enum RuneRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
    }

    /// <summary>The families of Desired Effects (design spec section 10).</summary>
    public enum EffectCategory
    {
        Offensive,
        Defensive,
        Movement,
        Control,
        CreationTransformation,
        UtilityExploration,
    }

    /// <summary>What a <see cref="CompatibilityRule"/> relates.</summary>
    public enum CompatibilityScope
    {
        /// <summary>Two aspects (most rules: they apply to every concept that carries them).</summary>
        AspectPair,
        /// <summary>Two specific concepts.</summary>
        ConceptPair,
        /// <summary>Two alignments.</summary>
        AlignmentPair,
        /// <summary>An intention and a desired effect: <see cref="CompatibilityRule.value"/> is the support (0-1).</summary>
        IntentionEffect,
        /// <summary>A concept and an intention (a bonus or a penalty).</summary>
        ConceptIntention,
        /// <summary>A concept and a desired effect (a bonus or a penalty).</summary>
        ConceptEffect,
    }
}
