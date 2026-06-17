# Old Town Hotel

A cozy **hotel-management simulation** built solo in **Unity 6 (C#)** — run the front desk, match guests to the right rooms, keep housekeeping flowing, and survive each day. A retro, warm-toned, fun-first management game, currently in active development.

> Built by Percy Wu as a portfolio project. The design draws on real hotel-operations domain knowledge (front desk, housekeeping, room turnover) turned into game systems.

![Front desk](Assets/Screenshots/frontdesk_overlay.png)

---

## What it is

You play the operator of a small old-town hotel. Across each in-game day you take walk-in guests from a queue, read their **preferences**, assign them to suitable rooms, dispatch **housekeeping** to clean and **inspectors** to sign off, manage **inventory**, and close the day on a **day-end summary** — balancing guest satisfaction with the realities of a busy front desk.

## Key gameplay systems

- **Front desk & guest queue** — incoming guests, guest details, and a check-in flow.
- **Guest preferences & room assignment** — match guests to rooms by bed type and other attributes.
- **Per-room day-cycle state machine** — rooms move through states (due-out, stayover, dirty, cleaning, awaiting inspection, ready, blocked) with guard logic on risky transitions.
- **Housekeeping & inspection loop** — assign housekeepers, an inspector queue, pass/reject and rework tracking.
- **Inventory, achievements & day-end scoring**, driven by an in-game time manager.

## Tech & architecture

- **Engine / language:** Unity 6 (`6000.3.x`), C#, new Input System.
- **Data-driven design:** `ScriptableObject`-based balance/config (e.g. room balance, bed-type preferences) so designers can tune without code changes.
- **UI:** a reusable **modal framework** (`ModalManager` / `ModalBase`) and view/controller separation (e.g. `FrontDeskScreenController`, `*View` components).
- **Structure:** multiple scenes (Boot, Main Menu, Front Desk, Lounge, Rooms), ~80 C# scripts, **unit tests** (EditMode), and a full **GDD / ADR documentation set** under `docs/` and `design/`.
- **Workflow:** AI-assisted development (Claude / Claude Code) with build/lint/test discipline.

## Status

🚧 **In active development** — playable systems exist (front desk, room operations, housekeeping loop, day cycle); art, content and polish are ongoing. Screenshots in [`Assets/Screenshots/`](Assets/Screenshots/).

## About the developer

**Percy Wu** — game developer & software engineer with commercial mobile-game experience at Tencent Games and Bilibili (C++ / Unreal / Lua), now building in Unity / C#.

- GitHub: [github.com/wsq94317](https://github.com/wsq94317)
- LinkedIn: [linkedin.com/in/siqi-wu-percy](https://www.linkedin.com/in/siqi-wu-percy)
