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
- Room2DPrototypeDemandLoop exposes a lightweight preparation view with room summary, upcoming demand, warnings, reservation, and priority marks
- Room2DEntity stores simple prototype preparation priority flags for cleaning and inspection
- Room2DController shows preparation priority markers on room debug labels
- Housekeeper2D can assign the best Dirty room using CLEAN PRIO, cleaning priority level, and oldest Dirty time
- Inspector2D can assign the best AwaitingInspection room using INSP PRIO and oldest inspection wait time
- Room2DPrototypeDebugHud shows the current best housekeeping and inspection targets
- Room2DWorkerSelectionPanel created for manual worker selection and selected-worker-to-selected-room assignment
- Room2DPrototypeDebugHud can show selected worker, worker states, and manual worker assignment results
- Room2DPrototypeDebugHud now uses smaller action buttons so the debug panel blocks less of the room grid
- Room2DPrototypeDebugHud now separates demand information into Upcoming, Active, and Latest Resolved prototype cards
- Room2DPrototypeDebugHud now shows a selected-room detail card with reservation, priority, checkout, block, quality, and match hints
- Room2DEntity now has a minimal Standard/Better prototype room type, and demand matching can prefer Better rooms
- Room assignment depth v1 now includes floor preference, quiet/view facing preference, and Street/Back facing in match hints
- Room2DPrototypeRoomConfigApplier created for scene-level batch room type, facing, and attribute configuration
- FrontDesk2D created for lightweight active-demand waiting pressure and delayed check-in penalties
- Lounge2D created for prototype clean cup, dirty cup, stock, washing, and lounge service pressure
- Room2DPrototypeDebugHud now surfaces front desk and lounge pressure in the portrait prototype HUD
- Room assignment now allows preference/type-risk rooms, then turns bad assignments into complaint reassignment pressure
- Room2DDemoDayController created to organize the prototype into Preparation, Operating, and Ended demo phases
- Front desk and lounge pressure now pause outside Operating phase and surface clearer demo-day pressure summaries
- Demo HUD now switches between preparation, operating, and end-of-day recording summaries
- Room2DShowcaseViewController created as the first Front Desk / Rooms / Lounge showcase navigation shell
- Room2DShowcaseViewController Phase 2 now connects front desk, room, worker, demand, and lounge data/actions into the three showcase views
- Room2DShowcaseViewController Phase 3 now separates showcase cards, shortens action labels, and adds Start/End/Reset controls for recording clarity
- Rooms View Interaction Phase 1 now supports clicking rooms directly and reading a mobile-style selected-room detail card
- Rooms View room selection now also supports screen-rect picking, so duplicated rooms can be selected even if their Collider setup is unreliable

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

## Current Milestone
Prove that multi-room turnover creates meaningful prioritization pressure.

## Immediate Next
- Finish one stable room prefab
- Duplicate it into 8 to 12 working rooms
- Add a simple cleaning priority rule for Dirty rooms
- Surface highest-priority Dirty room in room labels or overview
- Use prototype occupancy/check-in flow to create Ready-room demand

## Explicitly Not Next
- Full guest identity system
- Full front desk system
- Lounge expansion
- Deep room attribute calculations
- Extra tool-building

## Current Milestone
Prove that proactive preparation is better than reactive response.

## Immediate Next
- Add a lightweight prototype preparation panel
- Show room-state summary
- Show upcoming demand preview
- Allow a small number of preparation actions
- Test whether preparation improves outcomes
- Test that Dirty / Inspection priority markers are visible on selected rooms and clear after state changes
- Test that Best HSK / Best Insp chooses prepared priority rooms before normal backlog rooms
- Test manual worker selection by selecting HSK / Inspector and assigning the selected worker to the selected room
- Reapply the HUD layout and verify the smaller action buttons no longer cover the room grid

## Explicitly Not Next
- Full morning briefing system
- Full staffing system
- Inventory planning
- Lounge expansion
- Events
- Final UI polish

- Recorded room-assignment, guest-preference, early check-in, and late-checkout design notes in a separate design doc
