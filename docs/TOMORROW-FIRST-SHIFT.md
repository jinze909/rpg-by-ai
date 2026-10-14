# Tomorrow's first shift / 明天的第一班

An optional playable follow-up to Ivo's repaired shared-water machine. It uses the existing Sunwaste map, receiver, engineer, oasis shade and camp; the original mirror puzzle, water covenant and personal repair remain intact. No art, map geometry or protagonist assets are replaced.

## Join the trial

Finish the desert report, recruit Ivo and complete his personal repair (`desert_report`, `companion_1`, `personal_1`). Talk to the actual engineer in Sunwaste at `(22,18)` and select **Tomorrow's first shift / 明天的第一班**. The existing gift, travel invitation and goodbye remain first. Accepting tracks the new story without changing the selected companion or charging materials.

Go to the eastern receiver at `(29,17)`, press **E**, then choose **Begin Tomorrow's trial**. The original mirror instructions and first close option remain available. Start requires a quiet nearby area and no other active Boss; existing patrols must still be dealt with. Acceptance does not spawn an encounter automatically.

## Pressure test

The fixed trial guardian stands on dry ground at `(29,13)`, with 260 HP. Use the normal equipped attacks, Q, guard, dodge and companion actions. Its three patterns repeat in order:

| Pattern | Announcement and response |
| --- | --- |
| Pressure beam | An 8-tile beam lane locks toward the position at preparation, stopping at obstructing terrain. Leave its 0.55-tile half-width before release. Preparation: 1.15 seconds; damage: 20. |
| Marked release | A 1.3-tile circle locks to the position at preparation. Leave it before release. Preparation: 1.3 seconds; damage: 22. |
| Charge check | A 1.6-tile circle locks to the position at preparation. Move near the receiver and press E while its displayed operation window is open, or leave the circle. Preparation: 1.8 seconds; unvented damage: 24. |

E near the receiver operates the valve during the charge warning while more than 0.35 seconds remain. It stays in the live world, cancels that charge and gives a counterattack opening with feedback. The existing stun and recovery clocks govern the resulting pause; no exact two-second duration is promised. Early, late, remote, repeated or paused operations cannot cancel other moves or grant victory. The optional vent is useful but is not a completion prerequisite.

Warnings retain their captured geometry through hits and health-phase changes. Reactions cannot displace this stationary machine. Contact boundaries are redrawn over foreground scenery and, when necessary, over the HUD. The operation marker, hint and remaining window remain available with reduced motion and minimum effects. Existing chapter Boss timing and balance are unchanged.

## Put the repair to work

Defeat records test success without ordinary Boss money, XP, mastery, materials, bounty kills, chapter flags or gates. Return to Ivo manually to arrange the first shift:

- **Oasis shade**: the original western shade at `(8,17)`.
- **Desert camp**: the original campfire at `(6,12)`.

Both decisions pay **35 coins, 4 shards and +1 Ivo bond** (capped at 5) once. They preserve the original desert decision and personal-repair reward. The selected point supplies **one tea per in-game day**, collected through its actual interaction; the story completes upon the first cup. A full tea stock does not consume that day's portion. The other shade and the unchosen delivery point retain their original behavior.

The oasis still gives its cooling advice and passive cooling. The camp retains all six original actions, brewing costs and waystation requirements, with shared water appended as a seventh action. Report rereads, journal guidance and journey moments remember the selected first shift. Existing rest time and daily rules are unchanged.

## Saves and retries

The quest appends after all fifteen existing journal IDs. Progress uses existing flags and enemy snapshots; SaveData, EnemyRecord and save versions are unchanged. Pre-feature v1/v2 saves invent no acceptance, success or payment.

A valid same-room snapshot retains surviving HP on the canonical machine and restarts with a fresh, full warning. Wrong role, nonfinite values, wrong location and foreign-map/Abyss records cannot restore this reserved actor or leak into chapter rewards. Travel or death abandons an unfinished attempt without completing it; return and start explicitly for a fresh full-health retry. Nearby mature patrols retain their normal respawn rules. Completed trials cannot restart or reverse their paid decision.

## Validation boundary

Core simulation, production Update/input, recorded OnGUI layout and SaveStore checks are offline evidence. **NATIVE_UNVERIFIED**: Unity import/compile/build, GPU rendering, sound, difficulty and player/controller feel have not been verified in a native Editor. The existing missing Editor license and lack of a connected MCP Editor remain recorded in AI_STATE.md.
