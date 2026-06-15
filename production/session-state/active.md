# Active Session State

**Last updated:** 2026-05-20
**Current focus:** UI redesign — rebuilding all prefab dimensions for 1080×1920 canvas reference

---

## 🎯 Where we are RIGHT NOW

The user is mid-way through **rebuilding all UI prefab dimensions for 1080×1920 Canvas Scaler reference**. Earlier values were calibrated for 360×640 (legacy thinking) — too small on a 1080 canvas, leading to elements occupying ~30% of screen with massive empty space.

**Master spec doc** (the source of truth):
[`design/ui/prefab-dimensions-1080.md`](../../../design/ui/prefab-dimensions-1080.md)

This doc contains EVERY prefab's RectTransform values, font sizes, sprite assignments, recalibrated for 1080×1920. The user follows it section by section.

---

## ✅ What's done (completed in earlier sessions)

| Phase | Status |
|---|---|
| P0 — UI Spec + Art ADR | ✅ |
| P1 — UITheme + 22 GPT-generated PNGs sliced & named | ✅ |
| P2 — 13 Common prefabs + 7 Modal prefabs + 2 row prefabs (BUILT, but at OLD 360-ref dimensions) | ✅ assembly done, dimensions need rebuild |
| P3 — All 3 screen controllers + screen prefabs built | ✅ |
| P3.9a-d — Gameplay-side accessors + Lounge expansion + ui-spec corrections | ✅ |
| P4 — Modal framework + all 5+2 modal subclasses + HotelUIFlow | ✅ |
| P5 — UIAnimUtil + ToastView framework | ✅ |

All code is in place. **Prefab assembly is done but dimensions need rebuild per the 1080 spec doc.**

---

## 🚧 In progress NOW

**P3.assembly — REBUILD all prefab dimensions for 1080×1920 canvas**

User is going through the dimensions doc section by section. Progress:

| Section | Status |
|---|---|
| Canvas Scaler config verified (1080×1920, Match 0.5) | ✅ |
| §1 Common_TopBar dimensions | ⏳ user reported HLG locking Height (Control Child Size Height was ON); guided to turn all 4 HLG checkboxes OFF; also need icon Width × Height to BOTH be 64 |
| §2 Common_BottomNav | ⏳ pending |
| §3 Common_HeroBanner | ⏳ pending |
| §4-13 other Common prefabs | ⏳ pending |
| §14 7 Modal prefabs | ⏳ pending |
| §15 3 screen prefab layouts | ⏳ pending |
| §16 Shared infra (Toast / ModalRoot / etc.) | ⏳ pending |

---

## 🆕 Most recent work (LAST things we did)

### 1. Worker cards repositioning (just instructed, user implementing)

Asked user to reposition HskCard + InspCard **side-by-side at bottom of UI_RoomsScreen** (instead of stacked overlapping the tile grid). Includes:
- Shrink Common_WorkerStatusCard prefab to **480×220** (was 1000×280)
- New compact internal layout (Portrait 160×160, smaller fonts 22-28)
- Position HskCard bottom-left (Anchor 0,0 / Pos 40, 240, W488 H220)
- Position InspCard bottom-right (Anchor 1,0 / Pos -40, 240, W488 H220)

### 2. Room interior sprites added to RoomTile (script done, user wiring)

User generated **4 room interior images** (Single/Twin/Family/King) from GPT (2×2 sheet, transparent gutters between, sliced in Unity).

Updated `Assets/Game/Scripts/UI/RoomTileView.cs`:
- Added 4 SerializeField sprites: `interiorSingle`, `interiorTwin`, `interiorFamily`, `interiorKing`
- Replaced `previewBedLetter` string with `previewRoomCategory` enum (auto-derives letter from category)
- `Refresh()` now sets `backgroundImage.sprite` based on `room.roomCategory`
- Added `PickInteriorSprite()` + `LetterForCategory()` helpers

**Wiring step user is doing now**: open Common_RoomTile prefab → drag 4 interior sprites into the 4 new SerializeField slots. Editor preview should show different room interiors per `previewRoomCategory` change.

**Visual caveat known**: backgroundImage.color (state color) tints the interior sprite via multiply. Looks slightly washed. If user dislikes, we can split into 2 layers (StateStripe + clean interior). User can decide after seeing the result.

### 3. Discussions deferred for later sessions

- **ScrollRect for room grid** — designed but not implemented yet. User asked for scrollable tile grid for future hotels with more than 12 rooms. Full 7-step ScrollRect instructions are in chat history. Estimated 30 minutes when user comes back.
- **Multi-worker scalability** — user asked about extending beyond single HSK + single INSP. We discussed it's a significant gameplay design change (ADR 0004 amendment). Recommended keeping single bottleneck for prototype, revisit after playtesting. **No code change yet.**

---

## 🎨 GPT asset status

Generated and in `Assets/Game/UI/Sprites/`:
- 3 banners (FrontDesk / Rooms / Lounge — all replaced with 1792×1024 versions, look correct)
- All Common_* icons / cards / buttons / portraits / nav (in earlier batches)
- **NEW 2026-05-20**: 4 room interior sprites (Single/Twin/Family/King — User generated these as a 2×2 sheet, sliced into 4 sub-sprites; file likely in `Banners/` or `Rooms/` folder)

---

## 🔑 Key context for picking back up

1. **Read this file first** when starting a new session — gives full state in 30 seconds
2. **The dimensions spec doc** at `design/ui/prefab-dimensions-1080.md` is canonical — follow it section by section
3. **All in-game text must be English** — memory: `in-game-text-english-only` (no CJK font in editor)
4. **User prefers default-with-rationale decisions over open-ended questions** — memory: `feedback-decision-style`
5. **GPT image generation is the asset pipeline** — memory: `asset-pipeline-gpt-image-gen`
6. **Single-worker bottleneck is INTENTIONAL** per ADR 0004 — don't refactor unless user explicitly asks

---

## ⏭ Next steps when user returns

1. Finish §1 Common_TopBar (icon Width × Height to 64) — should be 5 min
2. Verify result by Play ▶, look for empty-space reduction
3. Proceed to §2 BottomNav, §3 HeroBanner, §4 RoomTile per spec doc
4. Wire 4 room interior sprites into Common_RoomTile (script ready, just needs Inspector drags)
5. Test result of room interior + state color tint — decide if 2-layer redesign needed
6. Continue down spec doc, ~2.5 hours total estimated

**If user wants to skip prefab tuning** and just see runtime gameplay, they can play with current dimensions (small but functional). Visual polish can come last.

---

## 🐛 Known issues to watch for

- TopBar icons may need Height manually set (HLG doesn't auto-size them)
- Common_RoomTile background tint heavy when state color saturated (Ready green, Dirty rust) — interior sprite hard to see. Solution: 2-layer redesign (deferred).
- HSK/INSP "Details" button currently triggers a Toast (HotelUIFlow.HandleHskDetailsRequested). Toast may appear off-screen if Toast prefab RectTransform isn't bottom-anchored at Pos Y 240+. Verify per spec §11.
- HotelUIFlow's `OnQueueCardTapped` and `RefreshActiveGuest` are stubbed — gameplay-side Guest data model not yet defined. ActiveGuestCard shows OnValidate preview values at runtime instead of real data. This is the MISSING item flagged in ui-spec.md §6.
