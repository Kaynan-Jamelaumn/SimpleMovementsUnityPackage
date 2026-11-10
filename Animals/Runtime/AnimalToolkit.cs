using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Between the animal rules and a player's inventory and purse, where the rules run (offline, the server): what an item is
/// to animals (its key and food), how many of it the player carries, the quality of each copy (feed, the farm's produce,
/// harvested meat and brewed medicine each keep theirs in their code), the care tools the player carries and their wear,
/// taking copies (the top ones, their qualities read first) and giving them back - copy by copy - when a command could not
/// be completed, the water a player carries, and the money a command costs (the veterinarian's fee).
/// <para>Items are matched by key, never by asset: a bound asset and the runtime twin of the same id are the same food,
/// the same tool, the same medicine.</para>
/// </summary>
public static class AnimalToolkit
{
    private static readonly List<InventoryItem> scratch = new List<InventoryItem>();

    private const string PotionPrefix = "potion:";
    private const string IngredientPrefix = "ingredient:";

    /// <summary>Every stack the player carries: bag, hotbar, equipment (a fresh list: callers may change the inventory while walking it).</summary>
    public static List<InventoryItem> Stacks(InventoryManager inv, List<InventoryItem> into = null)
    {
        into ??= new List<InventoryItem>();
        into.Clear();
        if (inv == null) return into;
        inv.GetBagItems(scratch);
        into.AddRange(scratch);
        IReadOnlyList<InventorySlot> eq = inv.EquipmentSlots;
        if (eq != null)
            foreach (InventorySlot s in eq)
                if (s != null && s.heldItem != null)
                {
                    InventoryItem it = s.heldItem.GetComponent<InventoryItem>();
                    if (it != null && !into.Contains(it)) into.Add(it);
                }
        return into;
    }

    // ---------------------------------------------------------------- what an item is
    /// <summary>
    /// The key an item goes by in the animal rules: its animal, farm or harvest key; a brewed potion's "potion:&lt;outcome&gt;"
    /// and an alchemy ingredient's "ingredient:&lt;id&gt;" (whatever its tier, colour or asset); crafting stock's
    /// "craftstock:&lt;kind&gt;:&lt;id&gt;" (a baked asset and its runtime twin alike); else its Item Database id.
    /// </summary>
    public static string KeyOf(ItemSO item)
    {
        if (item == null) return null;
        if (item is IAnimalItem a) return a.AnimalKey;
        string agri = AgriItemInfo.KeyOf(item);
        if (agri != null) return agri;
        if (item is IHarvestedItem h) return HarvestItems.IdOf(h.AnatomyId, h.MaterialId);
        string alchemy = AnimalAlchemy.KeyOf(item);
        if (!string.IsNullOrEmpty(alchemy)) return alchemy;
        if (item is CraftStockItemSO stock)
        {
            string runtime = stock.RuntimeId;
            if (!string.IsNullOrEmpty(runtime)) return runtime;
            // A baked asset has no runtime id: it goes by the key its runtime twin would have (metal and fasteners have none of their own).
            if (stock.StockKind != CraftStockKind.Metal && stock.StockKind != CraftStockKind.Fasteners && !string.IsNullOrEmpty(stock.CraftId))
                return CraftStockItemSO.IdPrefix + stock.StockKind + ":" + stock.CraftId;
        }
        return ItemDatabase.Id(item);
    }

    /// <summary>The food an item is to animals (null: not food). Harvested pieces count only when they are food (meat, not hide).</summary>
    public static AnimalFoodData FoodOf(ItemSO item)
    {
        string key = KeyOf(item);
        if (key == null) return null;
        if (item is IHarvestedItem && !(item is HarvestedFoodSO)) return null;
        return AnimalDatabase.Instance.FoodOfItem(key);
    }

    /// <summary>
    /// The item a key names, made when need be: an animal item, a standard bottle of a potion's outcome ("potion:antidote"),
    /// an alchemy ingredient ("ingredient:charcoal"), or any Item Database id ("agri:amend/straw_mulch",
    /// "craftstock:Supply:soap"). Null for a prefix key (ending in *) or an id nothing knows.
    /// </summary>
    public static ItemSO ItemOfKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        key = key.Trim();
        if (key.EndsWith("*", StringComparison.Ordinal)) return null;
        if (AnimalIds.IsAnimalId(key)) return AnimalItems.Resolve(key);
        if (key.StartsWith(PotionPrefix, StringComparison.Ordinal) && key.IndexOf('#') < 0)
            return key.Length > PotionPrefix.Length ? AnimalAlchemy.PotionItem(key.Substring(PotionPrefix.Length), 1) : null;
        if (key.StartsWith(IngredientPrefix, StringComparison.Ordinal))
            return key.Length > IngredientPrefix.Length ? AnimalAlchemy.IngredientItem(key.Substring(IngredientPrefix.Length)) : null;
        return ItemDatabase.Item(key);
    }

    /// <summary>One copy's code (the top copy is the last of the stack's list).</summary>
    public static int CodeOf(InventoryItem stack, int fromTop)
    {
        List<int> d = stack != null ? stack.DurabilityList : null;
        return d != null && fromTop >= 0 && fromTop < d.Count ? d[d.Count - 1 - fromTop] : 0;
    }

    /// <summary>
    /// The quality (0-1) of one copy: feed by its code; produce by its code (overripe less, rotten far less); harvested meat
    /// by its code (spoiled, poisoned or tainted pieces far less); a brewed potion by its strength and purity (a bottle
    /// without a code: its tier's typical quality). Any other copy is of middling quality.
    /// </summary>
    public static float QualityOfCopy(InventoryItem stack, int fromTop)
    {
        ItemSO item = stack != null ? stack.itemScriptableObject : null;
        int code = CodeOf(stack, fromTop);
        switch (item)
        {
            case AnimalFeedSO _:
                return AnimalItemCode.TryFeed(code, out float fq) ? fq : AnimalItemCode.UnknownQuality;
            case IAnimalProductItem _:
                return AnimalItemCode.TryProduct(code, out float pq, out _) ? pq : AnimalItemCode.UnknownQuality;
            case IAgriProduce _:
                if (!AgriItemCode.TryProduce(code, out float q, out _, out ProduceStage stage, out _)) return AgriItemCode.UnknownQuality;
                return stage == ProduceStage.Rotten ? q * 0.25f : stage == ProduceStage.Overripe ? q * 0.8f : q;
            case IHarvestedItem _:
                if (!HarvestItemCode.TryDecode(code, out double hq, out HarvestMarks marks)) return (float)HarvestItemCode.UnknownQuality;
                return (marks & HarvestedFoodSO.Sickening) != 0 ? (float)hq * 0.3f : (float)hq;
            case BrewedPotionSO _:
                return Mathf.Clamp01(AnimalAlchemy.QualityOfCopy(stack, fromTop));
            case null:
                return AnimalItemCode.UnknownQuality;
            default:
            {
                // A hand-made item that is a tier of an Alchemy outcome is a potion all the same.
                string key = AnimalAlchemy.KeyOf(item);
                if (key != null && key.StartsWith(PotionPrefix, StringComparison.Ordinal)) return Mathf.Clamp01(AnimalAlchemy.QualityOfCopy(stack, fromTop));
                return AnimalItemCode.UnknownQuality;
            }
        }
    }

    // ---------------------------------------------------------------- care tools
    /// <summary>
    /// The best care tool the player carries with any of these tags (a brush, a hoof pick, a fork or shovel; not a supply,
    /// which is used up): its quality is its data's, its edge what is left of the copy (1 for an item that does not wear),
    /// the best by quality x (0.5 + 0.5 x edge). None carried (or no tags asked for): <see cref="AnimalToolUse.has"/> is false.
    /// </summary>
    public static AnimalToolUse ToolOf(InventoryManager inv, IAnimalCatalog cat, IReadOnlyList<string> tags)
    {
        if (inv == null || cat == null || tags == null || tags.Count == 0) return default(AnimalToolUse);
        AnimalToolUse best = default(AnimalToolUse);
        float bestScore = -1f;
        foreach (InventoryItem it in Stacks(inv, new List<InventoryItem>()))
        {
            ItemSO item = it != null ? it.itemScriptableObject : null;
            if (item == null || it.stackCurrent <= 0) continue;
            string key = KeyOf(item);
            if (string.IsNullOrEmpty(key)) continue;
            AnimalToolData t = cat.ToolOfItem(key);
            if (t == null || t.consumed || !t.HasAny(tags)) continue;
            float edge = EdgeOf(it);
            float quality = Mathf.Clamp01(t.quality);
            float score = quality * (0.5f + 0.5f * edge);
            if (score > bestScore)
            {
                bestScore = score;
                best = new AnimalToolUse(key, t.id, quality, edge);
            }
        }
        return best;
    }

    /// <summary>What is left of a tool's copy, 0-1 (1 for an item that does not wear).</summary>
    public static float EdgeOf(InventoryItem copy)
    {
        ItemSO item = copy != null ? copy.itemScriptableObject : null;
        if (item == null || item.MaxDurability <= 1) return 1f;
        return Mathf.Clamp01(copy.durability / item.MaxDurability);
    }

    /// <summary>
    /// Wears the best copy of the tool with this key by <paramref name="uses"/> (where the rules run, after a job is done):
    /// a worn-out tool stays in the bag and keeps working, poorly. The change reaches the player's screen at once.
    /// </summary>
    public static void Wear(InventoryManager inv, string key, int uses)
    {
        if (inv == null || string.IsNullOrEmpty(key) || uses <= 0 || !inv.IsAuthoritative) return;
        InventoryItem best = null;
        foreach (InventoryItem it in Stacks(inv, new List<InventoryItem>()))
        {
            ItemSO item = it != null ? it.itemScriptableObject : null;
            if (item == null || item.MaxDurability <= 1 || item.DurabilityReductionPerUse <= 0 || KeyOf(item) != key) continue;
            if (best == null || it.durability > best.durability) best = it;
        }
        if (best == null) return;
        best.durability = Mathf.Max(0f, best.durability - uses * best.itemScriptableObject.DurabilityReductionPerUse);
        best.RefreshUI();
        inv.NotifyInventoryChanged();
    }

    // ---------------------------------------------------------------- counting, taking, giving back
    /// <summary>How many copies of the item with this key the player carries.</summary>
    public static int Count(InventoryManager inv, string key)
    {
        if (inv == null || string.IsNullOrEmpty(key)) return 0;
        int n = 0;
        foreach (InventoryItem it in Stacks(inv, new List<InventoryItem>()))
            if (it != null && it.itemScriptableObject != null && KeyOf(it.itemScriptableObject) == key) n += Mathf.Max(0, it.stackCurrent);
        return n;
    }

    /// <summary>
    /// Takes up to <paramref name="count"/> copies of the item with this key (the top copies of each stack, the held stack
    /// first), reading each copy's quality and code before it goes. Returns what was really taken.
    /// </summary>
    public static AnimalTaken Take(InventoryManager inv, string key, int count, out ItemSO takenItem) => Take(inv, key, count, out takenItem, out _);

    /// <summary>
    /// <see cref="Take(InventoryManager, string, int, out ItemSO)"/>, also telling the item of every copy taken
    /// (<paramref name="perCopy"/>, in the order of the taken codes): one key may be several items - a potion's tiers, an
    /// asset and its runtime twin - and each copy goes back as what it was.
    /// </summary>
    public static AnimalTaken Take(InventoryManager inv, string key, int count, out ItemSO takenItem, out List<ItemSO> perCopy) =>
        Take(inv, key, count, out takenItem, out perCopy, 0);

    /// <summary>
    /// <see cref="Take(InventoryManager, string, int, out ItemSO, out List{ItemSO})"/> of the copies whose code is
    /// <paramref name="code"/> only (0: any copy): a chosen crate, and no other.
    /// </summary>
    public static AnimalTaken Take(InventoryManager inv, string key, int count, out ItemSO takenItem, out List<ItemSO> perCopy, int code)
    {
        takenItem = null;
        perCopy = new List<ItemSO>();
        var taken = new AnimalTaken { qualities = new List<float>(), codes = new List<int>() };
        if (inv == null || string.IsNullOrEmpty(key) || count <= 0 || !inv.IsAuthoritative) return taken;
        List<InventoryItem> stacks = Stacks(inv, new List<InventoryItem>());
        InventoryItem held = inv.SelectedHotbarItem;
        if (held != null && stacks.Remove(held)) stacks.Insert(0, held);
        foreach (InventoryItem it in stacks)
        {
            if (taken.count >= count) break;
            if (it == null || it.itemScriptableObject == null || KeyOf(it.itemScriptableObject) != key) continue;
            // A chosen copy: only a stack whose next copy it is (a crate is a stack of one); "any plain crate": never one that
            // holds an animal of the world.
            if (code == AnimalCommands.PlainCrate)
            {
                if (AnimalItemCode.TryCrated(CodeOf(it, 0), out _, out _, out _, out _)) continue;
            }
            else if (code != 0 && CodeOf(it, 0) != code) continue;
            int n = Mathf.Min(count - taken.count, code != 0 ? 1 : it.stackCurrent);
            if (n <= 0) continue;
            var q = new List<float>(n);
            var c = new List<int>(n);
            for (int i = 0; i < n; i++)
            {
                q.Add(QualityOfCopy(it, i));
                c.Add(CodeOf(it, i));
            }
            ItemSO item = it.itemScriptableObject;
            int used = inv.ConsumeItem(it, n);
            for (int i = 0; i < used && i < q.Count; i++)
            {
                taken.qualities.Add(q[i]);
                taken.codes.Add(c[i]);
                perCopy.Add(item);
            }
            taken.count += used;
            if (takenItem == null && used > 0) takenItem = item;
        }
        return taken;
    }

    /// <summary>
    /// Gives back what was taken for a command that could not be completed (the same copies, with their codes; onto the
    /// ground by the player when the bag has no room): nothing is ever lost to a failed command. Every copy goes back as
    /// <paramref name="item"/> (see the per-copy overload when one key may be several items).
    /// </summary>
    public static void GiveBack(CombatEntity player, InventoryManager inv, ItemSO item, AnimalTaken taken)
    {
        if (taken.count <= 0 || item == null) return;
        var codes = new List<int>(taken.count);
        for (int i = 0; i < taken.count; i++) codes.Add(taken.Code(i));
        GiveCopies(player, inv, item, taken.count, codes);
    }

    /// <summary>
    /// Gives back what was taken, copy by copy: each copy as the item it was (<paramref name="perCopy"/> from
    /// <see cref="Take(InventoryManager, string, int, out ItemSO, out List{ItemSO})"/>), with its own code - a Weak bottle
    /// never comes back as a Superior one.
    /// </summary>
    public static void GiveBack(CombatEntity player, InventoryManager inv, List<ItemSO> perCopy, AnimalTaken taken)
    {
        if (taken.count <= 0 || perCopy == null || perCopy.Count == 0) return;
        int n = Mathf.Min(taken.count, perCopy.Count);
        int i = 0;
        while (i < n)
        {
            // A run of copies of the same item goes back together (in their order: the top copy is the top copy again).
            ItemSO item = perCopy[i];
            int j = i + 1;
            while (j < n && perCopy[j] == item) j++;
            if (item != null)
            {
                var codes = new List<int>(j - i);
                for (int k = i; k < j; k++) codes.Add(taken.Code(k));
                GiveCopies(player, inv, item, j - i, codes);
            }
            i = j;
        }
    }

    /// <summary>Puts copies back in the bag with their codes (read top first), the rest on the ground by the player.</summary>
    private static void GiveCopies(CombatEntity player, InventoryManager inv, ItemSO item, int count, List<int> codes)
    {
        if (item == null || count <= 0) return;
        // The codes were read top first; AddItem takes them from the end of the list, so given as they are the top copy
        // is the top copy again.
        int left = inv != null ? inv.AddItem(item, count, codes) : count;
        if (left <= 0) return;
        List<int> rest = codes.GetRange(0, Mathf.Min(left, codes.Count));
        Vector3 at = player != null ? player.transform.position + player.transform.forward * 0.6f : Vector3.zero;
        ItemHandler.SpawnWorldItem(item, left, rest, at, player != null ? player.gameObject : null);
    }

    // ---------------------------------------------------------------- water
    /// <summary>The container a player would pour from (the one in hand, else the fullest) and the water in it.</summary>
    public static InventoryItem WaterOf(InventoryManager inv, out float litres, out WaterSourceKind kind)
    {
        litres = 0f;
        kind = WaterSourceKind.Well;
        if (inv == null) return null;
        InventoryItem best = null;
        float bestScore = -1f;
        InventoryItem held = inv.SelectedHotbarItem;
        foreach (InventoryItem it in Stacks(inv, new List<InventoryItem>()))
        {
            if (!(it?.itemScriptableObject is WaterContainerSO)) continue;
            AgriItemInfo.Water(it, out float l, out WaterSourceKind k);
            if (l <= 0.01f) continue;
            float score = (ReferenceEquals(it, held) ? 1000f : 0f) + l;
            if (score > bestScore)
            {
                bestScore = score;
                best = it;
                litres = l;
                kind = k;
            }
        }
        return best;
    }

    /// <summary>Pours litres out of a container (its top copy's code keeps what is left). Returns the litres poured.</summary>
    public static float Pour(InventoryManager inv, InventoryItem container, float litres)
    {
        if (container == null || litres <= 0f || inv == null || !inv.IsAuthoritative) return 0f;
        AgriItemInfo.Water(container, out float have, out WaterSourceKind kind);
        float poured = Mathf.Min(have, litres);
        int code = AgriItemCode.Water(Mathf.Max(0f, have - poured), kind);
        List<int> codes = container.DurabilityList;
        if (codes.Count == 0) codes.Add(code);
        else codes[codes.Count - 1] = code;
        container.durability = code;
        container.RefreshUI();
        inv.NotifyInventoryChanged();
        return poured;
    }

    /// <summary>Puts litres back into a container (a command that poured and then could not be completed).</summary>
    public static void Refill(InventoryManager inv, InventoryItem container, float litres)
    {
        if (container == null || litres <= 0f || inv == null || !inv.IsAuthoritative) return;
        if (!(container.itemScriptableObject is WaterContainerSO c)) return;
        AgriItemInfo.Water(container, out float have, out WaterSourceKind kind);
        int code = AgriItemCode.Water(Mathf.Min(c.Capacity, have + litres), kind);
        List<int> codes = container.DurabilityList;
        if (codes.Count == 0) codes.Add(code);
        else codes[codes.Count - 1] = code;
        container.durability = code;
        container.RefreshUI();
        inv.NotifyInventoryChanged();
    }

    // ---------------------------------------------------------------- money
    /// <summary>
    /// Takes a fee from the player's purse, all or nothing (where the rules run). Refused - with why - when the purse is
    /// a client's copy or is still being fetched, when a network session would pay in money that is not kept between
    /// sessions, or when the player cannot pay. <paramref name="currency"/> null: the purse's default currency.
    /// (A copy of the housing office's own take, which is private there.)
    /// </summary>
    public static bool TakeFee(CombatEntity player, long amount, CurrencyDefinition currency, out CurrencyWallet wallet, out string why)
    {
        why = "";
        wallet = player != null ? CurrencyWallet.For(player) : null;
        if (amount <= 0) return true;
        if (wallet == null || wallet.IsReplica)
        {
            why = "You have no purse.";
            return false;
        }
        // (Its kept balances not back yet: what it paid would not be saved with what it bought.)
        if (wallet.AwaitingRestore || WalletSaves.IsRestoring(wallet))
        {
            why = "One moment: your purse is being fetched.";
            return false;
        }
        CurrencyDefinition money = currency != null ? currency : wallet.DefaultCurrency;
        if (money == null)
        {
            why = "There is no money to pay with.";
            return false;
        }
        if (GameNet.NetAuthority.IsNetworked && !money.KeepBetweenSessions)
        {
            why = "The fee is paid in money that is kept between sessions (the currency's Keep Between Sessions).";
            return false;
        }
        if (amount > int.MaxValue || !wallet.TrySpend(money, (int)amount))
        {
            why = $"You need {money.Format((int)Math.Min(int.MaxValue, amount))}.";
            return false;
        }
        return true;
    }

    /// <summary>Gives a fee back (the command it paid for could not be done). <paramref name="currency"/> null: the purse's default currency.</summary>
    public static void GiveBackFee(CurrencyWallet wallet, CurrencyDefinition currency, long amount)
    {
        if (wallet == null || amount <= 0 || amount > int.MaxValue) return;
        CurrencyDefinition money = currency != null ? currency : wallet.DefaultCurrency;
        if (money != null) wallet.TryAdd(money, (int)amount);
    }

    // ---------------------------------------------------------------- the actor
    /// <summary>The most characters of a player's name the animals keep (<see cref="ShownName"/>).</summary>
    public const int MaxNameLength = 32;

    /// <summary>
    /// Who asks, with what they carry, for the commands: their name as others are shown it (<see cref="ShownName"/>), counts
    /// read from the inventory as asked, the water in the container they would pour from, the best care tool for a job, and
    /// the veterinarian's fee (none without a vet).
    /// </summary>
    public static AnimalActor ActorFor(CombatEntity player, AnimalRights rights)
    {
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        var actor = new AnimalActor
        {
            key = AnimalDirector.KeyOf(player),
            name = player != null ? ShownName(PartySystem.NameOf(player)) : "",
            rights = rights,
            count = k => Count(inv, k),
            tool = tags => ToolOf(inv, AnimalDatabase.Instance, tags),
        };
        WaterOf(inv, out actor.waterLitres, out WaterSourceKind kind);
        actor.waterKind = (int)kind;
        AnimalVeterinarian vet = AnimalVeterinarian.Current;
        if (vet != null) actor.vetFee = vet.FeeFor;
        return actor;
    }

    /// <summary>
    /// A player's name as the animals keep it - in the owner's name, the histories and the messages other players read,
    /// all rich text: cleaned as an animal's name is (<see cref="AnimalCommands.CleanName"/>: no '&lt;' or '&gt;', no
    /// control characters, backslash, line breaks, invisible characters or half of a character), at most
    /// <see cref="MaxNameLength"/> characters, cut where a whole character ends; "" when nothing visible is left.
    /// </summary>
    public static string ShownName(string name) => AnimalCommands.CleanName(name, MaxNameLength) ?? "";

    // ---------------------------------------------------------------- giving
    /// <summary>Gives a player items (onto the ground by them when the bag is full).</summary>
    public static void Give(CombatEntity player, ItemSO item, int count, List<int> codes = null)
    {
        if (item == null || count <= 0) return;
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        int left = inv != null ? inv.AddItem(item, count, codes != null && codes.Count > 0 ? codes : null) : count;
        if (left <= 0) return;
        List<int> rest = codes != null && codes.Count > 0 ? codes.GetRange(0, Mathf.Min(left, codes.Count)) : null;
        Vector3 at = player != null ? player.transform.position + player.transform.forward * 0.6f : Vector3.zero;
        GameObject dropped = ItemHandler.SpawnWorldItem(item, left, rest, at, player != null ? player.gameObject : null);
        if (dropped != null) inv?.ShowMessage("Your bag is full: the rest is on the ground.", InventoryFeedback.Kind.Info);
    }

    /// <summary>Everything to start keeping animals with: a crate of each species' first breed and some of every feed.</summary>
    public static void GiveStarterKit(CombatEntity player)
    {
        AnimalDatabase db = AnimalDatabase.Instance;
        foreach (AnimalSpeciesData s in db.AllSpecies)
            Give(player, AnimalItems.Livestock(s.id, s.BreedOrDefault(null)?.id ?? ""), 1);
        foreach (AnimalFoodData f in db.AllFoods)
        {
            ItemSO item = f.IsItem(AnimalIds.Feed(f.id)) ? AnimalItems.Feed(f.id) : null;
            if (item == null) continue;
            var codes = new List<int>();
            for (int i = 0; i < 5; i++) codes.Add(AnimalItemCode.Feed(0.7f));
            Give(player, item, 5, codes);
        }
        InventoryManager.For(player)?.ShowMessage("An animal keeper's starter kit is in your bag.", InventoryFeedback.Kind.Info);
    }

    /// <summary>The brewed medicines of the care kit (Alchemy outcome ids): two standard bottles of each.</summary>
    private static readonly string[] CareKitPotions =
    {
        "antidote", "healing_salve", "vet_vermifuge", "vet_antiseptic", "vet_electrolytes", "ginger_tonic", "calming_tonic",
    };

    /// <summary>
    /// Everything to care for animals with (where the rules run): one of each care tool of animal husbandry's own, the
    /// other systems' supplies it uses (5 straw, 3 soap: whatever of them resolves), two standard bottles of the common
    /// medicines (brewed: coded at quality 0.6) and two of each plain remedy a merchant sells (wormwood, vinegar, charcoal,
    /// lavender).
    /// </summary>
    public static void GiveCareKit(CombatEntity player)
    {
        if (player == null) return;
        AnimalAlchemy.EnsureInstalled();
        AnimalDatabase db = AnimalDatabase.Instance;
        foreach (AnimalToolData t in db.AllTools)
        {
            if (t == null) continue;
            string own = AnimalIds.Tool(t.id);
            if (!t.consumed && t.IsItem(own))
            {
                Give(player, AnimalItems.Resolve(own), 1);
                continue;
            }
            if (!t.consumed) continue; // another system's tool (the shovel): its own merchants sell it
            ItemSO supply = FirstItemOf(t.items);
            if (supply != null) Give(player, supply, t.HasTag("bedding") ? 5 : 3);
        }
        foreach (string outcome in CareKitPotions) AnimalAlchemy.MakePotions(player, outcome, 2, 0.6f);
        foreach (AnimalTreatmentData tr in db.AllTreatments)
        {
            if (tr == null || tr.items == null || tr.items.Count == 0) continue;
            string first = tr.items[0];
            if (string.IsNullOrEmpty(first) || !first.StartsWith(IngredientPrefix, StringComparison.Ordinal)) continue;
            ItemSO remedy = FirstItemOf(tr.items);
            if (remedy != null) Give(player, remedy, 2);
        }
        InventoryManager.For(player)?.ShowMessage("An animal keeper's care kit is in your bag.", InventoryFeedback.Kind.Info);
    }

    /// <summary>The item of the first of these keys that resolves (see <see cref="ItemOfKey"/>), or null.</summary>
    public static ItemSO FirstItemOf(IReadOnlyList<string> keys)
    {
        if (keys == null) return null;
        for (int i = 0; i < keys.Count; i++)
        {
            ItemSO item = ItemOfKey(keys[i]);
            if (item != null) return item;
        }
        return null;
    }
}
