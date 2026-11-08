# Housing 02 — Placing Houses and Inns

[Back to the Housing guide](README.md)

Houses and inns are placed in the city scene like any object; their **areas** — the plot, the house's footprint, the
zones, the inn's building, each room's floor — are outlines on the ground (X, Z, in metres, in the object's own
space), shaped in the Scene view. The areas price the house and decide what is inside, so the editor keeps them
**truthful**.

## The Property inspector

Select a property. The inspector has, from the top:

* **Summary** — its id (**New Id** for the copy of a duplicated property: it asks first, since with a new id it no
  longer finds its old lease, rent account and chests; its chests and doors that share their ids with another place's
  get new ones too), its **Name**, **Description** (what the realtor says) and **District** (picked from the Housing
  Settings' districts), and its sizes: land, house footprint and floor area, garden, each zone.
* **Land** — the plot. **Make Rectangle** (width, depth, angle) where it is, or **Fit Around House** (the house with
  2 m around it). Not all land is garden: a plot can be just the house, or a house with a yard and a garden.
* **House** — the footprint on the land, its **Floors** (1 to 12) and **Floor Height** (Floors × Floor Height is the
  house's height: what passes more than 0.5 m over its roof does not stand on the land). **Make Rectangle** with an
  offset from the property's origin.
* **Zones** — parts of the land with a purpose: Garden, Yard, Orchard, Pasture, Workshop, Storage, Courtyard, Other,
  each with a name ("Back yard") and its outline. **+ Zone** adds a 4 × 4 m yard on a free spot of the land (clear of
  the house, the gardens and the other zones; smaller when that does not fit). Each use has its own rate per square
  metre (Housing Settings); the realtor lists them.
* **Renting** — **Weekly** and/or **Monthly**, **Quality** (multiplies the base rent its sizes give: 1 = ordinary),
  **Base Rent Per Day** (0 = from its sizes). Below, what the sizes are worth: "Base rent: 12 a day · 84 a week (7
  days) · 336 a month (28 days) — from its sizes, its quality and its district". The market moves the actual rent
  around it ([chapter 03](03-The-Market.md)).
* **What belongs to it** — its **Gardens** (Soil Fields), **Chests** (each with an id its contents are saved under),
  **Beds** (Sleep Spots) and **Doors** (Lockable Doors). With **Include Children** the children are taken too.
  **Collect From Children** lists them (a new id for each new chest). **+ Garden** adds a Soil Field child on a free
  spot of the land beside the house ([Agriculture, chapter 02](../Agriculture/02-Defining-Soil-Areas.md) shapes its
  soil). **Make Gardens Networked** gives each garden a NetworkObject and a NetSoilField (needs Fish-Net).
* **Checks** — what is wrong with it, refreshed every half second and at once after a change (below).

### The Scene view tools

Choose a tool at the top of the inspector:

| Tool | What it does |
|---|---|
| **Select** | Unity's own tools: pick and move objects |
| **Land** | Drag the plot's points on the ground. **Shift+click** near an edge adds a point; **Ctrl/Cmd+click** a point removes it |
| **House** | The same for the house's footprint; the square in its middle moves the whole house |
| **Zones** | Click a zone on the ground (or choose it in the list with ○), then shape it like the land; the square moves it |

**Snap** makes points snap to a grid (metres, in the property's space). The Scene view also draws the gardens (brown),
the other properties and inns around (grey, **filled red when they overlap this land**) and the sizes. Rotate the property
object to turn the whole plot; keep its scale at 1 (areas are in metres and ignore the scale).

## Inns and rooms

Select an inn. **Building** shapes its area (the building's footprint, checked against the properties around it);
**Rooms** shapes and moves the rooms. *Create Inn* makes the area with two rooms; **+ Room** adds a 4 × 4 m room with
a bed, a chest and a door on a free spot of the area whose door opens onto free floor - inside the inn, in no other
room - that can be walked to from the entrance (the middle of the inn's front, a step inside, where *Create Innkeeper
NPC* puts the innkeeper), without cutting off another room's door: the rooms fill the area with corridors between them.
The room list shows each room's **Quality** (multiplies the nightly price) with **Select** to open its own inspector.
**Collect From Children** lists the Inn Rooms among the children (and drops a listed room that now lies under another
inn). Set its **Nightly Price** (what a night in an ordinary room costs when the inn is as full as usual) and
**District**. An innkeeper lets its *Inn*, else the inn it stands under, else the nearest inn of its scene (by the
inns' origins); the checks follow the same rule.

A room (the **Inn Room** inspector) has its **Area** tool, **Make Rectangle**, **Name**, **Quality**, its **Bed** (a
Sleep Spot; empty: the one among its children), **Chests** and **Doors**, and **Collect From Children** (finds its bed
and lists the chests and doors, a new id for each new chest). Rooms on different floors (2.5 m apart or more) never
overlap each other.

## Areas are truthful: the checks

The Property, Inn and Inn Room inspectors show their checks every half second, and at once after a change (while a
point is being dragged, at most every half second, and once more when it is let go). They look only at what is near
the place - the physics scene finds the colliders over the land - so they stay quick in a big city. *Housing ▸
Validate Housing* runs them all and logs them to the Console (click a line to select its object); the Project
Validator runs the same checks, with a fix button where there is one.

**Errors** (must be fixed):

* Two properties' lands overlap, or a land and an inn's area ("'Rose Cottage' overlaps 'Ivy House' by 3.2 m²").
  Plots may touch — share a fence — but not overlap. Two places are compared in one place's own space, and what
  counts as touching grows with how far from the world's origin they lie (by what float positions allow there), so
  plots kilometres out that share a long fence are never reported.
* The house is not on its land ("2.4 m² of its footprint is outside it").
* **Another object of the city stands on the plot** by 1 m² or more — the enabled solid colliders that reach the land
  between 0.2 m above the plot (flat ground, roads and pavements do not count) and 0.5 m above the house's roof (a
  bridge, an arch or a balcony passing higher does not count), and that are not part of the property (not under it,
  nor under its listed gardens, chests, beds and doors), not a terrain, not on the Housing Settings' *Ground Layers*,
  not an NPC, a creature or a moving rigidbody. An object is measured by the ground it really covers seen from above —
  boxes, spheres, capsules and convex meshes by their own outline, other meshes by rays from above (a turned wall or a
  round tower beside the plot covers none of it) — and the colliders of one object count together (those of one
  rigidbody, or of one prefab instance; colliders that overlap count once): "'Market Stall' stands on the land of 'Rose
  Cottage' (6 m²; its parts 'Counter', 'Crate (2)' and 3 more): move it, make it a child of the property if it is part
  of it, or put it on a Ground Layer if it is ground". Below 1 m² (from *Intrusion Warn Area*) it is a warning. A model
  inside a prefab cannot become a child until the prefab is unpacked (*Create Property Over Selected Object* asks).
* A Soil Field lies on the plot but is not one of its gardens (or a garden of another property lies on it); one garden
  belongs to two properties (both would be priced and let with it, and only one tenant could garden it).
* The land or the house has fewer than 3 points, crosses itself, or is too small (land 4 m², house 2 m²).
* It can be let neither by the week nor by the month.
* A chest is a child but not in Chests (it would get a new id every session and lose what tenants keep in it — **Collect**),
  has no id (**New Id**), is listed twice, or **spawns random items** (it would roll them into a tenant's chest — **Turn Off**).
* An inn room has no bed or no chest, no proper area, or shares its id with another room; two rooms on the same floor
  overlap; a room is in an inn's Rooms but lies under another inn (both would let it — **Remove From List**), or is in
  the Rooms of two inns.
* Two properties, inns, doors or chests share an id (a duplicated object — **New Id…**, which asks which of the two
  keeps it); a garden, chest, bed or door belongs to two places (in play the place registered first keeps it).
* An Innkeeper has no inn to let (no *Inn* set, not under an inn, no inn in its scene).

**Warnings**: a zone or garden reaching outside the land or lying over (or under) the house, a zone over a garden,
two zones or two gardens overlapping (that ground would count twice in the rent), a chest, bed or door off its land, a
property or inn room without a Lockable Door (anyone may walk in) or with a Door Controller among its children that
has none (**Add Lock**), a room's door opening into another room, a scaled property, a district not in the Housing
Settings, a room reaching outside its inn or listed while under no inn, a property or inn in a prefab (every copy
would share its id), no Realtor while there are properties (**Create Realtor**) or a property no realtor lets, a
realtor with empty slots in its Listings (**Remove Empty Slots**; one whose Listings are all missing lets nothing —
fill them, or empty the list to let every property), an inn without an Innkeeper (**Create Innkeeper**), a realtor or
innkeeper that serves one player at a time (**Serve Everyone**). A property or inn outside the City scene is noted
(the hub world's other scenes are made again every cycle).

## Ids

Every property, inn, room, door and chest has an id, made when it is created: the save, the leases and the network
messages go by it. Duplicating an object duplicates its id — the checks report it, and **New Id…** asks which of the
two gets a fresh one, naming both and where they are. Changing the id of a place that is already rented in a world
loses its tenant's lease and chests in that world, so the one that keeps the id should be the one in the save, and the
new id goes to the copy; only you know which that is, which is why every New Id asks first (Fix All too, once per
pair; in batch mode nothing is changed). A property's or inn's new id comes with new ids for its chests and doors
that another place's share, so one choice moves the whole place. A property saved without an id gets one from its place in the scene
(the same on every machine and every session), never a random one.
