# Docs Moved

This directory used to contain `AI_CONTEXT.md`, `Design.md`, `Iteration_Plan.md`, `TASK_LOG.md`, `SHOWCASE_GOAL.md`, `碎片设计.md`, and `Design/`. As of **2026-05-13**, all content has been reorganized into the project-root template structure.

## Where things went

| Original file (`Old-Town-Hotel/Docs/...`) | New location |
|---|---|
| `AI_CONTEXT.md` | `docs/ai-context.md` |
| `Design.md` | Split into `design/gdd/*` + `docs/architecture/adr/*` (original archived at `design/archive/design-md-original.md`) |
| `Iteration_Plan.md` | `production/iterations/early-iteration-plan-archived.md` (archived) |
| `TASK_LOG.md` | Split into `production/iterations/{showcase-session,manual-room-ops-pass,guest-waiting-pressure-v1}.md` |
| `SHOWCASE_GOAL.md` | `docs/showcase-goal.md` |
| `碎片设计.md` | Archived at `design/archive/碎片设计.md`; replaced by `design/gdd/room-assignment.md` |
| `Design/Room_Assignment_And_Guest_Preferences.md` | `design/gdd/room-assignment.md` (original archived) |

## Where to start now

- **Building a new feature:** read `docs/ai-context.md` first, then the relevant `design/gdd/*` file
- **Architectural question:** check `docs/architecture/adr/`
- **Coding rules:** `docs/coding-conventions.md`
- **What's the live status:** `production/session-state/active.md`
- **Recent iteration history:** `production/iterations/`
- **Looking for an old idea / draft:** `design/archive/`

All paths above are relative to the project root (`/Users/wsq/Projects/OldTownHotel/`).
