# Architecture decisions

Format: date · decision · why.

## 2026-11-09 · Vertical slice, session 1

- **Grey-box Bakery scene is generated, not hand-built.** `Tools/Tidy Up/Build Bakery Greybox Scene` rebuilds `Scenes/Test/Bakery_Greybox.unity` from data (CSV → ItemDefinitions → ShelfGenerator → MuddleScatterer). *Why:* "generators over manual building"; re-running the tool is how the scene evolves.
- **Assembly definitions:** `TidyUp.Runtime`, `TidyUp.Editor`, `TidyUp.Tests.EditMode`. Editor tools set component fields through `internal EditorConfigure(...)` methods (`InternalsVisibleTo`). *Why:* faster iteration compiles, testable runtime code, no reflection or public setters on gameplay components.
- **`SlotDefinition` is a serializable class inline on `ItemSlot`, not a ScriptableObject.** *Why:* thousands of slots per location; one asset per slot would bloat the project. Validation lives in the static `SlotRules` (pure, unit-tested).
- **Tidy state + runtime scatter.** Slots hold a `startOccupant`; `MuddleScatterer` (seeded `System.Random`, fixed number of rolls per slot) scatters them at Start. *Why:* never hand-place the mess; same seed = same mess. Note: positions are deterministic, the physics settling after the drop is not. Save files will store seed + placed-slot progress, not transforms.
- **Carried items stay in their location scene, deactivated.** *Why:* hard-binding — items never leave their scene.
- **Input actions defined in code (`TidyInput`)** rather than the template's `InputSystem_Actions.inputactions`. *Why:* bindings sit next to the code reading them while the slice is in flux. Revisit when adding rebinding UI. `Interactor.TryPickUp / TryPlace` take explicit targets so an XR hand interactor can call the same logic.
- **Placeholder feedback.** Tones are synthesized at startup (`ToneSynth`), pooled `AudioSource`s and particle systems, pitch follows a major-pentatonic scale as a shelf fills, gamepad rumble tick. All fire synchronously on placement (<50 ms budget is dominated by the audio DSP buffer). Replace with FMOD events behind the same `FeedbackService` API.
- **Legacy uGUI `Text` for the grey-box HUD.** *Why:* avoids the TextMeshPro essential-resources import during grey-box; switch to TMP or UI Toolkit with the real UI pass.
- **Placed items are locked.** Only correct placements succeed, so there's nothing to undo. Revisit for ordered sort (library), where re-ordering may be needed.

## 2026-11-09 · Bakery hints

- **New folder `Scripts/Hints/`** (not in the CLAUDE.md §7 tree): `HintService`, `TidySense`, `HintBeacon`. *Why:* hints are their own system used by every location, not part of Core or UI.
- **Ghost silhouettes are built at runtime from the tidy state.** `HintService` (execution order -50, before the MuddleScatterer's Start) copies each slot's start-occupant meshes into a transparent child under the snap point. *Why:* zero authoring and always in sync with the item. Cost: one extra renderer per slot sharing one material (SRP Batcher). For 5,000-slot venues, revisit with proximity activation or baked ghosts.
- **Hint unlocks live in `HintService`** (ghosts, Tidy Sense, checklist breakdown). All on for the slice. Signs are always on (they're world geometry from the ShelfGenerator).
- **Checklist categories come from slot labels** (`Checklist`, plain C#, tested). The total line is always shown; the per-category breakdown is the unlockable part.
- **Tidy Sense** (F / gamepad North, 8 s cooldown, 3 s highlight): holding items, it marks the nearest free slot for the selected item (falling back to any carried item); empty-handed, the nearest loose item. Searches run only on a press, not per frame.

## 2026-11-09 · Bakery restoration visuals

- **`RestorationController` drives restoration from a smoothed completion value** (eases toward `LocationProgress.CompletionRatio` at 0.35/s), so each placement sends a short wave of change instead of a jump. On the first frame it snaps to the current state with no fanfare.
- **Colour comes from one global URP Volume (Color Adjustments saturation -90 → +10)** plus main-light colour/intensity and ambient intensity. *Why:* one cheap full-screen effect works for every location without per-material work. The runtime uses `Volume.profile` (a clone), so Play mode never edits the asset. Cost on Quest: Color Adjustments is part of URP's uber post pass; acceptable, re-check when profiling Quest.
- **Grey-box base colours are now warm and saturated** (terracotta floor, cream walls, teal counter). *Why:* the volume can only give back colour that's there.
- **`RestorationStage` components fire at thresholds**: lamps at 25/50/75% (light fade + bulb material swap, no shadows), flowers at 50%, OPEN sign + baker NPC at 100% ("the location opens"). Stages only Update while animating. A chord plays per stage; a fuller one at 100%.
- **Music layers are not done**: `RestorationController.RestorationChanged(float)` is the hook for FMOD adaptive music once FMOD is approved.
- Render pipeline assemblies (`Unity.RenderPipelines.Core/Universal.Runtime`) added to the Runtime and Editor asmdefs.

## 2026-11-09 · Restoration reversed: cozy from the start (Jesse)

- **Supersedes the entry above.** Jesse: dimmed lights and a grey start feel depressing; it's a cozy game about helping a city. Locations now start warm, lit and colourful, with lamps, flowers and the OPEN sign present. **Only the NPC (the baker) returns at 100%.**
- Removed: the colour volume (`Bakery_Restoration.asset`), main-light/ambient drive, lamp/flower/sign stages, and the smoothing. `RestorationController` now only applies `RestorationStage`s from the completion ratio; `RestorationChanged` stays as the music hook.
- **Conflicts with the GDD** ("The city starts grey, silent and empty… colour, light, music and people come back") and CLAUDE.md §1/§5 (restoration drives saturation, lights). `Docs/GDD.md` is updated; the GDD doc and CLAUDE.md are Jesse's to update.

## 2026-11-09 · Bakery story memory

- **CLAUDE.md updated** (§1, §2, §5 Restoration): locations are warm and finished from the start; 100% brings NPCs back and surfaces the story memory.
- **Memories are data:** `MemoryDefinition` (Data/Story/*.asset): title, location name, lines (speaker, text, auto-advance seconds), clue. Linked from `LocationDefinition.Memory`. The builder writes the Bakery's text only when the asset doesn't exist yet; after that the asset is the source of truth.
- **Player-triggered, not automatic:** at 100% a glowing photo (`MemoryFragment`, an `Interactable`) appears on the counter with the baker and announces itself. *Why:* an automatic cutscene would interrupt the player mid-placement; this keeps agency and lets them relive it.
- **New `Interactable` base class** (Core): the Interactor now handles items, slots and interactables. NPCs, the Lost & Found box and doors will use it.
- **Presentation:** sepia veil + text band over the live room, HUD hidden, title → lines → clue. E advances, G skips to the clue. Gameplay input blocked via `PlayerInputReader.GameplayBlocked`; time keeps running.
- **`MemoryJournal`** (plain C#, tested) records collected memory ids in order; persistence comes with the save system.
- **New folder `Scripts/Story/`** and `Data/Story/` (not in the §7 tree).
- **Mystery hooks planted** (kept ambiguous between the GDD's culprits): tiny footprints in spilled flour (small creature) and the square's clock striking thirteen (city forgot itself / experiment).
- Builder fix: `EditorSceneManager.NewScene` unloads unreferenced assets, so asset references are reloaded after creating the scene.

## 2026-11-09 · Roofs, shared location kit, Library (ordered sort)

- **Every location gets a ceiling and a pitched tiled roof** (`LocationKit.BuildRoom`). The shell (walls, ceiling, roof) does **not cast sun shadows**: interiors stay evenly bright and cozy instead of dark under a roof, and it saves shadow-map cost on Quest. Furniture and items still cast shadows.
- **`LocationKit` (editor)** holds everything shared by location builders: data prep, room shell, player, systems, hints, HUD, memory overlay/fragment, completion stage (NPC + memory at 100%), lamps, signs, flowers, NPCs. `BakeryGreyboxBuilder` and `LibraryGreyboxBuilder` only describe their content. New locations should follow the same pattern.
- **Grey-box item shapes are data**: CSV columns `greyboxShape` (cube/sphere/cylinder/capsule/capsule_x), `greyboxSize` (w;h;d), `greyboxColor`. Prefabs are regenerated on every build (same path, so GUIDs and references survive); one material per colour (`Item_<hex>`), shared by all items and filler.
- **Ordered sort (family B) = exact slots computed from sort keys.** `ItemDefinition.SortKey` (CSV `sortKey`); `OrderedSortRules.Sort` (numeric when both keys are numbers, otherwise invariant text ignoring case/accents; stable) gives the order, and the ShelfGenerator writes one `ExactItemId` per slot in reading order. Validation reuses `SlotRules`, so no new runtime cost. *Why not "relative order" validation:* with as many slots as books the only consistent final layout is the exact one, and relative rules allow dead ends.
- **Ordered rejections give directions** ("goes further left/right", "belongs on the shelf above/below") from `OrderedSortRules.Direction` + `SlotGroup.IsOrdered/Columns`. Items from another bookcase get the plain message.
- **Library content**: 20 novels A–T by author, City Atlas Vol. 1–10 (one per district) by number, 10 picture books by size (tallest first): all three ordered kinds from CLAUDE.md §6. Invented authors and titles (no real-world IP).
- **ShelfGenerator filler shelves**: decorative rows above the puzzle rows with seeded, static, collider-free filler books, so bookcases read full-height while puzzle rows stay at a comfortable height. Sign strips widen on narrow cases and text best-fits.
- **Library memory** follows the Bakery clues: a return card for City Atlas *Volume Eleven*, "The Streets That Aren't There" (the library only ever had ten), signed with a tiny floury handprint. Ms. Penhallow the librarian returns at 100%.

## 2026-11-10 · Fantasy shop (living items, family I)

- **Living behaviours are data**: CSV `living` = hop | float | shy | disguise, plus `disguiseName`; stored on `ItemDefinition`. The grey-box prefab builder adds a `LivingItem` component, googly eyes (so personality is visible) and, for disguised items, a stand-in visual (same silhouette, dull stone grey).
- **Core stays decoupled**: `ILivingBehaviour` (Items) is the only thing `TidyItem` and `Interactor` know about. The Interactor calls `TryCatch` on pick-up (an item may slip away or reveal itself), `OnGazed` while it's under the reticle, and refreshes the label when `LabelVersion` changes.
- **Cozy rules, no failure**: Skittish hops away (inside the roam area) when the player comes within 1.4 m or grabs it, 3 times, then tires (sleepy eyes) and can be caught. Floating drifts slowly at ~1.3 m and is caught mid-air. Shy is a translucent shimmer (eyes still visible) named "A faint shimmer" until within ~2.2 m (with hysteresis), then shows itself with a chime. Disguised reads and looks like its stand-in until looked at for 0.8 s or grabbed ("That Ordinary Pebble was a Dragon Egg in disguise!").
- **Performance**: `LivingItem.Update` only runs while the item is loose and only for behaviours that need it; placed/carried living items cost nothing. `LivingItemDirector` (Systems) gives them the player, roam area and feedback without any Find calls.
- `FillTidyState` now gives type slots the least-used matching item, so a shelf of four different potions gets each once while the bakery still gets four baguettes from one definition.
- Wall signs best-fit their text. Ghost silhouettes only use active meshes (a disguise must not show in the ghost).
- **Shop memory** moves the mystery forward: Madame Zephyrine's crystal ball shows a thirteenth lane behind the clock on the square, and something small and lonely knocking from the other side; a sugared bun left out is gone by morning.
- Not done: the Magic Quarter twist from the GDD ("the mess reshuffles itself every hour").

## 2026-11-10 · The street: City scene + Addressables locations

- **Addressables 4.2.0 installed** (approved by Jesse). Location scenes (`Scenes/Locations/*.unity`) are in the "Locations" group with addresses `Location_Bakery`, `Location_Library`, `Location_FantasyShop`; `LocationDefinition.SceneName` holds the address. `City.unity` is the only scene in Build Settings.
- **Persistent City scene** holds the street, the player side (player, feedback, memories, Tidy Sense, HUD) and the `LocationLoader`. Location scenes hold only location content (no sun, no player). Interiors live at the world origin, the street at z = -50; the street root is hidden while inside.
- **`GameServices` (minimal service locator)** joins the two sides, since scenes can't reference each other: `PlayerServices` registers the player side; `LocationRuntime` registers the active location. Location components resolve player services at use time; HUD and Tidy Sense follow `ActiveLocationChanged`. Standalone test scenes (`Tools/Tidy Up/Test Scenes/...`) use the same path.
- **Progress across visits**: `LocationStateStore` keeps filled-slot flags per location (session only; save system later). The MuddleScatterer skips saved slots on re-entry; unplaced items re-scatter from the seed. Saving starts only after the Muddle has scattered, so the tidy authoring layout is never saved as progress.
- **Hard-binding on exit**: carried items are set down by the location's door before it unloads ("Things stay where they belong").
- **Street HUD**: overview of every location (not visited / x/y / ✓). Doors show progress ("Enter the Bakery · 3/12").
- Bug found in testing: `LocationRuntime` disabled itself after its first save, which un-registered the active location. Fixed with a flag.

## 2026-11-10 · Save system, main menu, pause menu

- **Saves are seed-plus-progress** (`SaveData`): filled-slot flags per location (with names and slot counts for the menu), collected memory ids, play time, and where the player is (street position, or the location they're inside). Loose items and carried items are not saved; they come back from the seed.
- **Storage** (`SaveStorage`): 3 slots as JSON in `persistentDataPath/Saves/slotN.json`; write to `.tmp` then replace (no half-written saves); damaged or newer-version files are reported, never thrown. Tested with a temp folder.
- **Loading reloads the City scene** with the save pending (`SaveSession`), so every load starts from a clean scene; a save made inside a location walks the player back in through its door.
- **Autosave** into the current slot: right after a new game starts (so Continue works), when the player is back on the street, when a location completes, and before going to the main menu or quitting from the pause menu.
- **MainMenu.unity** (first in Build Settings): Continue (most recent save), New Game (choose slot; confirm before replacing), Load Game, Controls, Quit. The home panel always shows the controls table. **Pause menu** (Esc / Start): Resume, Save Game, Load Game, Controls, Main Menu, Quit; time is paused and the HUD hides. Delete is available on every slot card with a confirmation.
- **Menu UI is built at runtime by `GameMenu`** (uGUI, legacy Text, one component for both modes) instead of authored prefabs. *Why:* one code path, nothing to keep in sync during the prototype. Revisit with the real UI pass (likely UI Toolkit).
- `PlayerInputReader`: Esc/Start raise `PauseRequested` (falls back to freeing the mouse when a scene has no pause menu); `MenuOpen` blocks gameplay and cursor capture while a menu is open. An EventSystem with `InputSystemUIInputModule` is created on demand.

## 2026-11-10 · Direction change (Jesse): textured look, thousands of items, piles, cleaning

Decisions (asked and answered): procedural in-house art; 3,000–5,000 items per location; piles + carry upgrades; cleaning gates stocking. CLAUDE.md updated (§5 Carry/Place/Clean rows, §6 family K, §9 budget, §10 art line). Work split into 4 steps: look → scale & handling → cleaning → big locations.

### Step 1 · Storybook look

- **Art bible** in `Docs/ArtBible.md`, palette in `Scripts/Core/Palette.cs`. New colours come from the palette (older builder literals migrate as they're touched).
- **Generated textures** (`Editor/Art/ProceduralTextures.cs`, menu `Tools/Tidy Up/Art/Generate Textures`): planks, wood, plaster, tiles, cobbles, roof tiles, fabric, stone; 512² tileable albedo (light, tinted by material colour) + normal map from a height field. Lesson: `Mathf.SmoothStep(from, to, t)` is not GLSL `smoothstep(e0, e1, x)`.
- **Generated meshes** (`MeshGen`, `MeshAssets`): rounded boxes with UVs in metres (shared per size, saved as assets; GUIDs stable on rebuild), lathe shapes, deformed spheres. `GreyboxAssets.Box` and kit cube primitives now produce rounded boxes, so every fixture, wall, sign and door is soft-edged. No ProBuilder needed so far.
- **Item shapes** (`ItemShapes`, CSV `greyboxShape`): loaf, baguette, croissant, egg, crystal, book, book_flat, bottle, candle, cup, wand, globe. Multi-material items (book pages, bottle cork, candle wick + flame) are one mesh with submeshes → one renderer per item (important for thousands).
- **Rooms** get baseboards, cornices and framed windows (glowing sky panes); facades get framed windows with warm lamplight. Shop windows, signs and doors use rounded meshes.
- Ghosts and shy items handle multi-material renderers.

## Backlog (after the prototype)

- **Library sorting is boring** (Jesse, 2026-11-10). Ideas: readable spines (generated spine texture atlas, one material: big author initial / volume number, face-out picture-book covers, letter tabs on shelves); insert-and-slide rows instead of exact slots (books slide apart, any order while working, warm chime for correct neighbours, shelf completes when in order); run tones, shelf-complete domino wiggle, grab small piles.

## Pending approval

Packages in CLAUDE.md §3 that are **not installed yet** (need Jesse's OK before adding): XR Interaction Toolkit, FMOD for Unity. (Addressables installed 2026-11-10.)

- 2026-11-09 · Installed the Unity Pipeline package (`com.unity.pipeline`), approved by Jesse, so Claude can drive the open Editor from the CLI.
- 2026-11-09 · Git deferred at Jesse's request (CLAUDE.md §3 lists Git + LFS). `.gitignore` and `.gitattributes` (LFS rules) are prepared at the project root for when it's set up.

## 2026-11-10 · Step 2 · Scale and handling
- **Resting items:** the Muddle lays items down kinematic (`TidyItem.SetLooseResting`); only items the player drops get physics. Thousands of loose items cost no simulation.
- **Piles:** same-kind items are laid in piles of 3–8 (2×2 layers, tall things lie down; `PileLayout`, tested). Living items stay loose on their own. One press picks up a whole pile, top first, as far as capacity allows.
- **Run placement:** placing one item also places every other carried item that fits that fixture into the nearest empty accepting slots (the placed kind first). The logic is instant; a capped (14-note) rising cascade is the feedback.
- **Carry tiers:** Hands 10 → Basket 20 (100 placed) → Trolley 40 (500) → Satchel unlimited (2,000), derived from total placed across the city (`CarryTiers`, `CarryUpgrades`). No extra save data.
- **Scale:** `SlotGroup` and `LocationProgress` update counts in O(1) per slot change, and `ProgressChanged` fires at most once per frame. Ghosts are captured as a recipe at load and only built and shown within 4 m of the player.
- **HUD:** the carry strip is grouped by kind ("Basket 14/20 · Croissant ×8 · [Baguette ×6]"); cycling the selection jumps between kinds.

## 2026-11-10 · Step 3 · Cleaning (family K)
- **Clean before stocking:** every fixture with slots gets a required `Cleanable`. It closes its slots through `ISlotGate` (`ItemSlot.IsOpen`) until it's clean. A closed slot hides its marker and ghost, and placing gives a friendly "too dusty, scrub it first" message.
- **Optional goals:** windows, counters and tables (`LocationKit.MakeCleanable`) and seeded floor smudges. All of them count toward the checklist line "Cleaned x/y".
- **Scrubbing:** hold E on something dusty. While held, mouse or stick movement scrubs instead of turning the camera (`PlayerInputReader.LookLocked`). Holding still also cleans, so gamepad and XR work, but brisk movement is about 3× faster. A shelf takes about 3.5 s of brisk scrubbing. Rules are in `CleaningRules` (tested).
- **Dirt look, no new shaders:** fixtures fade toward a soft dusty beige through a MaterialPropertyBlock, which is cleared once clean so batching returns. Flat tops (shelf boards, counter and table tops, sills) carry a pale "Dust Film" quad. Floor smudges are a generated alpha texture (`DustAssets`). Dust layers fade out while scrubbing.
- **Feedback:** a soft generated brush sound and dust puff about 5× per second while scrubbing; a chime and sparkle when clean.
- **Save:** clean flags per location live in `LocationStateStore` (separate from the filled-slot flags) and in `LocationSave.cleaned`. The order is by hierarchy path, so it's stable between builds. The field is additive, so older saves still load (everything starts dusty).

## 2026-11-10 · Step 4a · The big Bakery (3,998 items)
- **Size:** the room grows from 8×8 m to 18×14 m (3.6 m ceiling), with 32 fixtures and 26 item kinds (`bakery.csv`). Each kind has its own slot type, so every row and fixture says exactly what belongs there.
- **Generators:** `ShelfGenerator` gains `SlotsDeep` (slots one behind the other; back rows fill through run placement) and styles: Bookcase, OpenRack (posts only), Case (cabinet, glass front and top without colliders so the reticle reaches inside), Crate. The new `StandGenerator` builds round tiered stands with slots in rings, items facing outwards.
- **New item shapes:** bun, bagel, donut, cookie, macaron, muffin, cupcake, cake, pie, jar, sack. Low segment counts, one mesh per item, multi-material through submeshes.
- **Items layer:** the player ignores item collisions (walk through piles). The Muddle never piles onto items, fixtures or steep surfaces, and keeps piles apart (seeded best-of-16 spot search).
- **Heaps instead of towers:** round things lie in a sunflower mound (about 62% on the bottom, the rest resting on what's below). Long things stack like a woodpile with crossing layers. Pile sizes are 6–14 here (`MuddleScatterer.EditorSetPileSize`).
- **Performance** (Editor, this PC, player at the door): frame 4.9 ms, 7,300 draw calls (SRP Batcher), 2.9 M triangles, load about 2.8 s including the fade. Items and slot markers don't cast shadows: inside a roofed room the sun's shadow covers everything anyway, and this halved draw calls and shadow casters (7,100 → 605). Resting items don't interpolate. For Quest later: item LODs and instancing.
- **HUD:** with more than 8 kinds the checklist shows "Kinds done x/y" plus the 5 unfinished kinds closest to done.
- **Saves:** older saves have a 12-slot Bakery. The layout length differs, so that progress is ignored and the Bakery starts fresh. Other locations are unaffected.

## 2026-11-10 · Step 4b · The big Library (4,840 books)
- **Sorting at scale:** exact-order sorting (one unique book per slot) doesn't scale to thousands of unique ItemDefinitions. Mass books are sorted by section and range instead: novels by author range A–C … V–Z, mysteries A–F … S–Z, non-fiction by subject number (500 Science … 910 Travel), poetry A–M / N–Z, encyclopaedias A–M / N–Z, picture books by size. That's 64 kinds in `library.csv`. Each section is one colour family with 2–3 shades, so books read at a glance. The original 40-book exact-order puzzle stays as two **featured cases** on the north wall.
- **Layout:** the room grows from 8×7 m to 20×16 m (3.8 m ceiling). It has six double-sided stacks (12 sides, 240 books each), ten low subject shelves under the windows, four encyclopaedia cases, four children's book bins (Crate style) on a rug, and a returns desk by the door. That's 32 fixtures.
- **Cheaper books:** the new `book_lite` shape is a bevelled cover block plus a page strip, 216 triangles against 1,008 for the featured books' full book.
- **Piles:** tall or slab-like items now tip over so their **thinnest side is vertical**. Books lie flat on their covers instead of standing like dominoes; bottles and candles still roll onto their side. Square picture books and board books lie down too.
- **Performance** (Editor, this PC): frame 5.0 ms, 14,100 draw calls (each book is two submeshes: cover and pages), 2.0 M triangles, load about 2.9 s, 530 heaps. Later, a single-material book (atlas UVs) would halve the draw calls if needed for Quest.

## 2026-11-10 · Cleaning minigames, piles removed (Jesse's feedback)
- **Why:** Jesse play-tested: "The cleaning and piles part are stupid and make it unfun. Cleaning should be fun little minigames that don't take up 30s. As for the piles, just delete them."
- **Piles removed:** `ItemPile`, `PileLayout` and pile pickup are gone. The Muddle scatters every item on its own (kinds shuffled together) over floors, rugs, counters and tables. A `ScatterGrid` (spatial hash) keeps 4,000 items from landing inside each other with O(1) checks. Tall and slab-like items still tip over onto their thinnest side (`ScatterRules.ShouldLieDown`). One press picks up one item; run placement still fills a fixture from what you carry.
- **Cleaning is two quick minigames** (`CleanGame`), replacing hold-and-move scrubbing (input look lock removed):
  - **Dust bunnies** on every fixture: E releases 4–6 fluffy bunnies with googly eyes on the fixture's front row (facing the player, at reachable heights). Look and tap E to poof each one, with a dust puff and a bell that climbs a step per poof in a row. Bunnies hop to a new spot every 1.6–2.6 s. The last poof cleans the fixture and opens its slots. About 5 s in total.
  - **Wipe timing bar** for windows, counter and table tops (3 hits) and floor smudges (2 hits): tap E when the sliding marker is in the gold zone. Each hit moves the zone and speeds the marker up a little; misses cost nothing. Looking away pauses it (dirt progress is kept). About 3–5 s. Logic in `TimingBar` (tested), UI in `WipeBarView`.
- Dust looks (tint, dust films, smudges), clean-before-stocking, the checklist line and saving are unchanged.

## 2026-11-10 · Step 4c · The big Fantasy shop (3,336 items)
- **Size:** the room grows from 8×7 m to 18×14 m (3.8 m ceiling), with 20 fixtures and 38 new kinds in `fantasy_shop.csv`, each with its own slot type and none of them living:
  - 8 potions, 6 crystals, 4 candles, 4 wands, 4 spellbooks, 4 herb jars
  - snow globes, teacups and rune stones for the round stands
  - 2 scrolls
- **Living items stay rare and special:** the original 20 items, including all 10 living ones (hoppers, floaters, shy and disguised), keep their two original fixtures (potion shelf, curiosity cabinet) in the middle of the north wall. Living items roam the whole room.
- **Layout:**
  - North wall: two potion walls (576 each), a spellbook case and a herb shelf.
  - West wall, under the windows: glass crystal cabinets and candle racks.
  - East wall: glass wand cases and scroll crates, with the counter (Madame Zephyrine, crystal-ball memory) by the door.
  - By the door: six round curio stands.
  - The middle stays open (rug, cauldron, display table, giant mushroom) for the mess.
- **Performance** (Editor, this PC): frame 4.1 ms, 8,600 draw calls, 2.1 M triangles, load about 2.6 s. Cleaning has 20 dust-bunny fixtures and 21 wipe spots.
- **Step 4 totals:** Bakery 3,998, Library 4,840, Fantasy shop 3,336, so 12,174 items across the slice.

## 2026-11-12 · New art direction: warm semi-realistic, still all in-house
- **Decision (Lowie):** get "as close as possible" to the reference screenshots (a grand wooden library with balconies and chandeliers, a book-strewn desk by a frosty window, a museum hall with a dinosaur skeleton and flowers) **without real assets**. This replaces the 2026-11-10 "procedural storybook" look.
- **Plan, in steps with before/after stills:** (1) mood lighting, (2) richer materials, (3) an architecture kit (wainscoting, columns, mouldings, ceilings, chandeliers, frames), (4) generated book covers and item detail.

## 2026-11-12 · Art step 1 · Mood lighting
- **`InteriorMood`** (runtime, per location): locations load additively, so lighting belongs to the City scene. While a location is loaded, this swaps in the interior's ambient (trilight), haze (exponential fog) and reflection intensity, enables its post volume and renders its box-projected reflection probe once; it restores the street's settings on unload.
- **Colour space:** ambient and fog colours are given in sRGB, but the project renders in linear, so they land roughly half as bright and more saturated. Ambient colours are therefore light and only gently tinted.
- **`MoodAssets` + `MoodPreset`** (editor): per-location presets (Bakery golden morning, Library amber evening, Fantasy shop candle-warm with purple shadows) build a VolumeProfile (ACES tonemapping, colour adjustments, white balance, shadows/midtones/highlights, bloom, vignette).
- **Lamps** become wide downlights (spot 150°) in warm white, six per room with soft shadows. That means no ceiling hot spots, and a spot's shadow is one shadow map instead of a point light's six.
- **Windows:** frosty glass that blooms, plus faked daylight (an additive beam and a window-shaped bright patch on the floor), since the walls are solid.
- **Shell shadows:** the room shell now casts shadows, so the street sun no longer lights interiors through walls and roof.
- **Materials and camera:** varnished wood and trim (smoothness 0.55), polished floors (0.42); SMAA anti-aliasing on the player camera. Palettes are richer: the Library has deep green walls and mahogany, the Fantasy shop deep plum walls.
- **Performance** (Editor, this PC): Bakery 4.4 ms, Library 5.7 ms, Fantasy shop 4.5 ms per frame.

## 2026-11-12 · Art step 2 · Richer materials
- **New generated texture sets** (`ProceduralTextures`, albedo plus normal map, tileable):
  - Parquet: basket-weave squares of four boards, per-board tone, grain, seams.
  - Wainscot: raised panel with stile, groove and bevel.
  - Damask: mirrored flourish with half-drop motifs on faint stripes.
  - Brick: running bond, 14 courses per metre tile, light mortar, per-brick tone and speckle.
  - StarPanels: the first full-colour set, a gilded coffer frame around a navy field of gold four-pointed stars.
- **`LocationPalette.UseRichSurfaces`** swaps wall, ceiling and floor materials and adds a **wainscot band**: 1 m of raised-panel wood round the lower walls with a chair rail, built by `BuildWainscot`. Box UVs are centred, so the panel texture is offset half a tile.
- **The starry ceiling glows faintly** (its albedo doubles as emission at 55%), as a painted ceiling catching bounced lamplight. Otherwise the stars disappear under downlights.
- **Per location:**
  - Bakery: warm brick, plank ceiling, terracotta tiles, painted teal panelling.
  - Library: green damask, starry coffered ceiling, parquet, mahogany panelling.
  - Fantasy shop: plum damask, lilac-tinted starry ceiling, parquet, dark wood panelling.
- **Performance** is unchanged at 4.3–5.7 ms per frame.

## 2026-11-12 · Art step 3 · Architecture kit
- **`ArchitectureKit`** (editor, all generated) builds multi-part pieces as one mesh with a submesh per material, so a room full of them stays cheap. Wall pieces follow the window convention: local -Z faces the room. The pieces:
  - **Chandeliers** replace pendant lamps (`BuildLamp` delegates to it): chain, turned brass body, a ring of six candles with glowing flames, and a hidden lamp light that the mood turns into the shadowed downlight. They hang lower in tall rooms.
  - **Wall sconces:** a brass bracket and candle with a small unshadowed point light that washes the wall. They're named "Sconce Light", so the mood leaves them alone; it only tunes lights named "Light".
  - **Pilasters** (base, shaft, flutes, moulded capital) between the windows and beside the door, with sconces on them. Sconces also sit above the north wall's tall fixtures.
  - **Crown moulding:** a real profile swept along the walls (`MeshGen.Extrude`) instead of a flat strip.
  - **Framed pictures** in gilt frames, with generated pictures: golden-hour landscapes (Bakery), parchment city maps with streets, river and compass rose (Library, after the reference), starry nights over rooftops (Fantasy shop).
  - **Bookcase crowns:** every Bookcase-style fixture gets a carved crown along the front and sides.
- **The Library is now two storeys** (6.4 m) with a **gallery** at 3 m round three walls:
  - a deck with a moulded fascia and brackets
  - a turned balustrade, merged into one mesh per run
  - 17 full-height decorative bookcases with seeded filler books, merged per case
  - Decor only; the player stays downstairs.
- **`LocationPalette.Architecture`** (default on) switches all of this; `GalleryHeight` stops pilasters under a gallery.
- **Performance** (Editor, this PC): Bakery 4.7 ms / 7.7 k draws / 3.4 M tris; Library 6.3 ms / 16 k draws / 6.9 M tris (the gallery's books); Fantasy shop 4.6 ms / 9.2 k draws / 2.4 M tris. Fine on PC. For Quest, the gallery books would need a cheaper version (a texture or impostor).

## 2026-11-12 · Art step 4 · Book covers and item detail
- **Books (`book_lite`):**
  - The cover is a cloth-weave material in the kind's colour, with edges rubbed lighter.
  - One **shared detail material** (`Book_Detail`, alpha-cut, metallic map) carries a generated 2×2 atlas: two gold-foil cover designs (double frame and diamond emblem; corner flourishes and a round crest with a star; both with "lettering"), spine bands with a title panel, and the cream page block.
  - Foil quads sit about 1 mm above front and back covers and the spine. Each book picks a design by a stable hash of its id.
  - Pages and foil share the detail material, so a book is still **two draws**. The atlas keeps alpha coverage in its mips, so foil doesn't vanish at a distance.
- **Potions:** clear glass bottles (transparent, full-strength reflections) holding a separate coloured liquid, filled to about two thirds and glowing faintly, plus a cork. **Crystals** glow softly from inside.
- **Baked goods** (loaf, baguette, croissant, bun, bagel, cookie, pie) use a crust texture (mottled bake, flour flecks) tinted per kind.
- **Bug fixed in `MeshAssets.GetOrCreate`:** regenerated meshes were copied into existing assets with `EditorUtility.CopySerialized`. That updated the asset data but not the copy already on the GPU, so the old shape kept rendering (no foil, bottles without potion) until the asset reloaded. Meshes are now copied through the Mesh API, which refreshes the GPU copy; the asset keeps its GUID.
- **Performance** is unchanged: Bakery 5.1 ms / 7.7 k draws, Library 6.3 ms / 16 k, Fantasy shop 5.5 ms / 10 k.

## 2026-11-13 · A living street
- **Why (Lowie):** the street with the three shops looked boring; make it more alive and part of the world.
- **`StreetBuilder`** (editor, all generated, called by `CityGreyboxBuilder`):
  - **Both sides built up:** townhouses at both ends of the shop row, and a full south row of seven houses replacing the grass verge (south boundary now at the house fronts).
  - **House details:** plinth, string courses, carved cornice, pitched roof, chimneys (some smoking), shuttered windows (some lit, some evening-dim), flower boxes, the odd balcony, a door with a lantern.
  - **Ground floors with character:** a café with a striped awning and a terrace of tables and parasols, the post office with a red postbox (a nod to the future City Post), a florist with buckets of flowers. Blade signs hang over the pavement.
  - **The street goes on:** a stone archway at the east end and the clock-tower square at the west, with more houses beyond both ends fading into haze. Each view is closed by a big building (a town hall with a clock turret to the east, a tall row to the west), and ground runs on under the whole town.
  - **Skyline:** house blocks, a church spire and a copper dome above the roofs.
  - **Bunting and string lights** criss-cross the street. Pennants are two separate faces so both sides are lit.
  - **Props:** a fountain with spray on the square, a fruit stall, barrels and crates, planter trees along the south pavement, laundry lines across the alleys. The shopfronts gained a cornice and door lanterns.
- **Life** (runtime, `TidyUp.Ambience`):
  - `PigeonFlock`: pigeons hop and peck, scatter and circle when the player comes within 3 m, and land again.
  - `Stroller`: two townsfolk walk the pavements, pausing to look about.
  - Particles: chimney smoke drifting down the street, falling leaves, fountain spray.
- **Light:** golden late afternoon. A low warm sun with soft shadows, warm trilight ambient, exponential haze (0.016) and the `Street` post volume (priority 10; interiors override it with priority 50).
- **Cost:** about 3.5 ms per frame, 5 k draws, 2 M tris on the street (Editor, this PC).

## 2026-11-13 · Mega town: mega locations streamed in rooms (Bakery first)
- **Why (Jesse):** the buildings felt too small; locations should be massive, like "Tidy Up a Library". The setting is a **mega town** (a city reads as modern), and every location is a mega place, big outside too.
- **Architecture:** a location is a **shell scene** plus **zone scenes** (one per room).
  - The shell (`Location_Bakery`) holds the building: walls, doorways, light, mood, the NPC and memory, and a `MegaLocation`.
  - `MegaLocation` streams the zones through Addressables. A room loads within 20 m of its edge and unloads 8 m past that. A room is never unloaded while its items are carried, and the room the player stands in is the active location (HUD, Tidy Sense, hints).
  - Each zone scene is a full location set (progress, Muddle, hints, cleaning). Its `LocationRuntime` has a `zoneId` and saves under `"bakery/<zone>"`.
- **Grand total:** `LocationStateStore.PlacedCount("bakery")` sums every `"bakery/…"` key. `ZoneInfo.TotalSlots` is recorded at build time, so the total is known before a room is visited. Restoration (the baker and the memory at 100%) follows the grand total. The save-slot summary adds the rooms up.
- **Hard-binding kept:** every kind of item lives in exactly one room. Items never change scene; carried items are set down by the street door when leaving.
- **Building map** (`MapView`, on the HUD; built at runtime from the zone rectangles, so any mega location gets one):
  - a corner minimap that follows the player
  - M (gamepad Back) for the full floor plan: rooms, doorways, the street door, and each room's progress
- **Mega Bakery** (`MegaBakeryBuilder`): a 60 × 40 m ground floor with 6 m ceilings and six 20 × 20 m rooms, 11,060 slots in total (Jesse chose ~10k):
  - Shop Hall: cake cases, pie stands, the sales counter
  - Bread Hall: loaf walls, bread racks
  - Pastry Salon: pastry cases, café tables
  - Cake & Cookie Room: cookie counters, cake stands
  - Pantry: jars, sacks
  - Bakehouse: roll racks, ovens, workbenches
  - Signed doorways, offset so long sightlines don't show an unloaded room. Wall fixtures rise to about 4 m with stocked decorative upper shelves (`ShelfSettings.FillerBaskets`). Upper floors come later.
- **Outside:** the Grand Bakery closes the west end of the street, behind a small square:
  - 32 m wide, three storeys over a 6 m ground floor
  - an entrance pavilion with a pediment, an arched double door up two steps, arched shop windows with awnings, dormers, smoking chimneys
  - the street now runs on to it; the old bakery's spot is a townhouse, and the clock tower moved to the side of the square
- **Cost (Editor):** 5–8 ms per frame. Draw calls run 10–16 k with 3–4 rooms loaded, so batching items is the next performance step.
- **Fix (same day):** the street's extension toward the Grand Bakery (x -23 to -29.5) was a visual slab without a collider, so walking to the Bakery dropped the player into the void. It's walkable now; a raycast sweep along the whole walk finds solid ground. Safety net: `FirstPersonController` remembers the last solid ground and puts the player back there if they ever fall below y = -25 (and logs a warning, so any new gap shows up).
- **Bigger fixtures and more decor (same day; Jesse: "the cabinets are too tiny … fill the building with more decorations or space to store items"):**
  - Fixtures are sized for the 6 m halls: glass counters on 0.6 m bases, loaf walls and cabinets with about 2 m of reachable shelves and stocked shelves above to about 4 m, roll trolleys and bread racks about 2 m tall. Islands fill the empty floor.
  - The Bakery now holds **19,965 items** (3.0–3.8k per room).
  - Decor:
    - the building: ceiling beams, a painted frieze along the partitions, paintings beside every doorway
    - the rooms: rugs, potted plants, chalkboards, a wedding cake centrepiece, a harvest table, barrels and crates, flour sacks, copper pans over the workbenches, dried herbs in the pantry
  - **Cost (Editor):** 5–11 ms per frame with 3–4 rooms loaded, peaking at 27k draw calls. Batching the loose items (instanced rendering or merged static meshes for slotted items) is the next performance step before more mega locations.
- **Visual fixes (same day):**
  - Two flower pots in the Shop Hall floated where islands used to be; removed.
  - The chalkboards' boards were placed with world coordinates under an offset parent (black rectangles far from their easels); the ones by the street door also faced the wall.
  - Dust patches sat exactly level with the rug borders and flickered; raised to 0.026 m.
  - The Muddle now keeps out of decorations: `MuddleScatterer` has keep-clear bounds (built per room from the standing decor, plus the ovens), so no items end up inside sacks, cakes, plants or ovens.

## 2026-11-24 · Bug fixes and performance for the mega Bakery
- **Dust bunnies from the side:** bunnies only appeared on shelf faces turned toward the player. Standing beside a cabinet left no spot, and the cabinet cleaned itself. Spots now fall back to the faces seen at an angle, then to any face. The minigame is never skipped.
- **Inventory strip:** shows at most five kinds (the selected kind and its neighbours in cycle order), with "‹ n more" and "n more ›" for the rest, so it can't run off the screen.
- **Held stack:**
  - capped by height (0.22 m in camera space), so ten cakes no longer tower into the view
  - held items shrunk to 0.13 m
  - the base fixed low on the right
  - the corner minimap moved to the top right, under the controls
- **Lag spikes** (measured with the new `FrameSpikeMonitor`, development builds only, and perf logs):
  - Each room load froze one frame for 1.2–3 s: Unity activates a 3,000-item scene in a single frame, and that can't be spread out. Unloads cost about 100 ms.
  - Fix: a mega location of up to 30,000 items (`MegaLocation._keepAllLoadedUpTo`) loads every room behind the entry fade (shown as "Tidying the way in…") and never unloads while you're inside. Bigger locations still stream rooms by distance.
  - Result: no spikes while walking (before: 34 spikes of up to 3 s). Entering takes about 11 s in the Editor; a built game will be faster.
- **Steady frame cost:**
  - Items were almost all of it: 20k item renderers, with several draws each.
  - The **GPU Resident Drawer** is on (URP `PC_RPAsset`, Instanced Drawing; Graphics "BatchRendererGroup Variants" set to Keep All so builds keep the shaders). At the busiest spot, draw calls fell from 24k to about 3–5k and triangles from 11.8M to 4.3M; frames sit at about 9–11 ms in the Editor.
  - Its GPU occlusion culling stays **off**: it broke the held-items overlay camera (flat cyan shapes). The held stack also opts out of the drawer (the Disallow GPU Driven Rendering component, added by type name because the class is internal).
  - `SceneBatcher` statically batches the fixed parts of every location scene when it loads (Unity only does this automatically for scenes in the build).
  - `ItemDrawDistance` skips items more than 30 m from the camera.
  - The decorative stock baskets cast no shadows.
  - The hint-ghost check caches slot positions and checks 600 slots per frame instead of every slot every 0.2 s.
- **Minimap update (same day; Jesse chose: nicer look, show shelves, rotate with me):**
  - **Look:** a parchment floor plan in a wooden frame with rounded corners (sprites generated at runtime, no art assets), soft room colours (the room you're in brighter), and an N that circles the rim. Under it: the room you're in, its progress, and "M full map".
  - **Shelves:** every fixture of each loaded room is drawn to scale from its geometry, warm wood when clean and dusty grey while it still needs cleaning (checked twice a second).
  - **Rotation:** the minimap turns so your view always points up, with a fixed arrow in the middle. The full map stays north-up with a turning arrow, and its room names sit on parchment cards above the fixtures.
