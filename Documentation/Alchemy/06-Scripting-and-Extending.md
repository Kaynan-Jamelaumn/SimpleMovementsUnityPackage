# Alchemy 06 — Scripting and Extending

**Scripts:** `Alchemy/Core/AlchemyBrew.cs`, `AlchemyMixture.cs`, `AlchemyRules.cs`, `AlchemyConditions.cs`, `AlchemyJournal.cs`,
`AlchemyProcessing.cs`, `Alchemy/Runtime/AlchemyStation.cs`, `AlchemyConditionSources.cs`, `AlchemyKnowledge.cs`,
`Alchemy/UI/AlchemyMinigames.cs`.

---

## 1. Preparations from code

```csharp
AlchemyRules rules = AlchemyDatabase.Instance.Rules;                 // or AlchemyDefaults.Rules() for the built-in ones
var brew = new AlchemyBrew(rules, AlchemyEquipmentProfile.DefaultCauldron(), ambient: 20, seed: 1);

brew.AddIngredient(AlchemyDefaults.Spec("Spring Water"), 2, out _);  // any IAlchemyIngredient (items are)
brew.AddIngredient(myIngredientItem, 3, out string why);
brew.AddIngredient(sunpetalItem, 3, out why, form: "coarse", evenness: 0.6f); // a grind level (rules.GrindLevels(profile))
brew.AddSubstance("bitter_sludge", 0.4, 20, out why, "left in the vessel"); // a substance directly (residue, scripts)

brew.SetHeat(0.35);                                                  // a burner: -1 cooling … 0 off … 1 full heat
brew.AddFuel("firewood", 2, out why);                                // a manual fire (rules.Fuels): the flames follow the fuel
brew.Bellows();                                                      // one pump of air (fades over a few seconds)
brew.SetDamper(0.4);                                                 // the air vent: 0 chokes the fire, 1 open
brew.SetBath(AlchemyBath.Water);                                     // None, Water, Ice

brew.Stir();                                                         // one stir
brew.SetStirring(0.5);                                               // continuous stirring speed (0–1); changes make turbulence
brew.SetVariable("night", 1);                                        // surroundings (recorded when they change)
brew.Advance(Time.deltaTime);                                        // whole fixed steps; the remainder carries over
brew.RunUntil(90);                                                   // or jump to a preparation time (tools, tests)

AlchemyResult now = brew.Evaluate();                                 // what would be poured: outcome, tier, quality, purity, notes
AlchemyResult taken = brew.Bottle(2);                                // takes 2 bottles of liquid; the rest keeps going
AlchemyHarvest caught = brew.Filter("paper", out why);               // rules.Filters: Items caught, Taken (units by substance)
AlchemyHarvest top = brew.Decant(out why);                           // the top layer: Liquid (bottle it) or Items
AlchemyHarvest skimmed = brew.Skim(out why);                         // what floats
AlchemyHarvest distilled = brew.CollectDistillate();                 // distilling vessels
AlchemyHarvest end = brew.Empty();                                   // solids harvested, liquid evaluated, vessel cleared;
                                                                     // end.Leftovers: the residue film for the next preparation
```

Queries: `brew.Mixture` (`Amount`, `Concentration`, `Temperature`, `Agitation`, `Pressure`, `LiquidVolume`,
`UndissolvedAmount`, `UndissolvedVolume`, `Capacity(substance)` — what the liquid can hold now, `SolventVolume`, `Pending`
— each with `Form`, `Progress`, `Released` per substance —, `DistillateAmount`, `LiquidColor`, `Variable(name)`;
the fire: `FireManual`, `Fuel`, `FireLevel`, `Air`, `Damper`, `Heat`, `EffectiveHeat`; the stirring: `StirSpeed`,
`Turbulence`, `Aeration`, `Emulsion`; `Bath`, `TemperatureMean`, `Swing`, `CoolingRate`; the liquid: `Ph`, `IsLayered`,
`Layers(out aqueous, out oily)`, `LayerSummary(...)`, `OilShare(substance, ...)`, `LiquidDensity`, `Floats(substance)`,
`SuspendedShare(substance)`, `ContaminantLevel()`), `brew.RateOf(reaction)`, `brew.TotalOf(reaction)`,
`brew.IsReacting(reaction)`, `brew.IsBoiling`, `brew.History`, `brew.RecentEvents`, `brew.IsSuspended`.

**Events**: `brew.EventRaised` (`AlchemyEvent`: kind, time, id, text, value) — ingredient added / dissolved, substance
added, reaction started / stopped (breakdowns and clashes included: `breakdown_hot.<id>`, `breakdown_acid.<id>`,
`clash.<id>.<n>`), substance appeared / depleted, precipitated (came out of solution), crystals forming, boiling started /
stopped, eruption, splashed (slopped over the rim), fire out, vessel ruptured, filtered, decanted, skimmed, suspended,
resumed, bottled, distillate collected, emptied, refused. A station re-raises them as `AlchemyStation.BrewEvent`.

**Saving and replaying**:

```csharp
string json = brew.Capture().ToJson();                               // everything, exact
AlchemyBrew again = AlchemyBrew.Restore(rules, AlchemyBrewState.FromJson(json));
AlchemyBrew replayed = AlchemyBrew.Replay(rules, equipment, 20, seed, brew.History, brew.Mixture.Step);
```

## 2. Stations from code

`AlchemyStation.Request(player, new AlchemyRequest(kind, value, count, item, form, evenness), out reason)` runs an
operation for a player where the rules run — `AddItem`, `AddWater`, `SetHeat`, `Stir`, `Suspend`, `Resume`, `Bottle`,
`CollectDistillate`, `Empty`, `AddFuel`, `Bellows`, `SetDamper`, `SetStirring`, `SetBath`, `Filter`, `Decant`, `Skim`,
`CleanVessel` (`AlchemyRequest.For(kind, id)` for the ones that name a fuel or a filter). `form` grinds an `AddItem`
first (the station needs *Has Mortar*; `station.GrindLevels(item)` lists the levels it can reach, `GroundForm(item)` the
first); fuels and filters use up their items from the player's inventory (or the woodpile, free supplies).
`RequestAndTell` also shows the reason. Direct calls: `AddItem(player, item, count, out reason, form, evenness)`,
`AddWater`, `AddFuel`, `FuelAvailable(player, fuel)`, `AvailableFilters()`, `FilterUses(player, filter)`, `Filter`,
`Decant`, `Skim`, `CleanVessel`, `Bottle`, `CollectDistillate`, `Empty`, `EnsureBrew()`, `ApplyConditions()`,
`SavePreparation()`, `LoadState(state)`; `HasManualFire`. `Changed` fires after each operation; `AlchemyStation.All`
lists the stations.

**The minigames** are presentation over these operations: `AlchemyCirclePad` (`Alchemy/UI/AlchemyMinigames.cs`) is a round
pad that follows the pointer and reports `TurnsPerSecond`, `Turns`, `Rhythm` (0 erratic … 1 steady) and a `Turned` event;
the window turns the mortar's turns into a grind level and evenness (one `AddItem`), and the stir pad's speed into
`SetStirring` a few times a second. `AlchemyTrace` draws the last minute of the temperature. Use them in your own UI, or
drive the operations from anything else (a rhythm game, a VR pestle).

## 3. A new kind of condition

Derive from `AlchemyCondition`, mark it `[Serializable]` (and optionally `[AbilityMenu("Conditions/...")]`): it appears in
every condition dropdown (reaction conditions, conditional rates, outcome requirements).

```csharp
[Serializable, AbilityMenu("Conditions/Moon Phase", "The moon is at least this full.")]
public class MoonPhaseCondition : AlchemyCondition
{
    [Range(0f, 1f)] public float minFullness = 0.9f;
    public override bool IsMet(IAlchemyState state) => state.Variable("moon") >= minFullness;
    public override string Describe(AlchemyRules rules) => minFullness >= 0.9f ? "under a full moon" : $"moon ≥ {minFullness:0%}";
}
```

`IAlchemyState` gives conditions the temperature, agitation, pressure, volumes, time, any variable (`ph`, `fire`, `heat`,
`turbulence`, `aeration`, `emulsion`, `swing`, `cooling`, `bath`, `layered`...), vessel tags,
amounts and concentrations (by selector), property values, role shares and the dominant substance of a role. The
journal shows `Describe` once the reaction is understood.

## 4. A new source of conditions

```csharp
public class MoonClock : MonoBehaviour, IAlchemyConditionSource
{
    public void Collect(AlchemyConditionSample sample) => sample.Set("moon", MyCalendar.MoonFullness);
}
```

Put it on the station (or a child). `sample.Ambient` can override the surroundings' temperature. Values are read every
*Conditions Interval* and recorded in the history when they change, so replays stay exact.

## 5. A new ingredient type

Any `ItemSO` that implements `IAlchemyIngredient` (`AlchemyId`, `AlchemyName`, `AlchemyProfile`) can go into a vessel —
`AlchemyIngredientSO` and `BrewedPotionSO` do. Or list an existing item in the database's *Item Ingredients*.

## 6. New properties, reaction types, equipment

* **Properties** are data: add one and use it in substances, selectors (*anything with X ≥ …*) and conditions.
* **Reaction behaviour** beyond rates, heat and eruptions: combine conditions and modifiers, or listen to
  `BrewEvent` (`ReactionStarted` with its id) to trigger gameplay — the rules stay in data, your effect in a component.
* **Equipment**: tags and variables on an `AlchemyEquipment` asset; reactions and outcomes read them.
* **Rules from code**: `AlchemyRules.Compile(settings, properties, substances, reactions, outcomes, forms, filters, fuels)`
  builds rules without assets (procedural content, tests); `AlchemyDatabase.extensions` merges content packs. Compiled
  rules expose `Forms`, `Form(id)`, `GroundForm(profile)`, `GrindLevels(profile)`, `CoarserForm(id)`, `FinerForm(id)`,
  `Filters`, `Filter(id)`, `Fuels`, `Fuel(id)`, `Solvents`, and each substance's breakdowns and clashes as ordinary
  reactions (`IsGenerated`, `generatedFrom`).
* **Substances in code**: `AlchemySubstance` has fluent helpers — `Dissolves(solvent, units)`, `StableUpTo(max, rate,
  observation, (product, ratio)...)`, `StableDownTo(...)`, `StableBetweenPh(min, max, rate, observation)`,
  `ExtractsBestAtPh(ph, sensitivity)`, `WithDensity(relative)`, `InLayer(layer)`, `CrystallisesAs(crystal, startsAt,
  growth, coolingAtMost)`, `Interacts(kind, with, strength, observation, (product, ratio)...)`, `WithProperty(id, value)`
  — and `SolubilityIn(solvent)`, `Property(id)`.
* **Ingredient forms**: `AlchemyIngredientForm` (`releaseSpeed`, `selectivity`, `groundInto`, `volatileLoss`,
  `RatePerSecond`, `LossFor`) — add one to the database's *Forms*, or pass a list to `Compile`.
* **Filters and fuels**: `AlchemyFilter` (`Adsorbs(selector, share)` to chain) and `AlchemyFuel`, in the database's lists or
  passed to `Compile`.

## 7. Knowledge

`AlchemyKnowledge.For(player)` (added when missing) and `AlchemyKnowledge.Local`: `Data` (`AlchemyJournalData`),
`Observe(brew)`, `RecordIngredient`, `RecordResult(brew, result, bottles)`, `Changed`, `Learnt(message)`, `Export()` /
`Import(json)`, `ResetKnowledge()`. The rules of learning are static in `AlchemyJournal` (`IsDiscovered`, `IsUnderstood`,
`IsIdentified`, `SubstanceLabel`, `DescribeReaction`, `SubstanceBehaviour` — how a substance dissolves, where it
breaks down (heat, acid, alkali), how it crystallises and what it cannot coexist with, as far as the player has seen —,
`Procedure(brew)` — the steps, with grind levels, fire tending and stirring runs).

## 8. Networking

The station runs its rules where `GameNet.NetAuthority.RunsRules` is true (offline, the host, a server) and every
player action goes through `AlchemyStation.Request` as an `AlchemyRequest` (items by Item Database id) — the hook a
network component plugs into by setting `AlchemyStation.Net` (`IAlchemyStationNet`: `RunsStationRules`, `SendRequest`,
`Tell`). No Fish-Net component ships for it yet: in a network session, only the host can brew (clients are told so),
and potions need item assets (*Create Potion Items*) to be carried. A future `NetAlchemyStation` would forward client
requests, run them on the server and send `AlchemyBrewState` snapshots (plain data, already serializable) to clients.
