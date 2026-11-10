using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase 5's commands: crating an animal (to sell or trade it) and letting a crated one out to its new owner, improving a
/// pen and fitting it with machines (a feed hopper, an automatic waterer, egg trays), paying its upkeep, and what the
/// egg trays and the hopper add to Collect and StockFeeder. Every one is planned and committed like the others: the price
/// is the one the player was shown, the money is taken before the commit and given back if it fails.
/// </summary>
public static partial class AnimalCommands
{
    // ---------------------------------------------------------------- the hopper and the trays in other commands
    /// <summary>A StockFeeder request for the hopper (its text "hopper").</summary>
    private static bool ToHopper(AnimalRequest r) => string.Equals((r.text ?? "").Trim(), AnimalUpgrades.HopperText, StringComparison.OrdinalIgnoreCase);

    /// <summary>A Collect request for the egg trays only (its text "trays").</summary>
    private static bool TraysAsked(AnimalRequest r) => string.Equals((r.text ?? "").Trim(), AnimalUpgrades.TraysText, StringComparison.OrdinalIgnoreCase);

    /// <summary>A tray lot a Collect request asks for: all (no text, or "trays"), or the product its text names.</summary>
    private static bool TrayAsked(AnimalTrayLot t, AnimalRequest r) =>
        t != null && (string.IsNullOrEmpty(r.text) || TraysAsked(r) || string.Equals(t.product, r.text, StringComparison.OrdinalIgnoreCase));

    /// <summary>The units waiting in a pen's egg trays that this player may take for this request.</summary>
    private static int TrayUnitsFor(AnimalPenRecord pen, AnimalRequest r, AnimalActor actor, IAnimalCatalog cat)
    {
        if (pen?.tray == null || actor == null || cat == null) return 0;
        int n = 0;
        foreach (AnimalTrayLot t in pen.tray)
            if (t?.lot != null && t.lot.units > 0 && TrayAsked(t, r) && AnimalUpgrades.MayCollect(t, actor.key, actor.rights, cat.Rules)
                && AnimalProduction.Spec(cat.Species(t.species), t.product) != null) n += t.lot.units;
        return n;
    }

    /// <summary>
    /// Takes what waits in the egg trays for the player (each lot only by who may collect from its animal): every lot goes
    /// into the product ledger with its whole origin, as if collected from the nest, and becomes item copies with its code.
    /// An animal's owner is told when someone else took its eggs. Returns the units taken.
    /// </summary>
    private static int CollectTrays(AnimalBook book, AnimalHerd herd, AnimalPlan plan, double now, AnimalRules rules, IAnimalCatalog cat, AnimalSimContext ctx,
        Dictionary<string, AnimalItemChange> byItem, List<string> order, out float meanQuality, out AnimalProductTier bestTier, out string what)
    {
        meanQuality = 0f;
        bestTier = AnimalProductTier.None;
        what = "";
        AnimalPenRecord pen = herd?.pen;
        if (pen?.tray == null || pen.tray.Count == 0) return 0;
        AnimalRequest r = plan.request;
        int total = 0;
        float qSum = 0f;
        var told = new HashSet<string>(StringComparer.Ordinal);
        AnimalProductSpec lastSpec = null;
        var kept = new List<AnimalTrayLot>(pen.tray.Count);
        foreach (AnimalTrayLot t in pen.tray)
        {
            if (t?.lot == null || t.lot.units <= 0) continue;
            AnimalSpeciesData sp = cat.Species(t.species);
            AnimalProductSpec p = AnimalProduction.Spec(sp, t.product);
            if (p == null || !TrayAsked(t, r) || !AnimalUpgrades.MayCollect(t, plan.actor.key, plan.actor.rights, rules))
            {
                kept.Add(t);
                continue;
            }
            AnimalProductLot lot = t.lot;
            float q = AnimalItemCode.Hundredth(lot.quality);
            AnimalProductTier tier = AnimalProduction.Tier(q, t.rare, rules.production);
            long serial = book.Ledger.NextSerial();
            book.Ledger.Add(new AnimalProductRecord
            {
                serial = serial, product = p.id, animal = t.animal, animalName = t.animalName ?? "", species = t.species, breed = t.breed, owner = t.owner,
                cycle = lot.cycle, cycleStart = lot.cycleStart, madeAt = lot.madeAt, collectedAt = now, by = plan.actor.key ?? "", units = lot.units,
                quality = q, tier = tier, nutrition = lot.nutrition, welfare = lot.welfare, health = lot.health, environment = lot.environment,
                hygiene = lot.hygiene,
            });
            int code = AnimalItemCode.Product(q, serial, t.rare);
            if (!byItem.TryGetValue(p.Item, out AnimalItemChange change))
            {
                change = new AnimalItemChange { item = p.Item, count = 0, codes = new List<int>() };
                order.Add(p.Item);
            }
            change.count += lot.units;
            for (int i = 0; i < lot.units; i++) change.codes.Add(code);
            byItem[p.Item] = change;
            total += lot.units;
            qSum += q * lot.units;
            if (tier > bestTier) bestTier = tier;
            lastSpec = p;
            // Its animal's history, where it still lives in the world.
            AnimalRecord a = book.Find(t.animal);
            if (a != null)
            {
                AnimalProduction.Log(a, new AnimalProductEntry
                {
                    at = now, product = p.id, kind = AnimalProductEntryKind.Collected, units = lot.units, quality = q, cycle = lot.cycle, by = plan.actor.key ?? "",
                }, rules.production);
                if (t.owner != plan.actor.key && a.owner == t.owner && told.Add(a.id))
                {
                    string who = string.IsNullOrWhiteSpace(plan.actor.name) ? "someone" : plan.actor.name.Trim();
                    ctx?.Notice(a, AnimalEventKind.Collected, $"{who} collected {AnimalSim.NameIn(a, sp)}'s {p.DisplayName.ToLowerInvariant()} from the egg trays.");
                }
            }
        }
        if (total <= 0) return 0;
        pen.tray.Clear();
        pen.tray.AddRange(kept);
        meanQuality = qSum / total;
        what = lastSpec != null ? lastSpec.Count(total) : total.ToString();
        return total;
    }

    // ---------------------------------------------------------------- crating and letting a crated animal out
    /// <summary>
    /// Crating an animal: its owner's, here and alive, fit to travel (not expecting, not nursing, not too weak: see
    /// <see cref="AnimalTrade.WhyNotCrate"/>), while the world has crate serials left.
    /// </summary>
    private static AnimalPlan PlanCrate(AnimalBook book, AnimalHerd herd, AnimalRecord a, AnimalPlan plan, AnimalRequest r, AnimalActor actor, AnimalRules rules,
        IAnimalCatalog cat)
    {
        if (a == null || a.pen != r.pen) return Refuse(plan, "That animal is not here.");
        if (!a.Alive) return Refuse(plan, "It is dead.");
        if (a.owner != actor.key) return Refuse(plan, "Only its owner may crate it.");
        AnimalSpeciesData sp = cat.Species(a.species);
        if (sp == null) return Refuse(plan, "It cannot be crated now.");
        string why = AnimalTrade.WhyNotCrate(a, sp, herd, cat, rules);
        if (why != null) return Refuse(plan, why);
        if (book.CrateSerial >= AnimalItemCode.MaxCrateSerial) return Refuse(plan, "No more animals can be crated in this world.");
        return plan;
    }

    /// <summary>
    /// Puts the animal in a crate (checked again against the present): it leaves the world, judged (its grade) as it is now,
    /// and its owner is given the crate - a livestock item whose code names it (see <see cref="AnimalItemCode.Crated"/>).
    /// Its history keeps it. The world is saved soon after (a crate given must never find its animal back in the pen).
    /// </summary>
    private static AnimalResult CommitCrate(AnimalBook book, AnimalHerd herd, AnimalRecord a, AnimalPlan plan, double now, AnimalRules rules, IAnimalCatalog cat,
        AnimalSimContext ctx)
    {
        if (a == null || a.pen != plan.request.pen || !a.Alive) return AnimalResult.Fail("That animal is not here.");
        if (a.owner != plan.actor.key) return AnimalResult.Fail("Only its owner may crate it.");
        AnimalSpeciesData sp = cat.Species(a.species);
        if (sp == null) return AnimalResult.Fail("It cannot be crated now.");
        string why = AnimalTrade.WhyNotCrate(a, sp, herd, cat, rules);
        if (why != null) return AnimalResult.Fail(why);
        AnimalBreedData breed = sp.BreedOrDefault(a.breed);
        int grade = AnimalTrade.Grade(a, rules);
        string name = AnimalSim.NameOf(a, sp);
        string id = a.id;
        long serial = book.Crate(a, now, ctx != null ? ctx.realHours : 0.0, grade, plan.actor.key, plan.actor.name);
        if (serial <= 0) return AnimalResult.Fail("It could not be crated.");
        string who = string.IsNullOrWhiteSpace(plan.actor.name) ? "its owner" : plan.actor.name.Trim();
        a.Log(AnimalEventKind.Crated, now, $"Put in a crate by {who} ({AnimalTrade.GradeWord(grade)} stock).", plan.actor.key, rules.keepEvents);
        plan.give.Clear();
        plan.give.Add(new AnimalItemChange
        {
            item = AnimalIds.Livestock(sp.id, breed != null ? breed.id : ""), count = 1,
            codes = new List<int> { AnimalItemCode.Crated(a.sex, a.stage, grade, serial) },
        });
        book.Urgent = true;
        AnimalResult res = AnimalResult.Ok($"{name} is in a crate: let it out into a pen, sell it to a merchant, or trade it.", id);
        res.lines.Add($"{char.ToUpperInvariant(AnimalTrade.GradeWord(grade)[0])}{AnimalTrade.GradeWord(grade).Substring(1)} stock: worth about x{AnimalTrade.ValueFactor(a.stage, grade, rules):0.0} a young one.");
        float days = AnimalTrade.RulesOf(rules).crateKeepDays;
        if (days > 0f) res.lines.Add($"Let it out within {days:0.#} days, or it is gone (the carrier sells it on).");
        return res;
    }

    /// <summary>
    /// Lets a crated animal out (the release's room, habitat and keeping limit were checked): the crate must still hold it
    /// and be of its kind; it lives here from now, as it was crated. Let out by someone else, it changes hands: its new owner
    /// has it, with the rules' share of its affinity, and its history says from whom. A crate is let out once.
    /// </summary>
    private static AnimalResult CommitReleaseCrated(AnimalBook book, AnimalHerd herd, AnimalPlan plan, int code, AnimalSpeciesData sp, AnimalBreedData breed,
        double now, AnimalRules rules)
    {
        // A crated animal is let out only when its very crate was chosen (never as "any crate of the kind").
        if (plan.request.code != code) return AnimalResult.Fail("Choose which crate to let out: that one holds an animal of the world.");
        AnimalItemCode.TryCrated(code, out AnimalSex sex, out GrowthStage stage, out int grade, out long serial);
        AnimalCrated c = book.Crated(serial);
        AnimalRecord a = c?.animal;
        if (a == null) return AnimalResult.Fail("The crate is empty: the animal it held is gone.");
        AnimalBreedData its = sp.BreedOrDefault(a.breed);
        // The crate and the animal must agree in everything the code holds (a stale or forged copy does not open another).
        if (a.species != sp.id || (its != null ? its.id : "") != (breed != null ? breed.id : "") || a.sex != sex || a.stage != stage || c.grade != grade)
            return AnimalResult.Fail("The crate does not hold what it says.");
        if (book.Find(a.id) != null) return AnimalResult.Fail("The crate is empty: the animal it held is gone.");
        string name = CleanName(plan.request.text, rules.maxNameLength);
        string from = a.owner ?? "", fromName = string.IsNullOrWhiteSpace(a.ownerName) ? "its keeper" : a.ownerName.Trim();
        bool changesHands = from != plan.actor.key;
        if (!book.Uncrate(serial, herd.pen.id, Math.Max(now, herd.pen.livedTo))) return AnimalResult.Fail("It could not be let out.");
        string whoNow = string.IsNullOrWhiteSpace(plan.actor.name) ? "its new keeper" : plan.actor.name.Trim();
        if (changesHands)
        {
            a.owner = plan.actor.key;
            a.ownerName = plan.actor.name ?? "";
            a.acquiredAt = now;
            a.origin = "traded";
            // A new keeper: what it felt for the last one is mostly not theirs; their time with it starts now.
            a.affinity = AnimalMath.Clamp100(a.affinity * AnimalMath.Clamp01(AnimalTrade.RulesOf(rules).affinityKept));
            a.lastOwnerCareAt = now;
            a.lastOwnerNearAt = -1.0;
            a.togetherDay = -1;
            a.togetherGiven = 0f;
            a.Log(AnimalEventKind.ChangedHands, now, $"Changed hands: from {fromName} to {whoNow}; let out into {herd.spec?.name ?? "a pen"}.", plan.actor.key, rules.keepEvents);
        }
        else a.Log(AnimalEventKind.Moved, now, $"Let out of its crate into {herd.spec?.name ?? "a pen"}.", plan.actor.key, rules.keepEvents);
        if (name != null) a.name = name;
        book.Urgent = true;
        return AnimalResult.Ok(changesHands
            ? $"{AnimalSim.NameOf(a, sp)} ({Sex(a.sex)}, {AnimalSim.Stage(a.stage).ToLowerInvariant()}{(its != null ? ", " + its.name : "")}) is yours now."
            : $"{AnimalSim.NameOf(a, sp)} is out of its crate.", a.id);
    }

    // ---------------------------------------------------------------- upgrades and machines
    /// <summary>
    /// Improving the pen ("level") or fitting a machine ("hopper", "waterer", "trays"): one who keeps animals here, a pen
    /// not in disrepair, a level above or a machine it lacks, and the price the player was shown. "remove:&lt;machine&gt;"
    /// takes one out (free; egg trays only once empty).
    /// </summary>
    private static AnimalPlan PlanUpgrade(AnimalHerd herd, AnimalPlan plan, AnimalRequest r, AnimalActor actor, AnimalRules rules)
    {
        AnimalUpgradeRules u = AnimalUpgrades.RulesOf(rules);
        AnimalPenRecord pen = herd.pen;
        AnimalPenSpec spec = herd.spec ?? new AnimalPenSpec();
        string text = (r.text ?? "").Trim().ToLowerInvariant();
        AnimalDevice d = AnimalUpgrades.DeviceOf(text);
        // A machine is taken out by who fitted it or one who may care for others' animals here (not a passer-by), even
        // while upgrades are off (its feed is never stuck in it).
        if (AnimalUpgrades.IsRemoval(text))
        {
            if (d == AnimalDevice.None) return Refuse(plan, "Take out what?");
            if (!AnimalUpgrades.Has(pen, d)) return Refuse(plan, $"There is no {AnimalUpgrades.DeviceName(d).ToLowerInvariant()} here.");
            if (!AnimalUpgrades.MayRemove(pen, d, actor.key, actor.rights)) return Refuse(plan, "Only who fitted it, or the pen's keepers, may take it out.");
            if (d == AnimalDevice.Trays && pen.TrayUnits > 0) return Refuse(plan, "Collect what waits in the egg trays first.");
            return plan;
        }
        if (!u.enabled) return Refuse(plan, "Pens are not improved here.");
        if ((actor.rights & AnimalRights.Keep) == 0) return Refuse(plan, "Only one who keeps animals here may build in the pen.");
        if (AnimalUpgrades.InDisrepair(pen, spec, rules)) return Refuse(plan, $"The pen owes {AnimalUpgrades.OwedPrice(pen)} in upkeep: pay it first.");
        long price;
        if (text == AnimalUpgrades.LevelText)
        {
            price = AnimalUpgrades.NextLevelPrice(pen, spec, rules);
            if (price < 0) return Refuse(plan, "The pen is as good as it gets.");
        }
        else if (d != AnimalDevice.None)
        {
            if (AnimalUpgrades.Has(pen, d)) return Refuse(plan, $"It has {AnimalUpgrades.DeviceName(d).ToLowerInvariant()} already.");
            string why = AnimalUpgrades.WhyNotFit(spec, d);
            if (why != null) return Refuse(plan, why);
            price = AnimalUpgrades.DevicePrice(d, rules);
        }
        else return Refuse(plan, "That cannot be built here.");
        // The price is the one the player saw, or nothing is done (the window shows the new one).
        if (r.expected != price) return Refuse(plan, $"It costs {price} now.");
        plan.fee = price;
        return plan;
    }

    private static AnimalResult CommitUpgrade(AnimalHerd herd, AnimalPlan plan, double now, AnimalRules rules)
    {
        AnimalPenRecord pen = herd.pen;
        AnimalPenSpec spec = herd.spec ?? new AnimalPenSpec();
        string text = (plan.request.text ?? "").Trim().ToLowerInvariant();
        AnimalDevice d = AnimalUpgrades.DeviceOf(text);
        AnimalResult res;
        if (AnimalUpgrades.IsRemoval(text))
        {
            if (d == AnimalDevice.None || !AnimalUpgrades.Has(pen, d)) return AnimalResult.Fail("There is nothing to take out.");
            if (!AnimalUpgrades.MayRemove(pen, d, plan.actor.key, plan.actor.rights)) return AnimalResult.Fail("Only who fitted it, or the pen's keepers, may take it out.");
            if (d == AnimalDevice.Trays && pen.TrayUnits > 0) return AnimalResult.Fail("Collect what waits in the egg trays first.");
            res = AnimalResult.Ok($"You take out the {AnimalUpgrades.DeviceName(d).ToLowerInvariant()}.");
            if (d == AnimalDevice.Hopper && pen.HopperPortions > 0f)
            {
                float put = AnimalUpgrades.EmptyHopper(pen, spec, rules, now, out float lost);
                if (put > 0f) res.lines.Add($"Its feed goes into the feeder ({put:0.#} portions).");
                if (lost > 0.05f) res.lines.Add($"{lost:0.#} portions did not fit and are lost.");
            }
            AnimalUpgrades.Set(pen, d, false);
        }
        else
        {
            // Checked again against the present (the price was taken for what the plan saw).
            if (AnimalUpgrades.InDisrepair(pen, spec, rules)) return AnimalResult.Fail("The pen owes upkeep: pay it first.");
            if (text == AnimalUpgrades.LevelText)
            {
                if (AnimalUpgrades.NextLevelPrice(pen, spec, rules) != plan.fee) return AnimalResult.Fail("The price has changed.");
                pen.level = AnimalUpgrades.Level(pen, rules) + 1;
                AnimalPenLevelSpec l = AnimalUpgrades.LevelSpec(pen.level, rules);
                res = AnimalResult.Ok($"The pen is now {AnimalUpgrades.LevelName(pen.level, rules).ToLowerInvariant()} (level {pen.level}).");
                if (l != null)
                {
                    res.lines.Add($"Room for {Mathf.RoundToInt(l.space * 100f)}% more, comfort +{l.comfort:0}, {Mathf.RoundToInt((1f - l.dirt) * 100f)}% less dirt, products {Mathf.RoundToInt(l.production * 100f)}% faster.");
                    res.lines.Add($"Upkeep: {AnimalUpgrades.UpkeepPerDay(pen, spec, rules):0.#} a day.");
                }
            }
            else
            {
                if (d == AnimalDevice.None || AnimalUpgrades.Has(pen, d) || AnimalUpgrades.WhyNotFit(spec, d) != null) return AnimalResult.Fail("That cannot be built here.");
                if (AnimalUpgrades.DevicePrice(d, rules) != plan.fee) return AnimalResult.Fail("The price has changed.");
                AnimalUpgrades.Set(pen, d, true, plan.actor.key);
                res = AnimalResult.Ok($"The pen is fitted with {AnimalUpgrades.DeviceName(d).ToLowerInvariant()}.");
                AnimalUpgradeRules u = AnimalUpgrades.RulesOf(rules);
                switch (d)
                {
                    case AnimalDevice.Hopper:
                        res.lines.Add($"Fill it ({u.hopperCapacity:0} portions): it keeps the feeder {Mathf.RoundToInt(u.hopperKeepsFeeder * 100f)}% full, and feed keeps better in it.");
                        break;
                    case AnimalDevice.Waterer:
                        res.lines.Add($"It keeps the trough topped up with clean water ({u.watererLitresPerHour:0.#} L an hour).");
                        break;
                    case AnimalDevice.Trays:
                        res.lines.Add($"Eggs roll into the trays as they are laid ({u.trayCapacity} hold): the nests never fill.");
                        break;
                }
                res.lines.Add($"Upkeep: {AnimalUpgrades.UpkeepPerDay(pen, spec, rules):0.#} a day.");
            }
        }
        pen.upgradedAt = now;
        pen.upgradedBy = plan.actor.key ?? "";
        return res;
    }

    /// <summary>
    /// Paying the pen's upkeep: one who tends it; what it owes, and (the request's count) that many game days ahead - so its
    /// machines keep working while its keepers are away - for the sum the player was shown.
    /// </summary>
    private static AnimalPlan PlanPayUpkeep(AnimalHerd herd, AnimalPlan plan, AnimalRequest r, AnimalActor actor, AnimalRules rules)
    {
        if ((actor.rights & AnimalRights.Tend) == 0) return Refuse(plan, "Not your pen.");
        AnimalPenSpec spec = herd.spec ?? new AnimalPenSpec();
        if (r.count > 0 && AnimalUpgrades.UpkeepPerDay(herd.pen, spec, rules) <= 0f) return Refuse(plan, "The pen costs nothing to keep up.");
        long price = AnimalUpgrades.PayPrice(herd.pen, spec, rules, r.count);
        if (price <= 0) return Refuse(plan, r.count > 0 ? "It is paid that far ahead already." : "The pen owes nothing.");
        if (r.expected != price) return Refuse(plan, $"That comes to {price} now.");
        plan.fee = price;
        return plan;
    }

    private static AnimalResult CommitPayUpkeep(AnimalHerd herd, AnimalPlan plan, AnimalRules rules)
    {
        AnimalPenRecord pen = herd.pen;
        AnimalPenSpec spec = herd.spec ?? new AnimalPenSpec();
        if (plan.fee <= 0 || AnimalUpgrades.PayPrice(pen, spec, rules, plan.request.count) != plan.fee) return AnimalResult.Fail("What the pen owes has changed.");
        bool was = AnimalUpgrades.InDisrepair(pen, spec, rules);
        pen.upkeepOwed -= plan.fee;
        AnimalResult res = AnimalResult.Ok($"You pay {plan.fee} for the pen's upkeep.");
        float ahead = AnimalUpgrades.PaidAheadDays(pen, spec, rules);
        if (ahead >= 0.05f) res.lines.Add($"It is paid {ahead:0.#} days ahead.");
        if (was) res.lines.Add("It is in good repair again: its machines work, and its level counts.");
        return res;
    }
}
