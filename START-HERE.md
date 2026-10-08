# 打开与升级工程

这是完整 Unity 工程源代码包，不是已构建的游戏安装包。

1. 解压后进入 `StarfallFrontier`，应同时看到 `Assets`、`Packages`、`ProjectSettings`。
2. 升级现有项目：关闭 Unity，备份项目，覆盖包内 `Assets`、`Packages` 与 `ProjectSettings`。**本轮也覆盖 `ProjectSettings/ProjectVersion.txt`**，去掉旧的 C1 和 revision 信息。
3. 使用国际版 **Unity 2022.3.53f1**；版本文件只有一行 `m_EditorVersion: 2022.3.53f1`。若使用其他已能运行的国际版 2022.3 LTS 补丁号，这一行填该版本的真实编号。新建项目选择 **2D / Built-In**，关闭后按上一步覆盖。添加工程时选择同时包含三大目录的根文件夹。
4. 打开 `Assets/Scenes/Frontier.unity` 后 Play，或使用 `Starfall → Play Adventure`。

无需删除原旅程存档或开始新游戏。旧自动/手动存档路径与产品名称保留。

本轮继续修复 Q/持续冰霜的属性变化、额外脉冲、同伴教学归属和死亡期间的深渊操作。记录见 [docs/DELAYED-SKILL-RECOVERY-BUGFIX.md](docs/DELAYED-SKILL-RECOVERY-BUGFIX.md)。此前读档、换装、弹体、死亡/训练、技能就绪灰色、特效偏移和人物尺寸修复继续保留。

本轮三个职业是同一个金发主角的装备版本，角色、敌人、八个 Boss、居民、地面、图标与特效一并升级。攻击有身体准备/释放动作，并与伤害接触时间同步。完整变化与离线预览见 [docs/VISUAL-COMBAT-UPGRADE.md](docs/VISUAL-COMBAT-UPGRADE.md)。

## 原生验收

使用 `Starfall → Visual QA → Open Review Lab` 打开独立测试场，选职业、地区与 Boss。可冻结、逐帧推进或解除冻结实战，测试场不写入旅程存档；退出恢复原旅程。截图保存到 `docs/native-captures`。

在 `Window → General → Test Runner` 中运行 EditMode 的 `Starfall.Tests` 与 PlayMode 的 `Starfall.VisualTests`。图形截图测试需要 Game View 可见，不使用 `-nographics`。

生产核心回归、离线运行时与布局、角色状态、像素边界、资源和导入规则的最终结果见 `docs/QA-REPORT.json`。本轮新增五组延迟伤害/归属/恢复检查，共62组核心回归；三职业18,000帧组合操作压力检查继续通过。全地区出生/潮水、36组深渊生成、屏障防绕过、人物尺寸/头部/握持、Boss流程、HUD遮挡和存档检查继续保留。**没有 Unity Editor/Player，因此 Unity 原生编译、实机画面、音频、手柄、性能与平台构建仍待验收。** 包内 PNG/GIF 均标为离线预览。
