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
- Room2DLabelView created for per-room TMP labels under each room entity
- Room2DSelectionManager created for controlling the currently selected room
- Room2DOverview now has prototype tools for refreshing and resetting all rooms
- Room2DEntity now has SetIdentity for room number/name setup
- Room2DOverview can assign prototype room numbers for duplicated rooms
- Room2DSelectionManager can select next/previous room by room number
- Room2DEntity now tracks whether the guest has checked out
- Room2DOverview assigns prototype room numbers by scene position
- Room2DEntity now exposes explicit room workflow actions
- Room workflow actions now guard against invalid state transitions
- Room2DEntity now tracks how long the room has stayed in its current state
- Room2DOverview now shows the oldest Dirty room wait time
- Room2DPrototypeLoop created to simulate guest check-in and checkout flow
- Room2DState now includes Occupied for guests staying in rooms
- Room workflow now uses Ready -> Occupied -> Dirty before cleaning begins
- Guest preference and room quality review user stories recorded in AI_CONTEXT
- Room2DState now includes Blocked for unavailable rooms
- Room2DEntity now stores block reason and block remaining hours
- Room2DEntity now stores generated room attributes for future UI and reviews
- Room2DPrototypeClock created to advance block duration and expire blocked rooms into Dirty
- Gameplay scripts now include concise Chinese comments for easier code reading
- Dirty rooms now expose a simple cleaning priority based on checkout waiting time
- Room2DOverview now identifies the highest-priority Dirty room
- Housekeeper2D created as a single-housekeeper cleaning resource prototype
- Inspector2D created as a single-inspector room approval resource prototype
- Room2DPrototypeDemandLoop created to consume Ready rooms and generate Dirty rooms again
- Room2DPrototypeDebugHud created to organize prototype testing information into readable panels

## Next
- Attach Room2DEntity to Room_A_2D in Unity
- Use Room2DEntity as the room data source
- Attach Room2DFakeDepthVisual to Room_A_2D in Unity
- Create Shadow, Floor, BackWall, LeftWall, RightWall sprite children
- Duplicate one working room after the first room entity feels correct
- Use Room2DOverview Assign Prototype Room Numbers after positioning duplicated rooms
- Add temporary UI buttons for Select Next Room and Select Previous Room
- Rebind Button_NextState to Room2DSelectionManager.PerformNextActionOnSelectedRoom
- Use Room2DOverview context menu tools to test multi-room states quickly
- Keep cleaning flow tied to Occupied checkout before adding full guest/front-desk systems
- Prefer explicit room actions before expanding into guest/front-desk systems
- Use CanStartCleaning/CanFinishCleaning/CanApproveInspection guards when adding workers or UI states
- Use stateElapsedSeconds later for cleaning priority and waiting pressure
- Attach Room2DPrototypeLoop to the scene and test Simulate Next Check In / Simulate Next Checkout
- Use the prototype loop to create guest occupancy and cleaning demand before building front desk UI
- Later add guest identity and room quality attributes: cleanliness expectation, room cleanliness, and room wear/oldness
- Attach Room2DPrototypeClock to the scene and test block expiration
- Use Room2DEntity Generate Prototype Room Attributes to create sample per-room condition data
- Later move room generation into new-save setup instead of manual context menu tools
- Convert the finished Room_A_2D object into a reusable Unity Prefab
- Duplicate the Room_A_2D prefab into 8 to 12 rooms and assign room numbers by scene position
- Test whether multiple Dirty rooms create visible prioritization pressure
- Attach Housekeeper2D to the scene and test one-room-at-a-time cleaning decisions
- Attach Inspector2D to the scene and test one-room-at-a-time inspection decisions
- Attach Room2DPrototypeDemandLoop to the scene and test unmet demand when no Ready rooms are available
- Reorganize the Hotel_Rooms_2D_Proto Canvas into selected room, overview, worker, and action debug panels
