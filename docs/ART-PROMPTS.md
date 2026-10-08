# 原创视觉素材

本次使用内置图像生成制作以下项目素材。标题与肖像不包含文字；菜单和排版由实际游戏代码完成。

## 场景道具图集

项目路径：`Assets/Resources/Frontier/Art/WorldProps.png`

透明图集为四列两行，通过 UV 读取大树、两种房屋、石拱、灯柱、棕榈、雪松与灯塔。背景透明度已检查。

最终提示词：

Use case: stylized-concept. Asset type: production pixel RPG environment sprite atlas. ONE image, landscape aspect ratio exactly 2:1. Exactly FOUR equal square columns by TWO equal square rows, eight equal cells, NO gaps, no dividers. TRANSPARENT BACKGROUND, genuinely alpha transparent around all sprites. Each cell contains ONE complete original game prop, standing upright, bottom-centered at 94 percent cell height, with a very subtle contact shadow. Never touch a cell boundary. Top row left to right: 1 lush large oak tree with an irregular layered leaf crown and visible warm brown trunk; 2 cozy frontier cottage with terracotta tiled roof, stone plaster walls and warm lantern window, seen from slightly above; 3 seaside cottage with slate blue tiled roof and wooden beams, seen from slightly above; 4 weathered ancient stone arch ruin with small luminous teal star crystal. Bottom row left to right: 5 ornate slim wooden and bronze lantern post with golden glass lamp; 6 lush desert date palm with curved trunk; 7 snowy layered evergreen pine with dark blue-green needles and snow clusters; 8 compact old stone harbor lighthouse, warm beacon and bronze roof. Style: beautiful highly detailed HANDCRAFTED PIXEL GAME ART, crisp blocky pixel clusters, clean readable silhouette, three-quarter top-down RPG game view, no isometric diamond floor, no cast floor plane, consistent upper-left warm illumination, coherent teal and indigo shadow palette, muted natural materials with amber highlights, enough texture to feel like a modern premium pixel RPG. Objects must be isolated on alpha transparency, not eight full scenes. Exactly eight props, in the precise 4 by 2 grid. No text, no labels, no frames, no watermark, no scenery outside individual props. Keep base centers aligned so a game can render cells via UV coordinates.


## 标题画面

项目路径：`Assets/Resources/Frontier/Art/TitleVista.png`

最终提示词：

Use case: stylized-concept. Asset type: original pixel RPG title-screen background, widescreen 16:10 landscape. Create a polished richly layered pixel painting for the original game Starfall Frontier. An atmospheric coastal frontier at twilight with a sheltered seaside town, warm lantern windows, lush sculpted trees and grasses, an ancient starlit ruin beyond the bay, glass-colored desert mesas in the distance and snowy bell towers on the far horizon. One tiny adventurer in a teal cloak overlooks the landscape on the RIGHT half. Strong clear chunky pixel clusters, hand-painted sprite-like detail, warm amber lighting against deep indigo and sea-green shadows, crisp silhouettes, fine water reflections, depth through distinct foreground/middle/background layers. Left 45 percent must be dark quiet terrain and dusk sky with spacious negative space suitable for a UI title and menu overlaid in code. Put the beautiful landmark scene and visual focus on the right. No typography, no logos, no borders, no UI elements, no watermarks. Original scene and characters.

## 八人肖像图集

项目路径：`Assets/Resources/Frontier/Art/Portraits.png`

这是上一阶段的四列两行图集。前三格错误地把不同职业绘制成不同人物，**当前不再用于主角**；米拉、伊沃、森、莉雅、厄洛的格仍用于对应NPC。新主角只依据实际 `ViolaMoves.png` 的奶油金短发、侧分额发、翘发、浅肤年轻圆脸与原基础服装。

最终提示词：

Use case: stylized-concept. Asset type: production-ready portrait atlas for an original pixel RPG. Create ONE wide image, aspect ratio exactly 2:1, tiled into EXACTLY FOUR equal square columns by TWO equal square rows, with zero gaps, zero outlines around cells, no labels or text. Each square contains one distinct original fantasy character's centered chest-up portrait, fully inside its own cell, against the same solid dark navy background. Every portrait uses the same scale, clear face, high quality detailed pixel painting with crisp blocky clusters, warm highlights, deep teal and indigo shadows, rich expressive eyes and readable silhouette. TOP ROW from left to right: 1 young female vanguard with auburn bob hair and bronze armor; 2 young ranger with olive-green hood and sandy hair; 3 young arcanist with dark purple hair and a teal star brooch; 4 Mira, a confident caring middle-aged female harbor captain with reddish hair and navy scarf. BOTTOM ROW from left to right: 5 Ivo, a young male desert mechanic with tan skin, curly hair, brass goggles and a sand-colored coat; 6 Sen, an elderly mountain keeper with white beard, fur collar and blue robes; 7 Lyra, a young guild steward with dark blue hair, a pale embroidered coat and a golden letter pin; 8 Orrin, a stern male starwarden with silver hair, violet armor and a small old scar. Ensure exactly eight original portraits in this precise 4 by 2 atlas arrangement. No text, no captions, no watermarks, no other faces, no borders.

## 当前统一升级：来源与提示约束摘要

下表是本轮提示约束的摘要，不是旧职业画像的延伸。制作先查看原可操控角色及现有 WorldProps，所有主角图都要求保持同一奶油金短发、侧分额发、翘发、浅肤年轻圆脸、粉色内衫与棕色基础服装。装备变动不得换人。共同环境方向为有组织的像素色块、左上暖光、青灰/靛蓝阴影、克制饱和度与可读轮廓。

原始图：`Assets/ArtSource/Frontier`；运行图：`Assets/Resources/Frontier/Art`。所有本轮位图均使用内置图像生成制作；`tools/pack_visual_atlases.py` 仅按透明度分区/身体连通边界或透明沟槽切格、最近邻规范化尺寸、统一锚点，不用程序重新绘画或虚构动画帧。技术记录见 [ATLAS-IMPORT.json](ATLAS-IMPORT.json)，SHA-256见 [VISUAL-ASSETS.json](VISUAL-ASSETS.json)。

| 文件 | 图集与内容约束 |
|---|---|
| PlayerVanguard | 4×3透明动作格；同一金发主角穿铜铁轻甲、粉袖与棕色内层、剑盾；待机/背面/两步行走/攻击准备与释放/战技/闪避/格挡/受击/倒地 |
| PlayerRanger | 同一身份与12姿态，绿色轻披风、猎弓与箭袋；弓动作包含准备拉弦与放箭，不遮住发型 |
| PlayerArcanist | 同一身份与12姿态，青蓝法袍、星饰与法杖；聚能与释放有身体及手臂动作 |
| PlayerPortraits | 三个对应装备版本的胸像，脸、年龄、发型、发色、肤色与身份相同，深蓝背景，无字 |
| PlayerRear | 4×3透明格，每行一职业；北向迈步A/B、准备和释放，身份及装备对应基础图集 |
| PlayerSouth | 4×3透明格，每行一职业；面向镜头的普攻准备/释放、战技准备/释放，武器朝向与向南判定匹配 |
| Monsters | 6×3透明格；低矮软泥、窄身持弓掠夺者、宽重矿卫各一行；待机、移动、前摇、释放、受击、死亡，三种结构不能只换色 |
| Bosses | 4×8透明格；8个既有Boss分别待机、准备、释放、死亡；枝角兽/潮壳蟹/齿轮监工/星甲统领/潮壳海兽/琉璃守卫/霜钟执事/地脉收割者独立轮廓与材质 |
| Npcs | 4×4透明格，16位居民；既有人物身份、同行者、教官、使者、船客、守卫与旅客，用发型、衣服和工具区分，统一比例与光照 |
| FieldProps | 4×4透明格，营帐、机关、镜面、晶体、火盆、路牌、井及室内家具；与现有房屋、树、灯柱方向一致 |
| Details | 4×4透明格，草丛/花/苔藓/碎石/箱桶/骨骸/倒木/残柱等地域装饰；低对比，留出道路和人物阅读空间 |
| Terrain | 8×2材质格；草、林地、石路、水、沙、雪、木地板、墙石及特殊地面等16种材料；真正绘制纹理与块面，无文字 |
| TerrainVariants | 4×4材质格；草地、森林土、石路、水面各四种变化；比初稿降低细碎噪声及对比，避免装饰压过人物 |
| Effects | 4×4透明格；剑弧、命中、火花、聚能、冲击、冰霜、箭/法球、拖尾、消散等16种；像素材质、短拖尾、克制光量，预警另由代码绘制 |
| Icons | 4×4透明格；剑、弓、法杖、炖汤、茶、同伴、闪避与资源等16种；与装备材质及金/青界面配色一致 |

各图格已接入真实运行时，不需要在 Unity Sprite Editor 手动切图。原始生成网格的沟槽可能不是等宽，运行图已经标准化。后续重画必须先查看 [ART-BIBLE.md](ART-BIBLE.md) 及原 `ViolaMoves.png`，不得重新启用旧职业肖像来定义主角。

## 握持与方向纠错批次

每次编辑先查看实际运行格和相邻动作，要求透明背景、完整身体、相同装备/手侧、闭合手握柄、连续手臂、没有多余肢体或武器；北/南向必须真正向上/下出手。法杖在身体一侧，不能穿入胸前；弓弦与准备时单支搭弦箭对应，释放后空手；剑盾角色始终一剑一盾。收到结果后人工检查，再指定运行格替换，不能把提示词当成通过验收。

| 作者图 | 接受的纠错内容 |
|---|---|
| RepairVanguardWalk | 先锋侧向迈步B，闭合手持侧置单剑与单盾 |
| RepairMageBackIdle / RepairMageRear | 星术师北向待机和释放，屏幕左手闭合握杖、另一手施法，与北向移动一致 |
| RepairRangerSouth | 4格南向弓准备/释放/战技，向下瞄准，正确搭弦与放弦 |
| EnemyArcherVertical / RepairArcherHit | 北/南向真实射击、倒地，以及连续手臂握弓的受击替换 |
| CompanionMira / RepairCompanionMira | 红辫海港队长的12姿态；定向修复侧向待机、北向准备及背向迈步B |
| CompanionIvo | 棕发护目镜机械师的12姿态，单弓与箭袋；指令复用已核对释放格，否决悬空弓格 |
| CompanionSen / RepairCompanionSen | 白发白须守钟人的12姿态；修复北向两段施法与背向迈步，单法杖一致握持 |
| RepairBoats | 完整单桅泊船和竞速帆船，船体/帆/绳索连接，无彩色悬空碎片 |

先锋/游侠/北向/南向基础图也进行整图编辑，随后以 [POSE-REPAIRS.json](POSE-REPAIRS.json) 覆盖剩余否决格。源文件中保留作者结果用于追溯，最终验收针对运行图集。本文记录约束摘要，实际图片、接受格与哈希随工程提供。
