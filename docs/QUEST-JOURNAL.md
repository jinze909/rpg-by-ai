# Quest journal views

The Quests tab adds All, Available and Completed views, plus **Show tracked quest**. All preserves the original 23 quest IDs, ordering and 19-row pages. Available includes quests whose existing guide is neither locked nor complete, including unlocked stories that have not yet been accepted. Completed follows the guide’s actual completion status.

Rows retain their canonical quest index. Selecting a filtered row still opens that quest’s original details, Track quest and Show destination actions. Show tracked quest selects the saved tracked ID and its page; if the active view hides it, the journal returns to All. An unknown saved ID displays the main story without rewriting the save.

The views refresh from existing story state. A queued Enter/controller confirmation belongs to its drawn quest selection and exact visible membership; a changed selection, filter, tab, owner or live row mapping retires that pending input. Deliberate view changes retain focus for the next fresh key. An empty view explains how to return and disables Track quest and Show destination instead of leaving stale task details. Filtering, paging and locating a tracked quest do not accept tasks, change tracking, travel, award rewards, advance simulation or alter a prepared attack. Explicit tracking and destination preview retain their original rules.

The selected view belongs to the current GameCore and SaveData instance and resets to All when either changes. It adds no save fields or migration requirements. Records, Factions and Journey tabs retain their original content and controls.

## Verification

- `python3 tools/run_quest_journal_core_checks.py`: live canonical status, original story transitions, read purity and version 1/2 save continuity.
- `python3 tools/run_quest_journal_checks.py`: shipped Update/OnGUI controls, mouse/keyboard/controller navigation, filtered canonical actions, empty and live views, second-page tracked destinations, owner changes and paused paid attacks. Captures cover Chinese/English, normal/large text and 960/1280 layouts.
- Unity NUnit wrappers retain the same four Core groups. The offline runtime check is included in GitHub Actions.

These checks execute production C# against the offline harness. They do not establish native Unity import, GPU rendering, hardware input or play acceptance: **NATIVE_UNVERIFIED**.
