# 药田的第一份收成 / First Harvest

完成「清水到药田」并把侦察员送回镇里后，杏增加可选话题「药田的第一份收成」。这是一条不限时的邻里支线，沿用原药田、厨房与回镇侦察员；原有对话选项保留原顺序。

1. 向杏接取。接受时明确追踪新任务；也可在任务手帐第二页查看或重新追踪。
2. 在杏的原药田实际收获一季作物。接受前已经种下、接受后才收获的作物也算；背包原有药草、野外采集、先前收获和试灌苗床不会代替这次收获。原种植消耗、生长60秒、药草／种子／硬币收益及水渠、结局加成保留。旅店休息仍会推进作物生长。
3. 找镇里或旅店的阿茴包汤料，交药草2。对话显示持有量和还需数量，材料不足时不扣料、不关闭对话。汤料是任务交接标记，不是新增物品，不会用掉炖汤、凉茶或远行餐，也不治疗或刷新餐食效果。
4. 将汤料交给回镇的侦察员；再向杏汇报，一次获得硬币25、凉茶1。两项报酬都能完整放下时才发放；空间不足保留未领奖状态，可以稍后再来。

每个阶段可离开、保存、读取后继续。后续收获与交付不会强行替换玩家另选的追踪任务或材料路标；菜单提供明确的重新追踪按钮。完成后仍可重读这段邻里对话。

After the garden-water delivery and the scout's rescue, ask An to share a crop with the returned scout. Harvest a real garden crop, pay Ari two herbs to pack for soup, deliver the packet, then report to An for 25 coins and one tea, once. Town and inn Ari both support packing. Existing growing crops count, ordinary yields stay intact, and every phase can be saved. Short materials or full reward capacity leave the transaction unpaid and retryable.

## Implementation and verification

- Ordered progress uses existing `SaveData.flags`: `firstharvest_accept`, `firstharvest_harvested`, `firstharvest_packed`, `firstharvest_delivered`, `firstharvest_complete`. No save-schema/version changes. Earlier saves without these flags start unaccepted.
- Every handoff rechecks ordered progress, living safe world state, authored resident membership, the actual nearest E target, and legal actor/resident ground. NPC handoffs follow original E conversation range; combat line-of-sight is not substituted for that authority at house corners. Stale, remote, unsafe and repeated callbacks do not spend supplies or grant progress.
- The crop witness runs only after the original successful harvest. The original reward, timer and notification remain. No new map objects, collision, scenery, font or art assets are required; existing Unity GUIDs are unchanged.
- The new quest appends after all 22 previous journal identities. Guidance reads have no economy or progress side effects and target the actual next resident, including Ari at the inn when already there.
- `python3 tools/run_first_harvest_core_checks.py`: prerequisites, real crop yields, physical guards, exact payments/rewards, stale/repeated handoffs, capacity boundaries and all saved phases in v1/v2.
- `python3 tools/run_first_harvest_checks.py`: actual offline `FrontierRuntime.Update`/`OnGUI` input, planting/rest/harvest/delivery, rejected transactions, SaveStore continuity and localized journal/map/dialogue captures. `--baseline-ref 2821199` preserves semantic before/after evidence.
- Full Core, related town/story/navigation regressions, seven-stage Unity SDK DLL compilation and Unity metadata checks supplement the focused suites.

These are offline logic, input, layout and SDK compilation checks, not Unity Editor import, PlayMode, GPU rendering, controller hardware or native playtest acceptance. **NATIVE_UNVERIFIED** remains.
