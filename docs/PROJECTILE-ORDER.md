# Projectile contact order

When one sampled bow or Starfire position overlaps multiple foes, the direct victim now follows the first expanded collision-circle boundary along that segment. Storage order no longer chooses the victim. This matters when enemies crowd the same lane: an off-axis foe with a nearer center can still have a later contact boundary.

Only actors admitted by the existing sample footprint, forward launch rule, hit identity and line of sight are ordered. Exact equal entries use enemy ID; duplicate IDs retain stable list order. Launch ordering follows the actual feet-to-attachment segment, while armor, recoil and damage feedback keep the prepared attack heading. Flying shots use their captured velocity. A stationary sample keeps its original order.

The existing sample spacing, terrain checks, impact position, attack timing, costs, critical rolls, piercing identities and 60% splash rule remain. Ordered targets still recheck membership after callbacks; encounter retirement stops the shot immediately. No saved fields or existing resource GUIDs change.

```sh
python3 tools/run_projectile_order_core_checks.py
python3 tools/run_projectile_order_checks.py
```

The Core runner also accepts `--source-root` for read-only baseline comparisons. Runtime checks accept `--baseline-ref`. The pre-fix baseline compiles and fails on actual victim selection. Offline input/OnGUI execution and Unity SDK DLL compilation are separate from native Editor import, PlayMode, GPU presentation and hardware feel: **NATIVE_UNVERIFIED**.
