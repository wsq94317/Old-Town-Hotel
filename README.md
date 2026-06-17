# Old Town Hotel 🏨

A cozy, retro **hotel-management simulation** built solo in **Unity 6 (C#)**. Run the front desk, read your guests, match them to the right rooms, keep housekeeping and inspections flowing, manage your inventory, and close out each day — a warm, fun-first management game.

<sub>Solo portfolio project by **Percy Wu**. The systems are modelled on real hotel-operations domain knowledge (front desk, housekeeping, room turnover) turned into game mechanics.</sub>

> **Status:** 🚧 In active development. Core loops are playable (front desk, room day-cycle, housekeeping/inspection, day-end). Art, content and polish are ongoing.
> **Engine:** Unity `6000.3.x` · **Language:** C# · **Input:** Unity Input System

<!-- Screenshots are being refreshed — newer captures coming. -->
![Front desk](Assets/Screenshots/frontdesk_overlay.png)

---

## 🎮 The game

You run a small old-town hotel. Each in-game day you:

1. **Take guests from the queue** at the front desk and read their details and **preferences**.
2. **Assign them to suitable rooms** (bed type and other attributes matter to satisfaction).
3. **Dispatch housekeeping** to clean turned-over rooms and send an **inspector** to pass or reject the work.
4. **Track inventory** and keep the operation supplied.
5. **Close the day** on a day-end summary and chase **achievements** — balancing guest happiness against the chaos of a busy front desk.

## ✨ Key systems

| System | What it does |
|---|---|
| **Front desk & guest queue** | Incoming-guest flow, guest detail cards, check-in handling |
| **Guest preferences & room assignment** | Match guests to rooms by bed type / attributes; satisfaction-driven |
| **Per-room day-cycle state machine** | Rooms move through *due-out → stayover → dirty → cleaning → awaiting inspection → ready / blocked*, with guards on risky transitions |
| **Housekeeping & inspection loop** | Assign housekeepers, inspector queue, pass / reject / rework tracking |
| **Inventory** | Stock tracking tied to room operations |
| **Time, scoring & achievements** | In-game time manager, day-end summary, achievement system |

## 🏗️ Architecture & engineering

- **Data-driven design** — gameplay balance and content live in `ScriptableObject` configs (room balance, bed-type preferences, etc.) so values can be tuned without touching code.
- **UI framework** — a reusable modal system (`ModalManager` / `ModalBase`) with clean view ↔ controller separation (`FrontDeskScreenController`, `*View`, `*Modal` components).
- **State machines** — explicit per-room day-phase state machine rather than scattered flags.
- **Scenes** — Boot, Main Menu, Front Desk, Lounge, Rooms (plus a 2D rooms prototype).
- **~80 C# scripts**, EditMode **unit tests**, and a full **GDD / ADR documentation set** (`design/`, `docs/`).
- **AI-assisted workflow** — built with Claude / Claude Code under build / lint / test discipline.

## 📂 Project layout

```
Assets/Game/        Scripts, Prefabs, Scenes, ScriptableObjects, UI, Art, Audio
Assets/Screenshots/ Captured screens
design/             Game design docs (GDD) and architecture decision records (ADR)
docs/               AI context, coding conventions, showcase notes
production/         Iteration logs and session state
Tests/              EditMode unit tests
```

## ▶️ Running it

1. Open the project in **Unity 6 (6000.3.x or newer)**.
2. Open `Assets/Game/Scenes/Boot.unity` (or `MainMenu.unity`) and press **Play**.

## 👤 About the developer

**Percy Wu** — game developer & software engineer with commercial mobile-game experience at **Tencent Games** and **Bilibili** (C++ / Unreal / Lua), now building in Unity / C#, plus full-stack and AI-assisted tooling.

- GitHub: [github.com/wsq94317](https://github.com/wsq94317)
- LinkedIn: [linkedin.com/in/siqi-wu-percy](https://www.linkedin.com/in/siqi-wu-percy)

<sub>This is a personal work-in-progress project shared as a portfolio piece. Code and a build walkthrough are available on request.</sub>
