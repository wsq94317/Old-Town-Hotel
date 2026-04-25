# Task Log

## Current Task
Build first playable fake-3D 2D room entity prototype.

## Done
- Unity URP project created
- Base folder structure created
- Scene Hotel_Rooms created
- Basic 3-floor, 4-room block layout started
- Scene Hotel_Rooms_2D_Proto created
- Room2DState created
- Room2DController created
- Room2DEntity created
- Room color, state marker, labels, button flow, and action count working
- Room2DFakeDepthVisual created for simple sprite layer ordering
- Room2DOverview created for scene-level room state counts
- Room2DController now auto-finds Room2DOverview for easier room duplication

## Next
- Attach Room2DEntity to Room_A_2D in Unity
- Use Room2DEntity as the room data source
- Attach Room2DFakeDepthVisual to Room_A_2D in Unity
- Create Shadow, Floor, BackWall, LeftWall, RightWall sprite children
- Duplicate one working room after the first room entity feels correct
- Rename duplicated rooms and update each Room2DEntity room number
