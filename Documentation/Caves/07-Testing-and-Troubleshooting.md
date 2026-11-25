# Caves 07 — Testing and Troubleshooting

## 1. Edit Mode tests

*Window ▸ General ▸ Test Runner ▸ EditMode* (`CustomEditor/Caves/Tests/CaveGenerationTests.cs`, compiled when the Unity
Test Framework is present, like the dungeon tests):

| Test | Checks |
|---|---|
| `CaveStageRunsBetweenAnalysisAndPopulation` | the stage's slot, and that compiling the profile picks it up |
| `CavesHaveDepositsInValidPlaces` | deposits on open ground, flagged Occupied, never on landings or locks; wall deposits back onto rock between floor and ceiling; floor deposits have open ground all round |
| `BlockersNeverCloseAWay` | every cell reachable without the formations and floor deposits is still reachable with them |
| `SameSeedSameCave` | the same request gives the same deposits and formations |
| `DeeperIsRicher` | deposits a level down are rarer on average, and more of them are rare |
| `HarderCavesAreDeeper` | difficulty and chains raise the depth score and the band |
| `EveryBuiltInResourceExistsInTheCraftingDatabase` | every built-in id (and gem by-product) gives a Crafting Database item |
| `EveryDepthBandHasResources` | no band without resources |
| `FormationsMeshTheSameEverywhere` | formation mesh data is identical on every build (server and clients) |
| `CaveMemorySurvivesASave` | a cave's record survives the world save; a record for another layout is not applied |
| `ACaveBeingVisitedIsNeverForgotten` | beyond `MaxCaves` the caves left longest ago go, never one a visit is going on in |
| `ARestoredSeamGoesOnGrowingBack` | a seam worked out in an earlier session goes on growing back where it was, and keeps counting when kept again |
| `PlayersStayInAMouthsTriggerAndComeBackOutsideIt` | a passage's trigger reaches from the opening to past the tunnel's end; players (and their party) come back outside it |
| `SitesArePlannedTheSameWay` | cave sites are a pure function of the seed and keep their distance |

## 2. Verification done while building the system

* **Compilation.** The whole project (runtime, editor and tests) compiles against the Unity reference assemblies with no
  new errors.
* **Headless run of the real code.** The dungeon pipeline with the Cave Mining Stage, the formation mesher and the
  mouth builder were run outside Unity (UnityEngine replaced by a math stand-in) over **160 caves / 502 levels**: every
  size class, difficulties 1, 2.5 and 4, and deeper caves of chains up to six caves down. Results:
  * 30,681 deposits (61 per level), all valid (the checks of §1 and more: spacing, clear of the arrival, wall and floor
    deposits only where their resource allows);
  * 28,048 standing formations; **no cell ever cut off** by a formation or a floor deposit;
  * **no** creature, chest, prop, hazard or portal among 131,967 placements on a deposit or formation cell;
  * 386 rich veins (48 guarded), 221 crystal grottos; all 38 resources placed;
  * mean deposit tier by level 1.05 → 1.42 → 1.91 → 2.34 → 2.58 → 3.00 (deeper is richer);
  * the same seed gives the same cave, formations mesh identically twice;
  * 120 cave mouths and passages: no rock covers the way in, and a player-sized capsule fits through every opening;
    a player walking straight in (or off the middle) stops at the rock closing the tunnel 0.5-1 m in front of the
    trigger's back, always still inside it; the place players come back to, and the ring their party stands in round
    it, is clear of the rock and the trigger;
  * generation about 85 ms per cave and formation meshing about 64 ms per cave, both on worker threads.

  The run found and fixed two problems. A floor-only resource (clay, sand) could be put in a wall in a chamber
  without open floor. The way deeper's roof lowered too early for the player's headroom.
* **The network code.** The Fish-Net side (`NetDungeonRun`, `NetInteractor`) is compiled only with Fish-Net
  installed; it was type-checked against a stand-in of the Fish-Net API with method bodies compiled (a planted error was
  caught, and nothing else).
* **The Crafting Database.** Every built-in resource id (38) and gem by-product (7) was looked up in the real crafting
  rules: all 45 exist.
* **The cave memory.** `CaveStore` was run headless with a real JSON serializer:
  * two copies of one seam (two visits at once) never get the same lump number, and the count survives a save;
  * a seam that grew back keeps its regrowth;
  * the world's part round-trips from its cached per-record JSON;
  * a record made for another layout is not applied;
  * eviction never drops the cave being opened, nor one being visited; once the visits are over they can go;
  * the play clock goes on from the saved one, in game time;
  * the real `OreDeposit`: a seam worked out in an earlier session (restored early in a new one, whose clock starts at 0)
    goes on growing back to the second, its time kept when kept again; one grown back while away is whole, numbered on.
* **Code review.** Two independent reviews of the runtime layer (object lifetimes, build order, networking, persistence,
  gameplay), every finding checked by a second reviewer. The first found nine problems, all fixed:
  * pooled deposits destroyed by a server's teardown;
  * lump numbers shared by two simultaneous visits;
  * client fades lighting up too early or too late;
  * the editor preview leaking pool objects;
  * shared visits not found again after a chunk reload;
  * broken urns bursting on reload;
  * item rewards collectable again behind remembered guards;
  * colliders added after the last floor;
  * kept generations not invalidated by table edits.

  The second found eighteen, all fixed:
  * a world mouth's trigger covered only the front of its tunnel (walking in to the end cancelled the way in);
  * players came back where they had stood, inside the trigger or the rock of the mouth;
  * a mouth's spot could depend on trees cut later (server and clients could place it differently);
  * a seam restored early in a session never grew back during the visit;
  * the passage rock's mesh changed while its collider was baking;
  * a client's fade could light up while the server was still making the cave;
  * players without a status controller (tag only) could not take a way out inside a cave;
  * deep creatures healed to full when their level was streamed back in;
  * a party member taken in first made the host's screen light up early;
  * a classic-mode world pause could light the screen while the cave was being made;
  * pickups and breakables taken just before the visit ended were not remembered;
  * a busy server could forget the record of a cave being visited;
  * the layout fingerprint missed edits that kept the counts;
  * pooled deposits lost to a teardown inside a physics callback;
  * a second Cave Mining Stage resolved deposits against the wrong table;
  * an editor preview saved with its scene lost its deposits' meshes;
  * clients' deposit ids, depths and names (deeper caves) differed from the server's;
  * gathering by hand through the dungeon relay skipped the server's hold check.

## 3. Symptoms and fixes

| Symptom | Likely cause | Fix |
|---|---|---|
| No cave entrances in the world | EndlessTerrain ▸ Cave Entrances has no profile, or *Enabled* is off | *Create Cave Setup* and answer Yes, or add a profile; *Validate Cave Setup* |
| | the chunk isn't ready yet: spawners start once a chunk's objects (and, with Bake NavMesh, its NavMesh within NavMesh Distance) exist | walk closer |
| | no ground fits (steep, wet, crowded, a forbidden biome or height, a town's area) | raise *Max Slope*, *Max Unevenness* or *Search Radius*; *Enable Detailed Logging* lists every rejection; *Enable Visual Debug* draws the sites |
| | too few slots | raise *Spawn Chance* (1 = every region) or *Max Per Region*, lower *Region Size* |
| Walking into a mouth does nothing | the entrance has no profile (its inspector says so) | set *Profile* |
| | just came out: the re-entry cooldown | step out and in again a moment later |
| | the player isn't recognised | players need a Combat Entity (or the Player tag as a fallback) |
| The screen went dark and came back, nothing else | entry refused (full, cooldown) or generation failed | the Console says why (`[Dungeon]`, `Portal ...: cannot enter`) |
| A cave has no deposits | the profile has no Cave Mining Stage in Custom Stages | add it (*Validate* reports it) |
| | *Deposits ▸ Caves Only* with a profile that has no cavern levels | use the cave profile (`CaveDefaults.ApplyProfile`) or turn *Caves Only* off |
| | every entry disabled, weight 0, or no entry for that depth | the Resource Table inspector shows *what each depth holds* |
| The pickaxe does nothing | the weapon's Tool Type isn't *Pick Axe*, or its Tool Damage is 0 | fix the pickaxe item |
| "your pick only scratches it" | the deposit's *Min Tool Damage* is above the pick's Tool Damage | a better pick, or lower *Min Tool Damage* |
| Lumps never reach the inventory | the entry's id is unknown to the Crafting Database ("gives nothing") | *Validate Cave Setup* lists such entries |
| Grey stand-in creatures | the profile's Encounter Table is empty | its inspector: *Import mobs from the world*, or add mob prefabs |
| Creatures walk through formations | *Formations ▸ Colliders* off, or the profile's *Bake NavMesh* off | turn them on |
| A mined cave is full again next visit | *Between Visits ▸ Keep State* off, `CaveStore.Enabled` false, or the deposit grew back (*Regrow Minutes*) | check the settings; the Cave Instance inspector shows the record |
| | the profile was edited, so the cave is a different cave now and its record was dropped (on purpose) | — |
| | a network server restarted (new secret seeds = new caves) | expected in multiplayer |
| Entering again takes as long as the first time | *Build ▸ Keep Generated* is 0 | set it (caves: 4) |
| A mouth floats or sinks | the ground wasn't loaded when it was built (hand-placed far from the player) | it fits the terrain under it at Start; in Play mode *Rebuild Mouth*; hand-placed mouths on non-terrain ground: *Build Mouth* off and model it |
| The deepest caves hold only a few kinds of deposit | *Depth ▸ Max Depth* beyond the deepest resources' ranges | lower *Max Depth* or widen the ranges (*Validate* warns) |
| Some deposits never appear (or a warning about Cave Mining Stages) | the profile lists more than one Cave Mining Stage: only the first is used | one stage per profile, every resource in its Resource Table |
| Gathering by hand does nothing on a network client | the client never held Interact long enough on the seam (the server checks the hold) | hold Interact for the seam's gather time |
