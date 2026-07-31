# Editable Floor Maps

The hotel is authored as `Floor1.prefab` through `Floor7.prefab` in `Floors/`. Each asset
contains the floor shell, rooms, doors, facilities, anchors, art pass, and
nested furniture prefabs used by that floor.

## Recommended workflow

1. Open a floor through `Old Town Hotel > Maps > Open Floor Prefab` or double
   click its asset in the Project window.
2. Edit the prefab in Prefab Mode and save it. The main hotel scene updates
   automatically because `World/Floor1` through `World/Floor7` are connected
   prefab instances.
3. Keep each prefab root at the origin. Floor height is a scene-instance
   override owned by `Hotel_Manager_25D`.
4. Use `Old Town Hotel > Maps > Validate Floor Prefabs` after structural edits.

If edits were made directly on floor instances in the main scene, use
`Old Town Hotel > Maps > Create or Update Floor Prefabs` to write all seven
scene floors back to their assets. This intentionally replaces the prefab
defaults with the current scene hierarchy.

## Structural contracts

- Do not rename `Floor1` through `Floor7`, `Ground`, `ArtPass_LowPoly`, room
  roots (`Room_201`), door roots (`Door_201`), elevator objects, or facility
  anchors unless the systems that discover them are updated at the same time.
- Room data is not stored inside a floor prefab. Doors retain a room number and
  bind to `RoomData` at runtime, which keeps floor assets independently editable.
- Floors 2 and 3 contain all 24 visible guest rooms. The simulation entities
  for the expanded rooms are still created at runtime.
- Furniture under the art pass remains nested furniture prefabs. Edit or swap
  those assets without unpacking the whole floor.

The generated art rebuild command temporarily unpacks floor instances, rebuilds
the art pass, and reconnects the seven floor prefabs. It is intentionally a
destructive regeneration command for generated art; normal layout work should
be done in Prefab Mode instead.
