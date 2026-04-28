# Room Assignment and Guest Preferences Design

## Purpose
This document records the design direction for room assignment, guest preferences, and operational opportunity systems such as early check-in and late checkout.

## 1. Core Assignment Principle
Room assignment has two layers:

### 1.1 Hard Constraints
Hard constraints determine whether a room can legally be assigned.

Examples:
- King-bed booking can only be assigned to king-bed rooms
- Twin booking can only be assigned to twin rooms
- Family booking can only be assigned to family rooms

### 1.2 Soft Preferences
Soft preferences determine how good the assignment feels, but they do not usually prevent check-in.

Examples:
- High floor / low floor
- Near elevator / far from elevator
- Street-facing / back-facing
- Quiet / better view

A guest may randomly generate 1 to 2 preferences.
Preferences can be satisfied or not satisfied.
Unsatisfied preferences reduce satisfaction but usually do not block assignment.

## 2. Rooms Should Have Trade-Offs
Rooms should not be simple “good vs bad”.
They should have strengths and weaknesses.

Examples:
- Street-facing: better view, but noisier
- Back-facing: worse view, but quieter
- High floor: better view, better sunlight, quieter
- Low floor: worse view, noisier, but easier access

This ensures room assignment becomes a meaningful management decision instead of a simple ranking.

## 3. Visible and Hidden Information
The room-assignment system should use two information layers.

### 3.1 Visible Information
This is the information the player actively reads and uses:
- Room type
- Current room state
- Floor level
- Street-facing / back-facing
- Elevator distance preference tags
- Upcoming demand
- Reserve / assign actions

### 3.2 Hidden Information
This is the information the system calculates internally:
- Cleanliness feeling
- Wear / oldness feeling
- Noise penalty
- Sunlight / view bonus
- Combined room comfort result

The player should not need to read every hidden variable directly.
Instead, these values should be reflected through:
- Match hint
- Outcome
- Day summary
- Guest reaction / satisfaction change

## 4. Room Quality Principle
Room cleanliness and room oldness should remain separate concepts.

A room can be:
- technically clean but old-looking
- visually decent but not truly clean
- both clean and comfortable
- both dirty and worn out

This distinction should continue to affect room assignment and later review/satisfaction systems.

## 5. Early Check-In Design
### 5.1 Base Time Rules
- Checkout time: 10:00 AM
- Standard check-in time: 3:00 PM
- Any successful check-in before 3:00 PM counts as early check-in

### 5.2 Design Role
Early check-in is not mandatory.
It is an optional operational opportunity.

### 5.3 Player Value
Approving early check-in can:
- generate extra revenue
- improve guest satisfaction
- create short-term strategic reward

### 5.4 Risk
Approving early check-in may:
- consume a strong room too early
- reduce flexibility for later demand
- increase future turnover pressure

## 6. Late Checkout Design
### 6.1 Request Behavior
Late checkout requests should appear as a high-visibility notification.
The player should make a quick decision:
- reject
- approve until a chosen time

### 6.2 Revenue Role
Late checkout is a paid opportunity.

### 6.3 Risk
The main risk of late checkout is not a separate abstract punishment.
The main risk is that it compresses the turnover window:
- less time for cleaning
- less time for inspection
- higher risk of not being Ready by later demand
- more backlog pressure

### 6.4 Suggested Approval Tiers
Example:
- Until 11:00 AM: low fee, low risk
- Until 12:00 PM: medium fee, medium risk
- Until 1:00 PM: high fee, high risk
- Until 2:00 PM: very high fee, very high risk

## 7. Preference Strength (Future Expansion)
Preferences may later be split into:
- normal preferences
- strong preferences

Example:
- “prefers high floor” = normal preference
- “must be quiet” = strong preference

This can create more nuanced matching outcomes later.

## 8. Design Principle Summary
- Room type decides whether a room can be assigned
- Preferences decide whether the assignment feels good
- Hidden room-quality calculations should not overload the player
- Early check-in and late checkout are revenue opportunities with turnover risk
- The game should reward “good enough hotel manager judgment” rather than perfect information solving