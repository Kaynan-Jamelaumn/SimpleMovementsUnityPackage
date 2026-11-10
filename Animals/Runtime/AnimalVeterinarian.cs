using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A hint the veterinarian gives about one of the medicines (how it is made, in folk words: never a recipe).</summary>
[Serializable]
public class AnimalVetHint
{
    [Tooltip("The medicine (an id of the Animal Database's medicines).")]
    public string treatment = "";
    [Tooltip("What the veterinarian says of it.")]
    [TextArea(1, 3)] public string hint = "";

    public AnimalVetHint() { }

    public AnimalVetHint(string treatment, string hint)
    {
        this.treatment = treatment;
        this.hint = hint;
    }
}

/// <summary>
/// The city's veterinarian (the "Veterinarian" option of an NPC). Sent for from a pen's window - the Health tab of an
/// animal, for the fee it shows - the vet sees the animal there: names everything that ails it (even what does not show
/// yet) and treats it, curing what the keeper's own medicine can only hold. Talked to, the vet only tells: what the vet
/// treats, what a visit costs for each kind of animal, and hints about the medicines a keeper can make or buy.
/// <list type="bullet">
/// <item>The first enabled veterinarian in the loaded scenes is the city's (<see cref="Current"/>); without one, nobody can
/// be sent for - and a keeper's medicine can then cure every illness.</item>
/// <item>The fee is the vet's own: a visit, plus a share by the animal's size and by its breed's value (<see cref="FeeFor"/>),
/// in <see cref="Currency"/>. The same NPC may also be a <see cref="Merchant"/> selling the keeper's goods.</item>
/// </list>
/// </summary>
[AddComponentMenu("SimpleMovements/Animals/Veterinarian")]
public class AnimalVeterinarian : NPCBehaviour
{
    [Header("Veterinarian")]
    [Tooltip("The money the vet takes (empty: the default currency).")]
    public CurrencyDefinition currency;
    [Tooltip("What every visit costs.")]
    [Min(0)] public int baseFee = 15;
    [Tooltip("Added for every unit of the animal's size, by how much it eats (an average animal 1, a cow about 4, a hen 0.2).")]
    [Min(0f)] public float feePerSize = 8f;
    [Tooltip("Share of the breed's value (a young animal's price) added.")]
    [Range(0f, 1f)] public float feeOfValue = 0.05f;

    [Header("What the vet tells")]
    [Tooltip("Hints about the medicines a keeper can make (folk words, not recipes), shown in the vet's window.")]
    public List<AnimalVetHint> hints = DefaultHints();

    private static readonly List<AnimalVeterinarian> all = new List<AnimalVeterinarian>();

    /// <summary>The city's veterinarian: the first enabled one in a loaded scene (null: there is none).</summary>
    public static AnimalVeterinarian Current
    {
        get
        {
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].isActiveAndEnabled) return all[i];
            return null;
        }
    }

    /// <summary>Every enabled veterinarian, in the order they were enabled.</summary>
    public static IReadOnlyList<AnimalVeterinarian> All => all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => all.Clear();

    protected override void OnEnable()
    {
        base.OnEnable();
        if (!all.Contains(this)) all.Add(this);
    }

    protected override void OnDisable()
    {
        all.Remove(this);
        base.OnDisable();
    }

    private void Reset()
    {
        optionLabel = "Veterinarian";
        hints = DefaultHints();
    }

    /// <summary>The money the vet takes (never null: the default currency when none is set).</summary>
    public CurrencyDefinition Currency => currency != null ? currency : CurrencyDefinition.Default;

    /// <summary>
    /// What the vet asks to see an animal: the visit (<see cref="baseFee"/>), plus <see cref="feePerSize"/> for every unit of
    /// its size by appetite, plus <see cref="feeOfValue"/> of its breed's value. <paramref name="a"/> null: an average adult
    /// of the species' first breed.
    /// </summary>
    public long FeeFor(AnimalRecord a, AnimalSpeciesData sp)
    {
        AnimalBreedData breed = sp != null ? sp.BreedOrDefault(a != null ? a.breed : null) : null;
        float size = AnimalSim.Appetite(sp, breed, a);
        long fee = Math.Max(0, baseFee)
                   + (long)Mathf.Round(Mathf.Max(0f, feePerSize) * size)
                   + (long)Mathf.Round((breed != null ? Mathf.Max(0, breed.value) : 0) * Mathf.Clamp01(feeOfValue));
        return Math.Max(0L, fee);
    }

    /// <summary>A fee in words, in the vet's money ("42 gold").</summary>
    public string FeeText(long fee) => Currency != null ? Currency.Format((int)Math.Min(int.MaxValue, Math.Max(0L, fee))) : fee.ToString();

    /// <summary>The hint for a medicine (by its id), or "".</summary>
    public string HintFor(string treatmentId)
    {
        if (hints == null || string.IsNullOrEmpty(treatmentId)) return "";
        foreach (AnimalVetHint h in hints)
            if (h != null && string.Equals(h.treatment, treatmentId, StringComparison.OrdinalIgnoreCase)) return h.hint ?? "";
        return "";
    }

    /// <summary>The built-in hints: the three veterinary medicines, in folk words.</summary>
    public static List<AnimalVetHint> DefaultHints() => new List<AnimalVetHint>
    {
        new AnimalVetHint("vermifuge", "Wormwood steeped in warm water - never in vinegar, which turns it into something else."),
        new AnimalVetHint("antiseptic", "Oak gall or willow bark steeped in strong spirit."),
        new AnimalVetHint("electrolytes", "A pinch of salt and a spoon of honey in clean water."),
    };

    public override bool IsAvailable(NPCInteractionSession session, out string reason)
    {
        reason = "";
        return isActiveAndEnabled;
    }

    /// <summary>Talking to the vet only tells (nothing is asked of the server): the visits are sent for from the pens.</summary>
    public override void Begin(NPCInteractionSession session) => AnimalVetWindow.Open(this, session);

    public override void End(NPCInteractionSession session) => AnimalVetWindow.CloseFor(this, session);

    public override void Validate(List<string> errors, List<string> warnings)
    {
        if (baseFee <= 0 && feePerSize <= 0f && feeOfValue <= 0f) warnings.Add("The veterinarian asks nothing: every visit is free.");
        NPC npc = GetComponent<NPC>();
        if (npc != null && npc.MaxPlayersAtOnce > 0) warnings.Add("A veterinarian should serve everyone at once (Max Players At Once 0): one player could keep others waiting.");
        if (hints != null)
            foreach (AnimalVetHint h in hints)
                if (h != null && !string.IsNullOrWhiteSpace(h.treatment) && AnimalDatabase.Instance.Treatment(h.treatment) == null)
                    warnings.Add($"A hint names the medicine '{h.treatment}', which the Animal Database does not have.");
    }
}
