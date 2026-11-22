# P5 牌堆界面重做计划

参考：P5R 暂停菜单实机逐帧截图 `docs/p5/preview/p5r-20261121-223943-frames-02-12/`；P3 牌堆（分支 `claude/admiring-knuth-lqj1x7` 的 `mod/src/Patches/PilePatches.cs`、`mod/src/Vfx/PileFx.cs`、`HeroStage.cs` 里的转牌逻辑）。

## 对应关系（用户已确认）
| 牌堆 | P5R 页 | Joker 立绘（research/p5r/art/portrait/） |
|---|---|---|
| 抽牌堆 | 主菜单（大手掌） | P5_CAMPTOP_2D（白手 + 红手套 + Joker，一张图） |
| 消耗堆 | SKILL | P5_CAMPSKILL_2D01 / 2D02 |
| 弃牌堆 | ITEM | P5_CAMPITEM_2D01 |
| 牌组 | EQUIP | P5_CAMPEQUIP_2D01 / 02 / 03 |

做的顺序：抽牌堆 → 消耗堆 → 弃牌堆 → 牌组。每个都走同样四步：素材 → 1920×1080 静态排版图给用户确认 → 写代码、出预览 → 用户实机看。

## 素材
- **原版优先**，原版没有或不合适的才生图。
- 已在仓库：
  - 各页 Joker 立绘，3072–4096 原质量，边缘有抗锯齿；
  - 预告信 P5_CARD01 / 02、塔罗牌和卡背；
  - SYSTEM.ACB 里的 camp_open / camp_whoosh / cursor / cancel / card_select。
- 待 Codex（`docs/codex/codex_p5_pile_assets.md`）：
  - `INIT/P5CAMP_00SPD.SPD`：星纹拼贴、COMMAND、标题牌、菜单字、列表行、标签牌、斜板等；
  - PLG 斜块数据、星纹背景动画数据、转场擦除贴图；
  - 预告信：用原版 P5_CARD02 拉正（`art/p5/ui/pile/calling_card.png`），正反两面都用它。

## 共用骨架（照搬 P3）
- 补丁挂在 `NCardPileScreen._Ready` 和 `NDeckViewScreen._Ready` 上；关闭时先播退场再关；只在 Joker 时生效，设置里有开关；出错退回原版；手柄 / 键盘走原版。
- 原版牌节点直接复用：悬停放大并显示名牌、点击翻牌特写、升级预览、原顺序 / 按类型排序、分区标题、字体预热。
- 文件：`P5PileCore.cs`（共用交互）+ 每页一个装饰文件。

## 背景
**不透明**，完全看不到局内（用户要求）。

## 抽牌堆
- **转牌**：预告信卡悬浮在红手套掌心上方，绕竖轴转，背面朝外时换背面图。底速每秒 36°，打开后 0.6 秒平滑起转；碰到任意一张牌就额外踢转 120°，弹簧 ω = 9 收回（HeroStage 的参数照抄）。
- **切入**：干脆、丝滑，不拖沓。
  - 从抽牌堆按钮那个角展开；
  - 用原版立绘做白、黑剪影交叉滑过，代替原版的硬切；
  - 原版星纹带、斜板扫入定住，白手轻微回弹；
  - 牌从按钮依次飞进格子。
  - 退场是倒放压短。

## 清晰度 / 锯齿（用户要求重点检查）
- 原图 3072+ 直接在 1080p 缩到 1/3 会闪烁、出锯齿：
  - 离线用 Lanczos 预缩到「1440p 显示尺寸」再进包；
  - 运行时开 mipmap，用 LinearMipmap 过滤；
  - 不让任何图被放大超过 1.0 倍。
- 转牌：贴图四周留 2px 透明边，压窄时边缘靠双线性过滤抗锯齿；透视在 shader 里做。
- 半透明边缘检查白边 / 黑边（预乘 alpha）。
- 每次出预览都在 1080p 和 1440p 截局部放大图自检。

## 原版数据解析结果（2026-11-22）
- `CAMP.PLG`：106 个带名字的多边形。每条记录 72 字节：顶点偏移、索引偏移、顶点数、索引数、包围盒、名字[32]；顶点 16 字节 = 颜色 u32 + x + y + 0；索引是 u16 三角形。内容是 SKILL / ITEM 页的队伍列表锯齿框（party_01～10）、角色头像剪影（st_*）、爆炸星形（bt_black / bt_white）。解析脚本：`tools/p5/pile/plg.py`。
- `CAMPBGSTARANIME.DAT`：大端序，0x20 起每颗星 32 字节，共 49 颗：x、y（1280×720 坐标 ×1000）、颜色（0x000000FF 黑 / 0x606060FF 灰）、缩放 ×1000、旋转角 ×1000，后面接逐帧动画数据。星纹是同心五角星，黑（或灰）白一圈圈交替。游戏里用 shader 照这份数据画（矢量，任何分辨率都锐利）。对比图：`docs/p5/preview/pile_stars_vs_p5r.jpg`。
- `P5CAMP_00SPD` 切图：COMMAND = 00201，Don't Look at ME = 00046，LIKE THAT! = 00047，MeNu& = 00048，MAIN = 00061，COMMAND SELECT = 00479，页码数字 0–9 = 00033–00042。原图都是白字，运行时上色。
- 排版图：`docs/p5/preview/pile_draw_layout_v1.png`，生成脚本 `tools/p5/pile/mock_draw.py`。
