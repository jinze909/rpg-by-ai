# Companion following beside houses

## Reproduced gap

On Emberfall's unchanged map, start at `(8, 6.5)` and select a companion. Walk east to `(9.5, 6.5)`, dodge north and finish walking to `(9.5, 3.63)`, then walk west to `(8, 3.63)`. The original follow anchor `(6.35, 4.18)` is legal for the full actor foot, but its cell center `(6.5, 4.5)` is inside the authored house. The old grid query rejects that center and falls back to a straight heading into the house. After twenty seconds the companion can remain about 1.8 tiles away, despite a legal walking detour.

## Behavior

The companion retains the original follow anchor, offsets, speed and 0.3-tile stopping distance. Normal legal-center goals continue using the existing direction query. For a legal anchor with an obstructed cell center, a companion-only query finds legal walking cells with a short, swept final approach to the exact anchor. The first and final legs check the full actor footprint. The companion still advances through the existing swept movement and distance-driven gait; it does not warp, gain speed or pass through a wall.

Enemy pursuit and return paths, authored maps, collision geometry, damage, automatic support, active commands and their planted preparation remain unchanged. No new persistent or transient follow cache is introduced, so current gates, tides and house geometry are read afresh. SaveData and its versions are unchanged.

## Verification

```sh
python3 tools/run_companion_follow_core_checks.py
python3 tools/run_companion_follow_checks.py
```

The baseline-compatible checks reproduce the authored traversal and exercise actual production following, input, collision, gait, action pauses and restoration. Mono checks, offline Update/OnGUI and Unity SDK DLL compilation are separate evidence from native Editor import, GPU rendering, PlayMode or hardware play. Native acceptance remains **NATIVE_UNVERIFIED**.
