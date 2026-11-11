# Tidy Up a City — GDD (key decisions mirror)

Source of truth: [Game Design Document](https://claude.ai/artifact/8rdqXTb12LNEBcQtFkmBJ9). This file mirrors the decisions code depends on.

## Premise
- The Great Muddle scattered thousands of items across a city. The player restores every location and uncovers what happened.
- Cozy, ASMR-adjacent. **No fail states, no timers in the main mode.**
- ~~The city starts grey, silent and empty~~ **Changed 2026-11-09 (Jesse):** locations are warm, lit and colourful from the start — never depressing. Tidying brings back people (NPCs at 100%) and, later, music layers. (The source GDD still says grey; update it there.)

## Core loop
Find → identify → carry → place → reward.
- **Identify:** every item shows its name when looked at or picked up.
- **Carry:** 10 items (hands) → 20 (basket) → 40 (trolley) → unlimited (magic satchel). Upgrades earned by restoring locations.
- **Place:** snaps into correct slot / mannequin / display case / stack.
- **Reward:** alignment tone, haptic tick, subtle sparkle on every correct placement.
- **Hints:** signs; ghost outlines in empty slots; Tidy Sense pulse (cooldown); per-location checklist ("Bakery: 412/600 · Bread ✓ · Pastries 80%").
- **Precision layer:** optional "perfect alignment" bonus. Never required.

## World
- Open-world streets, instanced locations (load on enter, unload on leave). All locations unlocked from the start.
- Map teleport, or walk the streets (the streets are a location with their own items and on-foot collectibles).
- **Hard-binding:** items never leave their location. **City Post:** strays go into Lost & Found and become delivery tasks at their true location (data only).

## Locations
- 60 locations, 10 districts, 10 mechanic families (A Shelf sort … J Shadow boards).
- Shops 300–800 items; big venues 2,000–5,000. Reuse meshes (2,000–4,000 unique). The mess is generated. Locations come from templates.
- District finale set piece per district.

## Progression & story
- Restoration per location: at 100% the shop comes alive (its NPC returns). City restoration meter.
- Collectibles ("Muddle oddities") for a personal display room; player's own home/shop to decorate.
- Each perfectly restored location plays a memory of the day before the Muddle; fragments (diary pages, cam stills) build the mystery. Environmental storytelling through objects.

## Replay & social (post-slice)
Co-op 2–4 in an instanced zone; speed-tidy challenges; opt-in Muddle Storms in completed zones only; daily request board.

## Platform
PC (Steam) first; VR (Quest) as hero mode / second platform.

## Plan
Vertical slice: one street, three locations — **Bakery** (core loop, feedback, carry limit, hints), **Library** (ordered sort, progression), **Fantasy shop** (living items) — plus restoration visuals and one memory each. Go/no-go: fun for 60–90 minutes.
Release: 3 districts (~20 locations: Old Town Market, High Street, Culture Quarter); the other 7 in free updates.
