# 材料来源 / Material sources

按 **I → 材料来源**，查看当前所选材料的取得方式。六个分类为木材、星晶、药草、银鳍鱼、零件与种子；从其他背包物品进入时，默认查看木材。

来源只列出**已经开放、已经探索**的规范地图对象，当前地区排在前面。尚未探索的位置和锁定地区不会因打开手册而出现；两处河岸练钓点还需要原教学任务已经接取。每页五项，使用原键盘／手柄菜单焦点或鼠标选择。

| 材料 | 现有获取方式 |
| --- | --- |
| 木材 | 木堆每次 +3；向阿茴购买每次 +4，按当前声望显示原有 10／8 硬币价格。 |
| 星晶、零件 | 同一个矿点每次同时给予星晶 +3、零件 +3，采矿悬赏仍只计一次。 |
| 药草 | 野外每次 +3；药田用 1 粒种子，60 秒后按水渠等级与原结局增益收获，另得种子 +2、硬币 +8。 |
| 银鳍鱼 | 老潮处或已接取教学的河岸钓点，三次成功收线后获得鱼 +1、硬币 +4，无鱼饵费用；原失败与容量规则保留。 |
| 种子 | 向杏以 5 硬币买 3 粒，或从原药田收获得到 2 粒。 |

已采完的节点仍可查看，并说明到旅店休息后更新。药田已种植时显示实际成熟倒计时；所有价格和说明随当前存档状态重新读取，查看本身不会种植、收获、购买或推进生长。

选择**当前地区**的可用来源后，按“设为路标”使用原有个人路线与 E／手柄 B 交互路径。只浏览或选行不会设路标。采完所设资源点后，原采集路标规则会恢复任务导航；居民等普通地点的路标保留，可从原本地路标页清除。

异地区来源只能“查看地区地图”，不会自动旅行或设远程路标。前往仍使用原地图的快速旅行安全条件或正常行走；到达后再从手册设本地路标。Esc／手柄 B 返回背包，LB／RB 沿背包所属菜单切换。

不新增存档字段或版本，不改变产出、费用、任务计数、报酬、地图碰撞或资源刷新。路标复用已有 `landmark_pin:<room>:<id>`；来源列表与选页状态不保存。旧 v1/v2 存档沿用原迁移流程。

验证入口：`tools/run_material_source_core_checks.py` 与 `tools/run_material_source_checks.py`。离线生产 Core、Update／OnGUI、实际交互、SaveStore 与 SDK DLL 检查不等于 Unity Editor 原生试玩：**NATIVE_UNVERIFIED**。

Open **I → Material sources** for charted gathering, gardening, fishing and trading locations. Current-region sources can be explicitly pinned; other regions open the existing map without travelling. Prices and yields follow the existing rules. Viewing reveals no unexplored location, spends nothing and leaves the tracked quest intact. Existing waypoint persistence and v1/v2 saves are preserved. Native play remains **NATIVE_UNVERIFIED**.
