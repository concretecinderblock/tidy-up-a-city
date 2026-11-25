# CLAUDE.md — Tidy Up a City

Guidance for Claude Code working in this Unity project. Read this fully before every session.
Full game design document: https://claude.ai/artifact/8rdqXTb12LNEBcQtFkmBJ9 (mirror key decisions into `Docs/GDD.md`).

---

## 1. The game in one paragraph

**Tidy Up a City** is a cozy open-world sorting game. A mysterious event, **The Great Muddle**, scattered thousands of items across a **mega town** of 60 locations in 10 districts (a town, not a modern city; every location is a mega place, big inside and out: Jesse, 2026-11-13). The player visits each location, finds misplaced items, picks them up (max 10 at a time), and snaps them back into their correct slot. Every correct placement gives instant feedback: an alignment tone, a haptic tick and a subtle sparkle. Locations are warm, lit and colourful from the start: it's a cozy game about helping a city and must never feel depressing. As locations are restored, NPCs and music return, and memory fragments reveal what caused the Muddle. No fail states, no timers in the main mode.

## 2. Current milestone: vertical slice

Build **one street with three locations** before anything else:

1. **Bakery**: core loop, feedback, carry limit, hints (template for Old Town Market)
2. **Library**: ordered sorting (template for Culture Quarter)
3. **Fantasy shop**: "living items" that move or fight back (template for Magic Quarter)

Plus: restoration (NPCs returning at 100%) and one story memory per location.
Go/no-go: the slice must be fun for 60–90 minutes. Do **not** start other districts until the slice is approved.

## 3. Tech stack (fixed, don't deviate)

- **Unity 6**, **URP**, **C#**
- **New Input System** only (no legacy Input Manager)
- **XR Interaction Toolkit** installed from day one: VR is a later platform, so all interaction must work with mouse/keyboard, gamepad *and* XR hands
- **Addressables** + additive scene loading for locations
- **FMOD** for audio (placement tones, adaptive music)
- Git + Git LFS; commit small and often
- Target: PC (Steam) first, Meta Quest later. Keep Quest performance in mind (draw calls, overdraw, real-time lights).

If you think something here is wrong, say so and explain; don't silently switch approaches.

## 4. Core architecture rules

### Data first
- Every item type is an `ItemDefinition` ScriptableObject: `id`, `displayName`, `mesh/prefab`, `homeLocationId`, `slotTypes[]`, `mechanicFamily`, optional `rarity` / `isStray`.
- Every placeable slot has a `SlotDefinition` (accepted slot types, optional exact item id, optional order index for ordered sorts, optional rotation tolerance).
- Locations are `LocationDefinition` assets: id, district, scene reference, mechanic families used, item list, Muddle twist settings.
- Item data can be imported from CSV (`Data/Items/*.csv`) via an editor importer. The CSV is the source of truth for item lists.

### Never hand-place the mess
- Designers and generators build the **tidy** state only.
- The **Muddle** is generated: a seeded `MuddleScatterer` removes items from slots and scatters them over valid surfaces (floors, tables, wrong shelves, district-specific spots like floating in water or stuck in trees).
- Same seed = same mess. Save the seed plus player progress, not every item transform.

### Locations are instanced
- A persistent `City` scene (streets, map, player, managers) plus, per location, a **shell scene** (the building, loaded on entry) and **zone scenes** (its rooms, on one or more floors linked by lifts) that stream in and out as the player moves (`MegaLocation`). Every zone counts towards the location's grand total; each item kind lives in one zone. A building map (corner minimap + full map on M) guides the player. (Mega locations, 2026-11-13.)
- Entering a location loads its scene; leaving unloads it.
- **Hard-binding:** a location only spawns its own items. Items can never physically leave their location.
- **City Post exception:** "stray" items are dropped in a Lost & Found box, stored as data (item id + target location), and spawn as a delivery task at their true location. Never move GameObjects between scenes.

### Generators over manual building
Prefer editor tools that build content from data over placing objects one by one:
- `ShelfGenerator`: builds shelves, racks and display cases with slots from parameters
- `LocationBlockout`: lays out fixtures from a `LocationDefinition`
- `MuddleScatterer`: generates the mess
- `ItemImporter`: CSV → ItemDefinition assets

When asked to "build a location", build or extend the generators first, then run them.

## 5. Core loop: required behaviour

| System | Rules |
|---|---|
| Look / identify | Item under the reticle shows its `displayName`. Same on pickup. |
| Carry | Hands 10 → basket 20 → trolley 40 → unlimited satchel, earned through progress. The Muddle scatters items one by one (no piles; Jesse, 2026-11-10). Carried items shown grouped in an inventory strip. |
| Place | Item snaps into a valid slot when close and roughly aligned. Placing fills a run of matching empty slots from what you carry. Wrong slot = soft rejection (gentle sound, item stays in hand). |
| Clean | Dirty fixtures must be cleaned before they accept items, each with a quick, fun minigame of a few seconds (never a chore): poof the dust bunnies on shelves; a wipe timing bar for windows, counters and floor smudges (optional goals). Cleaning state is saved. |
| Feedback | Correct placement: alignment tone (pitch rises as a shelf fills), haptic tick, small particle sparkle. Must feel instant (<50 ms). |
| Precision bonus | Optional "perfect alignment" when rotation is within tolerance. Never required. |
| Hints | Signs and labels; ghost silhouettes in empty slots; "Tidy Sense" pulse on a cooldown; per-location checklist (e.g. "Bakery 412/600"). |
| Restoration | Locations look finished from the start (full colour, lights on, decor in place); never grey, dim or empty-looking. Completion % brings NPCs back (at 100%) and later music layers. 100% = location "opens" and its story memory surfaces. |

## 6. Mechanic families

Each family is a reusable component set. New locations combine 1–2 families.

| # | Family | Behaviour |
|---|---|---|
| A | Shelf sort | Item matches slot type/label (baseline) |
| B | Ordered sort | Slots have an order index (alphabetical, numeric, size) |
| C | Reassembly | Parts snap onto a base object until complete |
| D | Pattern & gradient | Slot validity depends on neighbours (colour, symmetry) |
| E | Reference match | Layout must match a reference image/poster |
| F | Pairing | Items must be placed as matching pairs/sets |
| G | Dress & fit | Mannequin slots, size categories, outfit sets |
| H | Orders | NPC request lists that consume specific items |
| I | Living items | Items with simple behaviours: move, hide, swap labels |
| J | Shadow boards | Outlined silhouette slots for tools/parts |
| K | Cleaning | Dirty props are cleaned with quick minigames (dust bunnies, wipe timing); dirty fixtures accept items only once clean (added 2026-11-10) |

Slice needs: **A** (bakery), **B** (library), **I** (fantasy shop), **K** in all three.

## 7. Folder structure

```
Assets/
  _Project/
    Scripts/
      Core/          # pickup, carry, snapping, feedback, save
      Items/         # ItemDefinition, SlotDefinition, item runtime
      Locations/     # LocationDefinition, loading, restoration
      Mechanics/     # one folder per family (ShelfSort, OrderedSort, ...)
      Muddle/        # scatterer, district twists
      CityPost/      # strays, lost & found, delivery tasks
      UI/
      Editor/        # generators, importers, validation tools
    Data/
      Items/         # CSV sources + generated ItemDefinitions
      Locations/
    Scenes/
      City.unity
      Locations/     # Bakery.unity, Library.unity, FantasyShop.unity
      Test/          # grey-box test scenes
    Prefabs/
    Art/             # imported asset packs live in their own subfolders
    Audio/
Docs/
  GDD.md
  DECISIONS.md      # log of architecture decisions
```

## 8. Code conventions

- C#: PascalCase types and methods, `_camelCase` private fields, `[SerializeField] private` over public fields.
- One responsibility per MonoBehaviour; keep logic testable in plain C# classes where possible.
- No `FindObjectOfType` / `GameObject.Find` in runtime code; use references or a simple service locator.
- No per-frame allocations in Update loops (thousands of items per location).
- Pool particles and audio sources.
- Write EditMode tests for slot validation, ordered-sort logic and save/load.
- Every editor tool gets a menu entry under `Tools/Tidy Up/...`.

## 9. Performance budget (per location, PC baseline)

- Mega locations: ~10,000+ items per location over streamed rooms of ~1,500–2,500 items each; only the rooms near the player are loaded (Jesse, 2026-11-13; was 3,000–5,000 per location)
- Loose items rest kinematic (no simulation) until the player touches them; ghosts and living behaviour only near the player
- Use GPU instancing / SRP Batcher-friendly materials; share materials across items
- Items far from the player: no physics, no Update; activate on proximity
- Static fixtures marked static and batched
- Profile every new location before calling it done

## 10. How to work with me (Jesse)

- Work in small, testable steps. After each step: what changed, how to test it in the Editor, what's next.
- Before large refactors or new packages: explain the plan and wait for approval.
- Look: warm, semi-realistic interiors, aiming as close as possible to the reference screenshots (grand wooden library, museum hall: glossy varnished wood, rich materials, detailed architecture, warm lamplight, haze, bloom), made entirely in-house with no bought or downloaded assets (generated meshes and textures; see `Docs/ArtBible.md`). Real models can still replace any item later through the CSV `prefab` column. (Was "procedural storybook style"; changed by Lowie 2026-11-12.)
- Log architecture decisions in `Docs/DECISIONS.md` (date, decision, why).
- If a request conflicts with this file or the GDD, point it out.

## 11. First session prompt

> Read CLAUDE.md. Set up the folder structure and core scripts for the vertical slice. Then build a grey-box Bakery test scene with: a first-person controller, one bread rack with 12 slots generated by a `ShelfGenerator` editor tool, 3 bread ItemDefinitions, pickup with a 10-item carry limit, name display on look, snapping into valid slots, and placeholder feedback (sound + particle). Finish with a short list of how to test it.
