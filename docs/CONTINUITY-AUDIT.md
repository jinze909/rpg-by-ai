# Continuity audit — 2026-10-06

Baseline: the previously delivered complete GUI/effect fix package (de633779), recovered before editing. The older workspace snapshot was not used as the base for this pass.

## Actual execution path

`Frontier.unity` starts `FrontierRuntime`, which simulates `GameCore` and draws the world and interface through OnGUI. World positions, collision sweeps, actor poses, projectiles, and contact events are code driven. This scene does not use the legacy SampleScene Animator, Rigidbody, Tilemap, or Canvas controllers. Changes to those unused components would not fix the reported gameplay.

The original playable blonde `ViolaMoves.png` remains the canonical identity reference. Existing runtime class, companion, monster and boss atlases are inspected before changing their animation. Class portraits do not redefine the hero.

## Confirmed shared causes

| Chain | Existing cause | Required correction |
| --- | --- | --- |
| Movement / animation | Input is treated as movement even against walls. Walking frame selection uses total elapsed time. Full-body attacks replace walking while translation continues. Bosses have no moving pose. | Measure actual swept displacement, drive gait by distance, plant feet for full-body preparation, make melee steps collision aware, preserve a fixed foot anchor. |
| Aim / direction | Movement overwrites aim; Q ignores pointer aim. Visuals lock action direction but several spawns and enemy releases calculate their own direction. | One aim input conversion and one immutable action direction, shared muzzle data and contact phase. |
| Projectile / damage | Missiles test only their end position; splash and some area attacks ignore walls. Effects and corpses keep ticking behind paused menus. | Swept flight/contact checks, line-of-sight area checks, shared simulation clock and finishing effects. |
| Camera / environment | Fractional camera projection resamples point-filtered art. Trees translate their complete sprite. Ground decoration disappears when actors approach. | A common pixel grid for all world drawing; fixed roots and independent canopies; persistent ground detail. |
| Audio | A fixed timer emits a bright synthesized step on the main effects source, including blocked movement. | Quiet material-specific foot contacts on a separate channel, driven by gait distance. |
| HUD / input | Large opaque status, task, minimap and boss panels cover the battlefield. Pointer blocking uses oversized hard-coded regions rather than drawn controls. | Compact combat layout, context fading/actor visibility, shared drawing and hit-test rectangles. |
| Level gates | Only column 26 above row 13 is blocked; the arena can be entered from the south. Drawing is narrower than its collider. | Protect the entire northeast starcore arena and draw a matching west/south boundary; retain the lower harbor rescue route. |

## Content constraints

No additional classes, maps, monsters or gameplay modes. Keep map rows, quests, balance, rewards and saves. Harbor's castaway at (28,18) must remain reachable before the arena opens. The protected starcore region is x=[26,34), y=[2,14); sealing the entire east side would break that rescue objective. Hidden caches already have enclosing wall tiles and sealed access; verify them rather than replacing their layout.

## Verification limits and evidence

No Unity Editor or Player executable is installed in this environment. The initial recovered baseline passes all 28 production Core regression groups. This pass will execute the actual Core and runtime drawing against offline fixtures, render the recorded production OnGUI calls, and provide native review/test entry points. Offline captures are not represented as a native Unity playtest, audio audition, or a GPU rendering certification. The user's missing screenshot paths cannot be opened; conclusions above are from the available project code and atlases.
