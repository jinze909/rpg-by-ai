# Sen's shelter trail / 森的避风路

Sen's old clapper becomes a road marker that the player can use. This optional follow-up adds three physical waymarks to Frostchime Highland and a three-leg walking trial, followed by a lasting choice of sheltered route. The existing chime puzzle, Frost Prelate, mountain covenant, personal reward, patrols and map geometry remain intact. All graphics use accepted existing assets.

## Prepare and depart

Finish the mountain report, recruit Sen and complete his personal story (`mountain_report`, `companion_2`, `personal_2`). Talk to the actual keeper at `(15,6)` and choose **Sen's shelter trail** after the original gift, invitation and goodbye options. Accepting tracks the seventeenth quest without switching the selected companion or charging materials.

Light the original braziers through their original E interaction: each unlit brazier still costs **2 timber**. The three points are `(6,14)`, `(19,11)` and `(24,6)`. New departure choices append after the original warm/light and keep-traveling options. The current leg requires a lit source, a lit destination brazier when applicable, a quiet nearby area and no active Boss.

| Leg | Start | Read the physical waymark | Confirm arrival |
| --- | --- | --- | --- |
| 1 | Footpath brazier `(6,14)` | Lower trail `(12,13)` | Midpass brazier `(19,11)` |
| 2 | Midpass brazier `(19,11)` | Upper trail `(21,8)` | Highpass brazier `(24,6)` |
| 3 | Highpass brazier `(24,6)` | Return trail `(18,9)` | Sen `(15,6)` |

Begin each leg manually. Walk to its marker and press E to record it, then interact at the next stop to confirm arrival. The final stop is Sen rather than another brazier. No overall time limit, new material fee or required companion command applies. Nearby original foes remain active; clearing them is still necessary for safe recording and confirmation.

## Read the crosswind

Only an explicitly started leg creates trial weather. After 2.2 seconds, a horizontal or vertical wind lane locks to the player's position and announces contact for **1.4 seconds**. Its half-width is **0.6 tiles**, extending up to 6 tiles in either direction and stopping at terrain. Moving during the warning cannot retarget it. Subsequent lanes alternate orientation after a 2.2-second recovery.

Step across the lane's narrow edge or use the existing dodge. A lit brazier's original **3.5-tile radius** shelters the player from trial wind. Gold contact boundaries, mint shelter rings and the countdown remain visible under low effects, reduced motion and HUD overlap. The countdown occupies its own band above the action controls, away from notifications.

On standard difficulty before existing armor or protection, an exposed contact deals **8 environment damage**. A genuine surviving HP loss adds **7 cold**; a missed, sheltered or invulnerable contact does not add that cold. Existing difficulty, armor, guard, dodge and hit-reaction rules apply. Wind does not change ordinary enemy attacks, skill or dash timing, or chime operation.

The original climate rules remain: exposure adds **1.1 cold/second**, tea or a nearby lit brazier removes **8/second**, cold above 70 slows movement, and cold at 90 or higher can cause the original 4-damage climate pulse. Braziers and tea still matter between the markers.

## Choose whom the road serves

After confirming all three legs, manually return to Sen's appended story option and choose:

- **Shelter the descent**: a lower route connecting the camp-side approach `(5.5,17.5)`, footpath brazier, lower marker and midpass brazier.
- **Shelter the high road**: a route from the midpass brazier through the upper marker, highpass brazier and return marker to Sen.

The selected route is visibly traced in mint. Within its **0.9-tile band**, cold growth stops; this does not cool the player. Feet, terrain and line-of-sight checks prevent protection across a wall. The other route retains ordinary exposure. Tea and lit braziers retain their original cooling even on the sheltered route.

Either choice grants **35 coins, 4 shards and +1 Sen bond** (capped at 5) once. It grants no extra skill point, XP or tea and does not alter the original mountain choice or personal-story payment. Journey records, report rereads and the journal remember the decision.

## Saves and retries

Progress uses existing flags. SaveData, EnemyRecord, versions, inventory and map counters are unchanged. The original sixteen quest IDs retain their exact indices; `shelter` appends at index 16. Waymarks appear after actual acceptance.

Recorded markers and confirmed legs persist. An unfinished leg's warning and clock are transient: load, travel, death or practice cancels the attempt without completing it or starting a surprise storm. Return to that leg's source brazier and begin again manually. Already recorded markers need not be paid for or rediscovered. Departure, recording, arrival and settlement revalidate current location and world state. Stopping a leg grants no progress or reward; completed choices cannot switch or pay again.

## Validation boundary

Core simulation, production Update/input, SaveStore and recorded OnGUI checks provide offline evidence. **NATIVE_UNVERIFIED**: native Unity import, compile/build, rendering, audio and player/controller feel are not certified. The known Editor license limitation is recorded in AI_STATE.md; this milestone does not repeat that probe.
