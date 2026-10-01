# Mobs 05 — Troubleshooting

Most problems are listed in the mob inspector's **Checks** with a **Fix** button. Start there.

| Symptom | Likely cause | Fix |
|---|---|---|
| The mob does not move | No NavMesh under it; no `MobMovementStateMachine`; NavMeshAgent of a type the NavMesh was not baked for | Bake the NavMesh; **Auto-Configure All Components**; match the agent type |
| "Failed to create agent" warning | Spawned off the NavMesh | Spawn on the NavMesh (spawners do); move the mob onto it |
| Ignores the player | `Player` not in *Preys*; Aggression Passive / Defensive; the player is outside its sight or field of view | Add `Player` to Preys; set Aggression to Aggressive or Territorial; raise Sight Range / FOV |
| Attacks other mobs of its kind | They have different Types or Team Overrides, or hostile factions | Same Type, or the same Team Override / an allied faction |
| Never attacks | No `MobAbilityController`; no abilities and *Auto Basic Attack* off; abilities' Mob AI Priority 0 | Add the controller; turn on the basic attack; set Priority ≥ 1 |
| Abilities hit nothing | The target's collider layer is not in *Combat Settings ▸ Character Layers*; Hit Filter does not include the target's relation | Add the layer; check the filter (Enemies for hostile targets) |
| Only a few mobs attack, the rest circle | Attack slots (*Max Simultaneous Melee Attackers*) | Intended; raise it in Combat Settings or turn off *Use Attack Tokens* on the profile |
| Mob runs home in the middle of a fight | Leash Distance / Max Chase Time reached | Raise them, or 0 = no limit |
| Frozen or sliding animation | Animator parameter names differ | Match the names in the profile's Animation section; or *Cross Fade Legacy States* for old controllers |
| Rigidbody jitter | Rigidbody not kinematic | **Make Kinematic** (inspector) |
| All copies of a prefab walk to the same points | Absolute patrol points on a prefab | **Make Relative** (*Patrol Points Relative To Home*) |
| Mobs far away do nothing | They sleep beyond *AI Sleep Distance* to save CPU | Intended; *Always Full Rate* for bosses / important NPCs |
| A player's party member is hit by a mob's ally | Factions / teams not set as intended | See [Inventory 09](../Inventory/09-Teams-Factions-and-Targeting.md) |
