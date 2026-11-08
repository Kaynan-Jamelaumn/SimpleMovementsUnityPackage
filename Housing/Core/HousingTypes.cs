using System;
using System.Collections.Generic;

/// <summary>What a tenant lets someone else do in their house or room (the tenant may do everything).</summary>
[Flags]
public enum HousingAccess
{
    None = 0,
    /// <summary>Open its doors.</summary>
    Enter = 1,
    /// <summary>Work its gardens (sow, water, harvest...).</summary>
    Garden = 2,
    /// <summary>Open its chests.</summary>
    Storage = 4,
    /// <summary>Sleep in its beds.</summary>
    Sleep = 8,
    /// <summary>Let others in (or out) - all but this right; only the tenant gives it.</summary>
    Manage = 16,
    /// <summary>Everything a friend may do: enter, the garden, the chests, the beds.</summary>
    Friend = Enter | Garden | Storage | Sleep,
    All = Friend | Manage,
}

/// <summary>How long a lease runs before it is renewed (the days come from Housing Settings).</summary>
public enum LeaseTerm
{
    Week = 0,
    Month = 1,
}

/// <summary>The facts of a property the rules price it by (from its component; pure data).</summary>
[Serializable]
public class PropertyFacts
{
    public string id = "";
    public string name = "";
    public string district = "";
    public float landArea;
    public float houseFootprint;
    public int floors = 1;
    /// <summary>Area of its Soil Fields (the gardens one can grow in).</summary>
    public float gardenArea;
    /// <summary>Area of its zones of each use (indexed by LandUse; zoneArea[Garden] = garden zones without a Soil Field).</summary>
    public float[] zoneArea = new float[8];
    public float quality = 1f;
    /// <summary>Base rent per day set by hand (0 = from the sizes).</summary>
    public int baseRentPerDay;
    public bool weekly = true;
    public bool monthly = true;

    public bool Allows(LeaseTerm term) => term == LeaseTerm.Week ? weekly : monthly;
    public float HouseFloorArea => houseFootprint * Math.Max(1, floors);
}

/// <summary>A room of an inn, as the rules see it.</summary>
[Serializable]
public class InnRoomFacts
{
    public string id = "";
    public string name = "";
    public float quality = 1f;
}

/// <summary>An inn, as the rules see it.</summary>
[Serializable]
public class InnFacts
{
    public string id = "";
    public string name = "";
    public string district = "";
    public int nightlyPrice = 20;
    public List<InnRoomFacts> rooms = new List<InnRoomFacts>();
}

/// <summary>Someone a tenant let in, and what they may do.</summary>
[Serializable]
public class HousingGrant
{
    public string key = "";
    public string name = "";
    public int access;
    /// <summary>Who let them in (the tenant, or a manager: a manager's grants go when the manager does).</summary>
    public string grantedBy = "";

    public HousingAccess Access
    {
        get => (HousingAccess)access;
        set => access = (int)value;
    }
}

/// <summary>A stack taken out of a chest (exactly what the chest held: the item, how many, each copy's data).</summary>
[Serializable]
public class HeldStack
{
    public string itemId = "";
    public int count;
    public List<int> durabilities = new List<int>();
    public float durability;
    public int loadedRounds = -1;
}

/// <summary>What was left in a place's chests when a stay or lease ended, held for its owner to claim.</summary>
[Serializable]
public class HeldBundle
{
    public long id;
    public string key = "";
    public string name = "";
    /// <summary>Where it came from ("Room 3, The Gilded Goose").</summary>
    public string source = "";
    public double createdAt;
    /// <summary>A claim paid its fee already (the rest of a partial claim is free).</summary>
    public bool feePaid;
    public List<HeldStack> stacks = new List<HeldStack>();

    public int Count
    {
        get
        {
            int n = 0;
            foreach (HeldStack s in stacks) n += s != null ? s.count : 0;
            return n;
        }
    }
}

/// <summary>The saved contents of one chest of a rented place.</summary>
[Serializable]
public class ChestContents
{
    public string id = "";
    /// <summary>The place the chest belongs to ("property id" or "inn id/room id").</summary>
    public string place = "";
    /// <summary>The tenancy that owns what is in it (0: nobody rents the place; -1: not seen yet), and its tenant.</summary>
    public long owner = -1;
    public string ownerKey = "";
    public string ownerName = "";
    /// <summary>1 once its contents were recorded (before that, the chest keeps what the scene gives it).</summary>
    public int version;
    /// <summary>One entry per slot that holds something.</summary>
    public List<ChestSlot> slots = new List<ChestSlot>();
    /// <summary>Stacks whose item could not be found when the chest was filled (kept, tried again, held with the rest).</summary>
    public List<HeldStack> pending = new List<HeldStack>();
}

[Serializable]
public class ChestSlot
{
    public int index;
    public HeldStack stack = new HeldStack();
}
