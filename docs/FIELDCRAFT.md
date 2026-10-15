# Ada’s field notes / 艾妲的实战记录

Ada’s training now leads into an optional journey through real encounters. Recover the first starcore, return to Ada at the Emberfall training yard, and choose **Field notes / 实战记录**, then accept. The original practice and tactical sparring remain in their original numbered positions with their original rewards.

This is the eighteenth journal entry. Acceptance costs nothing and never changes the protagonist’s class, appearance rules, selected companion or equipment. All three classes can switch between the existing weapons and complete the notes.

## Demonstrate the weapons

Only actual damage from a basic attack after acceptance counts. The target must still be alive when contacted; a lethal hit counts. Class skills, companion attacks, safe practice, tactical sparring and town/inn targets do not count.

| Weapon | Field demonstration | Preparation |
| --- | --- | --- |
| Sword / 短剑 | Dodge through an incoming attack, then land a basic sword attack while the counter opportunity is active. | Watch for **COUNTER / 闪避反击**. Merely dodging, swinging or missing does not record it. |
| Bow / 猎弓 | One **Focused / 蓄势** basic arrow hits two distinct foes. | Line up two living targets. Separate arrows cannot combine their contacts. |
| Starfire staff / 星火杖 | One basic starfire blast hits three distinct foes. | Gather a group; the direct target counts alongside targets hit by the explosion. Separate blasts cannot combine. |

The original blacksmith crafts the bow for **4 timber and 2 parts**, and the staff for **4 crystals and 3 parts**. The first core unlocks the existing styles. Select weapons and styles through the inventory; learning a different class or changing its Q is unnecessary. The guide points to the forge when the next weapon is missing, then to current foes or the forest route.

A released attack retains its own weapon, style and counter identity. Changing gear after release cannot change what its hits demonstrate. Multiple simultaneous arrows retain separate records. The new feature changes no damage, enemy health, windup, recovery, cooldown, dash or input-buffer rules and spawns no enemies.

## Read the existing counter window

A successful dodge through a genuine incoming hit already opens a **1.5-second** counter opportunity, or **3 seconds** with the existing relevant relic. A new read-only HUD card shows the remaining time and the existing **+50% next basic attack** benefit. It works before accepting this optional quest and for all weapons.

Starting the next basic attack consumes the opportunity, as before. The quest’s sword record requires that captured counter attack to make actual contact. Menus, application pause, paused review, death and between-round sparring screens hide the live card. Its area passes aiming input through; existing action controls still protect their clicks. Threat overlap uses the established translucent HUD and foreground witness rules.

## Return to Ada

After all three notes, return manually to Ada and deliver them in a quiet nearby area. The one-time payment is **40 coins, 5 shards and 1 talent point**. There is no extra XP, repeat payout, equipment grant or automatic travel. The journal and journey records remember the demonstrations and handoff; the completed dialogue can be reread.

## Save compatibility

Progress uses five new flags in the existing save format. SaveData, EnemyRecord, version, inventory, map data and all original quest indices are unchanged. Old version1/2 saves acquire no notes or reward automatically.

Completed demonstrations persist across saves and travel. In-flight attacks and partial contacts are transient: load, travel, death or practice clears them. A fresh attack must demonstrate the full two-target arrow or three-target blast; contacts from earlier attempts never accumulate into one shot.

## Verification boundary

The new shared Core checks exercise real three-class combat, captured attack ownership, exclusions, local handoff, once-only payment, guide routes and legacy saves. The production runtime runner is `python3 tools/run_fieldcraft_checks.py`; it uses actual Update input, equipment/crafting menus, numbered Ada dialogue, SaveStore/checksum and recorded OnGUI commands against offline Unity declarations. Existing combat, story, map and save suites remain enabled in CI.

**NATIVE_UNVERIFIED**: these checks and approximate font/layout previews do not certify native Unity import, compilation/build, GPU rendering, audio, hardware input or player feel. The existing Editor license limitation remains recorded in AI_STATE.md.
