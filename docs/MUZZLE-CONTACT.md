# Point-blank weapon contacts

Bow and staff shots used to begin collision detection at their visible weapon attachment. An enemy between the shooter's feet and that attachment could be skipped, particularly with free aim. The same gap applied to an ordinary archer shooting a nearby player.

At the original release event, a weapon projectile still captures its authored attachment, speed, target ray and impact profile. Before its first flight step, it resolves the short feet-to-muzzle corridor once. Terrain is checked in order, and actor contacts use the existing hit, piercing, slow and explosion rules. Launch contacts require the actor's center to be ahead of the prepared heading; overlapping actors behind the shooter do not become direct launch hits. Splash still follows its original area rules.

A close launch contact uses that prepared heading for recoil, armor approach and incoming-damage feedback. Surviving shots retain their original muzzle-to-target flight direction. No timers, costs, damage values, range, artwork, input bindings or save fields change. Shots created directly at a position, such as Boss orbs and existing hazard projectiles, keep their original flight behavior. Transient launch state is consumed once and is not saved.

Checks:

```sh
python3 tools/run_muzzle_contact_core_checks.py
python3 tools/run_muzzle_contact_checks.py
```

The checks exercise real production combat and the offline runtime input adapter. Unity SDK DLL compilation and offline OnGUI execution do not establish native Editor import, PlayMode, GPU presentation or hardware feel: **NATIVE_UNVERIFIED**.
