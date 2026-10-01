# Player 05 — Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| The player does not move | `MovementStateMachine` is missing references, or the generated `PlayerInput` class has no `Movement` action | Press **Auto-assign References**; check the Player map's action names ([02 §2](02-Movement-and-Input.md)) |
| Moves but falls through the floor / floats | Gravity is positive or 0; the *Shell* (feet) is not at the feet | Gravity negative (−9.81), Gravity Multiplier ≥ 1; Movement Model ▸ Player Shell Object = the root at the feet |
| Jump sometimes does nothing | Old version: the jump ended on its first frame | Use the current `JumpingState` / `MovementContext` (jump buffering, ends on landing) |
| Cannot sprint / jump / dash | Not enough stamina (with *Should Consume Stamina*), dash / roll on cooldown, or stunned | Watch the stamina bar; Availability State Machine inspector shows stun / death |
| Never stops sprinting | Sprint action is a toggle | Make *Sprint* a plain Button (held) |
| Camera does not turn | Look event not wired, no `CinemachineBrain` | Player Input ▸ Events ▸ Look → `PlayerCameraView.OnLook`; add a CinemachineBrain to the main camera |
| Player turns the wrong way | *Camera Transform* not set | `PlayerCameraModel ▸ Camera Transform` = the active camera |
| No interaction prompt / cannot pick up | `Player ▸ Cam` empty, Interact event not wired, the item has no collider | Assign the camera; wire Interact → `Player.OnInteract`; give the item a collider and `ItemPickable` |
| Tab does not open the inventory | No inventory action and fallback off | Add an `Inventory` action, or turn on *Keyboard Fallback* |
| Clicking does not attack | The use action has another name, the inventory is open, or no `WeaponController` | See the startup log line `[Inventory] Use item / attack input: …`; close the inventory; add a `WeaponController` |
| Status bars missing or duplicated | UI not built, or built twice on a prefab instance | Select the player (auto build) or **Build / Repair UI Now**; extras are removed automatically |
| Animations do not play | Parameter names differ | Use the names in [04 §2](04-Camera-Animation-Abilities.md); missing ones are only warned about |
| Abilities do nothing | Key has no ability, input not found, silenced or stunned | `PlayerAbilityController` inspector shows each key's problems with Fix buttons |
| `IndexOutOfRangeException` in `Selectable.OnDisable` | Scripts recompiled during Play mode, or *Reload Domain* off | Stop Play mode before copying scripts; *Project Settings ▸ Editor ▸ Enter Play Mode Settings ▸ Reload Domain* on |
