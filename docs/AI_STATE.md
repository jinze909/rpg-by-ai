# Starfall Frontier — CLI state

- Contract: `docs/AI_CONTRACT.md` v1.5 read completely on 2026-10-11; initialization complete.
- Branch: `codex-cli/dev`, origin `jinze909/rpg-by-ai`; main remains at `b0aae3d329c5b0b200b30b1457d0f692257b9f06`.
- Verified baseline: `f86b618d54cb0bef273aee59da7d9045c0aa4c28`; recovery ref `refs/codex/checkpoints/2026-10-11-initialized` retains that commit.
- Baseline evidence: clean working tree; LFS fsck OK, all 92 files materialized; Frontier build scene and scene-loaded runtime bootstrap verified; Campaign has ten maps, seven configured Bosses plus the abyss Boss, seventeen lore records. All 62 production Core regression groups passed with the existing Mono compiler/runtime.
- Completed goal: Q/Y and clickable HUD take priority over held attacks; one 0.20s simulation-time skill intent joins recovery while preserving contact poses, pressed direction and target. Invalid world/profile boundaries discard it; resources and combat values are captured at actual start. Variable frame intervals and exact deadline tolerance are covered.
- Last trusted gameplay commit: the commit containing this state (`git log -1 --format=%H -- docs/AI_STATE.md`); initialization checkpoint `302078b01a06f5e76ad39128f8a9ec0d86dd0dfa` is also retained.
- Fresh verification: 67/67 production Core groups and 5/5 production Runtime input groups passed; offline Editor/native-test-source syntax and texture import rules passed; diff check clean. Regression witnesses fail against pre-fix Core/Runtime, and an independent boundary probe passed. CI now runs the focused runtime input harness.
- Native status: `NATIVE_UNVERIFIED`; offline C# checks do not certify Unity compilation, playback, feel, graphics or audio. No repeated Editor probes.
- Current goal / next: fix hidden scouts producing interaction prompts and tutorial credit before/after rescue. Keep the original rescue/gift/story/save behavior, share presence between drawing and queries, and run targeted checks. No implementation work remains on the Q change; push/CI outcome is recorded by Git/GitHub.
