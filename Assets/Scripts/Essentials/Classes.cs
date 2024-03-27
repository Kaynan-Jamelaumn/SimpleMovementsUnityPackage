public class BasePlayerClass
{
    public float health;
    public float stamina;
    public float speed;
    public float hunger;
    public float thirst;
    public float weight;

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
        weight = 200;
    }

}
