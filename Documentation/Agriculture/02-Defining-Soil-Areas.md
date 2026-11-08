# 02 — Defining Soil Areas

[← Agriculture](README.md)

A **Soil Field** is a piece of ground that is soil. You place it on the ground (of the city, for now), draw its outline
and say what its soil is — and, since no garden is one soil from end to end, you add **sections**: the parts where it
differs. Everything is edited in the Soil Field inspector and directly in the Scene view, and the **preview** draws the
exact soil the game will start with over the field.

```
 ┌───────────────────────────────────────────────┐   outline: the field (any polygon)
 │  base soil: organic-rich, pH 6.5        ░░░░░ │
 │   ┌──────────┐                          ░sour░ │   a polygon section: peat, pH 4.8
 │   │ compost  │      ( stony )           ░░░░░ │
 │   │  bed     │      (  patch )                 │   a circle section: stones 0.6, soft edge
 │   └──────────┘                                 │   a rectangle section: organic matter 14 %, N 70
 │ ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒ │   an irrigated strip (kept moist with river water)
 │      ▓▓ ▓▓▓  painted cells: a magical seam      │   painted cells: magic 0.8
 └───────────────────────────────────────────────┘
```

## The inspector

At the top: the field's **Name** (the window's title, the notebook), its **Field Id** (its save file and its network
messages: made for you; **New Id** after duplicating a field by hand), the area, the cell grid and how many sections it
has, and every problem the layout has (errors in red, warnings in yellow). A field outside the City scene says so.

Below, the **tool bar** picks what the Scene view edits — **Select**, **Outline**, **Sections**, **Paint** — and the
**Preview** shows the baked soil over the field (with **Opacity**):

| Preview | What it colours |
|---|---|
| Soil type | Each cell's soil type (its dry colour) |
| Sections | Which section decides each cell (its colour; blended at soft edges) |
| pH, each nutrient (N, P, K, Ca, Mg, S, Fe, Zn, Cu, Mn, B, Mo), organic matter, salinity, water retention, drainage, compaction, moisture, magical essence, pathogens, stones, tilth | The property on a dark-blue → teal → green → yellow scale, with a legend of its range |
| Fertility | An overall score (what it can feed now, its reserves, organic matter, texture) |
| Irrigation | Where channels keep the soil moist, and how moist |

Hovering over the field shows the value under the mouse, the section that decides it and the field-local position.
The preview is **exactly** the soil a new game starts with (the same bake the game runs).

## The outline

**Outline** tool: drag the points on the ground. **Shift + click** near an edge adds a point there; **Ctrl/Cmd + click**
a point removes it. While any tool but **Select** is on, clicks on the ground belong to the field (they do not select
other objects) and, while the field is selected, Unity's move/rotate/scale gizmo is hidden, so a stroke in the middle
of the field never drags the field away — pick *Select* to select or move objects. The Scene view's hand (Q, Alt, right or middle drag) still pans
and orbits. In the inspector's *Outline* group: **Snap** (to a step in metres),
**Rectangle** and **Circle** (replace the outline by one around it), **Centre** (moves the object to the middle of the
outline, by whole cells, without moving the outline, the sections or the paint in the world), **Cell Size** and the
points as numbers.

* The outline is in **metres**, in the object's space (X, Z): keep the object's scale at 1 (a warning tells you).
  Turn the object to turn the field.
* **Cell Size** (0.25 m by default) is how finely the soil is divided: each cell has its own soil. Smaller cells follow
  sections and roots more closely and cost more memory (a cell is about 200 bytes: 40 × 30 m at 0.25 m is ~4 MB). A
  very large field grows its cells by itself (at most 262 144 cells, 1024 a side; a warning says so).

## The base soil

The soil of the whole field where no section says otherwise. Pick a **Type** and **Use its soil** to set every
property to that type's typical soil, then change what you like. The eight types are *Sandy, Clay, Rocky, Organic-rich,
Peat, Volcanic, Swamp* and *Magical* ([chapter 03](03-Soil-Water-and-Weather.md)); the properties:

| Property | Unit | |
|---|---|---|
| pH | 3–10 | Decides what the roots can take of every nutrient |
| Nitrogen, Phosphorus, Potassium | ppm | The primary nutrients |
| Calcium, Magnesium, Sulfur | ppm | The secondary nutrients |
| Iron, Zinc, Copper, Manganese, Boron, Molybdenum | ppm | The micronutrients |
| Organic matter | % | Feeds the soil as it rots, holds water, buffers the pH |
| Salinity | dS/m | Salt: dries plants out, burns leaves |
| Water retention, Drainage | 0–1 | How much water it holds, how fast the excess drains |
| Compaction | 0–1 | Packed soil: roots and seeds struggle, rain runs off |
| Moisture | 0–1 | The water in it at the start |
| Magical essence | 0–1 | What magical plants feed on |
| Pathogens | 0–1 | Soil diseases (root rot, wilt) |
| Stones | 0–1 | Stones in it: in the way of roots, seeds and tools |
| Tilth | 0–1 | How worked it is at the start (0 = never worked) |

## Sections

**+ Rectangle**, **+ Circle**, **+ Polygon**, **+ Painted** add one (with the base soil's values, nothing ticked). Each
section in the list has its **enabled** box, its **colour** (in the Scene view and the *Sections* preview), its name,
**▲ ▼** (order), **⧉** (duplicate) and **✕** (remove). Click its name to select it; unfold it to edit it:

* **Shape**: a *Rectangle* (centre, size, angle), a *Circle* (centre, radius), a *Polygon* (its points) or *Painted*
  (only the cells you paint). **Sections** tool: click a section on the ground to pick it (the last one covering the
  spot), then its handles — the centre square moves it, the corner (rectangle) or edge (circle) sphere sizes it, the
  disc turns a rectangle; a polygon's points edit like the outline.
* **Feather**: the width of its soft edge in metres — across it the section's soil blends into the soil around it
  (0 = a hard edge).
* **Its soil**: tick only the properties that differ here; each ticked property gets its own value. **Soil type** too
  (a cell takes the section's type where the section weighs at least half). **Use its soil** ticks everything and sets
  the values of the chosen type.
* **Irrigated**: channels keep this part at a moisture (*Kept at moisture*) with a kind of water (*River* by default:
  its minerals and salts go into the soil with it).
* **Mulch**: mulch lying here from the start (it keeps the water in and rots into organic matter).
* **Note**: for you (never shown to players).
* **Paint this section** / **Clear its paint**.

**Order matters**: where sections overlap, the one **lower in the list** wins (each blends over what is above it by its
weight). An unticked property never changes anything.

## Painting cells

For the corners no shape fits. **Paint** tool, then drag over the field (hold **Alt** to orbit the camera). In the
*Painting* group: **Brush** size in metres and what it **Paints**:

* **a section** — the cells belong to it fully (whatever its shape says; a *Painted* section has only these cells);
* **Base soil (no section)** — the cells keep the base soil even inside sections (to cut a hole in a section);
* **Erase** — back to what the shapes decide.

Each stroke is one Undo step. **Clear all paint** empties the map. The paint is kept per cell in the layout; removing or
reordering a section moves its paint (and the brush) with it. Trying another cell size or reshaping the outline never
alters the paint: the soil reads it by position, and it is stored again at the current cells only when you paint.

## Natural patchiness

No soil is the same a metre further on. **Variation** (0–1, 0.35 by default) adds smooth patches to everything —
pH ±0.35, nutrients ±30 %, organic matter ±25 %, water, packing, magic, diseases, stones — of **Variation Scale**
metres, from a **Seed** (**New** picks another). 0 makes the soil exactly what you set.

## Weather, look, interaction, saving

* **Use World Weather**: in a scene with a Weather System its weather (plus the seasons); otherwise (the city) the
  Agriculture Database's climate.
* **Show Soil**, **Draw Distance**, **Detail Distance** (fruit, flowers and pests are drawn within it), **Ground Offset**,
  **Follow Ground** (the soil follows the ground below, raycast once when the field starts, on **Ground Layers**),
  **Plant Shadows**.
* **Interactable**, **Interaction Priority**, **Extra Reach** (metres beyond the outline a player can stand and still work).
* **Save To Disk**.

## Play mode

The inspector shows the date, how many plants grow and how many seeds wait in the ground, the weather, and where the
rules run; **Starter kit**, **+6 h**, **+1 day**, **+1 week**, **Rebuild from layout** (afresh: plants are lost) and
**Forget its save**.

## Checking a field

The inspector lists the layout's problems as you work: a broken outline (fewer than three points, under a quarter of a
square metre), cells grown for a large field, a missing base soil, a polygon
section with too few points, a section that changes nothing or lies outside the field, more than 250 sections.
**Agriculture ▸ Validate Agriculture** (and the Project Validator) also finds two fields with the same Field Id (a
duplicated object would share one save — the fix gives it a new id), scaled fields, fields in prefabs, and, with
Fish-Net in the project, fields without a NetSoilField.

## From code

```csharp
var field = new GameObject("Herb Garden").AddComponent<SoilField>();
field.layout = new SoilLayout
{
    outline = new List<Vector2> { new(-3, -2), new(3, -2), new(3, 2), new(-3, 2) },
    baseSoil = SoilProfile.Preset(SoilType.Sandy),
    variation = 0.2f,
};
var sour = new SoilSection { name = "Sour corner", shape = SoilSectionShape.Circle, center = new(2, 1), radius = 0.8f, feather = 0.4f };
sour.soil.values.ph = 4.8f;
sour.soil.SetOverride(SoilChannel.Ph, true);
field.layout.sections.Add(sour);
field.Rebuild(); // in play: bake the soil again from the layout
```
