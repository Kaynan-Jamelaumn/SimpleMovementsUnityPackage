# Alchemy 07 — Editor Tools and Testing

**Scripts:** `CustomEditor/Alchemy/AlchemyEditors.cs`, `AlchemyLabWindow.cs`, `AlchemyTests.cs`.

---

## 1. Menus

| Menu | |
|---|---|
| *Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Alchemy Database (Resources)* | The project's rules, from the built-in alchemy |
| *Tools ▸ SimpleMovements ▸ Alchemy ▸ Create Alchemy Station* | A cauldron on a fire with the station, surroundings and visuals |
| *… ▸ Create Test Bench (free ingredients)* | The same with every built-in ingredient free |
| *… ▸ Create Equipment Presets* | Iron Cauldron, Copper Alembic, Glass Retort, Frost Basin, Fractionating Still, Evaporating Dish (`Resources/SimpleMovements/Alchemy/Equipment`) |
| *… ▸ Create Ingredient Items* | The built-in ingredients — and the supplies Firewood and Filter Paper — with generated icons and models (`Items/Alchemy/Ingredients`); assigns harvest items (by name, or the ingredient a substance mainly comes from: brine → Rock Salt, alum → Alum Powder) |
| *… ▸ Create Potion Items* | A potion item per tier of every outcome (`Items/Alchemy/Potions`), assigned in the database |
| *… ▸ Open Alchemy Lab* | Below |
| *… ▸ Validate Alchemy Database* | Errors and warnings in the Console (also in the **Project Validator**, with a *Create Potion Items* fix) |
| *Assets ▸ Create ▸ SimpleMovements ▸ Alchemy* | Alchemy Database, Ingredient, Equipment, Brewed Potion |

## 2. Inspectors

* **Alchemy Database**: content counts, errors, warnings, *Open Alchemy Lab*, *Create Potion Items*, *Reset To Built-in*.
  Condition lists have a type dropdown.
* **Alchemy Station**: problems; in Play Mode the live preparation (time, temperature, heat, volume, agitation,
  pressure, the fire — flames, fuel, bellows air, vent —, the stirring — speed, turbulence, aeration, emulsion —, pH,
  swing, cooling rate, bath, layers, every substance with what is undissolved and what the liquid holds, the ingredients
  still giving up their substances, the reactions running with their rates, the verdict) with *Stir*, *Pause*, *Copy
  State JSON* and *Open In Lab* (replays its history in the Lab).

## 3. The Alchemy Lab

A preparation without a scene, on the project's rules (or another database), a chosen vessel, ambient temperature,
night and seed.

* **Procedure**: timed steps — *Pour Water*, *Add Ingredient* (presets, or any ingredient asset by name; a grind level —
  partially ground, finely ground, pulverised — and its evenness), *Pour Substance*, *Set Heat* (a burner), *Add Fuel*,
  *Bellows*, *Air Vent* (a manual fire), *Stir*, *Stirring* (a continuous speed), *Bath*, *Filter*, *Decant*, *Skim*,
  *Bottle*, *Set Variable*, *Set Ambient*, *Collect Distillate*. *Open In Lab* fills it from a live preparation's history,
  the minigames' operations included.
* **Timeline**: every non-solvent substance's amount over time (coloured as in the game), the temperature (dotted), the
  quality (green), and bands of what the liquid is at each moment — how a preparation moves from one result to the next.
  The list below gives bottlings, refusals and the final verdict.
* **Outcome map**: the same procedure with every *Set Heat* replaced by 0 … 100 % (rows) against time (columns), coloured
  by result and shaded by quality — where each result exists, how wide its window is, and whether a change to the
  content broke something.

## 4. Edit Mode tests

*Window ▸ General ▸ Test Runner ▸ EditMode ▸ AlchemySystem.Tests* (compiled when the Unity Test Framework is installed).
They run the built-in rules and check:

| Requirement | Tests |
|---|---|
| Valid configuration | `BuiltInRules_CompileAndValidate` |
| Same conditions and steps ⇒ same result | `SameOperations_GiveTheSameMixture`, `Replay_OfTheHistory_ReproducesThePreparation`, `History_CarriesTheSurroundings`, `SavedState_ContinuesExactly`, `RandomVariable_IsReproducibleBySeed`, `NewOperations_SaveAndReplayExactly` (fuel, bellows, vent, stirring, baths, filter, decant, skim, grind levels) |
| Time is a real variable (A → B → degraded) | `Time_MovesThePreparationFromOneResultToTheNext` |
| Ingredients are made of substances that behave differently | `Ingredient_GivesUpEachSubstanceAtItsOwnPace`, `Extract_GivesItsSubstancesAtOnce_AndPurer` |
| **Grinding**: levels and precision | `Grinding_IsFaster_ButBringsOutTheBitterness`, `Grinding_LevelsAndEvenness_ChangeWhatComesOut` |
| **Stirring**: speed, duration, consistency | `Stirring_SpeedsExtraction_TooHardSlopsOverAndBrowns`, `Soap_NeedsStirring_ThenHoldsItselfTogether` |
| **Fire control**: raise, lower, hold; poor control | `ManualFire_BurnsDownUnlessFed_TheBellowsRaiseIt`, `PoorFireControl_ScorchesAnUnstirredBottom_AWaterBathDoesNot`, `Boiling_HoldsTheTemperatureAndConcentrates` |
| **Distillation** by volatility | `Equipment_AlembicCatchesWhatTheCauldronLoses`, `FractionatingStill_CollectsAPurerSpirit`, `Fermentation_TurnsHoneyIntoMead_TooHotKillsTheYeast` |
| **Filtration**, decanting | `Filtration_RemovesParticles_CharcoalAdsorbsToxins`, `Decant_PoursOffTheOilLayerAndWhatItHolds` |
| **Evaporation and concentration** | `Evaporation_BelowBoiling_ConcentratesFasterInAWideDish`, `BoilingSeawaterDry_LeavesAllItsSalt`, `Solubility_BoilingDownOrAnotherSolventPrecipitates` |
| **Crystallisation and cooling** | `Crystals_GrowStillAndSlow_StirredOrCrashCooledItIsAPowder`, `Supersaturation_WaitsForASeedCrystal`, `Surroundings_NightAndStillnessGrowCrystals` |
| **Density and layers** | `Layers_KeepReactantsApart_StirringOrAnEmulsifierBringsThemTogether`, `Decant_PoursOffTheOilLayerAndWhatItHolds` |
| **pH** | `Ph_DecidesWhatComesOut_WhatReacts_AndWhatSurvives` |
| **Compatibility** | `Compatibility_ClashesBindsAndCurdles` |
| **Contamination** | `Contamination_SlowsReactions_AndADirtyVesselCarriesItOver`, `Filtration_RemovesParticles_CharcoalAdsorbsToxins` |
| **Saturation and solubility**: the solvent and its amount matter | `Solubility_TheWrongSolventLeavesItUndissolved`, `Solubility_TooLittleWaterThenMoreWater`, `Solubility_BoilingDownOrAnotherSolventPrecipitates`, `Solubility_TooMuchWaterPrecipitatesAVenomDrawnInSpirit` |
| **Thermal stability**: hotter is not always better | `ThermalStability_AShortBoilDestroysTheToxin_ThenTheBrewCools`, `ThermalStability_HotterIsNotAlwaysBetter` |
| **Volatility** | `Evaporation_BelowBoiling_ConcentratesFasterInAWideDish`, `FractionatingStill_CollectsAPurerSpirit`, `Grinding_LevelsAndEvenness_ChangeWhatComesOut` (pulverising loses volatile oils) |
| Conditions change the result | `Temperature_ChangesWhatTheSameIngredientsBecome`, `Quantity_DilutionLowersTheQuality`, `Surroundings_NightAndStillnessGrowCrystals`, `SealedVessel_BurstsWhenOverheated` |
| Adding during the process depends on the state | `SameIngredient_RedirectsByTheMixturesState`, `VoidSalt_GivesTheSameResultWhateverTheMixtureWasBecoming`, `Bitterroot_TaintsAHealingPreparation`, `Order_OfAdditionMatters` |
| Modify, accelerate, slow, stabilise, interrupt | `Catalysts_Inhibitors_AndStabilisers`, `Order_OfAdditionMatters`, `ViolentReaction_Erupts`, `BlastingPowder_MixedDry_GoesOffWhenHot` |
| Interrupt, resume, multiple products | `Suspend_StopsTimeAndKeepsTheState`, `PartialBottling_LeavesTheRestToTransform`, `Surroundings_NightAndStillnessGrowCrystals` (harvest) |
| Failed experiments are results | `FailedExperiments_AreResultsToo`, `Capacity_IsRespected` |
| Players learn from experiments | `Journal_LearnsFromRepeatedExperiments`, `Procedure_ReadsLikeTheStepsTaken` |
| Extensible without rewriting the core | `Extensions_AddAndReplaceRules`, `CustomConditions_PlugIntoReactions`, `BrewedPotion_PouredBack_ReleasesWhatItIs` |

56 tests. They need no scene and no assets; each runs a few simulated minutes in milliseconds.
