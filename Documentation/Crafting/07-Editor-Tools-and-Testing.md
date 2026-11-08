# 07 — Editor Tools and Testing

## Menus

| Menu | What it does |
|---|---|
| *Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Crafting Database (Resources)* | the project's database (`Settings/CraftingDatabase`), filled with the built-in content |
| *Tools ▸ SimpleMovements ▸ Crafting ▸ Create Smithy / Smeltery / Tannery / Leatherworks / Woodshop / Tailor's / Armourer / Spinnery / Lapidary / Jeweller's / Complete Workshop* | a station with those tools in the open scene: primitive work places in a row, a collider, the station, its visuals, its surroundings (and, with Fish-Net, a NetworkObject and a `NetCraftStation`); with a quench tank it keeps water, brine and quenching oil (`CraftThermal.PresetQuenchants`: oil hardens a blade of high-carbon steel that water would crack; a test station every quenchant) |
| *… ▸ Create Test Station* | a station with every tool and instrument, free materials and the numbers shown |
| *… ▸ Create Ore Deposit* | a seam of hematite (change its ore in the inspector) |
| *… ▸ Create Stock Items* | item assets (`Items/Crafting/Stock`, icons in `Items/Crafting/Icons`) for the stock metals (and the jeweller's wire and bezel strip), every ore, hide, wood (the species' blank, and a green log as long), rough gem, fuel, supply, agent and quenchant (rock salt, quenching oil...), and fasteners (ten rivets, ten studs, two buckles). A wooden blank still at the old bar's 200 × 30 × 6 mm (or none) and a log still at 90 × 24 cm take the species' size, and that size's price and weight (a 2.2 m log sold at a 90 cm log's price would be worth thirty times it on the bench); sizes an author chose stay. A supply item still at the 0.3 kg every supply once weighed takes what one item of it weighs (`ItemGrams`: a skein of thread a few grams, a batting 300 g) - what it weighs in the item it goes into; a weight an author chose stays. The other existing items keep their price, but those priced otherwise than the rules make them worth on the bench (an older built-in price, or one you set) are listed in a dialog that offers to bring them to the rules' prices (*Update prices* / *Keep their prices*; in batch mode they are only logged): an item sold under its worth is bought, put down, taken back and sold at a profit |
| *… ▸ Create Design Templates* | a weapon (built-in moveset of its kind) or armour asset per design in `Items/Crafting/Templates`, assigned in the database |
| *… ▸ Validate Crafting Database* | errors and warnings in the Console: besides what does not compile (an unknown element, flux or pattern, a design without parts, a joint to a part it lacks), where the content contradicts itself - a part's own profile outside its mass range in steel or in bronze, an eye leaving less than 1 mm of metal each side, a tang too short for its hilt, a wooden part no blank or log of a tough-enough wood can give, a weapon's reference mass outside its main part's range, a leather seam no thread is strong enough for, an edge angle over 70° or one its section is too narrow for along most of the edge, a stretch of edge too thick to hone (its bevels meeting steeper than 35° a side), an ore dearer than its poorest lump's metal or worth under a fifth of a usual lump's, classes of one base and one priority that overlap, a fibre spinning into what is not a thread or weaving into what is not a textile, a skein of thread priced over four times the fibre in it (spinning prints money) or a bolt worth less than the yarn woven into it (weaving throws value away), an ore's flux that is not a flux, a wood with no blank size, ranges that run backwards. The built-in content has none |
| *… ▸ Help* | this guide |
| *Assets ▸ Create ▸ SimpleMovements ▸ Crafting* | Crafting Database, Stock Material |

## Inspectors

* **Crafting Database** — counts, errors and warnings, buttons to create the templates and stock items, **Add Missing
  Built-in** (adds the built-in metals, ores, hides, woods, supplies, patterns and designs the database lacks, by id,
  and the alloy effects it lacks, by base, element and property: an older database catches up with new built-in content
  without losing your changes; then run Create Design Templates and Create Stock Items for the new ones) or **Reset To
  Built-in** (the content only - elements, classes, ores, hides, woods, supplies, designs: your simulation settings
  stay, and the rules are compiled again at once). A project's database keeps its own copy of the content: what it already has keeps its numbers, so to
  take the built-in content's newer numbers (prices, masses, sizes) fill it again or edit it - *Validate* flags the
  older contradictions in it.
* **Craft Station** — configuration problems (a quench tank that can only be filled with harsh quenchants - water, brine - is warned of: a blade of hardening steel cracks in them; add `oil` to its Quenchants), *Make Networked* (Fish-Net); in play mode the live workshop: time, the fires, the furnace, the
  crucible, every piece (class, temperature, hardness, mass; tannage and moisture; fragments), Pause/Resume, Copy State
  JSON (bug reports, tests), Clear.

## Edit Mode tests

`CustomEditor/Crafting/CraftingTests.cs` (Window ▸ General ▸ Test Runner ▸ Edit Mode), on the built-in rules:

| Area | Tests |
|---|---|
| Configuration | the built-in rules compile; content extends without code (a new element and class name a new alloy) |
| Reproducibility and saving | the same operations give the same workshop; replay from the history; capture and restore continue exactly; the state survives JSON; deposits are repeatable and vary |
| Conservation | forging conserves metal; splitting conserves mass; crushing conserves mass and washing cannot remove bound impurities; the crucible conserves metal and alloys by proportion; cutting moves cells out of the hide |
| Boundaries | refused operations change nothing; cold forging hardens bronze and cracks hardened steel; overheating burns steel; descaling does not mend cracks |
| Heat treatment | water hardens high-carbon steel, not mild steel; no hardening from below the critical heat; tempering trades hardness for toughness; annealing softens more than air cooling; quenchants differ in severity; a fantastic quenchant lends its element; an uneven quench warps |
| Smelting | cleaned ore yields more iron |
| Surface | grinding makes an edge and sharpening keens it; shaky hands give a worse edge; heavy grinding draws the temper |
| Leather | tanning reaches the core with time; scraping too hard scores; staking keeps drying leather supple; a hot vat shrinks tanned leather |
| Assembly | parts and fit are checked; an unfinished assembly moves between stations with its parts; hot parts are refused near wood; an overdriven wedge splits the haft; a belt from tanned leather and a buckle |
| Evaluation | better work makes a better item; incomplete items rate low |
| Observation and knowledge | no numbers without instruments; the journal learns metals by working and assay, and from what happens |
| Hand-skill models | the hammer is repeatable and rewards a full swing; the stroke measures what the hands did; the pour counts interruptions; the rhythm rewards timing |

`CustomEditor/Crafting/CraftProcessChainTests.cs` covers the production chains:

| Area | Tests |
|---|---|
| Woodworking | a careful debark keeps the wood, a hasty one gouges it; a split through the heart gives even halves; seasoning follows the air's humidity; sanding through the grits and a resin seal last longest |
| Fibre | hot, churned wool felts; prepared fibre spins a better yarn and plying strengthens it; a skein reels only its counted length; short staple is carded, not combed |
| Tailoring | layout refuses overlaps and a cut on the chalk is cleaner; measured garments fit; thread must suit the material |
| Lapidary | the tests read the stone's properties and a name sticks (a second is refused); the loupe finds what it sweeps over; facets at the right angles give a brilliant stone; a compound must be as hard as the stone, a table must not lie on the cleavage; cleaving along the plane parts cleanly |
| Jewellery | a ring from rough to finished carries its stone's element; a wide seat leaves the stone loose; jewellery is made only at a jeweller's bench |
| Bounds and knowledge | the guard holds faceting and carding to real hands; the journal learns gems and the new insights; the whole chain replays exactly |

`CustomEditor/Crafting/CraftMinigameTests.cs` covers the shared timing bar and the lapidary's and the weaver's steps:

| Area | Tests |
|---|---|
| The timing bar | a hit scores by its distance from the target through the zones' edge scores (all settings); the indicator turns back at the ends, and the same hits score the same |
| The lapidary's steps | cleaning takes the crust off and the pick marks bare stone; crust hides inclusions from the loupe; splitting depends on the stone and the blow; a missed saw stroke costs weight and chips the face; a preform ground to its outline is ready to facet; uneven or botched facets cut a worse stone; polishing brightens every region but never mends the cut |
| Weaving | yarn becomes cloth as the rows were beaten |
| The rest | the guard holds the new work to real hands; the whole chain replays exactly; accessories are made for every slot a gem suits |

`CustomEditor/Crafting/CraftMasteryTests.cs` covers practice:

| Area | Tests |
|---|---|
| Earning it | mastery grows with experience, each step harder won; small work comes only so fast, a finished item always counts; a new rank is announced with what it allows; an older journal turns its work into practice once, and it is saved |
| What it gives | more stitches, rows and fewer facets by hand with each rank; routines at their rank and never a master's best; a master's arm tires less and is steadier; the station multiplies a needle's or an awl's run and a reed's rows itself; a master's finishing touches raise what an item lacks, and replay |
| Which craft | each design and each request belongs to its craft (cloth is tailoring, leather leatherwork, the sizer jewellery; the bellows nobody's) |
| Routines | done as well as practice does them: fibre picked and teased, a skein counted closer by a master and never long, the wearer and the finger measured, a stone brushed clean without a mark and searched to the last inclusion; the guard lets them through one at a time and only known ones; they replay |
| Fibre | raw fibre spun from the bag never spins fine |

`CustomEditor/Crafting/CraftFoundationTests.cs` covers the station window's foundation that runs without Unity, and the
shared models the work areas rely on:

| Area | Tests |
|---|---|
| The pointer | the left button owns a gesture, the right button is only Cancel, the middle nothing, another finger neither begins nor ends it; drags and moves are sampled by distance (2-6 px apart at any frame rate, a long jump filled in with its own times, a still hand sends nothing); a release samples the last of the way; the wheel comes in whole notches, one a frame, and a fast spin does not run on |
| Timing | a message stays long enough to read; what cannot be undone is asked twice; a setting is sent at once, then five times a second, the last value when let go, and the workshop's value is not shown under the hand; a vat's fire turned by a hair records nothing |
| The window's rules | Take refuses a piece inside a working place until it is taken out, and hot metal; fitted parts are listed under their assembly |
| The stroke | it tires the arm by the time it works, the same at 30, 60 or 144 samples a second and in short batches; a tiny fast wiggle is no pass, a real stroke is, whatever the frame rate; no more than three passes a second; each pass keeps its direction and hold; a stumble mid-stroke does not split it |
| Practice | sawing, coating and brushing wood is woodwork (a wooden piece cut is *Sawn to length*); the furnace's pour is smithing and teaches as a pour; the rank-up line reads well |
| Shared drawing rules | a pattern's cutting line runs along its cells' edges (a strip's middle is half a cell off it; top row first; a corner touch and a hole make loops of their own); the glow shows exactly where the words say it does, earlier in the dark |
| Shared data | each flux says where it works, and one from an older database works everywhere; an optional part left off holds up neither the finish nor the rating |
| Requests and answers | an answer waits until the copy shows its request's operations, in the order the answers came, with only the events its own operations raised; a request is answered once, or given up on after 10 s or when the window closes, and one never sent is never answered; settings are told apart |
| The guard | a weave or a tier is all or nothing and a refusal uses up nothing (30 rows a weave, a round brilliant tier after tier); a tier is charged the hits of the player's rank by the rules' settings; the needle's pierce and pull are half a stitch each and never free; joint counts are capped and a hammer's joint work is a blow; sanding counts in the window's strokes; brisk ore scrubbing counts whole; each kind checks its own list (an odd clay coat is fine, an odd stroke is not, whole looks, no weave without beats, a long stroke keeps its first part); every refusal of every kind says why; too many strokes too fast says so; the guard only clamps and trembles only a client that strips its tremble; a bobbin's grams and a hand's pressure are bounded (in the workshop too); the clock and the scrap heap answer only a test station; practice past the minute's allowance changes nothing to send; a weave needs its beats; work the station refused is given back (a weave on raw fibre, a tier, a blow's rise) so the retry is not held back, but never the pace of requests, and a refund after the guard's own refusal gives nothing; at a test station only the pace of requests is kept, and the shape is checked everywhere |
| Settings | a setting dragged every frame goes ten times a second, never meets the guard and never holds the bellows back, and the last value turned stays; each setting (the forge's vent, the furnace's, each vat's fire, the oven; the drying stack's cover is one too, `CraftRequest.IsSetting`) keeps its own latest value, in the order first turned, and all go at once when the hands leave |

`CustomEditor/Crafting/CraftSmithAreaTests.cs` covers the forge, the anvil and the quench:

| Area | Tests |
|---|---|
| The forge weld | folded by hand its seam is left open and set blow by blow at welding heat: spring steel, mild steel and wrought iron are set within a few heats, fluxed again each heat, and hold; a seam left to cool open delaminates and is told; only the flat (not the edge) sets it, and not below welding heat; the gauges are what it multiplies; a cold bar (by its mean heat), an open seam or unequal bars are refused; a quick fold still welds at once |
| Flux | it burns off at the same rate however finely the bar is cut; it lands only where it is sprinkled, takes the scale up there, and only a welding flux welds; a bar reworked keeps its seam, its flux and a half-made cut |
| The fire | the heart is hottest, and blown it reaches every stock metal's welding heat in charcoal; one face runs ahead only while the bar lags, evens out, stays closer turned, and turning does not swap it |
| The anvil | the hands' tremble strikes flat, a tilt on purpose bevels; the horn bends the same however finely the bar is cut, the way chosen; the hardy and the hot chisel cut alike, the last stretch too, losing nothing; a tap of the drift only marks; fasteners come from a rod, never a blade, a flat bar or too little metal |
| The quench | clay is the spine's coat (wiped bare, none is left; only on the bench) and leaves the edge hard, in oil too; side to side or flat warps it; the ashes are warmed only by hot metal; the tank is filled again with another quenchant (not with a piece in it) and a replay or a save keeps it |
| The rest | wood chars at the same rate however finely it is cut and steam does not dry it; the forging window ends short of the burn; a double-edged blade ground on gets narrower, never weightless; a weld by hand is one at a time, the hammer at its window's pace never meets the guard, a flux coat is bounded |

`CustomEditor/Crafting/CraftTextileAreaTests.cs` covers the textile area's rules (the fibre bench, the wheel, the loom,
the tailor's table):

| Area | Tests |
|---|---|
| Bobbins and the loom | bobbins wound together add their lengths and grams and keep their stretches' evenness by length; a joined bobbin weaves every row its yarn allows, more than one bobbin could (30 at most); joining never puts a twist right and forgets the count; only the same fibre and plies, at a loom or a wheel, one at a time, teaching nothing; too little yarn says how much a row takes and what the bobbin has |
| Plying | the ply goes on the shorter bobbin, the longer keeps its rest (a tie: the first), a spent strand is gone, only the same fibre; it hangs straight exactly inside the band the wheel shows |
| Preparing and spinning | a card that never touches the fibre aligns nothing, pressing harder aligns no better and knots neps; fine tweezers never take dirt below nothing; preparation is counted once (a raw batch spins like raw fibre from the bag, one set of thread tiers); a fine draft of raw fleece is lumpy but draws much further, by the window's rule; two rinses clear a scour's soap, the bath's heat is the fibre's, a rinse near the bath's heat does not felt, past the shock it does |
| Sewing | quilting goes through its batting only once it is laid in; a finished item takes no more stitches; a stitch pulled past the cut damages the seam |
| The tailor's table | the chalk a cut goes through is told, but not its own; the patterns wanted are the cloth parts the items being made still lack; the fit of a measure is the evaluator's (worse than none below about 42%) |

`CustomEditor/Crafting/CraftSurfaceAreaTests.cs` covers grinding, polishing and sharpening as the Grind and Sharpen areas
show them:

| Area | Tests |
|---|---|
| The bevels | planes: the section loses exactly the area ground before and after the bevels meet, after a chisel grind and through the other bevel; ground on, a bevel keeps the angle held, the apex moves toward the face ground and the edge goes back; ground in turn the apex stays in the middle; a double edge goes back on both edges and a bevel reaches the middle at most; nothing is ground steeper than the stone is held |
| The faces and the edge line | thinning the faces keeps both bevels' angles, the apex in the middle and the edge's flat; the edge line takes the honing, the micro-bevel and the burr back with the edge |
| Heat | the wheel heats where it works (the bevels the edge, the spine the body); pressed hard on a hardened edge it draws the temper and says so (*grind_heat*), the ground steel shows the colours, an unhardened steel has no temper to lose |
| The surface | the wheel greys a mirror and the first emery pass makes it no duller; the fine wheel leaves it brighter; a skipped grit is noted (*grits*); a coat goes with the surface it was on and burns off hot metal; wood is neither coated nor polished here |
| Work that does nothing | a file on hardened steel is refused, records nothing and still teaches (*file_skate*); a file on an eye that is not there is refused |
| The stone | held far below the bevel it hones nothing and says so (*stone_angle*), at the bevel's angle it hones, never finer than the bevel; light trailing strokes at the slider's 0.45 take the burr off, heavy ones neither raise nor take it, leading ones raise it; a square bar is refused and a flat is worn away only slowly, with no keen edge or micro-bevel meanwhile; honed long enough the apex comes to the floor the window computes; the next step follows the model's thresholds; strokes on one stone are one line in the record |
| The window's readings | the stroke's segments are the real ones (uneven segments); the strip judges as the evaluator does (a chisel grind not done, met at another angle off, 13 % narrower too far) |
| Dips | a cold, hardened blade dipped in the tank is dipped, not quenched; dragon's blood lends its fire only to what hardens in it |

`CustomEditor/Crafting/CraftGemsAreaTests.cs` covers the lapidary's and the jeweller's rules:

| Area | Tests |
|---|---|
| Lapidary | the hardness points bracket every stone's own hardness; naming raises no words that tell whether it was right; the loupe finds exactly what it shows sharp; only a cleavage worth following is cleaved, and the refusal names no stone; a ground preform keeps its outline when sawn (a line through rough the wheel took is refused), and its plan; the planned stone weighs the same ground or freehand, as the plan said; grinding heat comes touch by touch however the touches are sent; a table on the cleavage flakes pass by pass and a light lap polishes it safely; a cut stone carries no crust; `CanFacet` says what `FacetGem` would refuse and changes nothing |
| Jewellery | forming uses only the length the part needs and puts the rest back; the seat is worded by its fit; a setting must suit its stone (collar and claws); the jeweller's hands count in the item; detaching unsolders and unsets; the solder's flow is the window's; the guard holds buffing to the time on the wheel; only a ring is sized to a finger; measurements wait for the item that takes them |

`CustomEditor/Crafting/CraftLeatherAreaTests.cs` covers the tannery and the cutting table:

| Area | Tests |
|---|---|
| The frame | a laced hide starts slack and the frame holds one; each side tears past what its weakest edge cell can take, and a side sent again unchanged never tears again; taut and even it dries flat, slack or on the rack it puckers, pulled unevenly it dries out of square and overdrawn its edges thin |
| The beam | a steep fleshing knife bites sooner, a flat one is gentler and takes less, and it scores a clean cell only past what it can bear; slicking burnishes a part's edges only along them; skiving holes only the cells it goes through; the knife's scores and the tears stay on their cells and go with the part cut there |
| Oil, wax and dye | used by what the leather takes in, and paid for from the pot opened |
| The vats and drying | resoaked leather is no longer dried and dries again; drying reads the air as wood and fibre do; hot lime loosens no faster past 30 °C and cooks a raw hide; any liquor past the leather's shrink temperature shrinks it; an agent poured into plain water makes its liquor there; stirring is as good as the paddle was steady and settles in vat time; the liquors are their own colours |
| The hide and the part | a part already cut is not cut again; a hide keeps the scars it was born with, apart from the knife's work; an item says when a leather part is too thin for its place; the calipers read a hide's thickness and a part shows its cut and its edges |
| The guard | a cut is no cleaner than the hands, the paddle within its range |

`CustomEditor/Crafting/CraftSmeltAreaTests.cs` covers the ore bench, the furnace and the crucible:

| Area | Tests |
|---|---|
| Casting | a casting never holds more metal than the pour took, and a small melt casts a short piece; a fastener mould casts only what the pour brought; a pour loses what missed the mould by the amount measured; a frozen melt is knocked out as a cake with all its metal; the pour is checked before it starts (`CanPour`); gas leaves a melt that froze |
| The pour control | an upright crucible never pours; a prompt tip back is clean, a late one runs over; the stream stops when the metal runs out, without a cold shut |
| The crucible | it takes only ores it can melt; the skimmer takes the ore's rock, not the gold; brass keeps its zinc under a cover while pure zinc boils away |
| The furnace | calamine's zinc goes up the shaft as fumes; a matte and lead carrying silver go back into the furnace; a bead too small to pour is lifted out; the pool is not taken frozen, and a broken stream leaves a cold shut; furnace fluxes are not metal and go only where they work; tapping the slag leaves the matte |
| The ore bench | charged ore is not worked at the bench; throwing out the last stone empties the lump, and a stale aim is refused; a gentle flow keeps more ore for the same washing; hot ore cools on the bench and is not carried hot; the guard holds the swirl and the sieve to the hands |
| Words | they follow the heat and the air |

`CustomEditor/Crafting/CraftHandsOnRulesTests.cs` covers the rules the 3D work places share with the window:

| Area | Tests |
|---|---|
| The hand's path | the quench's motion (up and down, side to side, round, held still, stopped) is the same at 30, 60 and 144 frames a second, and a slow real movement is not read as still; the hand's speed is the hand's, not the screen's |
| Brushing ore | a stone gone over in twenty strokes ends as one sweep over it leaves it (each brush); a brush takes only its share however long it scrubs and is then refused, saying which brush takes more; the wire brush knocks the rind off once, not the rock under it; a break starts unbrushed; the 3D stone counts what a brush went over as the workshop takes it (scrubbing only the dirty patches cleans the dirt, and the station agrees), also on a stone already half brushed |
| The pour | it ends only when tipped back, or full and upright, or empty; tipped back late it runs over (flash), promptly the cup takes it; its request carries the share, the metal lost, the rate and the furnace's breaks; nothing is sent for a pour that never ran; a big mould fills slower |
| The whetstone | passes are taken one by one, the same at every frame rate; a wiggle is no stroke |
| The bore and the answers | the bore goes in from the nearer end a stretch deeper each turn, three quarters at most; the window and the hands number their requests apart |

`CustomEditor/Crafting/CraftHandsOnShowTests.cs` covers the rules behind what the 3D work places show:

| Area | Tests |
|---|---|
| Which way it bends | tilted blows forging a bevel curve a single-edged blade toward its spine (a positive bend, whichever face is up), as the quench curves a single edge: the sign every drawing, icon and model reads |

`CustomEditor/Crafting/CraftWoodAreaTests.cs` covers the woodshop and the carving bench:

| Area | Tests |
|---|---|
| Logs | the wedges split the larger dimension and end in square stock; the froe refuses a round log too big and a rived log is a log no more; set off the middle it parts unevenly and runs out; elm will not split true; a round of pulls peels green bark, tight bark takes two; the knife takes the bark off where it cuts |
| The saw | it cuts exactly at the mark (a stretch is split there, keeping what is along it); a stroke counts by its travel and the line is judged; a rip tapers a kite board from the long linden plank |
| Steam and bend | a bend strains by the form's radius, not the angle; steamed wood stays soft only a while out of the box; a snath can be steamed into its sweep; the steam box wets green wood but never dries it |
| Seasoning and finishing | heat dries the same whatever the stretches, and the air gives it back; the stack's cover is the stack's, for every board in it; a skipped grit is judged by the surface every stroke; each finish follows its own rule |
| Slots and the guard | a slot's and an eye's size come from the design or the piece it goes on; the guard bounds the wood's requests |

`CustomEditor/Crafting/CraftAssemblyAreaTests.cs` covers the assembly bench and the finished item:

| Area | Tests |
|---|---|
| Hilts | the shortest blade takes the longest hilt and the eyes fit the tang; a hilt goes together, its slot closes only so far, the pommel is peened and it finishes once; the tang is measured from the blade (no tang takes no guard, a short one says by how much); the rules check warns of a tang too short for its hilt |
| Parts and fasteners | the item takes no more of a supply than it needs, and what is added stays in; fastener sets add up, but studs are not rivets; a buckle with no thread is riveted on and its rivets are counted; threads: the strongest suits the thickest sole |
| Joints | a rivet is set tight by a blow in its band (too hard crushes the leather, too light leaves it loose); stitches never pass their holes, and a joint taken apart starts over; a wedge driven past home splits the haft and is counted once; the rhythm takes one answer a beat, and half a beat off is a miss |
| The item | names say the leather once and the cloth it is sewn of; the item is named as the player knows it and trying it tells every note; an overweight head swings slower and hits no harder; a wooden weapon is rated by its wood, not an edge or a temper; the guard tries items from the bag one at a time |

`CustomEditor/Crafting/CraftFollowUpTests.cs` covers the rules several areas share:

| Area | Tests |
|---|---|
| An empty item | it is empty until a part or a supply goes in; it is put aside at any station and its measurement waits again; the guard lets an empty item be given up anywhere but throws nothing else away |
| A cut in one go | the guard takes it one cut at a time and keeps its mark; a split of wood cuts at the mark itself |
| Pots and the rasp | one open pot of each supply serves every bench; the rasp takes the bark off where it cuts and the papers start again |

`CustomEditor/Crafting/CraftGuideTests.cs` covers the guide (*Next*, `CraftGuide.Next`):

| Area | Tests |
|---|---|
| Every design | each built-in design is guided from its parts to its trial at every station preset that makes it (the ten presets but the complete workshop: 179 pairs): every step names a real area and place that **Go** reaches there (`Here`, as each area's own checks), the guide's *Finish it* is one the bench accepts, and the walk ends at *Take it: it is done* (wooden parts are fitted sawn to their length, as the guide asks first) |
| Pieces | a stone named wrongly goes on to be planned and is sawn or ground by the model's thresholds; a hide follows the path over the tannery and is cut at a leather bench; a raw hide hung to dry is taken down for what it still needs; cloth is cut at the tailor's table, or at a leather bench without one; fibre is made ready, spun and woven; ore goes to the bench, the furnace or the crucible as its metal demands; with nothing picked the furnace says what it holds once its charge is spent; an open weld, a matte and silver in lead have their steps; wood is debarked, split or riven by its size, bent, seasoned, sanded and sealed |
| Items | an item asks for its thread, and another set of fasteners, as the bench would |

`CustomEditor/Crafting/CraftContentFixTests.cs` covers the built-in content:

| Area | Tests |
|---|---|
| The rules' check | it finds no contradiction in the built-in content, and each kind of contradiction in content made to have it |
| Classes and metals | classes of equal priority are tried in the order listed; every class can be reached, but those of metals never found alone; *Add Missing Built-in* brings a new metal with what it does in alloys |
| Parts and stock | every part of every design can be had from the stock; every metal part made to its own profile is inside its mass range in steel and bronze; eyes leave metal round them and weapons are rated for their main part's mass; a wooden stock item is the species' blank and its log as long |
| Values | a bar is never worth less than the ore it took; the class multiplier counts the same raw, worked and finished; wood is worth its share of a blank, so sawing makes no more of it; hides and cloth weigh as leather and cloth do and are worth their share; fasteners are worth their metal and their making |

`CustomEditor/Crafting/CraftReviewShellTests.cs` covers what the station window shows from the model:

| Area | Tests |
|---|---|
| A wooden part's length | a stock blank meant for a part is sent to the saw (Carve's *Saw to length*, the Woodshop's Saw at a sawhorse alone, or where to take it) and one too short is told so; it is sawn before it is bent; the guide asks exactly when the evaluator notes the length; a quarterstaff fitted with the stock stave is taken apart and sawn before it is finished |
| Firewood | wood named as firewood burns first, then the cheapest: never an ironwood blank while a pine plank is left |
| The lettering | every name of the built-in content has its glyphs (none is written as a space); cutting to fit and wrapping keep to the room given |
| The arm | its steps and its words change together; blows are named only where the hands strike |
| What happened | the workshop's events are sentences; a piece's origin is words, never an id or a name the tests have yet to tell, and it is the first thing it came from (*rived from an ash log*) |

`CustomEditor/Crafting/CraftReviewSmithTests.cs` covers the smith's review (the forge, the anvil, the quench, the oven):

| Area | Tests |
|---|---|
| The bellows | pumped steadily, one or two a second, to a welding heat they leave the hammer arm fresh; frantic pumping tires it, for less air; light work is no movement of the hand and tires a practised arm less |
| A blade's edges | a piece has its purpose's edges however the purpose came - said, fitted to an item (and taken off again), cast in the part's mould - and a weld or a fold keeps them; a plain billet's bevels go into the stack; the wheel's first bevel gives the purpose's edges, else one; a double-edged blade bends in the quench as little whatever gave it its purpose |
| The oven | it takes only what its dial can be set to, in the workshop and through the guard: a bar left at its hottest tempers, it does not melt |
| The anvil | wood is straightened as far as it gives - steamed hot like hot iron, cold it mostly springs back; cold copper still moves and is never told it is too cold, cold tool steel barely moves and is told so, spring steel tears, a soft metal worked cold is told to anneal it; the hot chisel's cut is the chisel's, from above, the hardy's from below, and both halves start uncut |
| Clay | only clay that goes on is used: wiping it off, or the same coat again, uses none |

`CustomEditor/Crafting/CraftReviewSurfaceTests.cs` covers the surface's review (the wheel, the file, the brush, the buff; the
whetstone):

| Area | Tests |
|---|---|
| The guard and the wheel | a touch of the wheel the allowance covers is granted as claimed (refused once it is spent); the wheel, the file, the brush and the buff are charged for every stretch they work, and what is granted is spread back over them; the wheel's time is shared out over the stretch it is moved along, so an honest press - still or swept - is granted whole |
| Dips | a hardened blade the wheel heated past its martensite start is dipped, not quenched (nothing learnt, no martensite); at a hardening heat the wheel's dip is refused, a quench at the tank is a quench, and so is a quench cut short going back in |
| The strip | every built-in edge, ground at the angle asked for, is at the design - the angle its section carries (steeper toward a point); a narrow section asked for too acute an edge is judged at the angle it carries, and the content check names it (and a point too thick to hone); the evaluator's ideal is that angle; an edge wider or thicker than the design is still to grind, and the bevels ground on bring it there; a design angle past what the wheel grinds asks for no more, and is named |
| Grits and words | a grit is done where the next one takes over, so grit by grit nothing reads as skipped; the look says what the Grind area says of a surface (a mirror only off the rouge or a buff), sees the burr the stone draws and reads keenness on the stone's ladder; each grit looks brighter and keeps rust off better |
| The stone | a full bar is full (no hidden symmetry past it); the strop is drawn spine first - edge leading it cuts the leather, hones nothing, takes no burr off and never chips; held well above the bevel the stone hones a steeper edge |
| Requests | descaling takes only a brush or a scraper; a coat goes only on a piece in the hands or fitted |

`CustomEditor/Crafting/CraftReviewSmeltTests.cs` covers the smelting review (the ore bench, the furnace, the crucible):

| Area | Tests |
|---|---|
| The pour | one unbroken stream stopped early comes out short, not with a cold shut; stopped and started again, it breaks once; held in the cup (the window's own rule: the stream reaches as far as the tilt throws it, and falls where it fell once let go) and tipped back when full, it loses nothing on the floor - carried past the cup, it splashes |
| Moulds and melts | a fluid melt fills every cavity of a fastener mould a full pour brought (half a pour half of them), a sluggish one only as far as it runs, and the verdict says how many; an unstirred melt casts streaky - weaker, and the look, the verdict and a fold (which halves it) say so; a piece melted into an empty crucible is one even melt, more of its metal keeps it so, another metal streaks it by the lesser's share |
| The crucible | the crust left in a crucible poured empty is scraped out whole (a frozen melt still holds its dross); a melt that would cast porous says so in its look, one that would not does not; a whole sprinkle of borax covers the whole melt |
| The furnace | its pool holds no gas of its own and its slag is named as the furnace's (tap it, nothing to skim); fine ore in the charge chokes the draft, as slag left untapped does, and the furnace says so |
| The ore bench | the wire brush takes only the lump's weathered rind, which its broken stones share, never the rock inside; the stones lie where they lie (inside the tray, apart, a stone thrown out leaving the others be, a blow's pieces by the stone they came from); a blow makes no more pieces than the bench has room for; a side brushed is not counted again and the station agrees with what the sides show; the hammer's bar shows the blow as it lands (held too long it sinks); a stone ringed for these hands falls through the sieve |

`CustomEditor/Crafting/CraftReviewWoodTests.cs` covers the wood's review (the woodshop and the carving bench):

| Area | Tests |
|---|---|
| The guide | a log is split on into billets before it seasons (log, halves, quarters, billets), a green half too, a seasoned one left to the woodworker; stock meant for a part is split or rived while each half still holds it (a hatchet's haft from the square billet, a throwing axe's twice rived), never a plank; the stock stave meant for a haft is sawn, carved (the width first, the more off), sanded and sealed (optional), and is then ready - carved too thin, nothing more is asked; a grip is bored for its tang after it is carved |
| The wedges | a 16 mm plank (or the pine one) is too thin for them, and the workshop refuses it the same, the froe takes it; a log's chain is theirs to the square billet; a slab split far off the heart is rived |
| Seasoning | a stack laid well hardly checks (oak too), every gauge at its tick stays under what the look calls checked, unsealed or in the sun it checks, all wrong worst; up to the stock's 40 mm at the stack's full pace, a billet slower, a whole log far slower (and, laid well, only slow) |
| The stickers | one under each end and evenly between reads even (four or five), stickers floating in from the ends, bunched or too few do not; a haft stacked on the end layout dries straight |
| The saw | set back in its kerf it goes on from where the kerf ran (lifting never straightens a wandering cut, and a straight hand stays straight); across a wide board it takes more strokes than across a stick, a square section as many as a rip through its side (the 40 mm stave 14 strokes) |
| A cut | a part sawn from the long stave keeps as many stretches as from a short blank (a fresh blank's, a grip too), metal keeps its own rule; each part has one fresh, unsealed end, and a seal stays through the stack |
| The knife | a straight stroke along a sloping line is steady, a wavering one strays as far as it wavers; the sloping stroke does not split the board |
| The wedge's line and the bit | the line must run across the side the wedges part (a half's from its flat face out; a round log or a quarter either way); the bit goes on from the slot in the piece, reaches past turns on their way, never past three quarters of it |

`CustomEditor/Crafting/CraftReviewLeatherTests.cs` covers the leather's review (the tannery and the cutting table):

| Area | Tests |
|---|---|
| The hair | a cap cut from a hide tanned with its hair on comes out worse than one limed and unhaired (half the workmanship) and says why; hair keeps the slicker and much of the dye off the grain; the work's measure of the hair left is the path's and the guide's |
| The colour | an item is the colour of its leather - the tan's undyed, the dye's dyed black; cloth is its own colour, dyed as it took |
| Rot | a raw hide straight into a weak bark liquor (the path's way) tans without rotting; in plain water or a spent liquor it rots, faster in a warm vat (no faster past 40 °C); the window's warning: it rots, but nothing spoils yet, and in lime it does not rot |
| States held | a state told again is no worse (rot, a hide shrunk in hot water, a closed tan by the share that closed), while two scores at one place are; a burning stretch and scale hammered in blow after blow are no worse than the metal is |
| The frame and the rack | every side in the green dries flat, two left grey pucker (less than the rack); the look words the frame by its pulls (slack, taut, unevenly, overtight); the rack hangs four hides, a fifth when one is taken down |
| The tools | a sloppy stir after a steady one leaves the liquor as mixed; the hair knife scuffs only where unloosened hair holds (a drake's bare grain not at all); a stroke eased over the thin belly is worked at each stretch's own pressure; one pass of the skiving knife at the gauge's line does not go through, well past it does |
| The table and the vats | chalking leather is leatherwork, cloth tailoring; a cut along the chalk is cleaner and the part's look says so; a vat holding another liquor is refused before anything is poured (asking refuses nothing); a light knife score is marked as the knife's, a tear as the frame's, the rest as damage |

`CustomEditor/Crafting/CraftReviewTextileTests.cs` covers the textile's review (the fibre bench, the wheel, the loom and
the tailor's table):

| Area | Tests |
|---|---|
| The loom | a cloth is as sound as its yarn - a weak, kinked yarn weaves a weaker, stiffer cloth and its record says why; rows beaten true weave a stronger, no more flawed cloth than poor beats; a cloth of a poor yarn beaten unevenly looks loose and weak, a good one beaten true does not; the cloth's record keeps the trueness the station wove (the window words it from there) |
| Plying | the strand that keeps its rest is no longer a counted skein (it reels nothing till counted again) |
| The measure | a chest's measure waits for a garment - a shield started first does not take it, the cloak after it does, one taken with the shield picked waits; what takes a chest's measure is exactly what its fit counts for, design by design |
| The thread | a hood sewn with coarse wool yarn says its seam's thread was too weak for the cloth, one sewn with fine worsted says nothing of it |
| The needle | a pull pierces the holes its stitches need (those already punched first, never more than the seam takes, spaced as the hand did); a refused pull pierces nothing; the thread and the pulls land before the item is taken; only the stitches pulled past the cut cut the seam (a run without the count, by its pull); the guard charges a pierce-and-draw a whole stitch's time and keeps its hard pulls to those granted, the awl never pierces; a practised needle lays a run of hard stitches for a hard pull |
| The hands' caps | the reed's beats, the warp and the tape are no truer than the hands (`TrueCap`, the cut's measure) |
| The wheel and the bench | the green TWIST band keeps nine-tenths of the quality, a twist in it reels the middle's thread, 0.32 a coarser one; a part already cut takes no chalk; a miss picks nothing; wet fibre opens 0.6 as far, as the bench draws it |
| The scour | a soak is no longer than the real time since the hands came (a minute a second, a few minutes' room), never longer than the bath takes |

`CustomEditor/Crafting/CraftReviewGemsTests.cs` covers the gems' review (the lapidary and the jeweller):

| Area | Tests |
|---|---|
| The line | a line is the same line drawn either way (`NormalLine`); sawn across the top drawn left to right or right to left, the top goes; beside a planned stone off the centre, drawn downward, the plan is untouched; cleaved along the trace drawn either way, it parts where it was drawn |
| A line through nothing | below a face already sawn it is refused - no weight, no offcut, no chips, no heat, drawn either way, snapped or sawn; across what is left it saws; on a ground stone, a line past its ground outline is refused, one across it cuts the outline back |
| Polishing | hot (pressure × speed 1) a cut that was short, true or overcut comes out worse, never mended; cool it stays as cut; polished on past bright an overcut tier goes deeper (`WorseMeet`); a graze polishes as much as it touched, a pass no more than its half second, a pass spread evenly is one sent without regions; pressed hard on the corners a table on the cleavage does not flake, on the table it does |
| The compound | the first pass charges the lap, the same compound again does not, another does (and back again), and the stone keeps it (`ChargesLap`); a compound too soft is said every pass, one as hard as the stone is not |
| The rough and the tiers | an offcut of a preform is rough and is not faceted with most of it outside its plan; recutting a botched tier takes its chips and its flaw away, the saw's stay, and a botch on the recut is the tier's own again; facets cut alternately steep and shallow are uneven (`TierFromHits`), all to one side lie off the angle instead; the tier's own facet count is cut and timed (`TierFacets`, the guard), its misses counted against it |
| The loupe | a sparse sweep finds less and the SEARCHED meter says so; a look searches only the depths within its focus; rows close enough for the rings to meet fill the meter, and then every inclusion of five well included stones is found; a thick crust is not counted searched; a cut stone is searched over its own outline |
| Setting | pushed one by one, a stone is loose (and not inspected as finished) until the joint is done, and then the inspection and the item agree it holds (`SetSecure`); the seat's words say to start a new setting exactly where no pushing could set it |

`CustomEditor/Crafting/CraftReviewAssemblyTests.cs` covers the assembly's review (the assembly bench and the finished
item):

| Area | Tests |
|---|---|
| The socket | no pin's hole and no pin before the head is driven home, and a pin alone is neither done nor complete; driven home it is done, but a pin fitted is set (or taken off) before it is finished; through, the head goes no further; the pin never makes it worse; a halberd takes both its langet rivets; driven past home the shaft splits, said of the shaft, never of a wedge |
| The hammer's band | a wedge is best driven in the band - a hard blow splits the haft at once and drives it crooked, a light one bounces; a guard's slot closed with full blows is dented and worse; a pommel peened at full charge is ragged; a joint is as true as its blows, and a part taken off forgets them |
| The hilt | the peened pommel holds the blade (and no other blade goes in) till the pommel comes off |
| Rivets | a rivet set stays in its set - the item weighs it, is worth it and lists it; at the finish those left over come off as a set of their own on the bench, the metal shared by count, and a replay makes the same; a set taken off keeps those still to set, every one set from it drilled out and lost (the holes stay); a part taken off drills its rivets out of the set |
| A joint in progress | holes alone read as true as they were punched, four good stitches of twenty read good, the finished rating still a fifth; holes off true read so |
| Holes, cloth and play | misaligned holes cost a riveted joint once, as a seam; a clasp sewn onto cloth is cloth work and its thread's advice speaks of cloth; a wedge takes up a loose eye's play (3 mm), not a far looser one, and a socket's play stays |
| The item | the finisher is its maker (a replay keeps it); a blunt head's inspection rates its striking face, an edged one its edge |

`CustomEditor/Crafting/CraftReviewHandsonTests.cs` covers the 3D work places' review (the rules they measure the hands by
and show):

| Area | Tests |
|---|---|
| The bit's circles | six steady circles bore five or six turns, each read steady, whether the hand circles from where it pressed, round it or half a radius off it, at 30, 60 and 144 frames a second (`CraftHandCircles`); a straight drag and a back-and-forth turn nothing; a squashed loop reads unsteady, and turns as steady as a true circle bore without splitting the wood |
| The hammer's tilt | a hand that drifts 20-30 px on a 1080 px screen strikes flat and a deliberate tilt bevels (`CraftForging.TiltFrom`, the 3D anvil's 2 % and 12 % and the window's 8 and 60 px); the workshop forges no bevel from the drift and one from the tilt |
| The knife's line | a straight stroke down a slope is steady; a 55 px wander reads in the red band and the workshop splits the wood, as the band says (`StrokeSteady`); the newest point's side of the stroke's line (`StrokeWander`'s `last`) |
| The bellows | the meter is the draft the fire gets (`CraftThermal.DraftShare`): the fire's own with no air, raised by what a squeeze gave the fire, none with the vent shut; the pump's rhythm (`Rhythm`: steady, frantic) |
| The ore bench | the Clean meter is the station's dirt share: a stiff brush's going-over on a 20 % stone (5.9 %, still dirty), and several brushes in another order, give the station's stone (`CraftOres.BrushTo`) |
| The blow | held long enough, the blow's meter fills at a full blow (`FullBlow`), and held on the arm wavers |

`CustomEditor/Crafting/CraftReviewContentTests.cs` covers the content review (the default content, the economy, mastery
and the journal):

| Area | Tests |
|---|---|
| Insights | a crack teaches the lesson of its real cause - burnt steel burning, iron with sulfur torn at 1000 °C sulfur, phosphoric iron cracked cold phosphorus, cast iron cast iron, tool steel struck cold forging too cold, a crack as it hardens out of the tank the quench's; every one of the 40 insights has an event that teaches it; the magnet's only where there is a magnet; the temper colours on bright steel in the oven, scale hammered in (told once), a quench side to side warping (up and down not), a seam never fluxed cooling unset; zinc boiling off calamine; rot, a closed tan, shrinking, stiff drying, alum in water, dye over oil; a finished item's coarse grain and loose fit, in the item's own craft |
| The procedure | a vat by its strength and warmth, rivets made cold, a pour by its glow (with the heats once learnt), hardening in hot water by its warmth - no degrees, no percentages |
| Worth | 240 g of wool spun is worth more than its fibre and less than four times it, and woven worth more than its yarn; every built-in bolt is worth more than its finest yarn; a thread priced at its old 1.5 a skein is flagged; a woven length longer than a bolt is worth its cells, cut or whole; mythril welded onto iron and orichalcum in copper are worth their parts, and tool steel still more than twice iron |
| Stock and wood | every stock metal keeps its class with a twentieth more or less of what is alloyed in (Mythril is Mythril); a green log weighs 1.6 / 1.12 of the same log seasoned |
| Practice | reading, measuring and planning teach nothing, nor a skein counted again or a stone named again; a gambeson teaches over three times a sash, a sword more, the sixteenth sash half the first, an older journal seeded alike; the Mastery tab words the rules' own widening |
| The look | an unknown metal by its whole noun phrase ("Bar of soft, bright metal"); the glow in the middle or at the far end; the balance weighs an item with its parts; gold-bearing quartz is not heavy, a fallen star is; a linden log and a linden plank |

`CustomEditor/Crafting/CraftReviewJourneysMetalTests.cs` covers the review of the metal journeys (raw stock to a finished
metal item):

| Area | Tests |
|---|---|
| The bloom | hammered sound at welding heat, a bloom keeps its metal blow by blow (only its slag leaves) and ends smaller and lighter (`CraftForging.Compact`); consolidated (`Consolidated`) it is a bloom no longer - said once, its furnace slag no longer a flaw, named a bar (by its purpose first: a dagger's blade), no lumpy, cindery look |
| The strip | a stretch 6, 15 or 30 % thin with no bevel, or bevels just started, is still to grind and told apart as undersize (`Judgement.under`); with its bevels met, a little short is off and far short too far; where no edge is wanted the size is all |
| The stone | a flat past `EdgeMet` is back to the wheel, the hair under it the stone wears (`WearLastFlat`), honed under `FlatGone`; a stretch the strip calls done is worn and honed on the medium stone |
| Soundness | a dagger whose blade is cracked through rates Poor with its soundness its blade's (a weighted geometric mean), breaks early and says so; one small crack costs little (`Unsound`, `SoundnessFactor`) |
| The crucible | copper and tin melted together: the tin run far over its liquidus takes up no gas while the copper lies solid in it (`ChargeUnmelted`), the look says so; all molten and held too hot, it does |
| The quench | a preset smithy or armourer keeps water, brine and oil, a smeltery none, a test station every one (`CraftThermal.PresetQuenchants`); water and brine are harsh, oil is not (`CrackHarshness`, `HarshQuench`); the warning names the oil the station keeps (and only the clay where it keeps none), never for mild steel or over oil; water cracks a bare spring-steel blade, oil hardens it whole; the quench lesson names oil and clay |

`CustomEditor/Crafting/CraftMetalJourneyTests.cs` follows whole journeys at real stations, the requests shaped as the
windows send them (the guard on, a novice's hands, the window's pace) - a dagger from two stock bars (its purpose said at
the anvil, the tank filled with oil, the edge ground all along: whole, Good or better), a bronze hatchet and spear cast at
the smeltery, an iron bar from ore through the bloom (lighter than the bloom, no bloom), a mace head from ore through the
crucible (sound, as heavy as it should be), a welded scythe on a bent snath (hardened whole in oil), plated bracers from a
raw hide - and the rules they showed: a socket driven within the window's green band holds and splits past it, copper and
tin melted together cast as sound as bronze bars, a cracked blade makes a poor dagger, a bar fitted or cast as a
double-edged part takes its two edges, the stone hones where the strip is green, the bellows leave the arm fresh, a scale
pit is as bad as it is deep.

`CustomEditor/Crafting/CraftReviewJourneysCraftTests.cs` covers the review of the journeys to a finished leather, cloth or
jewelled item:

| Area | Tests |
|---|---|
| What guards | a jerkin guards by its body, as its design says, however thick its shoulder straps (`Guards`: they hold it on); a belt, all straps, is rated by its strap; a body hardened and thicker guards more, never more than half again the design's (`GuardsAtMost`) |
| Cloth | a flawless wool tunic guards as its design says and is no longer held to Fine (a cloth weighed against a plain weave: `ClothStiffness`, `ClothStrength`); a canvas robe guards more, never more than half again; a flawless canvas tabard lasts as its design says (no tannage judged), a bark-tanned belt still outlasts an alum-tawed one |
| What goes in | one item of a supply weighs its kind's (a batting 300 g, a skein `CraftFibre.SkeinGrams`, solder half a gram, a fibre its grams; an author's own kept); a gambeson weighs its panels, battings and thread, on the balance as it is made and finished, names its batting among its parts and is worth the padding that went in (linen's, wool's); supplies put in unnamed count as the first of their kind, a named one on top (`CraftAssembly.SupplyIn`) |
| The plan | for every cut the Orient place's expected yield is what the stone ground to its outline keeps (`CutKeeps`), leaving the wheel out never gives a heavier stone and is worth less |
| Dried stiff | a bark-tanned hide that dried stiff is told to be oiled supple at the beam (a tannery's or a leather bench's), and oiled so it is supple, its grain whole; staked dry, bark grain cracks (`CracksStakedDry`); an alum-tawed one is staked dry, harmlessly |

`CustomEditor/Crafting/CraftCraftJourneyTests.cs` follows whole journeys at real stations, the requests shaped as the
windows send them (the guard on, a novice's hands, the window's pace, supplies from the bag named as they go in) - a
hunting bow from a yew blank and from a green log (debarked, split, rived, seasoned sound), a leather jerkin from a raw
cow hide (tanned, framed, staked as it dried, oiled supple where the guide says it dried stiff), a kite shield of linden,
leather and rivets, a padded gambeson from bolts of canvas and linen (weighing its battings), a wool tunic from the
fleece (spun, woven, made to measure), a bezel ring, a pendant and a charm from rough stones (cut as the guide says,
weighing what the Orient place expected, set until the window says it holds and not called loose) - then a stone set as
the Set place says, the best stack seasoning a green billet sound, a wooden part told to be sawn to length, and every
leather and cloth design made well (each about its design's Defense, its protection no worse than good, a Superior or
better piece lasting most of its design's durability).

`CustomEditor/Crafting/CraftReviewDocsTests.cs` covers the review of the documentation against the code:

| Area | Tests |
|---|---|
| The pour | a mould's stretches add up to its whole length (`MoldSegments`); a full pour into every mould, stock or pattern, as the window and the 3D crucible pour it, is a full casting and its words say *A clean pour: ..., full and sound.* - doc 01's walkthrough among them: two stock Bronze bars (315 g each) covered, stirred and poured in a steady stream fill the hatchet's own head (616 g); a melt too small for the mould is still short and says why |

`CustomEditor/Crafting/CraftReviewCriticTests.cs` covers what the final review found missing between the areas and between the machines:

| Area | Tests |
|---|---|
| The bloom | the guide goes on from the furnace with the bloom it pulled: from a smeltery (no forge) it is taken to a forge; at welding heat it is struck on the **Anvil** with flat blows, cooled it goes back into the **Forge**'s fire, and once it is hammered sound (a bar, `CraftLook.Bloom` false) the step is gone |
| The weld | a forge weld at welding heat is struck shut on the **Anvil**; cooled, it is fluxed and taken back to the fire (**Forge**) |
| A casting everywhere | a casting is the same piece, flaw for flaw, on the server and on a copy that applies the server's operation (as a network client's does): a dirty pour broken twice from the crucible, and metal run out of the furnace under its slag - its flaws are seeded by the id the casting takes (`CraftWorkshop.NextId` before it is created), not by an id only the copies' operation carried |

`CustomEditor/Networking/Tests/StationNetTests.cs` covers the network parts (no Fish-Net needed):

| Area | Tests |
|---|---|
| Payloads | small and large text survive packing, splitting and reassembly (and compress); garbage and oversized payloads are refused; a new transfer replaces an unfinished one |
| Following the server | a client's copy of a forge session is the server's, number for number, and raises its own events; a late joiner starts from a snapshot; a lost packet or a drifted copy asks for a snapshot; a replaced workshop is sent whole; an answer waits for its operations and knows the events they raised on the copy; an alchemy copy follows the server's brew exactly |
| The guard's bounds | work takes real time; the hammer must be raised to strike hard; blows that never tremble are noticed; hands are never steadier than practice allows; malformed requests are refused; a network request carries every field, its number too |
