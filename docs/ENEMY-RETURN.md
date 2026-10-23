# 普通敌人脱战归位 / Returning to post

原区域（密林、遗迹、矿场、堡垒、雾港、沙海、高原）自然生成的普通敌人，发现玩家后仍追至10格。玩家脱离该范围时，它们会沿可行走路线返回本次区域生成的驻守位置，使用原步速、脚底碰撞和步行动画。尚未发现玩家的敌人仍在原位等候。

- 原警戒规则保留：8格内发现，已发现后追至10格。归位中再次进入8格会恢复追击；仅进入8到10格之间不会重新警戒。
- 归位保留受伤后的生命，不回血、不瞬移、不重生、不发放奖励，也不推进攻击冷却。到驻守点附近停止；路线封闭或阻塞时安全停留。
- 已锁定的攻击仍先完成。受击、受控与攻击释放锁仍先执行，归位不会取消、改瞄准或加速它们。
- Boss、援军、教学、切磋、试装、深渊和任务专用敌人沿用原行为。采用自然生成时明确启用归位的方式，不按敌人编号猜测任务身份。
- 存档仍只保存原有 `EnemyRecord` 字段。读档不改敌人已保存的位置、生命或初始警戒状态；与原自然生成点匹配的敌人恢复其驻守点，之后重新发现玩家便可开始新的追击与归位。

Original regional foes can now return to their posts after losing an acquired target beyond ten tiles. They walk the existing collision-safe path at their original speed and retain damage. Entering eight tiles reacquires the player. Returning grants no healing, respawn, rewards or free cooldown progress. Dedicated encounters and training keep their existing behavior.

## Verification

- `python3 tools/run_enemy_return_core_checks.py`: actual natural placements, terrain paths and speed, pursuit/attack priority, saved positions and reconstructed posts, dedicated-encounter exclusions.
- `python3 tools/run_enemy_return_checks.py`: production Update, held inputs, existing walk presentation, pause/read-only behavior and SaveStore. Immutable `--baseline-ref` exercises the old stationary behavior as a semantic negative control.
- Existing pursuit, archer escape, Boss commitment and full Core regressions guard combat compatibility. Unity 2022.3.53f1 DLL compilation checks all seven assembly stages.

No terrain, art, existing GUID, reward, SaveData or EnemyRecord schema changes. Offline behavior and captures do not certify native Editor import, GPU rendering, a player build or hardware play feel. **NATIVE_UNVERIFIED** remains.
