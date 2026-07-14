# Changelog

All notable changes to **Old Town Hotel** are documented here, newest first.

---

## 2026-07-14 — The Living Hotel

The day/night cycle became real, and the UI started telling the truth.

### Added
- **Overnight stays & morning checkout wave** — guests now sleep the night; when the hotel opens each morning, yesterday's guests check out one by one, settling their nightly bill and handing housekeeping its morning workload. Mornings with no overnight guests (fresh boot / loaded save) open with a few pre-dirtied rooms so the cleaning loop always has a day.
- **Day-to-day flow** — "Continue to Next Day" on the P&L summary now actually rolls the calendar, reopens the doors, and fires the checkout wave.
- **Live Front Desk data** — the active-guest card binds to the real demand system (guest type, preferences, live wait timer, mood that sours as they wait), with an empty state showing the next arrival countdown. The waiting strip renders the actual incoming-guest queue with per-type portraits.
- **Room state badges** — every room tile carries a color-coded state strip (READY / DIRTY / OCC / CLEAN / INSP) with the room photo kept readable underneath. Colorblind-safe: state is always spelled out in text.

### Fixed
- Choose-room list rebuilt as a proper scroll list (rows used to overflow the modal).
- Disabled action buttons now *look* disabled, with a reason ("READY · nothing to clean").
- Day-end summary: overlapping lines respaced; net profit renders green, loss red.
- Guests-served counter no longer accumulates across days; overnight guests can no longer be charged twice.
- Economy core self-heals if initialization order ever breaks (post-crash resilience).

## 2026-07-11 — Multi-Staff Economy (Phase 6)

Hiring became real: a second housekeeper is a second pair of hands.

### Added
- **StaffCrew** — the payroll roster drives live worker instances in the scene. Hire someone and they walk in; fire someone and they finish their current room before leaving (ADR 0008, superseding the single-worker cap of ADR 0004).
- **"Do It Yourself"** — the boss can cover one vacant role at a time: free, but half speed. Understaffed play means *you* are the bottleneck.
- **Tiered nightly revenue** — room income = renovation-tier rate × stay match quality (great matches tip, complaints pay less).
- **★ Reputation** — a rolling 1–5★ score built from recent stays, the foundation for reputation-driven guest volume.
- Interest-rate rebalance (0.15% → 0.05%/day) so the early game is survivable.

### Fixed
- Check-in was never wired in the new UI shell — choosing a room now actually checks the guest in.
- The operating day now auto-starts (it used to sit in preparation forever).

## 2026-06-19 — Economy Foundations (Phases 1–5)

Built test-first: the economy landed with a full EditMode suite.

### Added
- **Payroll & daily P&L** — staff wages settle at day end into a ledger modal.
- **Finance** — the hotel starts $183,000 in inherited debt; daily interest, hotel valuation, and a value-backed credit line for borrowing and repayment.
- **Staff** — attributes (speed / quality / stamina), personality traits, morale, raises; a deterministic hiring-candidate generator driven by archetype configs.
- **Renovation** — per-room tiers (Old → Basic → Better) with cost, build time, and batch discounts; renovated rooms raise both nightly rates and hotel valuation.
- **Manager Office** — an in-game hub with Finance / Staff / Renovation panels wired to the live systems.
- **Save system** — single-slot JSON autosave of economy progression at day end, plus editor tooling.

## 2026-06-15 → 2026-06-17 — Vintage UI Shell

- Recruiter-ready portrait UI: hero banners, dark-wood top bar, cream/gold vintage theme (ADR 0007), restyled cards and modals across Front Desk / Rooms / Lounge.
- First public screenshots and README.

## Earlier — Core Prototype

- Front desk & guest queue with preferences and patience states.
- Per-room day-cycle state machine (dirty → cleaning → inspection → ready) with a single housekeeper/inspector bottleneck as the core tension.
- The Lounge café loop: drinks, cup inventory, dishwasher.
- Three-phase day structure, scoring, achievements scaffolding.
