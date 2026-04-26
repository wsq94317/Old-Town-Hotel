# Old Town Hotel - AI Context

## Project Summary
A mobile portrait hotel operations management game.
Style: 2.5D, cute chibi, easy-to-develop cartoon visuals.
Engine: Unity (URP).

## Chapter 1
Old downtown travel hotel.

## Core Gameplay
- Front desk queue pressure
- Room cleaning and inspection flow
- Guest-room occupancy and one-to-one guest assignment
- Guest check-in timing
- Lounge cup/inventory management
- Random incidents like cockroach complaints

## Current Prototype Goal
Build the fake-3D 2D Room View prototype first inside the current Unity project:
- One clear room entity first
- Simple room state visualization
- UI labels for room name, current state, next action, and action count
- Explicit guest checkout flag for the room cleaning flow
- Lightweight state timing so dirty rooms can become a cleaning priority later
- Minimal prototype loop that creates room-cleaning demand through simulated check-in and checkout
- Blocked rooms with reason and duration for maintenance or renovation
- Per-room attributes for future guest preference and review calculations
- Beginner-friendly scene setup in Unity
- States: Dirty, Cleaning, AwaitingInspection, Ready, Occupied, Blocked

## Current Scene
Hotel_Rooms_2D_Proto

## Room Entity Development Flow
1. Room data entity: identity, floor, room number, state, checkout flag, action count, block data, room attributes.
2. Room visual controller: state color and state markers.
3. Room fake-depth visual: shadow, floor, back wall, side walls, furniture sorting.
4. Room label view: per-room TMP labels under each room entity.
5. Room overview: scene-level counts for Dirty, Cleaning, AwaitingInspection, Ready, Occupied, Blocked.
6. Room selection: choose one current room for the main action button.
7. Room interaction: one main action button that advances the selected room workflow.
8. Room workflow actions: SimulateCheckIn, SimulateCheckout, StartCleaning, FinishCleaning, ApproveInspection.
9. Room timing: track how long each room has been in its current state.
10. Prototype loop: simulate check-ins into Ready rooms, then checkouts from Occupied rooms to create cleaning demand.
11. Block flow: start maintenance or renovation blocks, tick down block duration, then become Dirty.
12. Room attributes: store condition data such as bed, floor, wardrobe, bathroom, wallpaper, air conditioner, and window.
13. Room prefab: convert one working room entity into a reusable Unity Prefab.
14. Room layout scale-up: duplicate one working prefab into multiple rooms and floors.

## User Stories To Preserve
- As a guest with very high cleanliness expectations, I want my assigned room to match my preference so that a clean but old-looking room can still affect my final review.
- As the hotel manager, I want each active guest to be assigned to exactly one Occupied room so that checkout, cleaning demand, and guest feedback can be traced back to a specific room.
- As the hotel manager, I want room quality attributes such as cleanliness and oldness/wear to matter separately so that a room can be technically clean but still feel unsatisfying to certain guests.
- As the hotel manager, I want to block a room for maintenance or renovation for a fixed number of game hours so that unavailable rooms return as Dirty when work finishes.
- As the hotel manager, I want each room to have generated internal condition attributes so that later UI, guest matching, complaints, and reviews can read those values from the room entity.

## Coding Rules
- Keep systems small and simple
- Prefer readable C# over clever architecture
- Add concise Chinese comments for new gameplay code so the beginner Unity workflow is easier to read
- Do not over-engineer
- Only change files requested
