# Player 09: Targeting, the target frame, parties, guilds, duels, trading and inspecting

**Scripts:** `Essentials/Social/PlayerTargeting.cs`, `PartySystem.cs`, `PartyBook.cs`, `PlayerSocial.cs`
(`PlayerSocial`, `CombatSocial`), `PlayerInspection.cs`, `PlayerActions.cs`; UI in `Essentials/Social/UI/`
(`TargetFrame`, `PartyFrame`, `InspectWindow`, `PlayerNameplates`, `PortalInfoPanel`, `SocialUI`); `PlayerHighlights.cs`
with its shader `Resources/SimpleMovementsSilhouette.shader`; `PlayerBonds.cs` (`PlayerBonds`, `CombatActivity`); network relay `Networking/Runtime/Social/NetSocial.cs`
(`#if FISHNET`); editor `CustomEditor/Social/SocialSetupMenu.cs`, tests `PartyBookTests.cs`. Combat rule changes in
`CombatEntity.cs` (`CombatRelations.Get`) and `CombatSettings.cs`.

---

## 1. What you get (no setup needed)

All of this is created by itself when the game starts with a player. A dedicated server shows nothing.

| You do | What happens |
|---|---|
| **Click** on another player | They become your **target**: a coloured ring under their feet, and the **target frame** in the top-left corner |
| **Tab** | Selects the next player in front of you (cycles) |
| **G** (or the ▾ button on the frame) | Opens the target's **menu** and frees the cursor |
| **U** | **Inspects** the target: their equipment, stats, guild, fame... |
| **Y / N** | Accepts / declines a party invitation (also buttons on the prompt) |

The ring and the name are coloured by how the target relates to you:

| Colour | Meaning |
|---|---|
| Red | Can fight you here (a PvP zone, a duel opponent, a hostile mob) |
| Green | Your party |
| Blue | An ally (guild, alliance, the city or a safe zone) |
| Grey | Neutral |

Every key is rebindable in **Key Bindings**: *Select Target, Cycle Target, Inspect Target, Target Menu*. While the
cursor is locked (third-person aiming), a click selects whoever is under the **middle of the screen**.

You also get, without selecting anyone, the **nameplates** and **highlights** of other players (sections 8 and 9) and the
**dungeon portal panel** (section 10).

## 2. The target frame and its menu

The frame shows:
* the target's **name**, coloured as above;
* their **level** (Lv);
* their **guild** with its tag (`Knights of Dawn [KND]`) and **alliance**, or "No guild", or their title;
* their **health** bar;
* what they are to you: *Party member, Party leader, Guild member, Alliance member, Duel opponent, Can attack you
  here, Friendly, Neutral*.

The **menu** (G or ▾) lists only the options that make sense right now:

| Option | When it appears | What it does |
|---|---|---|
| Invite to Party | They are not in your party | Sends a party invitation (§3) |
| Remove from Party / Make Party Leader | You lead and they are in your party | Kicks them / hands over the lead |
| Leave Party | You are in a party | Leaves it |
| Invite to Trade | Network sessions | Opens the trade request through `NetTrading` (the same as the T key) |
| Challenge to a Duel / Forfeit the Duel | There is a duel system (the Hub World) | Challenges them (the existing duel system), or gives up the running duel |
| Inspect | Always | Same as U |
| Clear Target | Always | Drops the target |

Opening the menu frees the cursor and stops clicks from attacking. Escape or picking an option closes it.

## 3. Parties

* **Invite:** the target menu ▸ Invite to Party. The other player sees *"Bram invites you to a party (30 s)"* with
  **Accept (Y)** and **Decline (N)**. If you had no party, accepting forms one with **you as the leader**.
* **Party frame** (left side): every member with a health bar; the leader is marked ★. Click a member to target them.
* **Leaving and the leader:** if the leader leaves, the next member leads. A party of one is disbanded.
* **Disconnecting:** a player gone for 10 s leaves their party.
* **Settings** (put a *Party System* in a scene with **Tools ▸ SimpleMovements ▸ Social ▸ Create Player Targeting And
  Party System**):

| Setting | Default | Meaning |
|---|---|---|
| Max Members | 5 | Most players in one party |
| Invite Seconds | 30 | How long an invitation waits |
| Only Leader Invites | off | Off = any member may invite |
| Disband When Alone | on | A party of one is removed |
| Auto Accept For Other Players | off | Single-player testing: invitations to a second player object are accepted at once |

### Party members cannot hurt each other (but buffs and heals work)

This is decided by the combat relations, on every machine:

```
duel opponents?             → ENEMIES (always, even in the same party, guild or alliance)
same party?                 → PARTY   (harmful effects blocked unless Party Friendly Fire)
same guild / alliance?      → ALLY    (harmful effects blocked unless Ally Friendly Fire)
zone / city rules           → enemies in PvP zones, allies in the city and safe zones
...
```

* **Combat Settings ▸ Party Friendly Fire** (off by default) is the inspector switch. Off: damage, crowd control,
  knockbacks and debuffs never land on party members, whatever the zone. On: harmful hits also hit party members
  caught in them.
* **Buffs and heals:** abilities that affect *Allies* or *Party* reach party members (and guild or alliance members)
  as before.
* **A single attack** can override it: *Target Rules ▸ Friendly Fire* (Always or Never).
* **Duels win over everything:** two players in a duel are enemies even if they share a party, guild or alliance. When
  the duel ends they are protected again.

## 4. Guilds, alliances, fame (Player Social)

Every player has a **Player Social** component, added automatically. Add it yourself with **Tools ▸ SimpleMovements ▸
Social ▸ Add Player Social** to set values for testing.

| Field | Meaning |
|---|---|
| Guild Name / Guild Tag | The guild and its short tag (up to 5 letters), shown on the target frame and when inspected |
| Alliance Name | The guild's alliance. Guilds of one alliance are allies |
| Fame | Renown, shown when inspected |
| Title | Shown under the name ("the Brave") |
| Allow Inspect | Others may inspect this player |

**From code (where the rules run: offline, or on the server):** `PlayerSocial.For(player).SetGuild("Knights of Dawn",
"KND")`, `.SetAlliance("Northern Pact")`, `.AddFame(25)`, `.SetTitle("the Brave")`, `.SetAllowInspect(false)`. Clients
receive the values; changing them on a client does nothing.

**Combat Settings ▸ Guild Members Are Allies / Alliance Members Are Allies** (on by default): members are allies
everywhere, even in PvP zones. They cannot hurt each other unless *Ally Friendly Fire* is on, and their heals and buffs
reach each other. A duel still lets them fight. Turn a switch off to let the zone decide, as for strangers.

There is no guild management UI yet (creating guilds, guild invitations). Set guilds from your own systems through
the API above.

## 5. Inspecting

U (or Inspect in the menu) on a player target opens the **inspect window**:
* their name and title, guild and tag, alliance;
* level, race and class, **fame**, health;
* **Equipment:** every equipped item with its icon, slot and name (armor, jewelry, the weapon in hand);
* **Stats:** Strength, Agility, Intelligence, Endurance always, and Defense, Magic Resistance, Critical Chance /
  Damage, Attack and Casting Speed, Weapon Damage when they are not zero, plus the armor's Defense.

**Rules:**
* At most `PlayerInspection.MaxDistance` (40 m) away.
* Same realm only (not from the City into the World).
* Not when the target's *Allow Inspect* is off.

The data is gathered **where the rules run**, because only there are a player's inventory and stats complete. In a
network session the server builds it from its own copy and sends it to the player who asked only.

## 6. Multiplayer

1. **Add the relay:** open the Bootstrap scene, then **Tools ▸ SimpleMovements ▸ Network ▸ Add Missing Relays To
   Network World**, and save. This adds `NetSocial` to the *Network World* object. *Create Bootstrap Scene* adds it to
   new scenes.
2. **What `NetSocial` syncs:**
   * Every party and invitation goes to every client, so all machines know each player's party id and compute the same
     relations. Damage is decided on the server.
   * Every player's guild, tag, alliance, title, fame, level and Allow Inspect.
3. **Requests:** invite, accept, decline, leave, remove, make leader and inspect go to the server, rate-limited. The
   asking player is always the caller's own player.
4. **Messages:** "X joined the party" and similar lines reach players through the Hub World messages (`NetHubWorld`).
   Without a Hub World HUD, the party frame shows them in a line above the hotbar.

Trading uses the existing `NetTrading` relay and window. Duels use the existing `DuelSystem` and `NetDuels`. Duels
are only allowed in the city unless *Duel Settings ▸ Allow Outside City* is on.

## 7. Settings of the Player Targeting

Create it with **Tools ▸ SimpleMovements ▸ Social ▸ Create Player Targeting And Party System** to change:

| Setting | Default | Meaning |
|---|---|---|
| Select / Cycle / Inspect / Menu Controls | left mouse / Tab / U / G | Default keys (rebindable in Key Bindings) |
| Target Mobs | off | Mobs can be targeted too (frame shows name and health) |
| Select / Cycle / Keep Distance | 60 / 40 / 90 m | Click range, Tab range, dropped beyond |
| Clear On Empty Click | off | Clicking the ground clears the target |
| Clear On Death | on | A dead target is dropped |
| Select Layers | everything | What a click can hit |
| Show Ring, colours, Ring Width, Ring Material | | The ring under the target |
| Highlight Prefab | none | Your own highlight (a decal, an outline) instead of the ring |
| Create Interface | on | The target frame, party frame, inspect window, nameplates, highlights and portal panel (each only when the scene has none of its own) |

**From code:**
* `PlayerTargeting.Instance.Target` and `SetTarget(entity)`; `SelectedPortal` and `SelectPortal(portal)`.
* Events `TargetChanged`, `MenuRequested` and `PortalSelected`.
* `PlayerBonds.Between(me, other)` (Duel, Party, Guild, Alliance or None) and `CombatActivity.InCombat(player)`.
* `PartySystem.Instance` methods `Invite`, `Accept`, `Leave`, `Kick`, `Promote`, `PartyOf`, `MembersOf`, and the
  `Changed` event.
* `PlayerInspection.Request(me, target)` and the `Received` event.

## 8. Nameplates of other players

`PlayerNameplates` draws names and health bars above other players' heads (screen-space, under the HUD):

| Who | Name | Health bar |
|---|---|---|
| Any other player within **Max Distance** (45 m) | yes | no |
| Your **party** members | yes, green | yes, green |
| Players in a **duel** (countdown or fight; yours or anyone's) | yes, orange | yes, orange |
| Players **in combat** (took or landed a hit in the last *Combat Seconds*, 6 s; a summon's hits count for its owner) | yes | yes: red if they can attack you, amber otherwise, green for your party/guild/alliance |
| Players farther than Max Distance, or in another realm | no | no |

Names are coloured like the relation: green party, light blue guild, blue alliance, orange duel, red "can attack you
here", white otherwise, grey when dead. The guild tag follows the name (`Name [KNT]`). Plates fade out over the last
*Fade Distance* metres and shrink with distance (*Far Scale*); nearer plates are drawn over farther ones.

Settings (put a `PlayerNameplates` on the Player Targeting object to change them): Show Names, Show Guild Tag, Max
Distance, Fade Distance, Hide Behind Walls (a line-of-sight test per player a few times a second; off by default), Bars
For Party / Duels / In Combat / Guild, Always Show Bars, Combat Seconds, Head Offset, Font Size, Bar Size, Far Scale and
every colour.

## 9. Highlights: party, guild, alliance and duellists

`PlayerHighlights` marks the players bound to you with a subtle effect, one **style** per bond:

| Bond | Default |
|---|---|
| Party (group) | green glow along the character's outline, faint green silhouette through walls |
| Guild | light-blue outline glow |
| Alliance | fainter blue outline glow |
| Duel (anyone's, countdown or fight) | orange outline glow, orange silhouette through walls and a pulsing glow on the ground |

Each style has: Enabled, Color (alpha = strength), Outline Glow, Through Walls, Ground Glow. Also Max Distance (60 m),
Outline Sharpness, Through Walls Strength and Ground Glow Scale. A duel wins over the other bonds, then party, guild,
alliance.

How it works: the glow and the silhouette are two extra materials (shader *SimpleMovements/Player Silhouette*, in
`Essentials/Social/Resources/`, so it is always in builds; works in URP and the Built-in pipeline) added to the
character's Skinned Mesh / Mesh Renderers, and removed when the bond ends or the player leaves the range. Renderers are
re-checked every second, so newly equipped armour is marked too. A renderer with several sub-meshes is marked on its
last sub-mesh only (Unity draws extra materials with the last sub-mesh). Without the shader, the ground glow is used
instead.

## 10. Dungeon portal panel: free player slots

Clicking a dungeon portal (the Select Target click) selects it; walking within *Near Distance* (10 m) of one shows it
too. `PortalInfoPanel` then shows, at the top of the screen:

| Portal | Panel |
|---|---|
| Shared Run or linked portals, with a limit | **"3 of 5 slots free"** (amber when a quarter or less is left, red **"Full (5 / 5 players)"**) and "A run is going: 2 players inside. Entering joins it." |
| Separate Runs, with a limit | **"4 slots free"**, "Each group gets a dungeon of its own (up to 4 players)"; Max Players 1 shows "solo only" |
| No limit | **"No player limit"** (and how many are inside a running shared dungeon) |
| Closed after use | **"Closed"** |

The dungeon's difficulty is shown next to the title when it is not 1. The numbers come from `Portal.Slots`
(`PortalSlots`: Max Players, players inside the running shared dungeon, open, difficulty). In a network session the
client asks the server about once a second while the panel is shown, through the `NetPortals` relay on the Network World
(**Add Missing Relays To Network World**); until the answer arrives the panel shows the portal's own settings with
"(asking the server…)". The selection is dropped when the player gets farther than *Keep Distance* (40 m).

## 11. Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Clicking a player does nothing | The click hit something in front (a trigger is fine, a wall is not), or Select Layers exclude the player layer | Check Select Layers; Tab-select instead |
| Clicking attacks too | Left click is also the attack | Rebind *Select Target* (e.g. to a key or the right mouse button) |
| Party works offline but not online | No `NetSocial` on the Network World | Add Missing Relays To Network World |
| No party messages | No Hub World HUD and no party frame | Keep *Create Interface* on |
| Party members still hurt each other | Party Friendly Fire is on, the attack's Target Rules say Always, or they are dueling | Combat Settings / the attack's Target Rules |
| Inspect shows no equipment | The target's Equipment Manager has nothing equipped on the machine that runs the rules | Equip items through the inventory |
| "Trading works in a network session" | Offline there is nobody to trade with | Expected |
| No names above players | Create Interface off, or they are farther than Max Distance / in another realm | Check the Player Nameplates settings |
| Party members show no green bar or glow online | Parties do not reach the clients | Add `NetSocial` to the Network World |
| No outline glow, only a disc on the ground | The *SimpleMovements/Player Silhouette* shader is missing from the build | Keep `Essentials/Social/Resources/SimpleMovementsSilhouette.shader` |
| Portal panel stays on "(asking the server…)" | No `NetPortals` on the Network World, or the portal is not a network object | Add Missing Relays To Network World |
