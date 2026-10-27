# 材料来源地图 / Material source routes

背包 → 材料来源 → 选择已发现来源 → 查看地区地图，现在会保留具体来源，而不是显示另一条任务的目标。当前地区与远区均用绿色标记显示所选来源，并显示其名称、取得方式与当前可用状态；任务与个人路标不因查看而改变。

- **返回材料来源**：回到同一材料、同一来源和对应分页。地图中浏览其他地区不会误改所选来源。
- **设为路标**：来源在当前地区时，明确选择后设置原有个人路标，任务追踪保留。
- **前往并设路标**：远区先显示确认按钮，再次选择才按原快速旅行规则前往，并设置该来源的本地路标。它会替换旧个人路标，不会替换追踪任务。
- **快速旅行 / 返回本地区**：原按钮、输入和旅行规则保留，单独使用不会替你设置材料路标。Escape／手柄B仍返回世界；返回材料页使用专门按钮。
- 已采完的来源仍可查看，提示到旅店休息更新；不能为它设置新采集路标。未发现、未开放、深渊与练习中的来源不会因此公开或获得访问权限。

抵达后沿原导航步行，按E采集或向原店主购买。查看、标记与旅行都不会自动采集、交易、治疗、付费或改变原产出；旅行本身仍执行原有场景重建、探索与任务访问逻辑。

Material maps retain the selected charted source with a green marker, live identity and availability. Return to the same materials selection, explicitly set a local waypoint, or confirm **Go + waypoint** twice to use ordinary fast travel and pin that destination. Your tracked quest stays intact. The original fast-travel button remains separate and does not set a source waypoint. Escape/controller B still returns to the world.

## Implementation and validation

- Preview ownership is transient: actual GameCore, SaveData, world generation and current source authority must still match. Load/new game/training/death/other menus clear it. No save-schema fields, map objects, assets or existing GUIDs change.
- Queries re-resolve the canonical discovered source and current depletion/quotes. Merely drawing or selecting the map does not modify SaveData, exploration, personal pin or tracking.
- Region/page changes, changed source authority and menu exits cancel the new travel confirmation. Input scopes include preview identity, region and pending confirmation state; a prior activation cannot confirm a different destination.
- The route action validates safe original travel access, then resolves the source again in the arriving world before setting the existing persistent landmark pin. Local pinning does not require a scene reload.
- `python3 tools/run_material_route_core_checks.py`: pure/live descriptions, actual local/remote source routes and original yields/payments, stale/foreign/unsafe guards, prepared-action and old-save continuity.
- `python3 tools/run_material_route_checks.py`: actual offline Update/OnGUI inventory selection, correct map markers, return controls, explicit route confirmation, walking/E transactions, lifetime/confirmation cleanup, bilingual layouts and legacy saves. The immutable `ea18368` baseline compiles against the same final checks and fails semantically.
- Related material-source, gathering-pin, landmark, quest-map and input regressions, full Core, Unity SDK compilation and metadata checks supplement the focused tests.

Offline logic/input/layout checks and SDK DLL compilation do not establish native Unity Editor import, PlayMode, GPU/font rendering, hardware controller or player-build acceptance. **NATIVE_UNVERIFIED** remains.
