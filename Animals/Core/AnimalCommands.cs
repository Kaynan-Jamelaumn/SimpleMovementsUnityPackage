using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>What a player asks to do with animals. Add new kinds at the END (they are sent and remembered as numbers).</summary>
public enum AnimalActionKind
{
    /// <summary>Look at an animal (what is seen of it).</summary>
    Inspect,
    /// <summary>Feed an animal from the hand (the item named).</summary>
    Feed,
    /// <summary>Put food (the item named) in the pen's feeder.</summary>
    StockFeeder,
    /// <summary>Pour water from a container into the pen's trough.</summary>
    FillWater,
    /// <summary>Give an animal a name (the owner only).</summary>
    Rename,
    /// <summary>Let a young animal out of its crate (a livestock item) into the pen: it becomes the player's.</summary>
    Release,
    /// <summary>Bring one of the player's animals (from another pen, or the boarding stable) to live in this pen.</summary>
    Move,
    /// <summary>Clear away the remains of a dead animal (its owner may butcher them instead: <see cref="Butcher"/>).</summary>
    RemoveRemains,
    // Phase 2 (care, welfare and health). Sent and remembered as numbers: add new kinds at the END only.
    /// <summary>
    /// A care action of the animal's species, named by its id in the request's text ("brush", "wash", "pet", "clean_hooves"
    /// or a species' own); the item is an optional supply (soap). No animal named: every animal in the pen that can take it.
    /// </summary>
    Care,
    /// <summary>Muck out the pen with a fork or a shovel (the item: straw for fresh bedding, optional). The text "trough": scrub out its fouled trough.</summary>
    CleanPen,
    /// <summary>Look an animal over carefully: what ails it is named once it shows enough.</summary>
    Examine,
    /// <summary>Give an animal medicine (the item named). No animal named: dose the whole pen (a drench or a remedy in the feed).</summary>
    Treat,
    /// <summary>Send for the veterinarian to see an animal, for the fee the player was shown.</summary>
    CallVet,
    /// <summary>Slaughter an animal (its owner only): its carcass can then be butchered.</summary>
    Slaughter,
    /// <summary>Lay a dead animal out for butchering through Harvesting (its owner only).</summary>
    Butcher,
    /// <summary>An animal's health and care history, and how its last days went (an answer: nothing changes).</summary>
    History,
    // Phase 3 (production).
    /// <summary>
    /// Collect what an animal has made and waits (eggs, milk, wool: the product's id in the request's text; empty: all of
    /// them). No animal named: from every animal in the pen the player may collect from. Milking and shearing need their tool.
    /// </summary>
    Collect,
    // Phase 4 (breeding).
    /// <summary>Breed an animal (the request's animal) with a partner in the same pen (its id in the request's text): both its owner's.</summary>
    Breed,
    // Phase 5 (trading, upgrades, automation).
    /// <summary>Put an animal of one's own in a crate (a livestock item that holds it): to sell it to a merchant or trade it to a player.</summary>
    Crate,
    /// <summary>
    /// Improve the pen (text "level") or fit it with a machine ("hopper", "waterer", "trays"), for the price the player was
    /// shown; "remove:&lt;machine&gt;" takes a machine out.
    /// </summary>
    Upgrade,
    /// <summary>Pay the upkeep the pen owes, and (the request's count) that many game days ahead, for the sum the player was shown.</summary>
    PayUpkeep,
}

/// <summary>What a player may do in a pen (from Housing, where the pen stands on a rented plot; everything on public land).</summary>
[Flags]
public enum AnimalRights
{
    None = 0,
    /// <summary>Look at the pen and its animals.</summary>
    Look = 1,
    /// <summary>Feed, water and stock the pen (care for the animals in it, whoever owns them).</summary>
    Tend = 2,
    /// <summary>Keep animals in it (let new ones in, bring one's own there).</summary>
    Keep = 4,
    /// <summary>Care for other players' animals in it (feed them by hand, clear their remains): a tenant and those they let in.</summary>
    CareForOthers = 8,
    All = Look | Tend | Keep | CareForOthers,
}

/// <summary>A request (sent as JSON on a network client). <see cref="token"/> names it for good: a retry carries the same token and is answered, never done twice.</summary>
[Serializable]
public struct AnimalRequest
{
    public AnimalActionKind kind;
    public string pen;
    /// <summary>The animal it is about (Care and Treat: empty for every animal in the pen).</summary>
    public string animal;
    /// <summary>The item: what to feed, stock or let out; Care: the supply chosen (soap; optional); CleanPen: the straw (optional); Treat: the medicine.</summary>
    public string item;
    /// <summary>How many (StockFeeder; Feed and CleanPen's straw: at most so many; PayUpkeep: game days to pay ahead).</summary>
    public int count;
    /// <summary>Rename and Release: a name; Care: the care action's id; CleanPen: "trough" to scrub out the trough.</summary>
    public string text;
    public string token;
    public int seq;
    /// <summary>
    /// CallVet, Upgrade, PayUpkeep: the price the player was shown (a different price now is refused: nobody pays what they
    /// did not agree to).
    /// </summary>
    public long expected;
    /// <summary>
    /// Release (Phase 5): the crate chosen, by its copy's code; <see cref="AnimalCommands.PlainCrate"/> (1): any crate that
    /// holds no animal of the world; 0: the one in hand, else the first carried. A crated animal is let out only when its
    /// own code is chosen.
    /// </summary>
    public int code;
}

/// <summary>An item a command takes from or gives to the player (the caller does it).</summary>
[Serializable]
public struct AnimalItemChange
{
    public string item;
    public int count;
    /// <summary>One per-copy code per copy given (a product's quality and ledger serial); null: copies with no code.</summary>
    public List<int> codes;
}

/// <summary>What came of a request.</summary>
[Serializable]
public class AnimalResult
{
    public bool ok;
    public int seq;
    public string message = "";
    public List<string> lines = new List<string>();
    [Tooltip("The animal it was about (a new animal's id after a release).")]
    public string animal = "";
    [Tooltip("It was answered from memory: the same request was done before.")]
    public bool repeated;

    public static AnimalResult Fail(string message) => new AnimalResult { ok = false, message = message ?? "" };
    public static AnimalResult Ok(string message, string animal = "") => new AnimalResult { ok = true, message = message ?? "", animal = animal ?? "" };

    public string ToJson() => JsonUtility.ToJson(this);
    public static AnimalResult FromJson(string json) => string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<AnimalResult>(json);
}

/// <summary>Who asks, and what they carry (the runtime fills it from the player and their inventory).</summary>
public sealed class AnimalActor
{
    /// <summary>The player's lasting key.</summary>
    public string key = "";
    public string name = "";
    /// <summary>What they may do in the pen asked about.</summary>
    public AnimalRights rights;
    /// <summary>How many copies of an item (by id) they carry.</summary>
    public Func<string, int> count;
    /// <summary>The litres and kind of water in the container they would pour from (FillWater, a wash).</summary>
    public float waterLitres;
    public int waterKind;
    /// <summary>The best tool the player carries with any of these tags (null: they carry none).</summary>
    public Func<IReadOnlyList<string>, AnimalToolUse> tool;
    /// <summary>The fee the city's veterinarian asks to see this animal; null: there is no veterinarian.</summary>
    public Func<AnimalRecord, AnimalSpeciesData, long> vetFee;

    public int Count(string item) => count != null && !string.IsNullOrEmpty(item) ? Math.Max(0, count(item)) : 0;

    /// <summary>The best tool carried for any of these tags (none asked for, or none carried: <see cref="AnimalToolUse.has"/> is false).</summary>
    public AnimalToolUse Tool(IReadOnlyList<string> tags) => tool != null && tags != null && tags.Count > 0 ? tool(tags) : default(AnimalToolUse);
}

/// <summary>
/// A command checked and ready: what it will take from the player (<see cref="takeItem"/> x <see cref="takeCount"/>,
/// <see cref="takeWater"/> litres, <see cref="fee"/> money). Nothing has changed yet; the caller takes them, then calls
/// <see cref="AnimalCommands.Commit"/> with what it really took. After a successful commit the caller wears the tool
/// (<see cref="wearKey"/>), gives the items in <see cref="give"/> and lays out <see cref="remains"/>: read them after the
/// commit, which sets some of them.
/// </summary>
public sealed class AnimalPlan
{
    public AnimalRequest request;
    public AnimalActor actor;
    /// <summary>Null: it may go ahead. Otherwise the refusal (nothing is taken, nothing changes).</summary>
    public AnimalResult refusal;
    public string takeItem;
    public int takeCount;
    public float takeWater;
    /// <summary>For Inspect, History and other commands that change nothing: the answer is ready.</summary>
    public AnimalResult answer;

    // ---------------------------------------------------------------- Phase 2
    /// <summary>Money the caller takes before the commit (CallVet's fee), and gives back if the commit fails.</summary>
    public long fee;
    /// <summary>The tool to wear AFTER a successful commit (its item key; null: none) and by how many uses (a pen's care: the animals really done).</summary>
    public string wearKey;
    public int wearUses;
    /// <summary>What it is done with (the commit reads its quality and what is left of it).</summary>
    public AnimalToolUse toolUsed;
    /// <summary>More tools to wear after a successful commit, each its item key and uses (Collect: a pail and shears in one go).</summary>
    public List<AnimalItemChange> moreWear = new List<AnimalItemChange>();
    /// <summary>Items to give the player AFTER a successful commit (CleanPen's manure: the commit fills it).</summary>
    public List<AnimalItemChange> give = new List<AnimalItemChange>();
    /// <summary>Butcher: the remains the caller lays out as a carcass after the commit (which changes nothing); it removes the record once they are laid out.</summary>
    public AnimalRemainsReport remains;
    /// <summary>The water the action wanted (a wash: the animal's; a pen's wash: the sum of the animals it will wash).</summary>
    public float plannedLitres;
    /// <summary>The items the action wanted (Treat's doses, CleanPen's straw, Care's supply).</summary>
    public int plannedCount;

    public bool Refused => refusal != null;
    public bool TakesSomething => (!string.IsNullOrEmpty(takeItem) && takeCount > 0) || takeWater > 0f || fee > 0;
}

/// <summary>What the caller really took for a plan: the count, and the quality of each copy (from its code).</summary>
public struct AnimalTaken
{
    public int count;
    public List<float> qualities;
    /// <summary>The raw per-copy codes of what was taken (a livestock crate's seed).</summary>
    public List<int> codes;
    public float litres;
    public int waterKind;
    /// <summary>The money taken for the plan's fee (set after the items and the water were taken).</summary>
    public long paid;

    public float Quality(int i) => qualities != null && i >= 0 && i < qualities.Count ? qualities[i] : AnimalItemCode.UnknownQuality;
    public int Code(int i) => codes != null && i >= 0 && i < codes.Count ? codes[i] : 0;
}

/// <summary>
/// The validated commands of animal husbandry: every change a player makes goes through here, in two steps so that a
/// command that takes something is all or nothing.
/// <list type="number">
/// <item><see cref="Plan"/> checks everything against the present (the pen is brought up to date first): the player's
/// rights, the animal, the item and whether the animal will take it, how many the player has, the room in the feeder or
/// the pen. It changes nothing.</item>
/// <item>The caller takes from the player what the plan says - and only then <see cref="Commit"/> records what was
/// really taken (fewer, if the bag changed meanwhile; nothing taken: nothing done). Should the commit fail the caller
/// gives the items back: no partial use ever stands.</item>
/// </list>
/// The rules never trust a client's word for anything but the choice: what an item is, its quality, the animal's state and
/// the player's rights are the server's.
/// <para>Phase 2 adds care (one animal, or every animal in the pen that can take it), mucking out and scrubbing the trough,
/// a careful look, medicine (one animal, or the whole pen), the veterinarian (for a fee), slaughter and butchering (the
/// owner's only) and the history. The owner may look over, treat, send the vet for and read the history of their own
/// boarded animal from any pen's window. The logic of each is Core's (<see cref="AnimalCare"/>, <see cref="AnimalHabitat"/>,
/// <see cref="AnimalHealth"/>, <see cref="AnimalRemains"/>): here it is checked, and called.</para>
/// </summary>
public static partial class AnimalCommands
{
    /// <summary>The text of a CleanPen request that scrubs out the trough instead of mucking out.</summary>
    public const string TroughText = "trough";

    /// <summary>A Release request's code for any plain crate (one that holds no animal of the world).</summary>
    public const int PlainCrate = 1;

    /// <summary>The tool tags that muck out a pen.</summary>
    public static IReadOnlyList<string> MuckOutTools => MuckTools;

    private static readonly string[] MuckTools = { "fork", "shovel" };
    private const string BeddingTag = "bedding";
    /// <summary>Floor dirt from which there is something to fork out.</summary>
    private const float MuckFrom = 3f;
    /// <summary>Bedding at and above which there is no straw to lay.</summary>
    private const float FreshEnough = 90f;
    private const string NoLonger = "You no longer have it.";
    private const string NoWater = "You no longer have the water for it.";
    /// <summary>What <see cref="AnimalHealth.Treat"/> says when a medicine acts on nothing it has.</summary>
    private const string NoHelp = "It does not seem to help.";

    // ---------------------------------------------------------------- planning
    public static AnimalPlan Plan(AnimalBook book, AnimalRequest r, AnimalActor actor, double now, AnimalSimContext ctx)
    {
        var plan = new AnimalPlan { request = r, actor = actor };
        if (book == null || ctx?.catalog == null) return Refuse(plan, "The animals are not kept here.");
        if (actor == null || string.IsNullOrEmpty(actor.key)) return Refuse(plan, "Nobody to do it.");
        if (!Enum.IsDefined(typeof(AnimalActionKind), r.kind)) return Refuse(plan, "That cannot be done.");
        AnimalRules rules = book.Rules;
        AnimalHerd herd = book.Herd(r.pen);
        if (herd == null) return Refuse(plan, "There is no such pen.");
        if ((actor.rights & AnimalRights.Look) == 0) return Refuse(plan, "Not your animals.");
        book.BringUpToDate(herd, now, ctx);
        AnimalRecord a = string.IsNullOrEmpty(r.animal) ? null : book.Find(r.animal);
        IAnimalCatalog cat = ctx.catalog;

        switch (r.kind)
        {
            case AnimalActionKind.Inspect:
            {
                // Its owner may look at an animal of theirs at the boarding stable from any pen's window.
                a = HereOrBoarded(a, r, actor);
                if (a == null) return Refuse(plan, "That animal is not here.");
                BringBoarded(book, a, now, ctx);
                AnimalSpeciesData sp = cat.Species(a.species);
                plan.answer = AnimalResult.Ok(AnimalSim.NameOf(a, sp), a.id);
                AnimalObservation.Describe(a, sp, cat, now, a.owner == actor.key, plan.answer.lines);
                return plan;
            }
            case AnimalActionKind.Feed:
            {
                if (a == null || a.pen != r.pen) return Refuse(plan, "That animal is not here.");
                if (!a.Alive) return Refuse(plan, "It is dead.");
                if (a.owner != actor.key && (actor.rights & AnimalRights.CareForOthers) == 0) return Refuse(plan, "Not your animal.");
                AnimalFoodData food = cat.FoodOfItem(r.item);
                if (food == null) return Refuse(plan, "That is not food for animals.");
                if (actor.Count(r.item) <= 0) return Refuse(plan, $"You have no {food.DisplayName.ToLowerInvariant()}.");
                AnimalSpeciesData sp = cat.Species(a.species);
                if (sp == null) return Refuse(plan, "It will not eat now.");
                if (a.lastFedAt >= 0.0 && now - a.lastFedAt < rules.handFeedCooldownHours)
                    return Refuse(plan, $"{AnimalSim.NameOf(a, sp)} has just eaten from a hand.");
                FoodAffinity aff = sp.DietAt(a.stage).AffinityOf(food);
                string why = HandRefusal(a, sp, food, aff, rules);
                if (why != null) return Refuse(plan, why);
                // As many items as it will eat (the rest of the last one goes into the feeder): to full, or - harmful or
                // toxic food - only as far as the hunger that makes it take it.
                float appetite = AnimalSim.Appetite(sp, sp.BreedOrDefault(a.breed), a);
                float perPortion = food.satiety / appetite;
                float limit = HandLimit(aff, rules);
                float portionsWanted = perPortion > 0f ? (limit - a.satiety) / perPortion : 1f;
                portionsWanted = Mathf.Clamp(portionsWanted, 0.25f, MaxHandPortions(rules, appetite));
                int items = Math.Max(1, Mathf.CeilToInt(portionsWanted / Mathf.Max(1, food.portionsPerItem) - 0.001f));
                if (r.count > 0) items = Math.Min(items, r.count);
                items = Math.Min(items, actor.Count(r.item));
                plan.takeItem = r.item;
                plan.takeCount = items;
                return plan;
            }
            case AnimalActionKind.StockFeeder:
            {
                if ((actor.rights & AnimalRights.Tend) == 0) return Refuse(plan, "Not your pen.");
                AnimalFoodData food = cat.FoodOfItem(r.item);
                if (food == null) return Refuse(plan, "That is not food for animals.");
                int have = actor.Count(r.item);
                if (have <= 0) return Refuse(plan, $"You have no {food.DisplayName.ToLowerInvariant()}.");
                AnimalPenSpec spec = herd.spec ?? new AnimalPenSpec();
                // The text "hopper" fills the pen's feed hopper instead (Phase 5).
                bool hopper = ToHopper(r);
                if (hopper && !herd.pen.hopper) return Refuse(plan, "There is no feed hopper here.");
                float room = hopper ? AnimalUpgrades.HopperRoom(herd.pen, rules) : Mathf.Max(0f, spec.feederCapacity - herd.pen.FeederPortions);
                int fits = Mathf.FloorToInt(room / Mathf.Max(1, food.portionsPerItem) + 0.0001f);
                if (fits <= 0) return Refuse(plan, hopper ? "The hopper is full." : "The feeder is full.");
                plan.takeItem = r.item;
                plan.takeCount = Math.Min(Math.Min(have, Math.Max(1, r.count)), fits);
                return plan;
            }
            case AnimalActionKind.FillWater:
            {
                // Poured water mixes with what is there (it never empties the trough: scrubbing it out does, see CleanPen).
                if ((actor.rights & AnimalRights.Tend) == 0) return Refuse(plan, "Not your pen.");
                AnimalPenSpec spec = herd.spec ?? new AnimalPenSpec();
                float room = Mathf.Max(0f, spec.waterCapacity - herd.pen.water);
                if (room < 0.05f) return Refuse(plan, "The trough is full.");
                if (actor.waterLitres < 0.05f) return Refuse(plan, "You carry no water.");
                plan.takeWater = Mathf.Min(room, actor.waterLitres);
                // Nor may a stranger on public land make the water already there murky, or murky water any dirtier, however
                // little at a time: water dirtier than the trough's is refused once the trough would be murky (a dry trough
                // takes any water: it is better than none).
                if (!MayChangeWater(herd, actor) && herd.pen.water > 0.05f)
                {
                    float before = herd.pen.water, poured = AnimalHealth.KindDirt(actor.waterKind);
                    float after = (before * herd.pen.waterDirt + plan.takeWater * poured) / (before + plan.takeWater);
                    if (after >= rules.tipOutAbove && poured > herd.pen.waterDirt + 0.001f) return Refuse(plan, "Only a keeper of an animal here may pour such water into it.");
                }
                return plan;
            }
            case AnimalActionKind.Rename:
            {
                if (a == null || a.pen != r.pen) return Refuse(plan, "That animal is not here.");
                if (a.owner != actor.key) return Refuse(plan, "Only its owner names it.");
                string name = CleanName(r.text, rules.maxNameLength);
                if (name == null) return Refuse(plan, "That is not a name.");
                return plan;
            }
            case AnimalActionKind.Release:
            {
                if ((actor.rights & AnimalRights.Keep) == 0) return Refuse(plan, "You cannot keep animals in this pen.");
                if (!LivestockOf(cat, r.item, out AnimalSpeciesData sp, out AnimalBreedData _)) return Refuse(plan, "That is not an animal.");
                if (actor.Count(r.item) <= 0) return Refuse(plan, "You do not have it.");
                string why = RoomFor(book, herd, sp, null);
                if (why != null) return Refuse(plan, why);
                if (book.LivingOwnedBy(actor.key) + AnimalBreeding.ExpectedFor(book, actor.key) >= rules.maxAnimalsPerPlayer)
                    return Refuse(plan, $"You keep as many animals as you can look after ({rules.maxAnimalsPerPlayer}, young on the way counted).");
                if (!string.IsNullOrEmpty(r.text) && CleanName(r.text, rules.maxNameLength) == null) return Refuse(plan, "That is not a name.");
                plan.takeItem = r.item;
                plan.takeCount = 1;
                return plan;
            }
            case AnimalActionKind.Move:
            {
                if (a == null) return Refuse(plan, "There is no such animal.");
                if (a.owner != actor.key) return Refuse(plan, "Only its owner moves it.");
                // Brought up to the present first (where it lives now), so nothing the commit does steps the simulation.
                if (a.pen != r.pen) book.BringUpToDate(a, now, ctx);
                if (!a.Alive) return Refuse(plan, "It is dead.");
                if (a.pen == r.pen) return Refuse(plan, "It lives here already.");
                if ((actor.rights & AnimalRights.Keep) == 0) return Refuse(plan, "You cannot keep animals in this pen.");
                AnimalSpeciesData sp = cat.Species(a.species);
                if (sp == null) return Refuse(plan, "It cannot be moved now.");
                string why = RoomFor(book, herd, sp, a);
                if (why != null) return Refuse(plan, why);
                return plan;
            }
            case AnimalActionKind.RemoveRemains:
            {
                // The remains of an animal that died at the boarding stable are its owner's to clear, from any pen's window.
                bool boardedRemains = a != null && string.IsNullOrEmpty(a.pen) && a.owner == actor.key;
                if (a == null || (a.pen != r.pen && !boardedRemains)) return Refuse(plan, "That animal is not here.");
                if (a.Alive) return Refuse(plan, "It is alive.");
                if (a.owner != actor.key && (actor.rights & AnimalRights.CareForOthers) == 0) return Refuse(plan, "Not your animal.");
                return plan;
            }

            // ------------------------------------------------------------ Phase 2
            case AnimalActionKind.Care:
                return string.IsNullOrEmpty(r.animal) ? PlanHerdCare(herd, plan, r, actor, now, cat) : PlanCare(a, plan, r, actor, now, cat);
            case AnimalActionKind.CleanPen:
                return PlanClean(herd, plan, r, actor, now, rules, cat);
            case AnimalActionKind.Examine:
            {
                a = HereOrBoarded(a, r, actor);
                if (a == null) return Refuse(plan, "That animal is not here.");
                BringBoarded(book, a, now, ctx);
                if (!a.Alive) return Refuse(plan, "It is dead.");
                if (!MayCare(a, actor)) return Refuse(plan, "Not your animal.");
                AnimalSpeciesData sp = cat.Species(a.species);
                if (a.lastExaminedAt >= 0.0 && now - a.lastExaminedAt < rules.examineCooldownHours)
                    return Refuse(plan, $"You have just looked {Called(a, sp, false)} over.");
                return plan;
            }
            case AnimalActionKind.Treat:
                return string.IsNullOrEmpty(r.animal) ? PlanHerdTreat(herd, plan, r, actor, now, cat) : PlanTreat(book, a, plan, r, actor, now, ctx, cat);
            case AnimalActionKind.CallVet:
            {
                a = HereOrBoarded(a, r, actor);
                if (a == null) return Refuse(plan, "That animal is not here.");
                BringBoarded(book, a, now, ctx);
                if (!a.Alive) return Refuse(plan, "It is dead.");
                if (!MayCare(a, actor)) return Refuse(plan, "Not your animal.");
                if (actor.vetFee == null) return Refuse(plan, "There is no veterinarian in the city.");
                AnimalSpeciesData sp = cat.Species(a.species);
                if (sp == null) return Refuse(plan, "The veterinarian cannot see it now.");
                if (a.lastVetAt >= 0.0 && now - a.lastVetAt < rules.vetCooldownHours)
                    return Refuse(plan, $"The veterinarian saw {Called(a, sp, false)} {AnimalObservation.Age(now - a.lastVetAt)} ago.");
                long fee = Math.Max(0L, actor.vetFee(a, sp));
                // The fee is the one the player saw, or nothing is done (the window shows the new one).
                if (r.expected != fee) return Refuse(plan, $"The veterinarian asks {fee} now.");
                plan.fee = fee;
                return plan;
            }
            case AnimalActionKind.Slaughter:
            {
                if (!rules.allowSlaughter) return Refuse(plan, "Animals are not slaughtered here.");
                if (a == null || a.pen != r.pen) return Refuse(plan, "That animal is not here.");
                if (!a.Alive) return Refuse(plan, "It is dead.");
                if (a.owner != actor.key) return Refuse(plan, "Only its owner may do that.");
                return plan;
            }
            case AnimalActionKind.Butcher:
            {
                // Its owner's only (a carer may still clear the remains); the remains of one that died at the boarding stable
                // are laid out at a pen its owner keeps animals in.
                bool boardedRemains = a != null && string.IsNullOrEmpty(a.pen) && a.owner == actor.key;
                if (a == null || (a.pen != r.pen && !boardedRemains)) return Refuse(plan, "That animal is not here.");
                if (a.Alive) return Refuse(plan, "It is alive.");
                if (a.owner != actor.key) return Refuse(plan, "Only its owner may butcher it.");
                if (boardedRemains && (actor.rights & AnimalRights.Keep) == 0) return Refuse(plan, "Lay it out at a pen where you keep animals.");
                AnimalSpeciesData sp = cat.Species(a.species);
                if (sp == null || string.IsNullOrEmpty(sp.harvestAnatomy)) return Refuse(plan, "There is nothing to butcher: clear the remains instead.");
                plan.remains = AnimalRemains.Report(a, sp, cat, now, rules);
                return plan;
            }
            case AnimalActionKind.Collect:
                return string.IsNullOrEmpty(r.animal) ? PlanHerdCollect(herd, plan, r, actor, cat) : PlanCollect(book, a, plan, r, actor, now, ctx, cat);
            case AnimalActionKind.Breed:
                return PlanBreed(book, herd, a, plan, r, actor, now, rules, cat);
            case AnimalActionKind.Crate:
                return PlanCrate(book, herd, a, plan, r, actor, rules, cat);
            case AnimalActionKind.Upgrade:
                return PlanUpgrade(herd, plan, r, actor, rules);
            case AnimalActionKind.PayUpkeep:
                return PlanPayUpkeep(herd, plan, r, actor, rules);
            case AnimalActionKind.History:
            {
                // Its owner reads the history of an animal of theirs wherever it is; a carer that of one in this pen.
                if (a == null || (a.pen != r.pen && a.owner != actor.key)) return Refuse(plan, "That animal is not here.");
                if (!MayCare(a, actor)) return Refuse(plan, "Not your animal.");
                AnimalSpeciesData sp = cat.Species(a.species);
                plan.answer = AnimalResult.Ok($"The history of {Called(a, sp, false)}.", a.id);
                AnimalObservation.History(a, rules, now, plan.answer.lines, sp);
                return plan;
            }
        }
        return Refuse(plan, "That cannot be done.");
    }

    private static AnimalPlan Refuse(AnimalPlan plan, string why)
    {
        plan.refusal = AnimalResult.Fail(why);
        return plan;
    }

    /// <summary>The satiety up to which an animal takes a food of this regard from a hand.</summary>
    public static float HandLimit(FoodAffinity aff, AnimalRules rules) =>
        aff == FoodAffinity.Toxic ? rules.toxicFromHandBelow : aff == FoodAffinity.Harmful ? rules.harmfulFromHandBelow : rules.fullAt;

    /// <summary>Most portions an animal takes from a hand at once (the rules' number is for an average animal: a cow takes more, a hen less).</summary>
    public static float MaxHandPortions(AnimalRules rules, float appetite) => Mathf.Max(0.25f, rules.maxHandPortions * Mathf.Max(0.01f, appetite));

    /// <summary>Why an animal will not take a food from a hand now (null: it will).</summary>
    public static string HandRefusal(AnimalRecord a, AnimalSpeciesData sp, AnimalFoodData food, FoodAffinity aff, AnimalRules rules)
    {
        string who = AnimalSim.NameOf(a, sp);
        string what = food.DisplayName.ToLowerInvariant();
        switch (aff)
        {
            case FoodAffinity.Inedible: return $"{who} sniffs the {what} and turns away.";
            case FoodAffinity.Toxic:
                if (a.satiety >= rules.toxicFromHandBelow) return $"{who} sniffs the {what} warily and will not touch it.";
                break;
            case FoodAffinity.Harmful:
                if (a.satiety >= rules.harmfulFromHandBelow) return $"{who} noses the {what} and leaves it.";
                break;
        }
        if (a.satiety >= rules.fullAt - 1f) return $"{who} is not hungry.";
        return null;
    }

    /// <summary>Why a pen has no room for an animal of a species (null: it has).</summary>
    public static string RoomFor(AnimalBook book, AnimalHerd herd, AnimalSpeciesData sp, AnimalRecord moving)
    {
        AnimalPenSpec spec = herd.spec ?? new AnimalPenSpec();
        if (sp.habitat == null || !sp.habitat.Accepts(spec.kind))
            return $"{sp.Plural} cannot live in a {spec.kind.ToString().ToLowerInvariant()}.";
        // A mother moved brings her young on the way; the young on the way to the pen's own mothers are counted too.
        int coming = moving != null && AnimalBreeding.Expecting(moving) ? Math.Max(1, moving.pregnancy.litter) : 0;
        // Its level's room counts (Phase 5): an improved pen takes more.
        AnimalRules rules = book.Rules;
        int capacity = AnimalUpgrades.Capacity(herd.pen, spec, rules);
        if (capacity > 0)
        {
            if (book.LivingInPen(herd.pen.id) + AnimalBreeding.ExpectedIn(herd) + coming >= capacity) return "The pen is full.";
            return null;
        }
        // Room is judged against adults: a young animal grows into its space.
        float need = AnimalBook.SpaceOf(sp, moving) * (1 + coming);
        float area = AnimalUpgrades.Area(herd.pen, spec, rules);
        if (book.SpaceUsed(herd.pen.id) + AnimalBreeding.ExpectedSpace(book, herd) + need > area + 0.01f)
            return $"The pen is too small for another {sp.DisplayName.ToLowerInvariant()} ({area:0} m², each needs {sp.habitat.areaPerAnimal:0.#} m²).";
        return null;
    }

    /// <summary>
    /// A name made safe: no markup ('&lt;', '&gt;'), control characters, backslash, line or paragraph separators, invisible
    /// format characters (but the joiners emoji and some scripts need, and the tags of a tag flag) or half of a character
    /// made of two; nothing blank, invisible or a lone accent at its start, and no space, blank letter or joiner at its end
    /// (but a joiner after a virama, as in a Malayalam chillu or a Devanagari half form); at most <paramref name="max"/>
    /// UTF-16 characters, cut only where a whole character ends (with its accents and marks, every part of a joined emoji,
    /// a flag, a Hangul syllable, a Thai or Lao vowel AM, a halfwidth katakana sound mark). Null when nothing visible is
    /// left (letters that show as nothing, such as the Hangul filler, count as nothing).
    /// </summary>
    public static string CleanName(string text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        max = Math.Max(1, max);
        // What is kept, each character whole (a lone half of a pair is dropped); nothing blank, invisible or a lone mark leads.
        var kept = new StringBuilder(text.Length);
        int last = -1;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsControl(c) || c == '<' || c == '>' || c == '\\' || c == '\u2028' || c == '\u2029') continue;
            bool pair = char.IsHighSurrogate(c);
            if (pair && (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))) continue;
            if (!pair && char.IsLowSurrogate(c)) continue;
            int cp = pair ? char.ConvertToUtf32(c, text[i + 1]) : c;
            UnicodeCategory kind = CharUnicodeInfo.GetUnicodeCategory(text, i);
            // Of the format characters only the joiners, and the tags of a tag flag (after its black flag or a tag of it).
            bool tag = cp >= 0xE0020 && cp <= 0xE007F;
            bool keep = kind != UnicodeCategory.Format || cp == 0x200D || cp == 0x200C || (tag && (last == 0x1F3F4 || (last >= 0xE0020 && last < 0xE007F)));
            bool leading = kept.Length == 0 && (char.IsWhiteSpace(c) || kind == UnicodeCategory.Format || Mark(kind) || Blank(cp));
            if (keep && !leading)
            {
                kept.Append(c);
                if (pair) kept.Append(text[i + 1]);
                last = cp;
            }
            if (pair) i++;
        }
        string s = kept.ToString();
        if (s.Length > max)
        {
            // Cut before the character the cap falls in: never between two halves, nor off its accents or joined parts.
            int cut = char.IsLowSurrogate(s[max]) ? max - 1 : max;
            while (cut > 0 && JoinsBefore(s, cut)) cut = Before(s, cut);
            s = s.Substring(0, cut);
        }
        // Nor does a space or a blank letter end it, nor a joiner but after a virama.
        int end = s.Length;
        while (end > 0 && (char.IsWhiteSpace(s[end - 1]) || Blank(s[end - 1]) ||
                           (Joiner(s[end - 1]) && !(end >= 2 && CharUnicodeInfo.GetUnicodeCategory(s[end - 2]) == UnicodeCategory.NonSpacingMark))))
            end--;
        s = s.Substring(0, end);
        return Visible(s) ? s : null;
    }

    private static bool Joiner(char c) => c == '\u200D' || c == '\u200C';

    private static bool Mark(UnicodeCategory kind) =>
        kind == UnicodeCategory.NonSpacingMark || kind == UnicodeCategory.SpacingCombiningMark || kind == UnicodeCategory.EnclosingMark;

    /// <summary>Letters that show as nothing: the Hangul fillers and the blank Braille pattern.</summary>
    private static bool Blank(int cp) => cp == 0x3164 || cp == 0x115F || cp == 0x1160 || cp == 0xFFA0 || cp == 0x2800;

    /// <summary>Has a name something to see: a character that is not a space, a blank letter, a mark or a format character?</summary>
    private static bool Visible(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsLowSurrogate(s[i]) || char.IsWhiteSpace(s[i]) || Blank(s[i])) continue;
            UnicodeCategory kind = CharUnicodeInfo.GetUnicodeCategory(s, i);
            if (!Mark(kind) && kind != UnicodeCategory.Format) return true;
        }
        return false;
    }

    /// <summary>
    /// Is the code point starting at <paramref name="i"/> part of the character before it: an accent or other mark, a
    /// zero-width joiner or what it joins, a variation or skin-tone mark, a tag, the second letter of a flag, a letter of
    /// the same Hangul syllable, Thai or Lao SARA AM, a halfwidth katakana sound mark, or what follows a prepended sign?
    /// </summary>
    private static bool JoinsBefore(string s, int i)
    {
        if (i <= 0 || i >= s.Length) return false;
        int prevAt = Before(s, i);
        int cp = char.ConvertToUtf32(s, i), prev = char.ConvertToUtf32(s, prevAt);
        if (cp == 0x200D || prev == 0x200D) return true;
        if ((cp >= 0xFE00 && cp <= 0xFE0F) || (cp >= 0x1F3FB && cp <= 0x1F3FF) || (cp >= 0xE0020 && cp <= 0xE007F) || (cp >= 0xE0100 && cp <= 0xE01EF)) return true;
        if (cp == 0x0E33 || cp == 0x0EB3 || cp == 0xFF9E || cp == 0xFF9F || Prepend(prev) || HangulJoins(prev, cp)) return true;
        UnicodeCategory kind = CharUnicodeInfo.GetUnicodeCategory(s, i);
        if (kind == UnicodeCategory.NonSpacingMark || kind == UnicodeCategory.SpacingCombiningMark || kind == UnicodeCategory.EnclosingMark) return true;
        if (!Regional(cp) || !Regional(prev)) return false;
        // A flag is two regional letters: this is a second one when an odd number of them come just before it.
        int run = 0;
        for (int j = prevAt; j >= 0 && Regional(char.ConvertToUtf32(s, j)); j = Before(s, j)) run++;
        return run % 2 == 1;
    }

    private static bool Regional(int cp) => cp >= 0x1F1E6 && cp <= 0x1F1FF;

    /// <summary>A sign written before what it belongs to (Unicode's Prepend: Malayalam dot reph, some Brahmic script signs).</summary>
    private static bool Prepend(int c) => c == 0x0D4E || c == 0x111C2 || c == 0x111C3 || c == 0x1193F || c == 0x11941 || c == 0x11A3A ||
        (c >= 0x11A84 && c <= 0x11A89) || c == 0x11D46 || c == 0x11F02;

    /// <summary>Do two code points belong to one Hangul syllable (a leading, vowel or trailing letter, or a syllable they build on)?</summary>
    private static bool HangulJoins(int prev, int cp)
    {
        bool L(int c) => (c >= 0x1100 && c <= 0x115F) || (c >= 0xA960 && c <= 0xA97C);
        bool V(int c) => (c >= 0x1160 && c <= 0x11A7) || (c >= 0xD7B0 && c <= 0xD7C6);
        bool T(int c) => (c >= 0x11A8 && c <= 0x11FF) || (c >= 0xD7CB && c <= 0xD7FB);
        bool LV(int c) => c >= 0xAC00 && c <= 0xD7A3 && (c - 0xAC00) % 28 == 0;
        bool LVT(int c) => c >= 0xAC00 && c <= 0xD7A3 && (c - 0xAC00) % 28 != 0;
        return (L(prev) && (L(cp) || V(cp) || LV(cp) || LVT(cp))) || ((LV(prev) || V(prev)) && (V(cp) || T(cp))) || ((LVT(prev) || T(prev)) && T(cp));
    }

    /// <summary>Where the code point before the one starting at <paramref name="i"/> starts (-1: none).</summary>
    private static int Before(string s, int i) => i >= 2 && char.IsLowSurrogate(s[i - 1]) && char.IsHighSurrogate(s[i - 2]) ? i - 2 : i - 1;

    /// <summary>The species and breed of a livestock item id ("animal:livestock/&lt;species&gt;/&lt;breed&gt;").</summary>
    public static bool LivestockOf(IAnimalCatalog cat, string itemId, out AnimalSpeciesData sp, out AnimalBreedData breed)
    {
        sp = null;
        breed = null;
        if (!AnimalIds.ParseLivestock(itemId, out string species, out string breedId)) return false;
        sp = cat.Species(species);
        if (sp == null) return false;
        breed = sp.BreedOrDefault(breedId);
        return true;
    }

    // ---------------------------------------------------------------- planning: care
    /// <summary>
    /// One care action on one animal: it is here and alive, the actor may care for it, its species takes the action, it can
    /// take it now (age, not done lately), the tool it needs is carried (only when it needs one), enough water is carried
    /// (at least <see cref="AnimalCare.LeastWater"/> of what it wants) and the supply chosen is one it uses.
    /// </summary>
    private static AnimalPlan PlanCare(AnimalRecord a, AnimalPlan plan, AnimalRequest r, AnimalActor actor, double now, IAnimalCatalog cat)
    {
        if (a == null || a.pen != r.pen)
        {
            // Its owner's animal at the boarding stable: the stable grooms it.
            bool boarded = a != null && a.Alive && string.IsNullOrEmpty(a.pen) && a.owner == actor.key;
            return Refuse(plan, boarded ? "The boarding stable sees to it." : "That animal is not here.");
        }
        if (!a.Alive) return Refuse(plan, "It is dead.");
        if (!MayCare(a, actor)) return Refuse(plan, "Not your animal.");
        AnimalSpeciesData sp = cat.Species(a.species);
        if (sp == null) return Refuse(plan, "It cannot be cared for now.");
        AnimalCareActionSpec spec = sp.CareAction(CareId(r.text));
        if (!AnimalCare.CanDo(a, sp, spec, now, out string why)) return Refuse(plan, why);
        if (spec.NeedsTool)
        {
            AnimalToolUse use = actor.Tool(spec.tools);
            if (!use.has) return Refuse(plan, $"You need {AnimalCare.ToolNames(cat, spec.tools)} for that.");
            plan.toolUsed = use;
            plan.wearKey = use.key;
            plan.wearUses = 1;
        }
        float litres = AnimalCare.WaterFor(spec, sp, a);
        if (litres > 0f)
        {
            if (!AnimalCare.EnoughWater(litres, actor.waterLitres)) return Refuse(plan, $"You need about {Litres(litres)} of water.");
            plan.takeWater = Mathf.Min(litres, actor.waterLitres);
            plan.plannedLitres = litres;
        }
        if (!string.IsNullOrEmpty(r.item))
        {
            AnimalToolData supply = cat.ToolOfItem(r.item);
            if (!Supplies(supply, spec)) return Refuse(plan, "That is not used for this.");
            if (actor.Count(r.item) < 1) return Refuse(plan, $"You have no {supply.DisplayName.ToLowerInvariant()}.");
            plan.takeItem = r.item;
            plan.takeCount = 1;
            plan.plannedCount = 1;
        }
        return plan;
    }

    /// <summary>
    /// One care action on every animal of the pen that can take it now (see <see cref="WalkHerdCare"/>): the water is the sum
    /// of theirs (as far as the container carried goes), the tool is worn once per animal done, one supply goes to each that
    /// uses it (as far as they go).
    /// </summary>
    private static AnimalPlan PlanHerdCare(AnimalHerd herd, AnimalPlan plan, AnimalRequest r, AnimalActor actor, double now, IAnimalCatalog cat)
    {
        AnimalToolData supply = null;
        int carried = 0;
        if (!string.IsNullOrEmpty(r.item))
        {
            supply = cat.ToolOfItem(r.item);
            if (supply == null || !supply.consumed) return Refuse(plan, "That is not used for this.");
            carried = actor.Count(r.item);
            if (carried <= 0) return Refuse(plan, $"You have no {supply.DisplayName.ToLowerInvariant()}.");
        }
        HerdCare h = WalkHerdCare(herd, CareId(r.text), actor, now, cat, true, default(AnimalToolUse), actor.waterLitres, supply, carried);
        if (h.targets.Count == 0) return Refuse(plan, HerdRefusal(h, cat));
        if (supply != null)
        {
            if (h.supplyWanted == 0) return Refuse(plan, "That is not used for this.");
            plan.takeItem = r.item;
            plan.takeCount = Math.Min(h.supplyWanted, carried);
            plan.plannedCount = plan.takeCount;
        }
        int withTool = h.WithTool;
        if (withTool > 0)
        {
            plan.toolUsed = h.tool;
            plan.wearKey = h.tool.key;
            plan.wearUses = withTool;
        }
        if (h.used > 0f)
        {
            plan.takeWater = h.used;
            plan.plannedLitres = h.need;
        }
        return plan;
    }

    /// <summary>What one care action comes to over a pen: the animals it is done to (in id order) and why the others are not.</summary>
    private sealed class HerdCare
    {
        public readonly List<CareTarget> targets = new List<CareTarget>();
        /// <summary>Living animals: not the actor's to care for, of a species that does not take it, done lately, too young, the tool does not suit, the water ran out.</summary>
        public int others, lacking, lately, young, noTool, dry;
        /// <summary>What was done, in words ("brushed"), from the first animal that takes it.</summary>
        public string verb = "";
        public AnimalToolUse tool;
        /// <summary>The tags of the first animal the tool did not suit (for the refusal).</summary>
        public List<string> toolTags;
        /// <summary>Litres given and wanted by the animals done, and the want of the first one the water did not reach.</summary>
        public float used, need, dryNeed;
        /// <summary>Animals done that use the supply chosen, and of them those that get one.</summary>
        public int supplyWanted, supplied;

        public int WithTool
        {
            get
            {
                int n = 0;
                foreach (CareTarget t in targets) if (t.tool) n++;
                return n;
            }
        }
    }

    /// <summary>One animal a pen's care is done to: with the tool or not, its share of the water, a supply or not.</summary>
    private struct CareTarget
    {
        public AnimalRecord animal;
        public AnimalSpeciesData species;
        public AnimalCareActionSpec spec;
        /// <summary>The litres it wants, and those it gets (0: it takes no water).</summary>
        public float need, litres;
        public bool tool, supplied;
    }

    /// <summary>
    /// Which animals of a pen a care action would be done to, in id order (the herd's order): the living ones the actor may
    /// care for, whose species takes the action and that can take it now (age, not done lately); those that need a tool
    /// only if the tool suits them; those that want water while it lasts (each its own want, and at least the least share of
    /// it). <paramref name="pick"/> (a plan): the tool is chosen from what the actor carries, for the first animal that needs
    /// one; otherwise (a commit) <paramref name="tool"/> is the plan's. The first <paramref name="supplies"/> animals that
    /// use <paramref name="supply"/> get one. Changes nothing.
    /// </summary>
    private static HerdCare WalkHerdCare(AnimalHerd herd, string id, AnimalActor actor, double now, IAnimalCatalog cat, bool pick, AnimalToolUse tool, float water,
        AnimalToolData supply, int supplies)
    {
        var h = new HerdCare { tool = tool };
        bool chosen = !pick;
        float left = Mathf.Max(0f, water);
        foreach (AnimalRecord a in herd.animals)
        {
            if (a == null || !a.Alive) continue;
            if (!MayCare(a, actor))
            {
                h.others++;
                continue;
            }
            AnimalSpeciesData sp = cat.Species(a.species);
            AnimalCareActionSpec spec = sp?.CareAction(id);
            if (spec == null)
            {
                h.lacking++;
                continue;
            }
            if (h.verb.Length == 0) h.verb = spec.PastVerb;
            if (!AnimalCare.CanDo(a, sp, spec, now, out _))
            {
                if (a.stage < spec.minStage) h.young++;
                else h.lately++;
                continue;
            }
            bool withTool = false;
            if (spec.NeedsTool)
            {
                if (!chosen)
                {
                    AnimalToolUse use = actor.Tool(spec.tools);
                    if (use.has)
                    {
                        h.tool = use;
                        chosen = true;
                    }
                }
                if (!Fits(cat, h.tool, spec.tools))
                {
                    h.noTool++;
                    h.toolTags ??= spec.tools;
                    continue;
                }
                withTool = true;
            }
            float need = AnimalCare.WaterFor(spec, sp, a), give = 0f;
            if (need > 0f)
            {
                give = Mathf.Min(need, left);
                if (!AnimalCare.EnoughWater(need, give))
                {
                    h.dry++;
                    if (h.dryNeed <= 0f) h.dryNeed = need;
                    continue;
                }
                left -= give;
                h.used += give;
                h.need += need;
            }
            bool supplied = false;
            if (supply != null && Supplies(supply, spec))
            {
                h.supplyWanted++;
                if (h.supplied < supplies)
                {
                    supplied = true;
                    h.supplied++;
                }
            }
            h.targets.Add(new CareTarget { animal = a, species = sp, spec = spec, need = need, litres = give, tool = withTool, supplied = supplied });
        }
        return h;
    }

    /// <summary>Why a pen's care is done to none of its animals.</summary>
    private static string HerdRefusal(HerdCare h, IAnimalCatalog cat)
    {
        if (h.noTool > 0) return $"You need {AnimalCare.ToolNames(cat, h.toolTags)} for that.";
        if (h.dry > 0) return $"You need about {Litres(h.dryNeed)} of water.";
        if (h.lately > 0 && h.young == 0) return $"They have all been {h.verb} lately.";
        if (h.young > 0 && h.lately == 0) return "They are too young for that.";
        if (h.lately > 0) return "None of them can take it now.";
        if (h.lacking > 0) return "None of them need that.";
        if (h.others > 0) return "None of these animals are yours to care for.";
        return "No animals live here.";
    }

    // ---------------------------------------------------------------- planning: the pen
    /// <summary>
    /// Mucking out: the actor tends the pen and carries a fork or a shovel, it was not mucked out just now, and there is
    /// something to do (dirt to fork out, or straw chosen for a bedded pen whose bedding is not fresh, and not laid lately).
    /// With a clean floor it says what is missing: nothing ("The pen is clean."), the wait for straw, or the straw itself.
    /// The straw: as much as makes the bedding fresh, at most what is carried and what was asked for. The text "trough"
    /// scrubs out a fouled trough instead (no tool; on land nobody rents only a keeper of an animal in the pen may).
    /// </summary>
    private static AnimalPlan PlanClean(AnimalHerd herd, AnimalPlan plan, AnimalRequest r, AnimalActor actor, double now, AnimalRules rules, IAnimalCatalog cat)
    {
        if ((actor.rights & AnimalRights.Tend) == 0) return Refuse(plan, "Not your pen.");
        AnimalPenRecord pen = herd.pen;
        AnimalPenSpec spec = herd.spec ?? pen.spec ?? new AnimalPenSpec();
        if (IsTrough(r.text))
        {
            // Changing the water empties the trough, so a stranger on public land may not.
            if (!MayChangeWater(herd, actor)) return Refuse(plan, "Only a keeper of an animal here may change its water.");
            if (pen.water <= 0.0001f) return Refuse(plan, "The trough is empty.");
            if (!AnimalHabitat.CanScrub(pen, rules)) return Refuse(plan, "The water is clear: it does not need changing.");
            return plan;
        }
        AnimalToolUse use = actor.Tool(MuckTools);
        if (!use.has) return Refuse(plan, $"You need {AnimalCare.ToolNames(cat, MuckTools)} to muck out.");
        if (pen.lastCleanedAt >= 0.0 && now - pen.lastCleanedAt < rules.cleanCooldownHours) return Refuse(plan, "You have just mucked it out.");
        float bedding = pen.bedding >= 0f ? pen.bedding : spec.UsesBedding ? rules.freshBedding : 0f;
        int straw = 0;
        if (!string.IsNullOrEmpty(r.item))
        {
            AnimalToolData t = cat.ToolOfItem(r.item);
            if (t == null || !t.consumed || !t.HasTag(BeddingTag)) return Refuse(plan, "That is not bedding.");
            if (!spec.UsesBedding) return Refuse(plan, "This pen takes no bedding.");
            int have = actor.Count(r.item);
            if (have <= 0) return Refuse(plan, $"You have no {t.DisplayName.ToLowerInvariant()}.");
            int needed = AnimalHabitat.StrawNeeded(pen, spec, rules);
            straw = Math.Min(Math.Min(needed, have), r.count > 0 ? r.count : needed);
        }
        if (pen.dirt < MuckFrom)
        {
            // Nothing to fork out: only fresh straw is left to lay (a bedded pen whose bedding is not fresh), not again so
            // soon, and only with straw brought (the window's straw switch beside Muck Out, shown while straw is carried).
            if (!spec.UsesBedding || bedding >= FreshEnough) return Refuse(plan, "The pen is clean.");
            if (pen.lastBeddedAt >= 0.0 && now - pen.lastBeddedAt < rules.beddingCooldownHours)
                return Refuse(plan, $"Fresh straw was laid {AnimalObservation.Age(now - pen.lastBeddedAt)} ago.");
            if (straw <= 0) return Refuse(plan, "The floor is clean: carry straw and tick it beside Muck out to lay fresh bedding.");
        }
        plan.toolUsed = use;
        plan.wearKey = use.key;
        plan.wearUses = AnimalHabitat.CleanWear(spec);
        if (straw > 0)
        {
            plan.takeItem = r.item;
            plan.takeCount = straw;
            plan.plannedCount = straw;
        }
        return plan;
    }

    // ---------------------------------------------------------------- planning: medicine
    /// <summary>Medicine for one animal (here, or its owner's at the boarding stable): as many items as its doses take (a cow more than a hen).</summary>
    private static AnimalPlan PlanTreat(AnimalBook book, AnimalRecord a, AnimalPlan plan, AnimalRequest r, AnimalActor actor, double now, AnimalSimContext ctx, IAnimalCatalog cat)
    {
        a = HereOrBoarded(a, r, actor);
        if (a == null) return Refuse(plan, "That animal is not here.");
        BringBoarded(book, a, now, ctx);
        if (!a.Alive) return Refuse(plan, "It is dead.");
        if (!MayCare(a, actor)) return Refuse(plan, "Not your animal.");
        AnimalTreatmentData tr = cat.TreatmentOfItem(r.item);
        if (tr == null) return Refuse(plan, "That is not a medicine for animals.");
        AnimalSpeciesData sp = cat.Species(a.species);
        if (sp == null) return Refuse(plan, "It cannot be treated now.");
        // Given again too soon, a medicine that does no harm would do nothing at all: it is not given (one that harms is: an overdose).
        double wait = AnimalHealth.WaitBefore(a, tr, now);
        if (wait > 0.0 && tr.overdoseShare <= 0f)
            return Refuse(plan, $"Too soon for another dose: {AnimalSim.NameIn(a, sp)} can have the {tr.DisplayName.ToLowerInvariant()} again in {AnimalObservation.Wait(wait)}.");
        int needed = AnimalHealth.ItemsFor(tr, AnimalHealth.DosesFor(sp, a));
        int have = actor.Count(r.item);
        if (have < needed) return Refuse(plan, $"{AnimalSim.NameOf(a, sp)} needs {needed} of it (you have {have}).");
        plan.takeItem = r.item;
        plan.takeCount = needed;
        plan.plannedCount = needed;
        return plan;
    }

    /// <summary>
    /// Medicine for every animal in the pen the actor may care for: a drench or a remedy in the feed (a wash or a salve is
    /// put on one animal at a time); the items are the sum of their doses. One that had it too lately (within its interval)
    /// is left out, and when that is every one, it is refused with the wait.
    /// </summary>
    private static AnimalPlan PlanHerdTreat(AnimalHerd herd, AnimalPlan plan, AnimalRequest r, AnimalActor actor, double now, IAnimalCatalog cat)
    {
        AnimalTreatmentData tr = cat.TreatmentOfItem(r.item);
        if (tr == null) return Refuse(plan, "That is not a medicine for animals.");
        if (tr.route == AnimalTreatmentRoute.Topical) return Refuse(plan, $"{tr.DisplayName} is put on one animal at a time.");
        float doses = 0f;
        int living = 0, mine = 0, tooSoon = 0;
        double soonest = double.MaxValue;
        foreach (AnimalRecord m in herd.animals)
        {
            if (m == null || !m.Alive) continue;
            living++;
            if (!MayCare(m, actor)) continue;
            AnimalSpeciesData sp = cat.Species(m.species);
            if (sp == null) continue;
            double wait = AnimalHealth.WaitBefore(m, tr, now);
            if (wait > 0.0)
            {
                tooSoon++;
                soonest = Math.Min(soonest, wait);
                continue;
            }
            doses += AnimalHealth.DosesFor(sp, m);
            mine++;
        }
        if (mine == 0 && tooSoon > 0)
            return Refuse(plan, $"Too soon for another dose: {(tooSoon == 1 ? "it" : "the first of them")} can have the {tr.DisplayName.ToLowerInvariant()} again in {AnimalObservation.Wait(soonest)}.");
        if (mine == 0) return Refuse(plan, living == 0 ? "No animals live here." : "None of these animals are yours to treat.");
        int needed = AnimalHealth.ItemsFor(tr, doses);
        int have = actor.Count(r.item);
        if (have < needed) return Refuse(plan, $"The pen needs {needed} of it (you have {have}).");
        plan.takeItem = r.item;
        plan.takeCount = needed;
        plan.plannedCount = needed;
        return plan;
    }

    // ---------------------------------------------------------------- planning: products
    /// <summary>
    /// Collecting from one animal (here, or its owner's at the boarding stable): what of the products asked for (all, with
    /// no text) waits, and the tool each needs. Refused, with the reason, when nothing waits, or nothing can be taken
    /// without a tool the player lacks.
    /// </summary>
    private static AnimalPlan PlanCollect(AnimalBook book, AnimalRecord a, AnimalPlan plan, AnimalRequest r, AnimalActor actor, double now, AnimalSimContext ctx, IAnimalCatalog cat)
    {
        a = HereOrBoarded(a, r, actor);
        if (a == null) return Refuse(plan, "That animal is not here.");
        BringBoarded(book, a, now, ctx);
        if (!a.Alive) return Refuse(plan, "It is dead.");
        if (!MayCollect(a, actor, cat.Rules)) return Refuse(plan, "Not your animal.");
        AnimalSpeciesData sp = cat.Species(a.species);
        if (sp?.products == null || sp.products.Count == 0) return Refuse(plan, $"{Called(a, sp, true)} gives nothing to collect.");
        if (!string.IsNullOrEmpty(r.text) && AnimalProduction.Spec(sp, r.text) == null) return Refuse(plan, $"{Called(a, sp, true)} gives no such thing.");
        string why = null;
        int units = 0;
        foreach (AnimalProductSpec p in sp.products)
        {
            if (!Asked(p, r)) continue;
            AnimalProductState st = AnimalProduction.State(a, p.id, false);
            int n = st != null ? st.Waiting : 0;
            if (n <= 0)
            {
                why ??= NothingYet(a, sp, p, st, cat.Rules);
                continue;
            }
            if (p.NeedsTool)
            {
                AnimalToolUse use = actor.Tool(p.tools);
                if (!use.has)
                {
                    why = $"You need {AnimalCare.ToolNames(cat, p.tools)} to {p.Verb.ToLowerInvariant()} {Called(a, sp, false)}.";
                    continue;
                }
                plan.toolUsed = use;
            }
            units += n;
        }
        if (units <= 0) return Refuse(plan, why ?? "There is nothing to collect.");
        plan.plannedCount = units;
        return plan;
    }

    /// <summary>Collecting from every animal of the pen the player may collect from: those with something waiting they have the tool for.</summary>
    private static AnimalPlan PlanHerdCollect(AnimalHerd herd, AnimalPlan plan, AnimalRequest r, AnimalActor actor, IAnimalCatalog cat)
    {
        int units = 0, animals = 0, living = 0, mine = 0;
        string why = null;
        // What waits in the egg trays for this player (Phase 5): the text "trays" takes only that.
        int trayUnits = TrayUnitsFor(herd.pen, r, actor, cat);
        if (TraysAsked(r))
        {
            if (trayUnits > 0)
            {
                plan.plannedCount = trayUnits;
                return plan;
            }
            return Refuse(plan, herd.pen.TrayUnits > 0 ? "Nothing in the egg trays is yours to take." : "The egg trays are empty.");
        }
        foreach (AnimalRecord m in herd.animals)
        {
            if (m == null || !m.Alive) continue;
            living++;
            if (!MayCollect(m, actor, cat.Rules)) continue;
            mine++;
            AnimalSpeciesData sp = cat.Species(m.species);
            if (sp?.products == null) continue;
            bool any = false;
            foreach (AnimalProductSpec p in sp.products)
            {
                if (!Asked(p, r)) continue;
                AnimalProductState st = AnimalProduction.State(m, p.id, false);
                int n = st != null ? st.Waiting : 0;
                if (n <= 0) continue;
                if (p.NeedsTool)
                {
                    AnimalToolUse use = actor.Tool(p.tools);
                    if (!use.has)
                    {
                        why ??= $"You need {AnimalCare.ToolNames(cat, p.tools)} to {p.Verb.ToLowerInvariant()} them.";
                        continue;
                    }
                    if (!plan.toolUsed.has) plan.toolUsed = use;
                }
                units += n;
                any = true;
            }
            if (any) animals++;
        }
        if (mine == 0 && trayUnits <= 0) return Refuse(plan, living == 0 ? "No animals live here." : "None of these animals are yours.");
        units += trayUnits;
        if (units <= 0) return Refuse(plan, why ?? "There is nothing to collect.");
        plan.plannedCount = units;
        return plan;
    }

    /// <summary>
    /// The actor may collect what an animal made: its owner, or (when the rules' Others Collect is on) one who may care for
    /// others' animals in this pen.
    /// </summary>
    private static bool MayCollect(AnimalRecord a, AnimalActor actor, AnimalRules rules) =>
        a != null && actor != null && (a.owner == actor.key || ((rules?.production == null || rules.production.othersCollect) && (actor.rights & AnimalRights.CareForOthers) != 0));

    /// <summary>The products a Collect request asks for (its text names one; empty: all).</summary>
    private static bool Asked(AnimalProductSpec p, AnimalRequest r) =>
        p != null && (string.IsNullOrEmpty(r.text) || string.Equals(p.id, r.text, StringComparison.OrdinalIgnoreCase));

    /// <summary>Why nothing of a product waits, in words ("Dolly's fleece is still growing (about 9 days).").</summary>
    private static string NothingYet(AnimalRecord a, AnimalSpeciesData sp, AnimalProductSpec p, AnimalProductState st, AnimalRules rules)
    {
        string name = Called(a, sp, true);
        if (st == null || !AnimalProduction.Makes(a, sp, p)) return $"{name} gives no {p.DisplayName.ToLowerInvariant()}.";
        string status = AnimalProduction.StatusText(st.status, p);
        if (st.status != AnimalProductStatus.Producing && st.status != AnimalProductStatus.Full && !string.IsNullOrEmpty(status)) return $"{name}: {status}.";
        double left = (1.0 - AnimalMath.Clamp01(st.progress)) * Math.Max(0.5f, p.cycleHours);
        return $"{name} has no {p.DisplayName.ToLowerInvariant()} ready yet (about {AnimalObservation.Wait(left)} more).";
    }

    // ---------------------------------------------------------------- planning: breeding
    /// <summary>
    /// Breeding an animal with a partner (its id in the request's text) in this pen: both the player's, where they may keep
    /// animals; compatible (one kind, a female and a male, not close kin); each ready (its stage, health, nutrition,
    /// welfare, not expecting, not in its cooldown); room in the pen and in the player's keeping for the largest litter;
    /// and the item breeding takes, carried. Refused, with the reason, otherwise.
    /// </summary>
    private static AnimalPlan PlanBreed(AnimalBook book, AnimalHerd herd, AnimalRecord a, AnimalPlan plan, AnimalRequest r, AnimalActor actor, double now, AnimalRules rules,
        IAnimalCatalog cat)
    {
        if (a == null || a.pen != r.pen) return Refuse(plan, "That animal is not here.");
        AnimalRecord b = book.Find((r.text ?? "").Trim());
        if (b == null || b.pen != r.pen) return Refuse(plan, "Choose a partner for it in this pen.");
        if (!a.Alive || !b.Alive) return Refuse(plan, "It is dead.");
        if (a.owner != actor.key || b.owner != actor.key) return Refuse(plan, "Only their owner breeds them: both must be yours.");
        if ((actor.rights & AnimalRights.Keep) == 0) return Refuse(plan, "You cannot keep animals in this pen.");
        AnimalSpeciesData sp = cat.Species(a.species);
        if (sp == null) return Refuse(plan, "They cannot be bred now.");
        if (!AnimalBreeding.Compatible(a, b, sp, rules, out AnimalRecord mother, out AnimalRecord father, out string why)) return Refuse(plan, why);
        if (!AnimalBreeding.Ready(mother, sp, rules, now, out why)) return Refuse(plan, why);
        if (!AnimalBreeding.Ready(father, sp, rules, now, out why)) return Refuse(plan, why);
        AnimalBreedingSpec s = AnimalBreeding.SpecOf(sp);
        int most = s.LitterMax;
        if (book.LivingOwnedBy(actor.key) + AnimalBreeding.ExpectedFor(book, actor.key) + most > rules.maxAnimalsPerPlayer)
            return Refuse(plan, $"You could not keep their young: you keep as many animals as you can look after ({rules.maxAnimalsPerPlayer}), young on the way counted.");
        why = RoomForYoung(book, herd, sp, most);
        if (why != null) return Refuse(plan, why);
        if (s.costCount > 0 && !string.IsNullOrWhiteSpace(s.costItem))
        {
            string item = s.costItem.Trim();
            if (actor.Count(item) < s.costCount) return Refuse(plan, $"Breeding them takes {s.costCount} {KeyWord(item)}: you have not enough.");
            plan.takeItem = item;
            plan.takeCount = s.costCount;
        }
        plan.plannedCount = most;
        return plan;
    }

    /// <summary>Is there room in the pen for a litter of this many (as adults: the young grow into their space)? Null: there is.</summary>
    private static string RoomForYoung(AnimalBook book, AnimalHerd herd, AnimalSpeciesData sp, int young)
    {
        AnimalPenSpec spec = herd.spec ?? new AnimalPenSpec();
        int capacity = AnimalUpgrades.Capacity(herd.pen, spec, book.Rules);
        if (capacity > 0)
            return book.LivingInPen(herd.pen.id) + AnimalBreeding.ExpectedIn(herd) + young > capacity ? $"There is no room here for their young (up to {young})." : null;
        float need = AnimalBook.SpaceOf(sp, null) * young;
        if (book.SpaceUsed(herd.pen.id) + AnimalBreeding.ExpectedSpace(book, herd) + need > AnimalUpgrades.Area(herd.pen, spec, book.Rules) + 0.01f)
            return $"There is no room here for their young (up to {young}, each needing {sp.habitat?.areaPerAnimal ?? 1f:0.#} m² grown).";
        return null;
    }

    /// <summary>An item key in a word or two ("animal:feed/grain" -> "grain").</summary>
    private static string KeyWord(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        int cut = Math.Max(key.LastIndexOf('/'), key.LastIndexOf(':'));
        return (cut >= 0 ? key.Substring(cut + 1) : key).Replace('_', ' ').TrimEnd('*');
    }

    // ---------------------------------------------------------------- planning: helpers
    /// <summary>The actor may care for an animal: its owner, or one who may care for others' animals in this pen.</summary>
    private static bool MayCare(AnimalRecord a, AnimalActor actor) =>
        a != null && actor != null && (a.owner == actor.key || (actor.rights & AnimalRights.CareForOthers) != 0);

    /// <summary>The animal named, when it is in the request's pen, or its owner's (the actor's) at the boarding stable; else null.</summary>
    private static AnimalRecord HereOrBoarded(AnimalRecord a, AnimalRequest r, AnimalActor actor)
    {
        if (a == null) return null;
        if (a.pen == r.pen) return a;
        return string.IsNullOrEmpty(a.pen) && actor != null && a.owner == actor.key ? a : null;
    }

    /// <summary>Brings an animal at the boarding stable up to the present (the pen's own were brought up to date already).</summary>
    private static void BringBoarded(AnimalBook book, AnimalRecord a, double now, AnimalSimContext ctx)
    {
        if (a != null && a.Alive && string.IsNullOrEmpty(a.pen)) book.BringUpToDate(a, now, ctx);
    }

    /// <summary>Does the player (a lasting key) own a living animal in the pen?</summary>
    private static bool KeepsAnimalIn(AnimalHerd herd, string key)
    {
        if (herd == null || string.IsNullOrEmpty(key)) return false;
        foreach (AnimalRecord m in herd.animals)
            if (m != null && m.Alive && m.owner == key) return true;
        return false;
    }

    /// <summary>
    /// May the actor change the pen's water (scrub out the trough, or pour in water that makes it murky)? One who may care
    /// for others' animals here, or who keeps an animal here: on land nobody rents, a passer-by may not.
    /// </summary>
    private static bool MayChangeWater(AnimalHerd herd, AnimalActor actor) =>
        actor != null && ((actor.rights & AnimalRights.CareForOthers) != 0 || KeepsAnimalIn(herd, actor.key));

    private static bool IsTrough(string text) => !string.IsNullOrWhiteSpace(text) && string.Equals(text.Trim(), TroughText, StringComparison.OrdinalIgnoreCase);

    private static string CareId(string text) => string.IsNullOrWhiteSpace(text) ? "" : text.Trim();

    /// <summary>Is this item a supply the care action uses (used up, with the action's supply tag)?</summary>
    private static bool Supplies(AnimalToolData t, AnimalCareActionSpec spec) =>
        t != null && t.consumed && spec != null && spec.TakesSupply && t.HasTag(spec.supply);

    /// <summary>Does a tool suit an action that needs one of these tags? (A tool the catalog does not know was found by its tags where the items are: trusted.)</summary>
    private static bool Fits(IAnimalCatalog cat, AnimalToolUse use, List<string> tags)
    {
        if (!use.has) return false;
        AnimalToolData t = cat.Tool(use.tool) ?? cat.ToolOfItem(use.key);
        return t == null || t.HasAny(tags);
    }

    /// <summary>A tool's name in words ("pitchfork").</summary>
    private static string ToolName(IAnimalCatalog cat, AnimalToolUse use)
    {
        AnimalToolData t = cat.Tool(use.tool) ?? cat.ToolOfItem(use.key);
        return t != null ? t.DisplayName.ToLowerInvariant() : "tool";
    }

    /// <summary>An animal in a sentence: its name, or "the cow" (<paramref name="capital"/>: "The cow"; see <see cref="AnimalSim.NameIn"/>).</summary>
    private static string Called(AnimalRecord a, AnimalSpeciesData sp, bool capital)
    {
        string s = AnimalSim.NameIn(a, sp);
        bool named = a != null && !string.IsNullOrWhiteSpace(a.name);
        return capital && !named ? char.ToUpperInvariant(s[0]) + s.Substring(1) : s;
    }

    private static string Litres(float litres) => $"{litres:0.#} L";

    private static string Some(int n) => n == 1 ? "One" : n.ToString();

    private static string Were(int n) => n == 1 ? "was" : "were";

    // ---------------------------------------------------------------- committing
    /// <summary>
    /// Records a planned command with what was really taken. Commands that take something and were given nothing do
    /// nothing (each says what it needs: see <see cref="Missing"/>). Returns the answer (with the request's seq).
    /// </summary>
    public static AnimalResult Commit(AnimalBook book, AnimalPlan plan, AnimalTaken taken, double now, AnimalSimContext ctx)
    {
        if (plan == null) return AnimalResult.Fail("Nothing to do.");
        if (plan.Refused) return Seq(plan.refusal, plan.request);
        if (plan.answer != null) return Seq(plan.answer, plan.request);
        AnimalRequest r = plan.request;
        string missing = Missing(plan, taken);
        if (missing != null) return Seq(AnimalResult.Fail(missing), r);
        AnimalRules rules = book.Rules;
        IAnimalCatalog cat = ctx.catalog;
        AnimalHerd herd = book.Herd(r.pen);
        if (herd == null) return Seq(AnimalResult.Fail("There is no such pen."), r);
        AnimalRecord a = string.IsNullOrEmpty(r.animal) ? null : book.Find(r.animal);
        AnimalResult result;
        switch (r.kind)
        {
            case AnimalActionKind.Feed: result = CommitFeed(book, herd, a, plan, taken, now, rules, cat); break;
            case AnimalActionKind.StockFeeder: result = CommitStock(herd, plan, taken, now, cat); break;
            case AnimalActionKind.FillWater: result = CommitWater(herd, taken, rules, MayChangeWater(herd, plan.actor)); break;
            case AnimalActionKind.Rename:
            {
                AnimalSpeciesData sp = cat.Species(a.species);
                string old = AnimalSim.NameOf(a, sp), oldIn = AnimalSim.NameIn(a, sp);
                a.name = CleanName(r.text, rules.maxNameLength);
                a.Log(AnimalEventKind.Renamed, now, $"Named {a.name} (was {oldIn}).", plan.actor.key, rules.keepEvents);
                result = AnimalResult.Ok($"{old} is now called {a.name}.", a.id);
                break;
            }
            case AnimalActionKind.Release: result = CommitRelease(book, herd, plan, taken, now, rules, cat, ctx); break;
            case AnimalActionKind.Move:
            {
                AnimalSpeciesData sp = cat.Species(a.species);
                bool fromBoarding = a.status == AnimalStatus.Boarded;
                book.BringUpToDate(a, now, ctx);
                if (!a.Alive)
                {
                    // It died while its pen was brought up to date: its remains stay where it died.
                    result = AnimalResult.Fail($"{AnimalSim.NameOf(a, sp)} has died.");
                    break;
                }
                // Lived to the present where it was; from now on it lives here.
                book.MoveTo(a, herd.pen.id);
                a.livedTo = Math.Max(a.livedTo, herd.pen.livedTo);
                string text = fromBoarding ? $"Brought home from the boarding stable to {herd.spec?.name ?? "the pen"}." : $"Moved to {herd.spec?.name ?? "another pen"}.";
                a.Log(fromBoarding ? AnimalEventKind.Rehomed : AnimalEventKind.Moved, now, text, plan.actor.key, rules.keepEvents);
                book.Urgent = true;
                result = AnimalResult.Ok($"{AnimalSim.NameOf(a, sp)} lives here now.", a.id);
                break;
            }
            case AnimalActionKind.RemoveRemains:
            {
                AnimalSpeciesData sp = cat.Species(a.species);
                string who = AnimalSim.NameIn(a, sp);
                book.Remove(a);
                book.Urgent = true;
                result = AnimalResult.Ok($"The remains of {who} are cleared away.", "");
                break;
            }
            case AnimalActionKind.Care:
                result = string.IsNullOrEmpty(r.animal) ? CommitHerdCare(herd, plan, taken, now, rules, cat, ctx) : CommitCare(a, plan, taken, now, rules, cat, ctx);
                break;
            case AnimalActionKind.CleanPen: result = CommitClean(herd, plan, taken, now, rules, cat); break;
            case AnimalActionKind.Examine: result = CommitExamine(a, plan, now, rules, cat, ctx); break;
            case AnimalActionKind.Treat:
                result = string.IsNullOrEmpty(r.animal) ? CommitHerdTreat(herd, plan, taken, now, rules, cat, ctx) : CommitTreat(a, plan, taken, now, rules, cat, ctx);
                break;
            case AnimalActionKind.CallVet: result = CommitVet(book, a, plan, now, rules, cat, ctx); break;
            case AnimalActionKind.Slaughter: result = CommitSlaughter(book, a, plan, now, rules, cat, ctx); break;
            case AnimalActionKind.Butcher: result = CommitButcher(a, plan, cat); break;
            case AnimalActionKind.Collect: result = CommitCollect(book, herd, a, plan, now, rules, cat, ctx); break;
            case AnimalActionKind.Breed: result = CommitBreed(book, herd, a, plan, now, rules, cat); break;
            case AnimalActionKind.Crate: result = CommitCrate(book, herd, a, plan, now, rules, cat, ctx); break;
            case AnimalActionKind.Upgrade: result = CommitUpgrade(herd, plan, now, rules); break;
            case AnimalActionKind.PayUpkeep: result = CommitPayUpkeep(herd, plan, rules); break;
            default: result = AnimalResult.Fail("That cannot be done."); break;
        }
        if (result.ok)
        {
            if (herd?.pen != null) herd.pen.revision++;
            book.Touch();
        }
        return Seq(result, r);
    }

    private static AnimalResult Seq(AnimalResult res, AnimalRequest r)
    {
        res.seq = r.seq;
        return res;
    }

    /// <summary>
    /// What a command needed from the player and did not get (the bag changed between the plan and the take), or null. Items:
    /// those it takes (Care's supply and CleanPen's straw are optional: see their commits). Water: FillWater any; a wash on
    /// one animal at least the least share of its want (a pen's wash counts animal by animal). Money: CallVet the whole fee.
    /// </summary>
    private static string Missing(AnimalPlan plan, AnimalTaken taken)
    {
        AnimalRequest r = plan.request;
        switch (r.kind)
        {
            case AnimalActionKind.Care:
                if (string.IsNullOrEmpty(r.animal)) return null;
                return AnimalCare.EnoughWater(plan.plannedLitres, taken.litres) ? null : NoWater;
            case AnimalActionKind.CleanPen:
                return null;
            case AnimalActionKind.CallVet:
                return plan.fee > 0 && taken.paid < plan.fee ? "You cannot pay the vet." : null;
            case AnimalActionKind.Upgrade:
            case AnimalActionKind.PayUpkeep:
                return plan.fee > 0 && taken.paid < plan.fee ? "You cannot pay." : null;
            case AnimalActionKind.Breed:
                // What breeding takes is taken whole, or not at all.
                return !string.IsNullOrEmpty(plan.takeItem) && plan.takeCount > 0 && taken.count < plan.takeCount ? NoLonger : null;
            default:
                if (!string.IsNullOrEmpty(plan.takeItem) && plan.takeCount > 0 && taken.count <= 0) return NoLonger;
                if (plan.takeWater > 0f && taken.litres <= 0f) return NoLonger;
                return null;
        }
    }

    /// <summary>
    /// Takes what waits (see <see cref="AnimalProduction.Collect"/>): every lot goes into the world's product ledger and
    /// becomes item copies in <see cref="AnimalPlan.give"/>, each copy's code carrying its quality and its lot. Each tool
    /// used wears a use per animal (<see cref="AnimalPlan.wearKey"/>, then <see cref="AnimalPlan.moreWear"/>). Milking or
    /// shearing stresses a skittish animal a little (twice as much with a worn-out tool); its owner is told when someone
    /// else collected. The world is saved soon after (what was taken must never come back after a crash).
    /// </summary>
    private static AnimalResult CommitCollect(AnimalBook book, AnimalHerd herd, AnimalRecord one, AnimalPlan plan, double now, AnimalRules rules, IAnimalCatalog cat,
        AnimalSimContext ctx)
    {
        AnimalRequest r = plan.request;
        var targets = new List<AnimalRecord>();
        if (one != null) targets.Add(one);
        else
            foreach (AnimalRecord m in herd.animals)
                if (m != null && m.Alive && MayCollect(m, plan.actor, rules)) targets.Add(m);
        plan.give.Clear();
        plan.moreWear.Clear();
        var wear = new Dictionary<string, int>(StringComparer.Ordinal);
        var wearOrder = new List<string>();
        bool rough = false;
        var byItem = new Dictionary<string, AnimalItemChange>();
        var order = new List<string>();
        int total = 0, animals = 0;
        float qSum = 0f;
        string lastWhat = "", lastName = "", lastVerb = "Collect";
        AnimalProductTier lastTier = AnimalProductTier.None;
        foreach (AnimalRecord a in targets)
        {
            if (!a.Alive) continue;
            AnimalSpeciesData sp = cat.Species(a.species);
            if (sp?.products == null) continue;
            int fromThis = 0;
            var toolsHere = new List<string>();
            foreach (AnimalProductSpec p in sp.products)
            {
                if (!Asked(p, r)) continue;
                AnimalProductState st = AnimalProduction.State(a, p.id, false);
                if (st == null || st.Waiting <= 0) continue;
                bool worn = false;
                if (p.NeedsTool)
                {
                    // The best tool the player carries for it (none: it waits).
                    AnimalToolUse use = plan.actor.Tool(p.tools);
                    if (!use.has || string.IsNullOrEmpty(use.key)) continue;
                    if (!toolsHere.Contains(use.key)) toolsHere.Add(use.key);
                    worn = use.edge <= 0f;
                }
                var codes = new List<int>();
                int n = AnimalProduction.Collect(a, sp, p, book.Ledger, rules.production, plan.actor.key, now, codes, out float q, out AnimalProductTier tier);
                if (n <= 0) continue;
                if (!byItem.TryGetValue(p.Item, out AnimalItemChange change))
                {
                    change = new AnimalItemChange { item = p.Item, count = 0, codes = new List<int>() };
                    order.Add(p.Item);
                }
                change.count += n;
                change.codes.AddRange(codes);
                byItem[p.Item] = change;
                total += n;
                fromThis += n;
                qSum += q * n;
                lastWhat = p.Count(n);
                lastVerb = p.Verb;
                lastTier = AnimalProduction.Tier(q, AnimalProduction.Rare(a), rules.production);
                float temperament = a.genes != null ? a.genes.temperament : 0.5f;
                if (p.handlingStress > 0f)
                {
                    // A bond with its owner eases the owner's handling (none at all once it is calm with them).
                    float bond = AnimalSocial.HandlingFactor(a, sp, a.owner == plan.actor.key, rules);
                    a.stress = AnimalMath.Clamp100(a.stress + p.handlingStress * (worn ? 2f : 1f) * (1f - temperament) * bond);
                    rough |= worn;
                }
            }
            if (fromThis <= 0) continue;
            // A use of each tool it was done with, for each animal.
            foreach (string key in toolsHere)
            {
                if (!wear.ContainsKey(key))
                {
                    wear[key] = 0;
                    wearOrder.Add(key);
                }
                wear[key]++;
            }
            animals++;
            lastName = Called(a, sp, false);
            a.revision++;
            if (a.owner != plan.actor.key)
            {
                string who = string.IsNullOrWhiteSpace(plan.actor.name) ? "someone" : plan.actor.name.Trim();
                ctx?.Notice(a, AnimalEventKind.Collected, $"{who} collected {lastWhat} from {AnimalSim.NameIn(a, sp)}.");
            }
        }
        // The egg trays (Phase 5): a pen's Collect takes what waits there for this player too.
        int fromTrays = 0;
        if (one == null)
        {
            fromTrays = CollectTrays(book, herd, plan, now, rules, cat, ctx, byItem, order, out float trayQ, out AnimalProductTier trayTier, out string trayWhat);
            if (fromTrays > 0)
            {
                total += fromTrays;
                qSum += trayQ * fromTrays;
                lastWhat = trayWhat;
                lastTier = trayTier;
            }
        }
        if (total <= 0) return AnimalResult.Fail("There is nothing to collect.");
        foreach (string item in order) plan.give.Add(byItem[item]);
        plan.wearKey = null;
        plan.wearUses = 0;
        for (int i = 0; i < wearOrder.Count; i++)
        {
            if (i == 0)
            {
                plan.wearKey = wearOrder[0];
                plan.wearUses = wear[wearOrder[0]];
            }
            else plan.moreWear.Add(new AnimalItemChange { item = wearOrder[i], count = wear[wearOrder[i]] });
        }
        book.Ledger.Prune(now, rules.production);
        book.Urgent = true;
        string tierWord = AnimalProduction.TierWord(animals == 1 ? lastTier : AnimalProduction.Tier(qSum / total, false, rules.production));
        string message;
        if (fromTrays > 0 && animals == 0)
        {
            tierWord = AnimalProduction.TierWord(lastTier);
            message = $"You collect {lastWhat} from the egg trays ({tierWord}).";
        }
        else if (fromTrays > 0)
        {
            tierWord = AnimalProduction.TierWord(AnimalProduction.Tier(qSum / total, false, rules.production));
            message = $"You collect {total} from {animals} animal{(animals == 1 ? "" : "s")} and the egg trays ({tierWord} on the whole).";
        }
        else if (animals == 1 && order.Count == 1)
            message = string.Equals(lastVerb, "Collect", StringComparison.OrdinalIgnoreCase)
                ? $"You collect {lastWhat} from {lastName} ({tierWord})."
                : $"You {lastVerb.ToLowerInvariant()} {lastName}: {lastWhat} ({tierWord}).";
        else message = $"You collect {total} from {animals} animal{(animals == 1 ? "" : "s")} ({tierWord} on the whole).";
        AnimalResult res = AnimalResult.Ok(message, one != null ? one.id : "");
        foreach (string item in order)
        {
            AnimalItemChange c = byItem[item];
            AnimalProductSpec p = SpecOfItem(cat, item);
            if (p != null && order.Count > 1) res.lines.Add($"{char.ToUpperInvariant(p.Count(c.count)[0])}{p.Count(c.count).Substring(1)}.");
        }
        if (rough) res.lines.Add("Your worn-out tool is rough on it.");
        return res;
    }

    /// <summary>
    /// Breeds the pair (see <see cref="AnimalBreeding.Mate"/>), checked again against the present: whatever the roll, the
    /// mating happened (what it took is spent); conceived, she is expecting.
    /// </summary>
    private static AnimalResult CommitBreed(AnimalBook book, AnimalHerd herd, AnimalRecord a, AnimalPlan plan, double now, AnimalRules rules, IAnimalCatalog cat)
    {
        AnimalRecord b = book.Find((plan.request.text ?? "").Trim());
        if (a == null || b == null || a.pen != herd.pen.id || b.pen != herd.pen.id || !a.Alive || !b.Alive) return AnimalResult.Fail("They are not both here.");
        AnimalSpeciesData sp = cat.Species(a.species);
        if (!AnimalBreeding.Compatible(a, b, sp, rules, out AnimalRecord mother, out AnimalRecord father, out string why)) return AnimalResult.Fail(why);
        if (!AnimalBreeding.Ready(mother, sp, rules, now, out why) || !AnimalBreeding.Ready(father, sp, rules, now, out why)) return AnimalResult.Fail(why);
        bool took = AnimalBreeding.Mate(mother, father, sp, rules, now, book.World, plan.actor.key, book);
        book.Urgent = true;
        AnimalBreedingSpec s = AnimalBreeding.SpecOf(sp);
        string dam = AnimalSim.NameOf(mother, sp), sire = AnimalSim.NameIn(father, sp);
        if (!took)
            return AnimalResult.Ok($"{dam} and {sire} were put together, but it did not take. They may try again in {AnimalObservation.Wait(rules.breeding?.retryHours ?? 24f)}.", a.id);
        string young = string.IsNullOrWhiteSpace(sp.youngName) ? "young" : sp.youngName.ToLowerInvariant();
        var res = AnimalResult.Ok($"{dam} and {sire} are a pair: {dam} is {s.expectingWord}. If all goes well, {(s.LitterMax > 1 ? "young" : "a " + young)} in about {AnimalObservation.Wait(s.gestationHours)}.", a.id);
        res.lines.Add("Keep her well fed and well: a mother in poor health can lose her young.");
        return res;
    }

    /// <summary>The product spec whose item this is (any species), or null.</summary>
    private static AnimalProductSpec SpecOfItem(IAnimalCatalog cat, string item)
    {
        foreach (AnimalSpeciesData sp in cat.AllSpecies)
            if (sp?.products != null)
                foreach (AnimalProductSpec p in sp.products)
                    if (p != null && p.Item == item) return p;
        return null;
    }

    private static AnimalResult CommitFeed(AnimalBook book, AnimalHerd herd, AnimalRecord a, AnimalPlan plan, AnimalTaken taken, double now, AnimalRules rules, IAnimalCatalog cat)
    {
        AnimalSpeciesData sp = cat.Species(a.species);
        AnimalFoodData food = cat.FoodOfItem(plan.request.item);
        if (sp == null || food == null || !a.Alive) return AnimalResult.Fail("It will not eat now.");
        FoodAffinity aff = sp.DietAt(a.stage).AffinityOf(food);
        AnimalSim.Prime(a, sp, rules); // a memory of meals to add this one to (never an empty one that reads as perfect)
        float appetite = AnimalSim.Appetite(sp, sp.BreedOrDefault(a.breed), a);
        float perPortion = food.satiety / appetite;
        float limit = HandLimit(aff, rules), most = MaxHandPortions(rules, appetite);
        float eaten = 0f, qualitySum = 0f, left = 0f;
        for (int i = 0; i < taken.count; i++)
        {
            float q = taken.Quality(i);
            for (int p = 0; p < Math.Max(1, food.portionsPerItem); p++)
            {
                // It stops when full - or, for harmful or toxic food, as soon as the hunger that made it take it is eased.
                bool wants = a.satiety < limit && eaten < most;
                if (wants)
                {
                    float portion = perPortion > 0f ? Mathf.Clamp01((limit - a.satiety) / perPortion) : 1f;
                    portion = Mathf.Max(portion, 0.02f);
                    AnimalSim.Take(a, sp, rules, food, aff, portion, q, appetite, out _, out _);
                    eaten += portion;
                    qualitySum += q * portion;
                    left += 1f - portion;
                }
                else left += 1f;
            }
        }
        // What it leaves goes into the feeder, as far as there is room.
        float stocked = left > 0.01f && herd != null ? Stock(herd, food, left, taken.count > 0 ? taken.Quality(taken.count - 1) : AnimalItemCode.UnknownQuality, plan.actor.key, now) : 0f;
        float meanQ = eaten > 0f ? qualitySum / eaten : 0f;
        a.lastFedAt = now;
        a.Fed(new AnimalFeeding { at = now, food = food.id, portions = eaten, quality = meanQ, affinity = aff, by = plan.actor.key }, rules.keepFeedings);
        if (a.owner == plan.actor.key)
        {
            // Within the day's cap, as care and medicine are: a bond is not bought by feeding it all day.
            AnimalCare.GiveAffinity(a, rules.handFeedAffinity, now, rules);
            // A meal from its owner's hand is its owner's care: its bond does not fade.
            a.lastOwnerCareAt = now;
        }
        if (aff == FoodAffinity.Favorite) a.happiness = AnimalMath.Clamp100(a.happiness + rules.favoriteHappiness * Mathf.Min(2f, eaten));
        a.nutrition = AnimalSim.Nutrition(a.intake, a.need, sp.needs.importance, rules);
        a.revision++;
        string who = AnimalSim.NameOf(a, sp);
        string what = food.DisplayName.ToLowerInvariant();
        string reaction = aff switch
        {
            FoodAffinity.Favorite => $"{who} gobbles the {what} eagerly.",
            FoodAffinity.Preferred => $"{who} eats the {what} with appetite.",
            FoodAffinity.Normal => $"{who} eats the {what}.",
            FoodAffinity.Neutral => $"{who} chews the {what} without interest.",
            FoodAffinity.Poor => $"{who} picks at the {what}.",
            FoodAffinity.Harmful => $"{who} eats the {what}, then shifts uneasily.",
            FoodAffinity.Toxic => $"{who} eats the {what}. Soon it starts to drool.",
            _ => $"{who} eats.",
        };
        a.Log(AnimalEventKind.Fed, now, $"Fed {what} by hand.", plan.actor.key, rules.keepEvents);
        if (aff == FoodAffinity.Toxic) a.Log(AnimalEventKind.Poisoned, now, $"Ate {what}, which poisons it.", plan.actor.key, rules.keepEvents);
        else if (aff == FoodAffinity.Harmful) a.Log(AnimalEventKind.Upset, now, $"Ate {what}, which upsets its stomach.", plan.actor.key, rules.keepEvents);
        // Its care history keeps the meals from a hand (whose hand: its owner reads it).
        string hand = string.IsNullOrWhiteSpace(plan.actor.name) ? "by hand" : $"from {plan.actor.name.Trim()}'s hand";
        AnimalCare.Note(a, "fed", plan.actor.key, $"Fed {what} {hand}.", now, rules);
        var res = AnimalResult.Ok(reaction, a.id);
        if (stocked > 0.01f) res.lines.Add("What it left is in the feeder.");
        if (left - stocked > 0.01f) res.lines.Add("The feeder is full: the rest is trampled.");
        return res;
    }

    private static AnimalResult CommitStock(AnimalHerd herd, AnimalPlan plan, AnimalTaken taken, double now, IAnimalCatalog cat)
    {
        AnimalFoodData food = cat.FoodOfItem(plan.request.item);
        if (food == null) return AnimalResult.Fail("That is not food for animals.");
        if (ToHopper(plan.request))
        {
            if (!herd.pen.hopper) return AnimalResult.Fail("There is no feed hopper here.");
            for (int i = 0; i < taken.count; i++)
                AnimalUpgrades.StockHopper(herd.pen, cat.Rules, food, Math.Max(1, food.portionsPerItem), taken.Quality(i), plan.actor.key, now);
            return AnimalResult.Ok($"You fill the hopper with {taken.count} {food.DisplayName.ToLowerInvariant()}.");
        }
        for (int i = 0; i < taken.count; i++)
            Stock(herd, food, Math.Max(1, food.portionsPerItem), taken.Quality(i), plan.actor.key, now);
        return AnimalResult.Ok($"You put {taken.count} {food.DisplayName.ToLowerInvariant()} in the feeder.");
    }

    /// <summary>Puts portions of a food in a pen's feeder (lots of nearly the same quality and age are kept together; what does not fit is lost).</summary>
    public static float Stock(AnimalHerd herd, AnimalFoodData food, float portions, float quality, string by, double now)
    {
        AnimalPenSpec spec = herd.spec ?? new AnimalPenSpec();
        float room = Mathf.Max(0f, spec.feederCapacity - herd.pen.FeederPortions);
        float put = Mathf.Min(room, portions);
        if (put <= 0f) return 0f;
        foreach (FeederLot l in herd.pen.feeder)
        {
            if (l == null || l.food != food.id || Mathf.Abs(l.quality - quality) > 0.05f || Math.Abs(l.since - now) > 6.0) continue;
            float total = l.portions + put;
            l.quality = (l.quality * l.portions + quality * put) / total;
            l.since = (l.since * l.portions + now * put) / total;
            l.portions = total;
            herd.pen.revision++;
            return put;
        }
        herd.pen.feeder.Add(new FeederLot { food = food.id, portions = put, quality = AnimalMath.Clamp01(quality), since = now, by = by ?? "" });
        herd.pen.revision++;
        return put;
    }

    /// <summary>
    /// Water poured into the trough mixes with what is there: (litres before x their dirt + litres poured x the dirt of
    /// their kind) / litres after (a swamp's water carries dirt in; see <see cref="AnimalHealth.KindDirt"/>). Pouring never
    /// empties a fouled trough: scrubbing it out does (CleanPen with the text "trough"), which the pourer is pointed to only
    /// when they may do it (<paramref name="mayScrub"/>).
    /// </summary>
    private static AnimalResult CommitWater(AnimalHerd herd, AnimalTaken taken, AnimalRules rules, bool mayScrub)
    {
        AnimalPenSpec spec = herd.spec ?? new AnimalPenSpec();
        float before = herd.pen.water;
        float put = AnimalHabitat.Pour(herd.pen, Mathf.Max(0f, taken.litres), AnimalHealth.KindDirt(taken.waterKind), spec.waterCapacity);
        if (put <= 0f) return AnimalResult.Fail("The trough is full.");
        if (put > before) herd.pen.waterKind = taken.waterKind;
        AnimalResult res = AnimalResult.Ok($"You pour {put:0.#} L into the trough.");
        if (AnimalHabitat.CanScrub(herd.pen, rules)) res.lines.Add(mayScrub ? "The water is still murky: scrub out the trough to change it." : "The water is still murky.");
        return res;
    }

    private static AnimalResult CommitRelease(AnimalBook book, AnimalHerd herd, AnimalPlan plan, AnimalTaken taken, double now, AnimalRules rules, IAnimalCatalog cat,
        AnimalSimContext ctx)
    {
        if (!LivestockOf(cat, plan.request.item, out AnimalSpeciesData sp, out AnimalBreedData breed)) return AnimalResult.Fail("That is not an animal.");
        // Checked again against the present: nothing between the plan and now may have filled the pen.
        string why = RoomFor(book, herd, sp, null);
        if (why != null) return AnimalResult.Fail(why);
        if (book.LivingOwnedBy(plan.actor.key) + AnimalBreeding.ExpectedFor(book, plan.actor.key) >= rules.maxAnimalsPerPlayer) return AnimalResult.Fail("You keep as many animals as you can look after.");
        int code = taken.Code(0);
        // A crate that holds one of the world's animals (Phase 5): that very animal is let out, and changes hands.
        if (AnimalItemCode.TryCrated(code, out _, out _, out _, out _))
            return CommitReleaseCrated(book, herd, plan, code, sp, breed, now, rules);
        string id = book.NewId(sp.id);
        bool coded = AnimalItemCode.TryLivestock(code, out AnimalSex sex, out GrowthStage stage, out int seed);
        if (!coded)
        {
            // A crate bought from a merchant carries no animal yet: the world makes one of its own, from its new id.
            seed = (int)(AnimalMath.Hash(AnimalMath.Hash(book.World), AnimalMath.Hash(id)) & 0xFFFFFF) | 1;
            stage = DefaultStartStage(sp);
            sex = sp.sexed ? (AnimalMath.Noise(seed, 99) < sp.femaleShare ? AnimalSex.Female : AnimalSex.Male) : AnimalSex.None;
        }
        if (sp.StageOrNearest(stage)?.stage != stage) stage = DefaultStartStage(sp);
        if (!sp.sexed) sex = AnimalSex.None;
        else if (sex == AnimalSex.None) sex = AnimalMath.Noise(seed, 99) < sp.femaleShare ? AnimalSex.Female : AnimalSex.Male;
        float development = sp.DevelopmentAtStart(stage);
        var a = new AnimalRecord
        {
            id = id,
            owner = plan.actor.key,
            ownerName = plan.actor.name ?? "",
            species = sp.id,
            breed = breed != null ? breed.id : "",
            name = CleanName(plan.request.text, rules.maxNameLength) ?? "",
            sex = sex,
            genes = AnimalGenes.FromSeed(breed, seed),
            development = development,
            stage = stage,
            bornAt = now - development / Mathf.Max(0.01f, rules.growthSpeed),
            acquiredAt = now,
            origin = "bought",
            pen = herd.pen.id,
            livedTo = Math.Max(now, herd.pen.livedTo),
            satiety = 75f,
            hydration = 85f,
            nutrition = 72f,
            happiness = 50f,
            stress = 25f,
            // Out of its crate: fairly clean, at ease, sound hooves.
            hygiene = 75f,
            comfort = 60f,
            hooves = 100f,
        };
        AnimalSim.Prime(a, sp, rules);
        // Its welfare and its life record start from where it is (not from the fields' defaults).
        AnimalWelfare.Prime(a, rules);
        if (!book.Add(a)) return AnimalResult.Fail("It could not be let out.");
        a.Log(AnimalEventKind.Acquired, now, $"Let out into {herd.spec?.name ?? "the pen"}.", plan.actor.key, rules.keepEvents);
        book.Urgent = true;
        return AnimalResult.Ok($"{AnimalSim.NameOf(a, sp)} ({Sex(sex)}{(breed != null ? ", " + breed.name : "")}) is yours now.", a.id);
    }

    // ---------------------------------------------------------------- committing: care
    /// <summary>One care action on one animal (see <see cref="AnimalCare.Apply"/>), with the water and the supply that came.</summary>
    private static AnimalResult CommitCare(AnimalRecord a, AnimalPlan plan, AnimalTaken taken, double now, AnimalRules rules, IAnimalCatalog cat, AnimalSimContext ctx)
    {
        if (a == null || a.pen != plan.request.pen) return AnimalResult.Fail("That animal is not here.");
        AnimalSpeciesData sp = cat.Species(a.species);
        AnimalCareActionSpec spec = sp?.CareAction(CareId(plan.request.text));
        if (!AnimalCare.CanDo(a, sp, spec, now, out string why)) return AnimalResult.Fail(why);
        if (!AnimalCare.EnoughWater(plan.plannedLitres, taken.litres)) return AnimalResult.Fail(NoWater);
        AgriWeather w = AnimalCare.WeatherAt(ctx, now);
        // The supply is optional: one that did not come is not missed (the action is done without it).
        bool supplied = plan.takeCount > 0 && taken.count > 0;
        AnimalResult res = AnimalResult.Ok("", a.id);
        res.message = AnimalCare.Apply(a, sp, spec, cat, rules, in w, plan.toolUsed, AnimalCare.WaterFraction(plan.plannedLitres, taken.litres), supplied,
            a.owner == plan.actor.key, plan.actor.key, plan.actor.name, now, res.lines);
        return res;
    }

    /// <summary>
    /// One care action on every animal of the pen that can take it (the same choice as the plan's, with the water, tool and
    /// supplies that came): "You brush 4 animals (2 were brushed lately)." The tool's wear becomes the animals really done.
    /// </summary>
    private static AnimalResult CommitHerdCare(AnimalHerd herd, AnimalPlan plan, AnimalTaken taken, double now, AnimalRules rules, IAnimalCatalog cat, AnimalSimContext ctx)
    {
        AnimalRequest r = plan.request;
        AnimalToolData supply = plan.takeCount > 0 ? cat.ToolOfItem(r.item) : null;
        HerdCare h = WalkHerdCare(herd, CareId(r.text), plan.actor, now, cat, false, plan.toolUsed, Mathf.Max(0f, taken.litres), supply,
            supply != null ? Math.Max(0, taken.count) : 0);
        if (h.targets.Count == 0) return AnimalResult.Fail(h.dry > 0 ? NoWater : HerdRefusal(h, cat));
        AgriWeather w = AnimalCare.WeatherAt(ctx, now);
        AnimalResult res = AnimalResult.Ok("");
        var said = new List<string>();
        string first = "";
        foreach (CareTarget t in h.targets)
        {
            said.Clear();
            string m = AnimalCare.Apply(t.animal, t.species, t.spec, cat, rules, in w, t.tool ? plan.toolUsed : default(AnimalToolUse), AnimalCare.WaterFraction(t.need, t.litres),
                t.supplied, t.animal.owner == plan.actor.key, plan.actor.key, plan.actor.name, now, said);
            if (first.Length == 0) first = m ?? "";
            foreach (string l in said)
                if (!res.lines.Contains(l)) res.lines.Add(l);
        }
        int withTool = h.WithTool;
        plan.wearUses = withTool;
        if (withTool == 0) plan.wearKey = null;
        if (h.targets.Count == 1)
        {
            // One animal: its own reaction.
            res.message = first;
            res.animal = h.targets[0].animal.id;
        }
        else res.message = HerdMessage(h);
        HerdLines(h, cat, res.lines, h.targets.Count == 1);
        return res;
    }

    /// <summary>"You brush 4 animals (2 were brushed lately)."</summary>
    private static string HerdMessage(HerdCare h)
    {
        AnimalCareActionSpec s = h.targets[0].spec;
        string who = $"{h.targets.Count} animals";
        string m;
        switch (s.kind)
        {
            case AnimalCareKind.Brush: m = $"You brush {who}"; break;
            case AnimalCareKind.Wash: m = $"You wash {who}"; break;
            case AnimalCareKind.Pet: m = $"You pet {who}"; break;
            case AnimalCareKind.CleanHooves: m = $"You clean the hooves of {who}"; break;
            default: m = $"{who} were {s.PastVerb}"; break;
        }
        if (h.lately > 0) m += $" ({h.lately} {Were(h.lately)} {h.verb} lately)";
        return m + ".";
    }

    /// <summary>What else a pen's care should say: those not done, and why.</summary>
    private static void HerdLines(HerdCare h, IAnimalCatalog cat, List<string> lines, bool lately)
    {
        if (lately && h.lately > 0) lines.Add($"{Some(h.lately)} more {Were(h.lately)} {h.verb} lately.");
        if (h.young > 0) lines.Add($"{Some(h.young)} {(h.young == 1 ? "is" : "are")} too young for it.");
        if (h.noTool > 0) lines.Add($"{Some(h.noTool)} {(h.noTool == 1 ? "needs" : "need")} {AnimalCare.ToolNames(cat, h.toolTags)}.");
        if (h.dry > 0) lines.Add("There was not enough water for " + (h.dry == 1 ? "one of them" : h.dry + " of them") + ".");
        if (h.lacking > 0) lines.Add($"{Some(h.lacking)} {(h.lacking == 1 ? "does" : "do")} not take it.");
        if (h.others > 0) lines.Add("The others are not yours to care for.");
    }

    // ---------------------------------------------------------------- committing: the pen
    /// <summary>
    /// Mucking out (see <see cref="AnimalHabitat.CleanPen"/>, with the tool's factor and the straw that came: the manure
    /// forked up goes into <see cref="AnimalPlan.give"/>), or scrubbing out the trough (empty and clean).
    /// </summary>
    private static AnimalResult CommitClean(AnimalHerd herd, AnimalPlan plan, AnimalTaken taken, double now, AnimalRules rules, IAnimalCatalog cat)
    {
        AnimalPenRecord pen = herd.pen;
        if (IsTrough(plan.request.text))
        {
            if (!AnimalHabitat.CanScrub(pen, rules)) return AnimalResult.Fail("The trough does not need scrubbing.");
            AnimalHabitat.ScrubTrough(pen);
            AnimalResult scrubbed = AnimalResult.Ok("You tip out the foul water and scrub out the trough.");
            scrubbed.lines.Add("Fill it with fresh water.");
            return scrubbed;
        }
        // The straw is optional - unless laying it was all there was to do.
        int straw = plan.takeCount > 0 ? Math.Max(0, taken.count) : 0;
        if (plan.takeCount > 0 && straw <= 0 && pen.dirt < MuckFrom) return AnimalResult.Fail(NoLonger);
        AnimalCleanOutcome o = AnimalHabitat.CleanPen(herd, cat, rules, AnimalCare.ToolFactor(plan.toolUsed, rules), straw, now, plan.actor.key, plan.actor.name);
        plan.give.Clear();
        if (o.manure > 0 && !string.IsNullOrEmpty(rules.manureItem)) plan.give.Add(new AnimalItemChange { item = rules.manureItem, count = o.manure });
        bool forked = o.removed > 0.01f;
        AnimalResult res = AnimalResult.Ok(forked || !o.strawLaid ? "You muck out the pen." : "You lay fresh straw.");
        if (forked && o.strawLaid) res.lines.Add("Fresh straw goes down.");
        if (o.manure > 0) res.lines.Add($"You fork up {o.manure} manure.");
        if (o.disturbed > 0) res.lines.Add("The animals shuffle about while you work.");
        if (plan.toolUsed.has && plan.toolUsed.edge <= 0f) res.lines.Add($"Your {ToolName(cat, plan.toolUsed)} is worn out: it does a poor job.");
        if (AnimalHabitat.CanScrub(pen, rules)) res.lines.Add("The trough water is fouled: it wants scrubbing out.");
        return res;
    }

    // ---------------------------------------------------------------- committing: health
    /// <summary>A careful look (see <see cref="AnimalHealth.Examine"/>): what shows enough is named.</summary>
    private static AnimalResult CommitExamine(AnimalRecord a, AnimalPlan plan, double now, AnimalRules rules, IAnimalCatalog cat, AnimalSimContext ctx)
    {
        if (a == null || !a.Alive) return AnimalResult.Fail("It cannot be looked over now.");
        AnimalSpeciesData sp = cat.Species(a.species);
        AnimalResult res = AnimalResult.Ok("", a.id);
        res.message = AnimalHealth.Examine(a, sp, cat, rules, now, plan.actor.key, plan.actor.name, ctx != null && ctx.vetAvailable, res.lines);
        return res;
    }

    /// <summary>A dose for one animal (see <see cref="AnimalHealth.Treat"/>), as strong as the copies taken (fewer than it needs: weaker).</summary>
    private static AnimalResult CommitTreat(AnimalRecord a, AnimalPlan plan, AnimalTaken taken, double now, AnimalRules rules, IAnimalCatalog cat, AnimalSimContext ctx)
    {
        if (a == null || !a.Alive) return AnimalResult.Fail("It cannot be treated now.");
        AnimalSpeciesData sp = cat.Species(a.species);
        AnimalTreatmentData tr = cat.TreatmentOfItem(plan.request.item);
        if (sp == null || tr == null) return AnimalResult.Fail("It cannot be treated now.");
        float strength = AnimalHealth.Strength(tr, taken, plan.plannedCount);
        AnimalResult res = AnimalResult.Ok("", a.id);
        res.message = AnimalHealth.Treat(a, sp, tr, cat, rules, strength, a.owner == plan.actor.key, plan.actor.key, plan.actor.name, now,
            ctx != null && ctx.vetAvailable, res.lines);
        return res;
    }

    /// <summary>
    /// A dose for every animal of the pen the actor may care for, each at the same strength (the copies' quality, x the share
    /// of the pen's doses that came), but those that had it too lately (left out of the plan too).
    /// </summary>
    private static AnimalResult CommitHerdTreat(AnimalHerd herd, AnimalPlan plan, AnimalTaken taken, double now, AnimalRules rules, IAnimalCatalog cat, AnimalSimContext ctx)
    {
        AnimalTreatmentData tr = cat.TreatmentOfItem(plan.request.item);
        if (tr == null) return AnimalResult.Fail("That is not a medicine for animals.");
        if (tr.route == AnimalTreatmentRoute.Topical) return AnimalResult.Fail($"{tr.DisplayName} is put on one animal at a time.");
        float strength = AnimalHealth.Strength(tr, taken, plan.plannedCount);
        bool vet = ctx != null && ctx.vetAvailable;
        AnimalResult res = AnimalResult.Ok("");
        var said = new List<string>();
        int n = 0, helped = 0, left = 0;
        string first = "", firstId = "";
        foreach (AnimalRecord m in herd.animals)
        {
            if (m == null || !m.Alive || !MayCare(m, plan.actor)) continue;
            AnimalSpeciesData sp = cat.Species(m.species);
            if (sp == null) continue;
            if (AnimalHealth.WaitBefore(m, tr, now) > 0.0)
            {
                left++;
                continue;
            }
            if (ActsOn(m, tr, cat)) helped++;
            said.Clear();
            string msg = AnimalHealth.Treat(m, sp, tr, cat, rules, strength, m.owner == plan.actor.key, plan.actor.key, plan.actor.name, now, vet, said);
            if (n == 0)
            {
                first = msg ?? "";
                firstId = m.id;
            }
            n++;
            foreach (string l in said)
                if (l != NoHelp && !res.lines.Contains(l)) res.lines.Add(l);
        }
        if (n == 0) return AnimalResult.Fail(left > 0 ? "Too soon for another dose." : "None of these animals are yours to treat.");
        res.message = n == 1 ? first : $"{n} animals take the {tr.DisplayName.ToLowerInvariant()}.";
        res.animal = n == 1 ? firstId : "";
        if (helped == 0) res.lines.Insert(0, NoHelp);
        if (left > 0) res.lines.Add(left == 1 ? "One had it too lately and was left out." : $"{left} had it too lately and were left out.");
        return res;
    }

    /// <summary>
    /// Does a medicine act on an illness the animal has that its keeper can know of (one that has shown; not one it is
    /// recovering from)? A hidden one is treated too, but never told of.
    /// </summary>
    private static bool ActsOn(AnimalRecord a, AnimalTreatmentData tr, IAnimalCatalog cat)
    {
        if (a?.conditions == null || tr == null) return false;
        foreach (AnimalCondition k in a.conditions)
            if (k != null && !k.Recovering && k.shown && tr.EffectOn(k.id) != null && cat.Condition(k.id) != null) return true;
        return false;
    }

    /// <summary>
    /// The veterinarian's visit, paid for (see <see cref="AnimalHealth.VetVisit"/>, which logs it in the histories). When
    /// someone other than its owner sent for the vet, its owner is told.
    /// </summary>
    private static AnimalResult CommitVet(AnimalBook book, AnimalRecord a, AnimalPlan plan, double now, AnimalRules rules, IAnimalCatalog cat, AnimalSimContext ctx)
    {
        if (a == null || !a.Alive) return AnimalResult.Fail("The veterinarian cannot see it now.");
        AnimalSpeciesData sp = cat.Species(a.species);
        AnimalResult res = AnimalResult.Ok("", a.id);
        res.message = AnimalHealth.VetVisit(a, sp, cat, rules, now, plan.actor.key, plan.actor.name, res.lines);
        if (a.owner != plan.actor.key)
        {
            string who = string.IsNullOrWhiteSpace(plan.actor.name) ? "someone" : plan.actor.name.Trim();
            ctx?.Notice(a, AnimalEventKind.SeenByVet, $"{AnimalSim.NameOf(a, sp)} was seen by the veterinarian, sent for by {who}.");
        }
        // Money changed hands: saved soon.
        book.Urgent = true;
        return res;
    }

    /// <summary>
    /// Its owner slaughters it: it dies (nobody is told: its owner did it), and its history says so once, as slaughtered. Its
    /// carcass can then be butchered (when its species has an anatomy) or its remains cleared.
    /// </summary>
    private static AnimalResult CommitSlaughter(AnimalBook book, AnimalRecord a, AnimalPlan plan, double now, AnimalRules rules, IAnimalCatalog cat, AnimalSimContext ctx)
    {
        if (a == null || !a.Alive) return AnimalResult.Fail("It is dead.");
        if (a.owner != plan.actor.key) return AnimalResult.Fail("Only its owner may do that.");
        AnimalSpeciesData sp = cat.Species(a.species);
        List<AnimalNotice> notices = ctx.notices;
        ctx.notices = null;
        try
        {
            AnimalSim.Die(a, AnimalDeathCause.Slaughtered, now, ctx, sp);
        }
        finally
        {
            ctx.notices = notices;
        }
        if (a.Alive) return AnimalResult.Fail("It could not be done.");
        const string text = "Slaughtered by its owner.";
        if (!RelabelDeath(a, now, text, plan.actor.key)) a.Log(AnimalEventKind.Slaughtered, now, text, plan.actor.key, rules.keepEvents);
        if (!RelabelHealthDeath(a, now, plan.actor.key)) AnimalHealth.Log(a, AnimalHealthEventKind.Died, now, "", "", "Slaughtered", plan.actor.key, rules.keepHealthLog);
        a.revision++;
        book.Urgent = true;
        string name = Called(a, sp, true);
        bool carcass = sp != null && !string.IsNullOrEmpty(sp.harvestAnatomy);
        return AnimalResult.Ok(carcass ? $"{name} is slaughtered. Its carcass can be butchered." : $"{name} is slaughtered. Its remains can be cleared away.", a.id);
    }

    /// <summary>Makes the "died" event its death just logged (at <paramref name="at"/>) read as a slaughter. False: none was logged.</summary>
    private static bool RelabelDeath(AnimalRecord a, double at, string text, string by)
    {
        if (a.events == null) return false;
        for (int i = a.events.Count - 1; i >= 0 && i >= a.events.Count - 3; i--)
        {
            AnimalEvent e = a.events[i];
            if (e == null || e.kind != AnimalEventKind.Died || Math.Abs(e.at - at) > 1e-6) continue;
            e.kind = AnimalEventKind.Slaughtered;
            e.text = text;
            e.by = by ?? "";
            return true;
        }
        return false;
    }

    /// <summary>Makes a health-history "died" just logged (at <paramref name="at"/>) read "Slaughtered". False: none was logged.</summary>
    private static bool RelabelHealthDeath(AnimalRecord a, double at, string by)
    {
        if (a.healthLog == null || a.healthLog.Count == 0) return false;
        AnimalHealthEntry e = a.healthLog[a.healthLog.Count - 1];
        if (e == null || e.kind != AnimalHealthEventKind.Died || Math.Abs(e.at - at) > 1e-6) return false;
        e.text = "Slaughtered";
        e.condition = "";
        e.stage = "";
        e.by = by ?? "";
        e.shown = true;
        e.named = true;
        return true;
    }

    /// <summary>
    /// Butchering changes nothing here: the plan's remains report is what the caller lays out as a carcass after the
    /// commit; it removes the record only once the carcass is laid out (so nothing needs undoing when it cannot be).
    /// </summary>
    private static AnimalResult CommitButcher(AnimalRecord a, AnimalPlan plan, IAnimalCatalog cat)
    {
        if (a == null || a.Alive || plan.remains == null) return AnimalResult.Fail("There is nothing to butcher.");
        AnimalSpeciesData sp = cat.Species(a.species);
        return AnimalResult.Ok($"You lay out {Called(a, sp, false)} for butchering.", a.id);
    }

    /// <summary>The stage a young animal of a species is sold at: its Baby stage, else its first one past Newborn, else its first.</summary>
    public static GrowthStage DefaultStartStage(AnimalSpeciesData sp)
    {
        if (sp.Stage(GrowthStage.Baby) != null) return GrowthStage.Baby;
        foreach (AnimalStageSpec st in sp.stages)
            if (st != null && st.stage > GrowthStage.Newborn) return st.stage;
        return sp.FirstStage;
    }

    public static string Sex(AnimalSex s) => s == AnimalSex.Female ? "female" : s == AnimalSex.Male ? "male" : "unsexed";

    // ---------------------------------------------------------------- the world's own changes
    /// <summary>
    /// An animal whose owner can no longer reach its pen (the lease ended, they were no longer let in) goes to the boarding
    /// stable: it keeps living there, fed a plain diet, until its owner brings it home to a pen they keep.
    /// </summary>
    public static void Board(AnimalBook book, AnimalRecord a, double now, AnimalSimContext ctx, string reason)
    {
        if (a == null || !a.Alive || string.IsNullOrEmpty(a.pen)) return;
        book.BringUpToDate(a, now, ctx);
        if (!a.Alive) return; // it died meanwhile: its remains stay in its pen
        AnimalSpeciesData sp = ctx.catalog.Species(a.species);
        book.MoveTo(a, "");
        string text = $"{AnimalSim.NameOf(a, sp)} was taken to the boarding stable ({reason}).";
        a.Log(AnimalEventKind.Boarded, now, text, "", book.Rules.keepEvents);
        ctx.Notice(a, AnimalEventKind.Boarded, text);
        book.Urgent = true;
    }
}
