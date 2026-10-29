# 补给缺料与来源 / Supply ingredient sources

在阿茴的原炖汤／凉茶对话、营地凉茶对话或「批量备制补给」中读完当前对话后，如果某项配方因缺料而不可用，左侧会出现不带编号的「查找材料」按钮。旁边列出**第一个缺料配方的成品与数量**、所缺材料及准确差额。例如炖汤 ×5 按药草10、鱼5计算；这不表示材料足够的炖汤 ×1 也不可制作。

入口复用原配方的实时检查：存放空间不足、硬币不足、无效的零数量「最多」选项，以及营地本来不能制作的炖汤，都不会被当作材料短缺。原对话的剧情、编号、按钮位置、配方费用、产出与交易检查保留；「查找材料」只提供路线，不会替玩家制作、购买、治疗或使用补给。

## 操作与返回

1. 用鼠标或原 Tab／方向键、Enter／手柄 A 焦点操作选择「查找材料」。材料来源页默认选中该配方的第一项缺料，也可使用原六类材料分页与分类。
2. 来源仍只列出已开放、已探索的规范地点，保留实际产出、价格和资源枯竭说明。仅浏览不花费、不采集、不揭示地点，也不替换当前追踪任务。
3. 按「返回对话」、Esc 或手柄 B，回到同一段原对话及已读页；仍需主动选择原配方才会交易。未补齐材料的原按钮继续禁用，补齐后仍按原费用结算。
4. 可以继续使用原材料地图预览；从地图返回材料来源，再返回对话，不会因为预览而旅行或付费。主动设置路标仍使用原个人路标规则，任务追踪保留。
5. 明确选择地图中的原「前往并设路标」流程后，会按原规则离开对话；远区旅行仍需单独确认。抵达后不会恢复远处的旧对话。

返回信息只保存在本次运行时。它要求同一游戏实例、存档实例、世界代次、地区、玩家位置、原对话及原交互对象与位置仍然有效。读档、重新生成地区、移动、换到另一段对话或切换无关菜单后，旧返回入口失效，材料页沿原规则返回背包。普通 **I → 材料来源** 的背包入口保持原返回方式。

不新增存档字段或版本，不修改成熟任务、报酬、地图、角色身份或资源引用；旧 v1/v2 存档仍由原迁移流程读取。

## 验证边界

定向入口为 `tools/run_supply_ingredient_core_checks.py` 与 `tools/run_supply_ingredient_checks.py`。检查覆盖真实 E 交互、原编号交易、鼠标／键盘／手柄菜单输入、缺料差额、地图往返、旧返回状态拒绝、原始旧档与校验存档，以及离线 OnGUI 布局。

这些生产 Core、Update／OnGUI 和存档检查不能替代 Unity Editor 原生导入、GPU、字体渲染或硬件试玩：**NATIVE_UNVERIFIED**。

After reading an original cook, camp-tea or Pantry preparation dialogue, **Find ingredients** shows the first ingredient-blocked recipe and its exact missing amounts. It opens the existing charted material catalogue without spending, revealing locations or changing the tracked quest. Back restores the same valid conversation and read page; map previews can return through the catalogue. Deliberate routing, reloads, movement and unrelated navigation retire the old dialogue return. Original action numbers, recipes, payments and save schema remain unchanged. Native play remains **NATIVE_UNVERIFIED**.
