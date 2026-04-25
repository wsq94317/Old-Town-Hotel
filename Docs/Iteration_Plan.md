# Iteration Plan

## Iteration 1 - Room Entity Prefab
### Goal
Create one stable and reusable fake-3D 2D room entity prefab.

### In Scope
- Room2DEntity
- Room2DController
- Room2DLabelView
- Room2DFakeDepthVisual
- Room2DOverview integration
- Room state color and label update
- Main action button flow
- Convert one working room into a prefab

### Out of Scope
- Housekeeper logic
- Inspector logic
- Guest logic
- Front desk
- Lounge
- Events
- Timers
- Daily flow

### Acceptance Criteria
- One room works correctly as a prefab
- The prefab can be duplicated without breaking references
- Room number, state label, next action label, and action count all display correctly
- State visual changes correctly
- Overview counts update correctly


## Iteration 2 - Multi-Room Layout
### Goal
Scale one working room prefab into a small room-view layout.

### In Scope
- Duplicate the room prefab into 8 to 12 rooms
- Assign unique room numbers
- Arrange rooms into 2 or 3 floors
- Verify all rooms work independently
- Verify overview counts remain correct
- Improve spacing, alignment, and readability

### Out of Scope
- Housekeeper logic
- Inspection timing
- Guests
- Front desk
- Lounge
- Events

### Acceptance Criteria
- At least 8 rooms exist in the prototype scene
- Each room can change state independently
- Labels are readable
- The room layout clearly looks like a hotel room overview
- No duplicated room breaks the room data or UI


## Iteration 3 - Room Workflow Logic
### Goal
Replace manual arbitrary state changes with a simple business workflow.

### In Scope
- Dirty -> Cleaning
- Cleaning -> AwaitingInspection
- AwaitingInspection -> Ready
- Restrict invalid transitions
- Add simple timer or simulated progress for Cleaning
- Keep the system beginner-friendly

### Out of Scope
- Real staff movement
- Front desk
- Guests
- Lounge
- Events
- Employee skills

### Acceptance Criteria
- A Dirty room cannot jump directly to Ready
- Cleaning must happen before inspection
- Inspection must happen before Ready
- The room workflow feels like an actual hotel room turnover chain