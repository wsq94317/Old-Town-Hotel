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
- Guest check-in timing
- Lounge cup/inventory management
- Random incidents like cockroach complaints

## Current Prototype Goal
Build the fake-3D 2D Room View prototype first inside the current Unity project:
- One clear room entity first
- Simple room state visualization
- UI labels for room name, current state, next action, and action count
- Beginner-friendly scene setup in Unity
- States: Dirty, Cleaning, AwaitingInspection, Ready

## Current Scene
Hotel_Rooms_2D_Proto

## Room Entity Development Flow
1. Room data entity: identity, floor, room number, state, action count.
2. Room visual controller: state color, state markers, UI labels.
3. Room fake-depth visual: shadow, floor, back wall, side walls, furniture sorting.
4. Room UI feedback: room name, state, next action, action count.
5. Room overview: scene-level counts for Dirty, Cleaning, AwaitingInspection, Ready.
6. Room interaction: one main action button that advances the room workflow.
7. Room layout scale-up: duplicate one working room into multiple rooms and floors.

## Coding Rules
- Keep systems small and simple
- Prefer readable C# over clever architecture
- Do not over-engineer
- Only change files requested
