# 遗潮档案 · The Tide Archive

A new optional exploration chapter sits behind the entrance at `(10,14)` in Drowned Ruins. It adds a separate 36×24 map, three elbow-valve puzzles, a pressure catwalk, three recoverable records and a final defence encounter. Original Ruins puzzles, starcore, Boss, letters and travel routes remain in place. The archive uses the accepted Ruins tiles and props; it requires no replacement art, new scene, package or Inspector setup.

## Play route

1. Recover the second starcore (`core_2`), then enter from Drowned Ruins and inspect the waterworks plan at `(4,8)`. The entrance chamber is safe; its exit at `(3,18)` returns to Ruins.
2. Rotate the three elbow valves with E. Their two open ends must connect the source `(4,6)` through valves `(7,6)`, `(7,10)` and `(10,10)` to the outlet `(10,7)`. Rotation zero joins north/east, then each turn rotates clockwise. The connected solution is **2, 0, 3**. Completing the circuit permanently drains the western gate at rows 13–14; no fuel or consumable is required.
3. Cross the narrow central catwalk. Two pressure circles alternate at `(18.5,12.5)` and `(18.5,8.5)`: a 1.2-second warning precedes a single area strike; warning starts are 3.5 seconds apart. Entering the gallery starts with one second of grace. The following splash is cosmetic. Leaving or loading restarts a full warning; turning the far wheel disables this system permanently. Wait in the alcoves west of the upper circle or east of the lower circle, then move through during the quiet window. Turning the pressure wheel at `(21,6)` permanently stops the system and opens the eastern gate.
4. Read the water-level ledger `(27,5)`, evacuation route `(31,11)` and caretakers list `(27,17)` in any order. Recovering the third record awakens three ordinary defenders. They have distinct, reserved identities; leaving, saving or dying must preserve actual progress rather than generate new rewards or mark surviving foes defeated.
5. After the defence, use the archive desk `(31,19)`. Decide whether the names should be entrusted to the public guild archive or carried directly to their families. Both choices give **50 coins, 6 shards and 1 skill point**, once. Neither choice asserts that a missing person has returned or replaces Mira's original story.

The records describe rising water, evacuation priorities and the families who cared for separated children. Their collection appends lore entries **17–19**; all earlier entries keep their original indices and text. The two closing choices preserve that uncertainty and give equal mechanical rewards.

## Map and persistence contract

The western chamber occupies columns 2–11. Wall column 12 has one two-tile passage at rows 13–14. The central lower platform spans columns 13–22 and rows 13–15; a three-tile catwalk at columns 17–19 reaches the upper wheel platform. Its waiting alcoves span `(14..16,9..10)` and `(20..22,11..12)`. Wall column 23 has the eastern gate at rows 6–7. The eastern record room occupies columns 24–33. The spawn at `(4,18)` remains beside the exit.

Valves, drained water, disabled pressure, recovered records, defender defeat and the final choice use permanent progress records. New progress must remain compatible with old v1/v2 saves; old saves must not receive a completed chapter or lose existing inventory, actors, flags, lore or story choices. The transient pressure clock restarts with a full warning when returning or loading. World, terrain edges, local/world map and collision share the same permanent gate state. Reserved defenders use IDs 74000–74002; only their canonical normal-enemy roles may restore here, and foreign Boss/ordinary actors are rejected. The ordinary chapter enemy allocator remains independent. Saving during the defence must retain living defender HP and completed deaths. The final reward cannot be farmed by travel or repeated dialogue.

The main character remains the accepted blond Viola, and no mature map, quest, Boss, system or Unity GUID is removed or replaced.

## Verification status

Offline validation is complete:

- `tools/run_core_tests.py`: **109/109** groups, including eleven Archive groups. These exercise real valve actions, complete-footprint chamber/alcove paths, mobility/power bypass rejection, full starter combat for all three classes, exclusive once rewards, live quest routing, pressure contact/pause/reset, survivor HP, death recovery, malformed actor ownership and old v1/v2 map-counter prefixes. All previous 98 groups remain.
- `tools/run_archive_checks.py`: **3/3** production Runtime groups using actual E/numbered choices, J/Q/Space, thirteen visible journal rows, map page selection/travel and the real SaveStore JSON/checksum. Input battles use unchanged starter stats; staged warning/world views are identified separately. Both languages, ordinary/large text, 960 minimal/reduced-motion and 1920 rich-effect views are covered. Existing combat **14/14**, Watch **2/2**, Warden **3/3** and Kitchen **3/3** Runtime groups also pass.
- `tools/check_editor_syntax.py`: production/Editor/native-test sources compile against offline declarations; **75** texture import rules pass. CI now includes the Archive Runtime runner.
- Visual/resource checks: **121 PNGs**, **404 atlas cells**, **237 unique GUIDs**, existing font coverage and LFS integrity pass. All **358** pre-existing art/font/scene/import files match `78689c6` by bytes or decoded LFS content SHA. Packages, Unity version and SaveData schema are unchanged. Maps 0–9, their object prefixes, original Boss/items and lore 0–16 retain their exact contents.
- **264** fresh Archive GUI captures were generated. The approximate layout check reports zero estimated text overflow across 593 retained states. Network, pulse, map, journal, vault and both closing-choice previews were inspected. The pressure boundary has 32 bright marks with dark backing at the actual damage radius, an external countdown plate and the existing HUD-overlap witness; it stays readable with minimal effects and reduced motion.

Disposable logs/captures stay under ignored `.starfall-checks`; no CLI task artifacts are added to the repository. GitHub records the normal branch push and exact-HEAD CI result.

Unity native compilation, platform builds, GPU output, audio, controller play and player feel remain **NATIVE_UNVERIFIED**. Offline checks are separate evidence and must not be represented as a Unity-native playtest.
