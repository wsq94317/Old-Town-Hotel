# Old Town Hotel - AI Context

## Project Summary
A mobile portrait hotel operations management game.
Style: 2.5D, cute chibi, easy-to-develop cartoon visuals.
Engine: Unity (URP).

## Current Work Mode
The current priority is no longer proving the room prototype in isolation.
The current priority is building a recordable 3-view showcase inside `Hotel_Rooms_2D_Proto`.

Default showcase format:
1. Preparation
2. Start Operating
3. Front Desk handles guest demand
4. Rooms handles turnover and worker actions
5. Lounge handles cups, stock, and washing
6. End-of-day summary

## Showcase Goal
Push the current project toward a minimally presentable build that is easier to record as a short gameplay demo.

Priority order:
1. Front Desk interaction clarity
2. Rooms interaction clarity
3. Lounge interaction clarity
4. Reduce debug feel
5. Keep the three views visually and behaviorally consistent

## Current Scene
`Hotel_Rooms_2D_Proto`

## Main Showcase Views
### Front Desk
- waiting guest strip or list
- current guest/request card
- guest detail popup
- assign-room flow
- queue / wait pressure visibility

### Rooms
- clickable room tiles
- readable room state on tile
- room detail / info popup
- assign HSK / assign INSP flow
- reduced reliance on debug-heavy controls

### Lounge
- stock card
- washing card
- warning / result card
- grouped actions

## Preserved Prototype Systems
These systems are already in the project and should be reused instead of replaced:
- `Room2DShowcaseViewController`
- `Room2DPrototypeDemandLoop`
- `Room2DDemoDayController`
- `FrontDesk2D`
- `Lounge2D`
- `Room2DWorkerSelectionPanel`
- `Room2DEntity`, `Room2DController`, `Room2DOverview`

## Preserve These Gameplay Ideas
- Front desk queue pressure
- Room cleaning and inspection flow
- Guest-to-room assignment
- Check-in timing pressure
- Lounge cup and stock pressure
- Complaint-driven reassignment pressure

## Out Of Scope For This Run
- deep backend systems
- full guest identity
- full review system
- full economy system
- large architecture rewrites
- final polished art

## Coding Rules
- Keep systems small and simple
- Prefer readable C# over clever architecture
- Add concise Chinese comments for new gameplay/UI code
- Reuse existing systems whenever possible
- Avoid over-engineering
- Update docs when scope or current target changes
