# 主角完整帧与原生动画路径

2026-10-15；基线 `fed5782`，恢复点 `refs/codex/checkpoints/hero-animation-baseline-fed5782`。本次只替换主角显示实现，保留原角色、美术、动作时钟、碰撞、输入与存档。

## 已确认的割裂根因

正式 `Frontier.unity` 由 `FrontierRuntime.Boot` 创建游戏。此前 `DrawPlayerActor → HumanActor` 把完整动作图格切成四块，再挖掉估算的头部区域，贴入 `WalkVanguard` 的固定头部。该矩形还包含领口、手臂、肩甲及盾牌像素：游侠的弓弦被切掉，法师会混入战士肩甲，背向领口也会错配。旧测试把固定头部来源当作正确性的依据，无法拦截这个问题。

现有十张已接受图集本身都是同一金发 Viola 的完整装备/动作帧。此次不替换、重新打包或重绘任何美术，不改 `POSE-REPAIRS`、`ACTOR-REGISTRATION` 及原资源 GUID。取消主角矩形拼接，直接使用每个已接受的完整 256×256 图格；同伴原实现保留。

## 正式显示路径

`GameCore → HeroAnimationState.Select → Animator → SpriteRenderer → 透明纹理 → 原世界排序位置`。

- `HeroAnimationState` 只读取现有 `AppearanceWeapon/PlayerPose/VisualFacing/WalkFrame`。普攻/Q 接触、装备归属、距离迈步、最终闪避、受击与死亡朝向仍由原 Core 决定；没有新增动画事件、伤害结算、计时器或存档字段。
- `HeroAnimationAssets` 在 Editor 首次导入、进入 Play 和预构建时生成 `Resources/Frontier/HeroAnimationLibrary.asset` 与 `HeroWholeBody.controller`。十套完整帧、脚底 pivot `(128,16)`（图像上方坐标为 `(128,240)`）、PPU=1，共用一个 SpriteRenderer。真实 AnimationClip 的唯一曲线是 `m_Sprite`；禁止 root motion、自动过渡、状态行为与事件。重生成保留生成资产已有的 `.meta`，不修改任何原图导入器。
- Animator 停在 `speed=0`，按 Core 选定的状态和帧采样，不独立推动游戏。完整画布仍以 192×192 显示，原脚底位置、0.75 像素密度、船、阴影、符文和防御圈保留。
- 当前世界地形由 IMGUI 绘制，会覆盖普通屏幕相机。因此原生角色先由隔离相机渲染，再在原来已排序的玩家槽位合成。身体、整帧描边和受击闪光共用同一图格及翻转；渲染器只在显式相机 Render 时启用，避免在主相机重影。纹理缓存避免 IMGUI 多遍重复渲染，退出时释放自有对象。初始化失败时明确显示错误、停止更新且不写存档；该边界已有真实 RED→green 回归。
- Unity `SpriteFrag` 输出预乘 RGB，GUI 使用普通 alpha 合成。局部 shader 将透明中间纹理转换为 straight alpha，再交给原 GUI 位置；不改全局材质或现有世界排序。
- 离线检查编译时没有 Unity 平台符号，使用同一 selector 的完整图格提交分支。该分支不是原生 Animator 的模拟器，也不能证明真实 GPU 输出。

## 后续补强：运行中失败与 Player 编译隔离

基线 `75a73fb`，恢复点 `refs/codex/checkpoints/hero-failure-baseline-75a73fb`。原实现只在初始化阶段阻止失败后的模拟；若运行中完整主角绘制抛出异常，OnGUI 会退出，后续 Update 和自动保存仍可能继续。本次让完整主角绘制以专用异常保留原始原因，由 OnGUI 显示同一错误并遮盖当帧的残缺世界。沿用原错误状态阻止 Update、手动/自动/生命周期保存；不捕获或改写其他世界/UI 异常。

离线故障注入先在原代码复现第四组失败，修复后覆盖三个职业、实际 OnGUI/Update/Save、存档和备份字节、GUI 状态恢复及无关地形错误继续抛出。注入发生在离线整帧绘制边界，不能证明原生 GPU 故障已经重现。正常动作、原美术、场景、Core 和存档结构未改。

真实 Unity DLL 检查新增两个 Player 阶段：仅定义 Unity 平台/版本符号，剔除 Editor、测试与 Cecil 程序集，Player Runtime 仅依赖 Player Core。原五阶段保留。CI 另以六项纯编译计划测试阻断 Editor/Test 引用泄漏；实际 SDK 编译仍需安装的 Editor DLL。独立探针已确认同一 EditorWindow 依赖在 Editor 上下文可编译，在 Player 上下文被真实编译器拒绝。这是编译分支检查，不是 Player 构建。

本轮定向验证通过：主角 Runtime 4/4（含完整原异常堆栈、失焦/暂停/退出存档保护）、原战斗输入14/14、A–E 七项负向控制、六项编译计划测试、七阶段真实 DLL 编译；Core191/191、离线 Editor 语法和75项导入规则、276对元数据与7项检查器测试、LFS fsck 通过。475个基线保护文件（含276份旧 metadata、87项 LFS 资源）保持原字节或 OID/大小。日志与报告只保留于忽略目录 `.starfall-checks/hero-failure-*`。

## A–E 检查的范围

| 类别 | 本次检查 | 不能据此宣称 |
| --- | --- | --- |
| A 资源与身份 | 120 个完整帧、20 张匹配遮罩、原 Viola SHA、图集 SHA/GUID、修复来源、完整 PNG 解码及导入配置 | 仅凭哈希证明人体美术质量 |
| B 完整身体 | 192 次生产输入后的实际绘制记录；唯一完整 UV、原像素、尺寸、脚底与十张图集覆盖；拒绝头身再拼接 | 离线 draw-call 重建等于 GPU 截图 |
| C 动作衔接 | 四组 Core + 四组 Runtime：J/Q 接触、换装备收招、闪避末步、受击/死亡、暂停与 hitstop；保留原全部战斗断言 | 修改过攻击数值、dash 时机或输入缓冲 |
| D 正式接线 | Frontier 启动、真实 Animator/SpriteRenderer、整帧曲线、透明合成、alpha 转换、Editor/预构建门槛、运行中失败边界 | 静态源码检查等于原生场景运行 |
| E 负向控制 | 在临时副本注入头身拼接、错误接触姿态、关闭原生路径、跳过 alpha 转换、绕过运行中失败边界、LFS 指针和损坏 PNG，七项必须逐项被拒绝 | 未执行的原生测试通过 |

`tools/check_hero_animation.py` 是阻断式离线 CI；任一类别或负向控制失败即非零退出。它明确输出 `NATIVE_UNVERIFIED`，不把 D 的静态接线结果伪装成原生验收。现有 CI 所有游戏回归继续保留。

## 验证与原生剩余项

最初重构交付时实际通过：Core 191/191、成熟 Runtime 69/69、新主角 Runtime 4/4、156 组主角绘制与既有角色/Continuity 检查、A–E 六项负向控制、当时五阶段真实 Unity DLL 绑定编译、75 项导入规则、272 个唯一 GUID 与7项元数据单测。465 个受保护基线文件（含265个旧 metadata）保持原字节或 LFS OID/大小，LFS fsck 通过。汇总回归脚本曾收到退出143；保留原中断证据后，已按独立进程补齐全部未完成检查，没有把中断伪报为成功。

离线命令：

```sh
python3 tools/run_core_tests.py
python3 tools/check_hero_animation.py --report .starfall-checks/hero-animation-gates.json
python3 tools/check_editor_syntax.py
python3 tools/check_unity_metadata.py
```

CI 用 Pillow 解码 PNG，定向展开原 44 张 ActorMasks 和四张已接受修复原图，避免把 LFS 指针误当图像。既有战斗、任务、地图、存档回归及 ActorRegistration/Continuity 继续运行。旧“必须叠加战士头部”断言已替换为更严格的唯一整帧、原脚底、明确动作来源断言；原输入/接触/装备/死亡断言均保留。

已安装 Unity 的 Linux 环境可额外运行真实 DLL 绑定编译，无需启动 Editor：

```sh
python3 tools/check_hero_unity_bindings.py --editor /path/to/Editor/Unity \
  --report .starfall-checks/hero-unity-bindings.json
```

该命令现编译 Core、Runtime、Editor、Core 原生包装、PlayMode 测试，以及隔离的 Player Core/Runtime，共七阶段。使用安装目录内真实 Unity DLL，测试阶段另用真实 NUnit/TestRunner DLL，不使用离线 Unity 声明。它不导入资产、不编译 shader、不运行测试、不构建 Player。

**NATIVE_UNVERIFIED**：2026-10-15 的隔离 Unity2022.3.53f1 探针在进入项目方法前退出 1，日志为 `No ULF license found` / `No valid Unity Editor license found`。当前没有连接的 Editor；GitHub 也没有可用原生许可证 secret。生成的 library/controller 尚未由 Editor 实际落盘；不手写伪造生成资产来冒充导入成功。

许可证可用后，在交互式 Editor 打开正式 `Assets/Scenes/Frontier.unity`，检查无导入/生成错误，运行 `FrontierHeroNativeChecks` 四组测试：完整帧/pivot、实际 Frontier 启动与三职业四向攻击/闪避、所有图格正反向 Animator/相机 RGBA 与半透明合成、缓存/销毁生命周期。这些测试使用 `WaitForEndOfFrame` 和 GPU readback，需要显示 Game View；不能当成已经支持 `-batchmode -nographics` 的测试。

随后实看三个职业的 Walk→Attack→Walk、Q、闪避、受击和倒地，检查不同背景、HUD/树冠遮挡、窗口缩放及半透明边缘，构建并运行 Player，记录帧耗与 RenderTexture 成本。原生 RGBA 回读断言目前覆盖身体白色及 0.5 透明度；outline/flash 的像素与最终 GUI framebuffer 尚未回读验证，受击 1.5 倍 RGB 在最终 ARGB32 纹理上的提前夹断也须实查。原法师侧向静止/行走素材仍有持杖侧差异，未擅自换回已否决美术；需原生动作审阅判断下一步。完成这些证据前，本专项原生验收仍未完成。
