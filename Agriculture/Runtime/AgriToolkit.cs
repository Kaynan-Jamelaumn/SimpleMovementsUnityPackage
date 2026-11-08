using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Between the agriculture rules and a player's inventory: what the player works with (<see cref="WorkerFor"/>: tools,
/// instruments, the water they carry, how many of each seed and material, the seed copy to sow), and what came of the
/// work (<see cref="Apply"/>: seeds and materials used, water poured, tools worn, produce and seeds given - onto the ground
/// when the bag is full - experience). Runs where the rules run (offline, the server).
/// </summary>
public static class AgriToolkit
{
    private static readonly List<InventoryItem> stacks = new List<InventoryItem>();

    /// <summary>Every stack the player carries: bag, hotbar, equipment.</summary>
    public static List<InventoryItem> Stacks(InventoryManager inv)
    {
        stacks.Clear();
        if (inv == null) return stacks;
        inv.GetBagItems(stacks);
        IReadOnlyList<InventorySlot> eq = inv.EquipmentSlots;
        if (eq != null)
            foreach (InventorySlot s in eq)
                if (s != null && s.heldItem != null)
                {
                    InventoryItem it = s.heldItem.GetComponent<InventoryItem>();
                    if (it != null && !stacks.Contains(it)) stacks.Add(it);
                }
        return stacks;
    }

    public static FarmWorker WorkerFor(CombatEntity player, AgriKnowledge k, string seedKey)
    {
        var w = new FarmWorker { key = HubWorld.PlayerIds.Of(player), name = player != null ? PartySystem.NameOf(player) : "" };
        if (k != null)
        {
            w.skills = k.Data;
            w.pollen = k.Pollen;
        }
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        if (inv == null) return w;
        InventoryItem held = inv.SelectedHotbarItem;
        float bestWater = -1f;
        bool seedFound = false;
        foreach (InventoryItem it in Stacks(inv))
        {
            ItemSO item = it != null ? it.itemScriptableObject : null;
            if (item == null) continue;
            string key = AgriItemInfo.KeyOf(item);
            if (key != null) w.counts[key] = w.Count(key) + Mathf.Max(0, it.stackCurrent);
            switch (item)
            {
                case FarmToolSO t:
                    w.tools.Add(t.Profile(it));
                    break;
                case SoilInstrumentSO s:
                    w.instruments.Add(s.Profile);
                    break;
                case WaterContainerSO c:
                {
                    WaterProfile prof = c.Profile(it);
                    float score = (ReferenceEquals(it, held) ? 1000f : 0f) + prof.litres;
                    if (score > bestWater)
                    {
                        bestWater = score;
                        w.water = prof;
                    }
                    break;
                }
            }
            if (!seedFound && seedKey != null && key == seedKey && it.stackCurrent > 0)
            {
                w.seedCode = AgriItemInfo.CodeOf(it);
                seedFound = true;
            }
        }
        return w;
    }

    /// <summary>Applies what came of the work to the player (items, water, wear, pollen, experience).</summary>
    public static void Apply(CombatEntity player, AgriKnowledge k, FarmWorker w, FarmResult r, SoilField field)
    {
        if (player == null || r == null) return;
        InventoryManager inv = InventoryManager.For(player);
        if (inv != null)
        {
            foreach (FarmItemChange t in r.take) Consume(inv, t.item, t.count);
            if (r.waterUsed > 0f) UseWater(inv, w.water, r.waterUsed);
            if (r.wear > 0 && !string.IsNullOrEmpty(r.toolWorn)) Wear(inv, AgriIds.Tool(r.toolWorn), r.wear);
        }
        foreach (FarmItemChange g in r.give) Give(player, inv, g.item, g.count, g.codes, field);
        if (r.pollenChanged && k != null) k.Pollen = r.pollen;
        if (k == null) return;
        AgriSettings s = AgricultureDatabase.Instance.Settings;
        float total = 0f;
        foreach (FarmXp x in r.xp)
        {
            k.Gain(x.skill, x.amount);
            total += x.amount;
        }
        if (s.characterXpShare > 0f && total > 0f)
        {
            // Not '??': in the Editor a missing component is a fake null that '??' would take for a real one.
            ExperienceManager xp = player.GetComponentInParent<ExperienceManager>();
            if (xp == null) xp = player.GetComponentInChildren<ExperienceManager>();
            int amount = Mathf.RoundToInt(total * s.characterXpShare * s.xpMultiplier);
            if (xp != null && amount > 0) xp.AddExperience(amount);
        }
        if (!CardTable.IsLocalPlayer(player)) field?.Net?.SendSkills(player, k.Export());
    }

    /// <summary>Uses copies of an item (the top ones: their codes go with them). Returns how many were used.</summary>
    public static int Consume(InventoryManager inv, string key, int count)
    {
        if (inv == null || count <= 0 || string.IsNullOrEmpty(key)) return 0;
        int used = 0;
        foreach (InventoryItem it in Stacks(inv).ToArray())
        {
            if (used >= count) break;
            if (it == null || AgriItemInfo.KeyOf(it.itemScriptableObject) != key) continue;
            used += inv.ConsumeItem(it, Mathf.Min(count - used, it.stackCurrent));
        }
        return used;
    }

    /// <summary>Takes litres from the container poured from (the one in hand, else the fullest of its kind).</summary>
    public static void UseWater(InventoryManager inv, WaterProfile water, float litres)
    {
        InventoryItem best = null;
        float bestScore = -1f;
        InventoryItem held = inv.SelectedHotbarItem;
        foreach (InventoryItem it in Stacks(inv))
        {
            if (!(it?.itemScriptableObject is WaterContainerSO c) || c.ContainerId != water.id) continue;
            AgriItemInfo.Water(it, out float l, out _);
            float score = (ReferenceEquals(it, held) ? 1000f : 0f) + l;
            if (score > bestScore) { bestScore = score; best = it; }
        }
        if (best == null) return;
        AgriItemInfo.Water(best, out float have, out WaterSourceKind kind);
        SetTopCode(best, AgriItemCode.Water(Mathf.Max(0f, have - litres), kind));
    }

    /// <summary>
    /// Fills the player's container (the one in hand, else the emptiest) from a source with <paramref name="available"/>
    /// litres (infinity for one that never runs dry). Returns the litres taken and the container's name.
    /// </summary>
    public static float Fill(CombatEntity player, WaterSourceKind kind, float available, out string container)
    {
        container = null;
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        if (inv == null) return 0f;
        InventoryItem best = null;
        float bestScore = float.MinValue;
        InventoryItem held = inv.SelectedHotbarItem;
        foreach (InventoryItem it in Stacks(inv))
        {
            if (!(it?.itemScriptableObject is WaterContainerSO c)) continue;
            AgriItemInfo.Water(it, out float l, out _);
            float score = (ReferenceEquals(it, held) ? 1000f : 0f) + (c.Capacity - l);
            if (score > bestScore) { bestScore = score; best = it; }
        }
        if (best == null) return 0f;
        var cont = (WaterContainerSO)best.itemScriptableObject;
        AgriItemInfo.Water(best, out float have, out WaterSourceKind old);
        float room = Mathf.Max(0f, cont.Capacity - have);
        // Water of another kind is poured out first: a container holds one water.
        if (old != kind && have > 0.05f)
        {
            have = 0f;
            room = cont.Capacity;
        }
        float take = Mathf.Min(room, available);
        SetTopCode(best, AgriItemCode.Water(have + take, kind));
        container = cont.Name;
        return take;
    }

    private static void SetTopCode(InventoryItem it, int code)
    {
        List<int> codes = it.DurabilityList;
        if (codes.Count == 0) codes.Add(code);
        else codes[codes.Count - 1] = code;
        it.durability = code;
        it.RefreshUI();
    }

    /// <summary>Wears the best copy of a tool by <paramref name="uses"/>.</summary>
    public static void Wear(InventoryManager inv, string key, int uses)
    {
        InventoryItem best = null;
        foreach (InventoryItem it in Stacks(inv))
        {
            ItemSO item = it?.itemScriptableObject;
            if (item == null || AgriItemInfo.KeyOf(item) != key || item.MaxDurability <= 1 || item.DurabilityReductionPerUse <= 0) continue;
            if (best == null || it.durability > best.durability) best = it;
        }
        if (best == null) return;
        best.durability = Mathf.Max(0f, best.durability - uses * best.itemScriptableObject.DurabilityReductionPerUse);
        best.RefreshUI();
    }

    /// <summary>Gives items (onto the ground by the player when the bag is full).</summary>
    public static void Give(CombatEntity player, InventoryManager inv, string id, int count, List<int> codes, SoilField field)
    {
        ItemSO item = AgriItems.Resolve(id);
        if (item == null || count <= 0) return;
        int left = inv != null ? inv.AddItem(item, count, codes != null && codes.Count > 0 ? codes : null) : count;
        if (left <= 0) return;
        List<int> rest = codes != null && codes.Count > 0 ? codes.GetRange(0, Mathf.Min(left, codes.Count)) : null;
        Vector3 at = player != null ? player.transform.position + player.transform.forward * 0.6f : field != null ? field.transform.position : Vector3.zero;
        GameObject dropped = ItemHandler.SpawnWorldItem(item, left, rest, at, player != null ? player.gameObject : null);
        if (dropped != null) inv?.ShowMessage("Your bag is full: the rest is on the ground.", InventoryFeedback.Kind.Info);
    }

    /// <summary>Everything to start farming with: tools, instruments, water, seeds of every species and the common materials.</summary>
    public static void GiveStarterKit(CombatEntity player)
    {
        InventoryManager inv = InventoryManager.For(player);
        if (inv == null) return;
        foreach (FarmToolPreset t in AgriDefaults.Tools())
            Give(player, inv, AgriIds.Tool(t.id), t.kind == FarmToolKind.Stakes ? 10 : 1, null, null);
        foreach (InstrumentPreset i in AgriDefaults.Instruments())
            Give(player, inv, AgriIds.Instrument(i.id), 1, null, null);
        Give(player, inv, AgriIds.Container("watering_can"), 1, new List<int> { AgriItemCode.Water(6f, WaterSourceKind.Well) }, null);
        Give(player, inv, AgriIds.Container("bucket"), 1, null, null);
        foreach (PlantSpeciesData s in AgricultureDatabase.Instance.AllSpecies)
            Give(player, inv, AgriIds.Seed(s.id), 10, null, null);
        string[] materials = { "compost", "manure", "limestone", "wood_ash", "bone_meal", "mineral_fertilizer", "straw_mulch", "herbal_spray", "ladybirds", "slug_bait", "sulfur", "sand", "peat", "magical_compost", "fungicide" };
        foreach (string m in materials)
            Give(player, inv, AgriIds.Amendment(m), m == "ladybirds" ? 1 : 5, null, null);
        inv.ShowMessage("A farming starter kit is in your bag.", InventoryFeedback.Kind.Info);
    }
}
