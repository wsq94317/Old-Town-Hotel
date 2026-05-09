# Task Log

## Current Task
Push the project toward a recordable, minimally presentable 3-view showcase build in `Hotel_Rooms_2D_Proto`.

## Current Strategy
- keep one-scene showcase flow
- improve player-facing clarity before adding depth
- reduce raw debug presentation
- reuse existing systems
- update docs during each meaningful iteration

## Already In Place
- `Room2DShowcaseViewController` builds the 3-view showcase shell
- Front Desk has guest cards, detail popup, and ready-room popup
- Rooms supports room clicking, room popup, and worker assignment popup
- Lounge supports cups, stock, washing, warnings, and grouped actions
- `Room2DDemoDayController` organizes Preparation / Operating / Ended
- `Room2DPrototypeDemandLoop` already provides active demand, reservation, complaint reassignment, and outcome pressure

## Showcase Session

### Iteration 1
Goal:
Make Front Desk and Rooms easier to understand in a recording without changing core gameplay architecture.

Changed Files:
- `Assets/Game/Scripts/Gameplay/Room2DEntity.cs`
- `Assets/Game/Scripts/Gameplay/Room2DLabelView.cs`
- `Assets/Game/Scripts/Gameplay/FrontDesk2D.cs`
- `Assets/Game/Scripts/Gameplay/Room2DPrototypeDemandLoop.cs`
- `Assets/Game/Scripts/Gameplay/Room2DDemoDayController.cs`
- `Assets/Game/Scripts/Gameplay/Room2DShowcaseViewController.cs`

What Changed:
- room tile labels now prefer showcase-facing state and action text
- Front Desk now exposes clearer queue pressure and action-hint summaries
- demand loop now exposes concise guest/request and room-fit strings for showcase UI
- showcase controller rewrites major Front Desk / Rooms text blocks toward player-facing language
- ready-room cards now show fit and recommendation instead of raw placeholder text

Unity Steps:
- open `Hotel_Rooms_2D_Proto`
- enter Play Mode and let `Room2DShowcaseViewController` rebuild the showcase UI
- verify old debug HUD stays hidden while showcase UI is active

How To Test:
- in Front Desk view, confirm waiting guests can be understood in a few seconds
- open guest detail, then room list, and complete one assignment
- switch to Rooms and confirm selected-room card explains the next useful action
- click Dirty / AwaitingInspection / Ready rooms and verify popup actions match room state

Self Review:
- Easier to record: Yes, Front Desk and Rooms now explain action order more directly
- Reduced debug feel: Yes, raw prototype text is reduced in the main visible cards
- Improved player-facing interaction: Yes, selected room and ready-room decisions are easier to read
- Closer to Front Desk / Rooms / Lounge target: Partly, Front Desk and Rooms improved first
- Best next step: Lounge clarity and phase-wide recording flow

### Iteration 2
Goal:
Make Lounge and overall demo-phase messaging feel like part of the same showcase flow.

Changed Files:
- `Assets/Game/Scripts/Gameplay/Lounge2D.cs`
- `Assets/Game/Scripts/Gameplay/Room2DDemoDayController.cs`
- `Assets/Game/Scripts/Gameplay/Room2DShowcaseViewController.cs`
- `Docs/AI_CONTEXT.md`
- `Docs/SHOWCASE_GOAL.md`
- `Docs/TASK_LOG.md`

What Changed:
- Lounge now exposes clearer stock, washing, and action-hint summaries
- Demo Day controller now exposes showcase phase labels and focus text
- showcase controller now uses shared phase/focus wording across Front Desk, Rooms, and Lounge
- source-of-truth docs now reflect the 3-view showcase goal instead of the older room-only milestone

Unity Steps:
- re-enter Play Mode after script reload
- check Front Desk, Rooms, and Lounge headers during Preparation / Operating / Ended
- confirm Lounge cards read like stock / washing / warning / action cards

How To Test:
- start in Preparation and verify action hints emphasize setup
- start Operating and confirm all three views update phase/focus language
- in Lounge view, trigger low cups or low stock and verify warnings become readable
- end the day and confirm the build still transitions to summary state

Self Review:
- Easier to record: Yes, the scene now reads more like one guided demo
- Reduced debug feel: Yes, phase/focus and lounge summaries are more product-facing
- Improved player-facing interaction: Yes, Lounge now communicates recovery actions more clearly
- Closer to Front Desk / Rooms / Lounge target: Yes, all three showcase views now have clearer roles
- Best next step: Unity-side polish pass on layout balance, room overlay spacing, and any remaining noisy labels

### Iteration 3
Goal:
Make Front Desk feel more like a guest-handling page instead of a prototype info strip.

Changed Files:
- `Assets/Game/Scripts/Gameplay/Room2DShowcaseViewController.cs`

What Changed:
- added a permanent Front Desk current-request card between the guest strip and bottom actions
- guest cards now support stronger selected-state highlighting
- Front Desk now keeps a visible current guest portrait/badge structure on both the page card and the detail popup
- upcoming guest can now appear as a visible card when no active or complaint guest is waiting
- queue pressure badge is always visible and changes tone based on waiting state

Unity Steps:
- open `Hotel_Rooms_2D_Proto`
- enter Play Mode and switch to Front Desk
- confirm the page now has header, waiting strip, current-request card, and bottom action row

How To Test:
- confirm the page no longer feels like only a scrolling strip plus popup
- click between complaint / active / upcoming guest cards and verify the selected card stands out
- open guest detail and verify portrait / warning badge placeholders are visible
- assign a room from Front Desk and watch the current-request card update

Self Review:
- Is Front Desk more understandable now: Yes
- Is Rooms more playable now: No change this iteration
- Is Lounge more readable now: No change this iteration
- Is the build less debug-like now: Yes
- Is the result closer to a recordable game slice now: Yes
- Should the next iteration focus on interaction, visual hierarchy, or placeholder assets: Visual hierarchy and placeholder assets for Rooms

### Iteration 4
Goal:
Unify card, button, badge, and placeholder presentation across Front Desk, Rooms, and Lounge.

Changed Files:
- `Assets/Game/Scripts/Gameplay/Room2DShowcaseViewController.cs`

What Changed:
- card panels now get both border and shadow treatment for clearer mobile-card separation
- showcase action buttons now use a darker in-game style instead of near-default light buttons
- action buttons now carry consistent icon placeholders
- Rooms selected card now shows room-type, room-state, and worker badge placeholders visibly instead of hiding them
- waiting-guest portrait, ready-room icon, and lounge placeholders now use stronger visual blocks and colors

Unity Steps:
- re-enter Play Mode after script reload
- inspect all three views for unified card and button styling
- inspect Rooms selected card and Lounge cards for visible placeholder badges

How To Test:
- verify Front Desk, Rooms, and Lounge buttons now share the same base visual language
- verify Rooms selected card always shows type/state/worker badges
- verify Lounge stock / cups / wash / warning placeholders read as icons rather than empty debug blocks

Self Review:
- Is Front Desk more understandable now: Yes
- Is Rooms more playable now: Yes, selected-room focus is clearer
- Is Lounge more readable now: Yes, placeholder icon structure is stronger
- Is the build less debug-like now: Yes
- Is the result closer to a recordable game slice now: Yes
- Should the next iteration focus on interaction, visual hierarchy, or placeholder assets: Interaction polish and Unity-side spacing validation

## Next Best Objective
- run a Unity playtest pass in `Hotel_Rooms_2D_Proto`
- verify no panel overlaps remain in portrait aspect
- tune popup sizes and bottom action spacing where the recording still feels crowded
- if needed, add one more pass of placeholder icon differentiation for room states and worker roles

## Manual Room Ops Pass

### Phase 1
Goal:
Refactor HSK / INSP into future-ready single-worker states while preserving the current one-worker bottleneck.

Changed Files:
- `Assets/Game/Scripts/Gameplay/Housekeeper2D.cs`
- `Assets/Game/Scripts/Gameplay/Inspector2D.cs`
- `Assets/Game/Scripts/Gameplay/Room2DWorkerSelectionPanel.cs`
- `Assets/Game/Scripts/Gameplay/Room2DController.cs`
- `Assets/Game/Scripts/Gameplay/Room2DShowcaseViewController.cs`
- `Assets/Game/Scripts/Gameplay/Room2DPrototypeDebugHud.cs`

What Changed:
- HSK / INSP states now use `Idle / Traveling / Working`
- current assignment still jumps directly into `Working`, but Traveling now has placeholder-ready structure
- single-worker availability is now exposed through explicit helper methods instead of only implicit enum checks
- worker selection now reports unavailable worker status more clearly
- worker status text now reads from shared status/target getters

Unity Steps:
- let Unity recompile scripts
- make sure the scene still has exactly one `Housekeeper2D` and one `Inspector2D`

How To Test:
- enter Play Mode
- assign one Dirty room to HSK and confirm a second room cannot be assigned while HSK is still working
- assign one AwaitingInspection room to INSP and confirm a second room cannot be assigned while INSP is still working
- watch worker text and confirm it now shows `Cleaning` / `Inspecting` via the new status interface

Self Review:
- Is Front Desk more understandable now: No change this phase
- Is Rooms more playable now: Slightly, because worker bottleneck logic is now clearer and cleaner
- Is Lounge more readable now: No change this phase
- Is the build less debug-like now: Slightly, internal worker state naming is cleaner
- Is the result closer to a recordable game slice now: Yes, because the worker bottleneck is now explicit and future-ready
- Should the next iteration focus on interaction, visual hierarchy, or placeholder assets: Interaction and visual hierarchy in Rooms / Front Desk

### Phase 2
Goal:
Move room-operation awareness to a Front Desk summary and keep HSK / INSP status visible at all times in Rooms.

Changed Files:
- `Assets/Game/Scripts/Gameplay/Room2DShowcaseViewController.cs`

What Changed:
- Front Desk header now shows `Ready / Dirty / AwaitingInspection` counts directly
- Front Desk current-result card now reads as `Room Status` instead of desk notes, and explicitly tells the player to switch to Rooms when room turnover is blocked
- Front Desk request hint now pushes the player toward Rooms when guests are waiting but no Ready rooms exist
- Rooms now keeps `HSK Status` and `INSP Status` cards pinned on screen instead of hiding them behind side toggles
- Rooms worker summary card now shows room-operation counts and current worker target context
- old HSK / INSP launcher buttons are visually demoted so the main interaction remains room tapping and popup assignment

Unity Steps:
- let Unity recompile scripts
- open `Hotel_Rooms_2D_Proto`
- switch to Front Desk and Rooms in Play Mode to verify the pinned card layout still fits portrait aspect

How To Test:
- in Front Desk, confirm the page shows Ready / Dirty / AwaitingInspection counts without any HSK / INSP action button
- create a waiting guest with no Ready room and confirm the hint tells you to switch to Rooms
- switch to Rooms and confirm `HSK Status` and `INSP Status` remain visible even when no popup is open
- assign one Dirty room and one AwaitingInspection room, then watch the pinned worker cards update immediately

Self Review:
- Is Front Desk more understandable now: Yes, room readiness pressure is visible without exposing room-operation buttons
- Is Rooms more playable now: Yes, worker bottlenecks are now persistently visible on the main operations page
- Is Lounge more readable now: No change this phase
- Is the build less debug-like now: Yes, side toggles matter less and pinned status cards read more like product UI
- Is the result closer to a recordable game slice now: Yes, the player is now pushed to switch views to solve room-readiness problems
- Should the next iteration focus on interaction, visual hierarchy, or placeholder assets: Interaction, specifically room popup behavior and busy-worker feedback

### Phase 3
Goal:
Make room popups state-driven and give immediate busy-worker feedback without bypassing the Rooms workflow.

Changed Files:
- `Assets/Game/Scripts/Gameplay/Room2DShowcaseViewController.cs`
- `Assets/Game/Scripts/Gameplay/Room2DWorkerSelectionPanel.cs`

What Changed:
- Dirty room popup now focuses on `Assign HSK` only
- AwaitingInspection room popup now focuses on `Assign INSP` only
- popup priority buttons are hidden from the main assignment flow to reduce prototype noise
- if HSK or INSP is busy, opening the action now keeps the player on the room popup and shows `No available HSK / INSP`
- worker assignment result strings are now player-facing and aligned with the single-worker bottleneck
- worker popup now also shows the selected worker status and target room, which fits the future `Traveling` expansion path

Unity Steps:
- let Unity recompile scripts
- open `Hotel_Rooms_2D_Proto`
- in Play Mode, switch to `Rooms` and test dirty-room / inspection-room actions back to back

How To Test:
- click a Dirty room and confirm the popup only offers `Assign HSK` plus non-core utility buttons like info/close
- click an AwaitingInspection room and confirm the popup only offers `Assign INSP`
- assign one Dirty room, then immediately click another Dirty room and try to assign again; you should see `No available HSK`
- assign one AwaitingInspection room, then immediately click another AwaitingInspection room and try to assign again; you should see `No available INSP`
- confirm the HSK / INSP pinned cards and the popup text both reflect the busy target room

Self Review:
- Is Front Desk more understandable now: No direct change, but the division between front-desk handling and room operations is clearer
- Is Rooms more playable now: Yes, the room popup now drives a cleaner manual turnover loop
- Is Lounge more readable now: No change this phase
- Is the build less debug-like now: Yes, room actions now map more directly to one intended gameplay decision
- Is the result closer to a recordable game slice now: Yes, the single-worker bottleneck is now legible in both action flow and feedback
- Should the next iteration focus on interaction, visual hierarchy, or placeholder assets: Visual hierarchy and small recording polish

## Guest Waiting Pressure Loop v1 - Phase 1

Goal:
Add visible guest patience states and clearer Front Desk blocker messaging so the player understands when room readiness is the current problem.

Changed Files:
- `Assets/Game/Scripts/Gameplay/FrontDesk2D.cs`
- `Assets/Game/Scripts/Gameplay/Room2DShowcaseViewController.cs`

What Changed:
- added prototype waiting guest patience states: `Normal`, `Impatient`, `Critical`
- Front Desk now tracks a current blocker message for the waiting guest
- Front Desk now tracks a next-action message that can point the player back to Rooms
- when no Ready room exists, the Front Desk UI now says room readiness is blocking check-in
- when a Ready room exists, the Front Desk UI now tells the player to return and assign/check in
- guest card and guest detail text now show patience, blocker, and next action
- Front Desk status and room-status cards now surface readiness pressure more directly

Unity Steps:
- let Unity recompile scripts
- open `Hotel_Rooms_2D_Proto`
- make sure `Frontdesk2D` still references the current `Room2DPrototypeDemandLoop`
- tune `Impatient Threshold Seconds` and `Critical Threshold Seconds` on `Frontdesk2D` if you want faster testing

How To Test:
- set all rooms to Dirty, or let Ready rooms be consumed so no suitable Ready room remains
- go to Front Desk and call/activate a guest
- confirm the Front Desk shows `Patience: Normal`
- wait past `Impatient Threshold Seconds` and confirm the guest becomes `Impatient`
- wait past `Critical Threshold Seconds` and confirm the guest becomes `Critical`
- confirm the Front Desk says room readiness is blocking check-in and tells you to switch to Rooms

Self Review:
- Is Front Desk more understandable now: Yes, the waiting guest now has a visible patience state and blocker reason
- Is Rooms more playable now: No direct change this phase
- Is Lounge more readable now: No change this phase
- Is the build less debug-like now: Slightly, because the guidance text is more player-facing
- Is the result closer to a recordable game slice now: Yes, the Front Desk now explains why the player must go to Rooms
- Should the next iteration focus on interaction, visual hierarchy, or placeholder assets: Interaction, specifically the return flow after a room becomes Ready

## Guest Waiting Pressure Loop v1 - Phase 2

Goal:
Make Ready rooms resolve the blocker but not auto-complete check-in, so the player must return to Front Desk to finish the guest flow.

Changed Files:
- `Assets/Game/Scripts/Gameplay/Room2DPrototypeDemandLoop.cs`
- `Assets/Game/Scripts/Gameplay/FrontDesk2D.cs`
- `Assets/Game/Scripts/Gameplay/Room2DShowcaseViewController.cs`

What Changed:
- added `Allow Automatic Fallback Assignment` to the demand loop and left it off by default
- Active Demand no longer automatically assigns a fallback room after the fallback timer unless that test flag is explicitly enabled
- added one shared demand-loop query for whether Front Desk currently has a Ready room it can use
- Front Desk now uses the demand loop as the source of truth for readiness instead of scanning rooms separately
- Front Desk guidance now changes from Rooms preparation guidance to `Return to Front Desk: assign/check in` when a Ready room exists
- Showcase UI uses the same readiness query so Front Desk text and gameplay behavior stay aligned

Unity Steps:
- let Unity recompile scripts
- open `Hotel_Rooms_2D_Proto`
- select `Room2DPrototypeDemandLoop`
- make sure `Allow Automatic Fallback Assignment` is unchecked for the normal showcase flow

How To Test:
- start with no Ready rooms, then activate a Front Desk guest
- confirm the guest keeps waiting and Front Desk tells you to prepare a room in Rooms
- switch to Rooms and manually process a Dirty room through HSK and INSP until it becomes Ready
- confirm the guest does not auto check in when the room becomes Ready
- return to Front Desk and manually assign/check in from the Ready room list

Self Review:
- Is Front Desk more understandable now: Yes, it now distinguishes room-not-ready from ready-but-needs-front-desk-action
- Is Rooms more playable now: Yes, Rooms now solves the blocker without stealing the Front Desk completion step
- Is Lounge more readable now: No change this phase
- Is the build less debug-like now: Slightly, because fallback automation is no longer the default normal path
- Is the result closer to a recordable game slice now: Yes, the loop is now Front Desk problem -> Rooms solution -> Front Desk completion
- Should the next iteration focus on interaction, visual hierarchy, or placeholder assets: Pressure consequence and clearer Front Desk result text

## Guest Waiting Pressure Loop v1 - Phase 3

Goal:
Make guest waiting pressure create visible consequences, so Front Desk waiting is no longer just a timer.

Changed Files:
- `Assets/Game/Scripts/Gameplay/FrontDesk2D.cs`
- `Assets/Game/Scripts/Gameplay/Room2DShowcaseViewController.cs`
- `Docs/TASK_LOG.md`

What Changed:
- waiting guests now apply a one-time prototype satisfaction penalty when they become `Impatient`
- waiting guests now apply a stronger one-time prototype satisfaction penalty when they become `Critical`
- Front Desk now tracks total Impatient / Critical guest counts
- Front Desk summary now exposes patience, queue pressure, satisfaction score, and latest pressure consequence
- Front Desk focus/result cards now show the latest front desk impact so the result is visible in UI instead of only Inspector state
- automatic fallback assignment remains disabled by default, so the player still has to return to Front Desk to finish check-in

Unity Steps:
- let Unity recompile scripts
- open `Hotel_Rooms_2D_Proto`
- select `Frontdesk2D`
- for faster testing, set `Impatient Threshold Seconds` to `5` and `Critical Threshold Seconds` to `10`
- select `Room2DPrototypeDemandLoop`
- keep `Allow Automatic Fallback Assignment` unchecked for the normal showcase loop

How To Test:
- start with no suitable Ready room for the current guest
- go to Front Desk and call/activate a guest
- wait past the Impatient threshold and confirm the guest patience changes and satisfaction drops
- wait past the Critical threshold and confirm the stronger consequence is recorded
- switch to Rooms and manually prepare a room through HSK / INSP
- confirm Front Desk guidance changes to returning for assignment/check-in once a Ready room exists
- return to Front Desk and manually complete assignment/check-in

Self Review:
- Is Front Desk more understandable now: Yes, waiting now has visible patience state and visible outcome pressure
- Is Rooms more playable now: Yes, Rooms remains the solution path instead of auto-resolving check-in
- Is Lounge more readable now: No change this phase
- Is the build less debug-like now: Slightly, because the Front Desk UI now explains consequences in player-facing terms
- Is the result closer to a recordable game slice now: Yes, the loop now has a visible penalty if the player reacts too slowly
- Should the next iteration focus on interaction, visual hierarchy, or placeholder assets: Interaction clarity between Front Desk guest cards and Rooms readiness
