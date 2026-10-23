# 清水到药田 / Clear Water for the Garden

完成杏的月光草委托与诺尔的水泵委托后，诺尔增加可选话题「清水到药田」。原委托报酬、工坊、种植、水渠工程与炉火共餐保留。

1. 找诺尔接下送水；不会再次收取零件或药草。
2. 到镇井东侧 `(20,11)` 的取水桶，按 E 交互并选择装水。
3. 到药田东侧 `(19,6)` 的试灌苗床，按 E 交互并选择试灌。
4. 找杏，选择「清水到药田」，交回水桶。一次获得种子3、凉茶1；空间不足时不消耗任何进度或部分发奖，腾出空间后再来。

任务接受后自动追踪，也可从日志第二页重新追踪。导航依次指向诺尔、取水桶、试灌苗床、杏。旅行路牌与杏保留原 E 交互；新地点仅在需要操作的阶段出现。试灌后留下独立湿润苗床，不改变原作物的60秒成长、收成或工程收益。每个阶段都可离开、存档、读档后继续，不限时。

The optional follow-up unlocks after An's medicine and Noll's pump commissions. Accept at Noll, fill the bucket east of the well, water the trial bed east of the garden, then return the bucket to An. Earn 3 seeds and 1 tea once. No repeated commission payment, crop changes or delivery timer. A full reward inventory leaves the handoff unpaid and retryable.

## Implementation and verification

- Progress uses existing `SaveData.flags`: `watergarden_accept`, `watergarden_drawn`, `watergarden_watered`, `watergarden_complete`. No schema/version changes; earlier saves start without the optional follow-up.
- Actions recheck ordered progress, living/quiet town world, authored object membership, nearest E target and clear path. No remote reward or synthetic inventory bucket.
- The quest ID is appended after every existing journal row. Original NPC choices retain their order; completed commission dialogue now acknowledges the medicine and repaired pump.
- World markers reuse code drawing and existing art; the original well and garden remain. Noto Sans SC 2.004 is extended with the existing subset script; all original glyph outlines and metrics must remain intact.
- Font source: [Noto's official SC subset OTF](https://github.com/notofonts/noto-cjk/blob/main/Sans/SubsetOTF/SC/NotoSansSC-Regular.otf), under the existing [OFL](licenses/Noto-OFL.txt). The extension retains all 1,214 previous glyphs unchanged and adds the 11 characters needed by this story.
- `python3 tools/run_water_garden_core_checks.py`: ordered physical handoffs, rejection/lifecycle checks, atomic rewards/old saves, original town economy/navigation.
- `python3 tools/run_water_garden_checks.py`: actual offline production input, menus, walking, journal/map, SaveStore and bilingual OnGUI captures. Supports immutable `--baseline-ref` semantic failure evidence.
- Full Core, related town/quest/runtime checks, Unity SDK seven-stage DLL compilation, metadata and font/resource checks supplement the focused tests.

These offline checks are not Unity Editor import, PlayMode, GPU rendering, controller hardware or player-build acceptance. **NATIVE_UNVERIFIED** remains.
