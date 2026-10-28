# Live talent growth comparisons

The original K page shows the current rank and current → next-rank benefits before spending one skill point. Its card, rank pips, Spend, technique and reviewed-reset controls retain their original positions and actions.

- **Edge:** raw opening contact damage, or damage per arrow for bows, and critical chance. The current weapon/style, mastery, upgrades, relics, health threshold, active stew and Abyss damage boons all retain their original composition. Flow bow values include its per-arrow coefficient. Criticals, armor, sword-chain finishers and splash are separate contact rules.
- **Resolve:** the actual stored maximum health and the next +12 maximum from the original Learn operation; the talent-only incoming-damage factor is shown as a percentage. Difficulty, guard, relics, Abyss mitigation and source-specific meal protection still apply separately. The original +12 current-health gain is retained when learning.
- **Agility:** current walking speed and the initial cooldown of the next dodge. Existing profession, relic, cold, race and road-meal effects remain. Dodge travel, energy cost, duration and invulnerability are unchanged.

A rank-five talent displays equal current/next values and Max rank. Without spare points the next benefits remain visible while Spend stays disabled. Comparisons are immutable read-only snapshots: they do not learn, heal, refund, change equipment, advance simulation or recapture an already paid action. No save fields are added; version 1/2 saves reconstruct the values from their existing build.

The preview and actual action paths share the same math. Existing runtime wrappers keep their original current-rank behavior; hypothetical next-rank reads pass explicit ranks instead of temporarily editing the save.

## Validation

`tools/run_talent_growth_core_checks.py` covers real Learn, accepted contacts and critical profiles, incoming damage, walking, dodge clocks, paid-action/field/input-queue purity, capped ranks and legacy saves. `tools/run_talent_growth_checks.py` exercises shipped Update/OnGUI through K, mouse/keyboard/controller Spend, subsequent real combat, original reset flow and bilingual normal/large layouts. Both support immutable-baseline semantic failure checks. The four Core groups also have Unity NUnit wrappers, and the runtime check runs in GitHub Actions.

Offline C# execution and command-layout captures are **NATIVE_UNVERIFIED**: native Unity import, GPU rendering, controller hardware and play acceptance have not been established.
