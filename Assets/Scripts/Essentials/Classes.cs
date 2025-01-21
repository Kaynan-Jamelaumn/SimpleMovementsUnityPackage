public class BasePlayerClass
{
    // **Base Stats**
    public float health;
    public float stamina;
    public float speed;
    public float hunger;
    public float thirst;
    public float weight;
    /*

    // **Combat Stats**
    public float strength;        // Physical strength
    public float defense;         // Physical damage resistance
    public float agility;         // Dexterity and evasion
    public float endurance;       // Ability to withstand physical strain
    public float armorRating;     // Total protection from physical damage
    public float vitality;        // Health regeneration over time

    // **Magical Stats**
    public float intelligence;    // Magical power and problem-solving
    public float mana;            // Magical energy for spells
    public float manaRegeneration; // Rate at which mana regenerates
    public float spellCastingSpeed; // Speed of casting spells
    public float magicResistance;  // Resistance to magical attacks

    // **Mental and Social Stats**
    public float charisma;        // Social influence, bargaining
    public float morale;          // Mental state, affects performance
    public float luck;            // Random chance factors

    // **Combat Stats**
    public float criticalHitChance; // Chance of dealing a critical hit
    public float criticalDamage;    // Multiplier for critical damage
    public float attackSpeed;       // Speed of physical attacks
    public float rangedDamage;      // Damage from ranged weapons
    public float blockChance;       // Chance to block incoming damage

    // **Environmental Stats**
    public float fireResistance;    // Resistance to fire-based damage
    public float coldResistance;    // Resistance to cold-based damage
    public float poisonResistance;  // Resistance to poison effects
    public float lightResistance;   // Resistance to light-based effects (holy magic)
    public float waterResistance;   // Resistance to water-based effects
    public float earthResistance;   // Resistance to earth-based effects
    public float thunderResistance; // Resistance to thunder-based effects

    public float waterAffinity;     // Affinity for water-based spells
    public float earthAffinity;     // Affinity for earth-based spells
    public float windAffinity;     // Affinity for wind-based spells
    public float thunderAffinity;   // Affinity for thunder-based spells
    public float iceAffinity;       // Affinity for ice-based spells
    public float fireAffinity;      // Affinity for fire-based spells
    public float poisonAffinity;    // Affinity for poison-based spells
    public float lightAffinity;     // Affinity for light-based spells
    public float darkAffinity;      // Affinity for dark-based spells

    // **Movement & Stealth Stats**
    public float stealth;           // Stealth ability for sneaking
    public float jumpHeight;        // Jumping height
    public float climbSpeed;        // Climbing ability
    public float swimmingSpeed;     // Swimming ability

    // **Other Stats**
    public float honor;             // Personal honor, affects reputation
    public float sanity;            // Mental stability, influences behavior
    public float underwaterBreathing;
    public float miningSkill;
    public float foragingSkill;
    public float woodcutting;
    public float fishingSkill;

      // **Combat Maneuvers and Abilities Stats**
    public float counterattackChance;
    public float dodgeChance;
    public float knockbackResistance;
    public float stunResistance;
    public float interruptResistance;
    public float disarmResistance;
        public float buffDuration;
    public float debuffDuration;
    public float summonControl;
    public float cooldownReduction;
    public float nightVision;          // Ability to see clearly in dark environments
    public float temperatureTolerance;

    */

    // Constructor to set default values
    public BasePlayerClass()
    {
        // Default stats could be set here or in derived classes
    }

    // Virtual method for initializing stats, to be overridden by derived classes
    public virtual void InitializeStats()
    {
        // Set base initialization if any
    }
}

// Warrior class with higher health and stamina
public class WarriorClass : BasePlayerClass
{
    public WarriorClass() : base()
    {
        health = 150;
        stamina = 120;
        speed = 5;
        hunger = 100;
        thirst = 100;
        weight = 40;
    }

}
