# Controller aiming · 摇杆瞄准

Right-stick aiming lets the Ranger and Arcanist retreat while shooting forward, and lets every profession set the direction of a future attack independently of walking. It reuses the existing Core explicit-aim/action-snapshot path. Damage, attack/skill/dodge timings, energy costs, movement, Q buffering/contact, accepted appearance and mature content remain unchanged.

## Player controls

Open Pause → Controller aiming. Try pushing the right stick right and up in the paused direction-check panel. Choose Standard stick or Alternate stick if the directions do not match; Invert vertical corrects an upside-down response. Movement aiming disables the new right-stick behavior and retains the original movement-directed controller aim. The left stick still moves the hero. X attacks, Y uses the profession technique, and A dodges along movement (or aim while standing).

A stick movement outside the 0.25 radial center zone acquires a direction. Releasing the right stick retains that direction, so walking backward does not turn the next arrow or spell backward. A stationary outlined cross shows the live direction, including where HUD panels would otherwise hide it. It does not imply target lock, a projectile origin or a damage radius. Existing critical warning witnesses retain drawing priority.

Deliberate keyboard activity or mouse movement/click takes over. A fresh controller button retains the original same-frame pad priority when the pointer also moves, including into the HUD. After desktop takeover, menus, application/focus pause, travel, load or death, a still-held right stick must return to center before it can acquire a new direction. This prevents lingering menu or old-world input from steering a new world. Prepared attacks and buffered Q continue along their captured direction/target even when the live aim changes.

## Persistence and integration

Only zero-default `controllerAimProfile` and `controllerAimInvertY` are added to SaveData; version 2 and all existing fields/counters/flags remain. Old v1/v2 saves use Standard stick without inversion. Actual acquisitions are transient, and new adventures retain the selected preferences. Invalid saved profiles fall back to Standard. Settings writes are restricted to pause/controls and refuse death or invalid profiles.

`ProjectSettings/InputManager.asset` appends four joystick axes; every original byte remains. Standard uses device axes 4/5 (serialized 3/4), Alternate uses 3/4 (serialized 2/3), with the existing up-positive virtual-axis convention converted to down-positive world y. The engine deadzone is zero for those new axes; Core performs finite-safe radial filtering. The [Unity 2022.3 Input Manager manual](https://docs.unity3d.com/2022.3/Documentation/Manual/class-InputManager.html) describes named joystick-axis queries and their device selection. These two selectable mappings are not a claim of universal controller compatibility.

`GameCore.WorldGeneration` increments only with the existing transient-world reset. It is not saved. The Runtime checks generation as well as room/save identity, covering same-room Enter and Load of the exact same SaveData object. Calibration reads the selected axes without acquiring world aim or advancing the paused simulation. No scene, package, art, font or existing GUID is replaced.

## Offline verification

- Core: **115/115** groups, including six new groups for radial/finite/overflow boundaries, guarded preferences, legacy saves, NewGame/travel/death/reused-world persistence, real retreat/shots/dodge rules and captured Q direction. Existing 109 groups remain.
- Controller Runtime: **6/6**, using actual Update, OnGUI, X/Y/A, both axis profiles/inversion, heading hold/drift rejection, desktop and fresh-pad handoff, original dash/cost, buffered Q, same-room/reused-save resets, SaveStore JSON/checksum and actual pause/title settings. The runner validates all four axis declarations and is included in CI.
- Existing combat Runtime: **14/14**; Watch **2/2**, Warden **3/3**, Kitchen **3/3**, Archive **3/3** also pass. Offline production/Editor/native-test-source compilation and **75** import rules pass.
- A real old-regression failure caught mouse-HUD movement incorrectly overriding a fresh pad attack; restoring the original pad priority makes the unchanged combat regression pass. A new same-room reset test failed before generation binding and passes after it. A disposable real-Update probe also confirms hitstop freezes prepared Q while later stick motion changes live aim without redirecting the captured five-arrow release.
- **96** fresh controller captures cover both languages, ordinary/large text and 960/1280/1920 displays, including explicitly staged HUD overlap. The approximate layout check across **689** retained states has zero estimated overflows; calibration/pause/reticle previews were inspected.
- Visual/resource checks: **121 PNGs**, **404 atlas cells**, **241 unique GUIDs**, existing subset-font coverage and LFS fsck pass. All **361** pre-existing art/font/scene/import files and exact Campaign bytes match `84ad2e8`. Four new source metadata files have unique IDs; all original input bytes remain.

Logs/captures stay in ignored `.starfall-checks`. Unity native compilation/import, platform builds, GPU output, physical device mappings and controller feel remain **NATIVE_UNVERIFIED**. Native JSON wrappers exist but were not run here. GitHub records the ordinary dev-branch push and exact-HEAD CI result.
