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

## 实现（分支 claude/p5-pile-ui，2026-11-22）
- `p5mod/src/PilePatches.cs`：`NCardPileScreen._Ready` 后置、`AfterCapstoneClosed` 前置（先播退场）。只接抽牌堆；其他牌堆没做之前走原版。
- `p5mod/src/PileScreen.cs`：照搬 P3 PileFx 的交互（复用原版 NGridCardHolder、按类型 / 原版顺序、悬停名牌、点击翻牌特写 + 查看升级、手柄、滚动、出错回滚），配色和纸片换成 P5 剪报风。
- `p5mod/src/Vfx/PileDraw.cs`（只依赖 Godot，预览工具能编译）：星纹 shader（49 颗原版星，左红右黑白）、原版立绘 / 字图、牌数大数字、真 3D 预告信（`CallingCard`）、入场 / 退场时间轴。
- 设置：`P5Settings.Option.Pile`（设置页「牌堆界面（P5R 菜单风格）」）、`DrawOrder` 记住排列方式。
- 预热：`Warmup.RequestPile()`（贴图、canvas 着色器、音效）。
- 素材：`tools/p5/stage_p5.py` 的 `pile_textures()`（按 1080p 显示宽 ×1.34 离线预缩，白字图出黑描边版 `_ol`），音效 `camp_open` / `camp_cancel`。
- 预览：`tools/p5/fxpreview/render.sh pile_draw 6`（假牌格子 + 真装饰 / 动画）。

## 消耗堆（SKILL 页，2026-11-23 定稿入场）
- 入场照原版 frame 113–125 逐帧还原：`tools/p5/menu3d/skill_entrance.py` 用 Codex 导出的逐帧顶点（research/p5r/menu3d_data）拟合出机位和动作，渲出 11 帧（research/p5r/menu3d_skill）。
  定格姿势 = main_0016（和 2D 立绘 CAMPSKILL_2D01 的剪影重合度 0.85），前面是 main_0015 抬膝 + 镜头右摇，119 镜头上仰、混合进 main_0016。
- 撞色：白板里 Joker 是白的；黑色反色区从立绘白腿那条直边往右扫开（同心圆、半径逐帧变大），区域里白板变黑、腿变白。
- 对比图 `docs/p5/preview/pile_exhaust_entrance_vs_p5r.jpg`，预览 `pile_exhaust_v7.mp4`。

## 排列切换
- `p5mod/src/Vfx/PileSwitch.cs`（PileModeSwitch）：「原版顺序 / 按类型」，每个牌堆分开记（P5Settings.DrawOrder / ExhaustOrder，弃牌堆 / 牌组做的时候再加）。
  原版顺序 = 直接读原版网格 `NCardGrid._cards`（读不到时抽牌堆按稀有度 + ID 现排，同原版不暴露真实顺序）。

## 弃牌堆（ITEM 页）
- 排版图 v2：`docs/p5/preview/pile_discard_layout_v2.png`（和原版对照 `pile_discard_layout_v2_vs_p5r.jpg`），脚本 `tools/p5/pile/mock_discard.py`。
- 左边（用户 2026-11-23：立绘左半边原版是被队伍列表挡住的，不该露出来）：原版队伍列表样式的类型列表——黑色长条 + 阶梯白边、PLG 头像锯齿底、
  标签牌 00253（DISCARD / BY TYPE）、斜体大数字 00235–00244、青 / 粉两行 + 下划线 00256；第一行原版 Joker 头像 00245 + 总张数，
  类型行用原版属性图标：物 00312 = 攻击、风 00317 = 技能、雷 00316 = 能力、念 00318 = 状态、呪 00321 = 诅咒；没有的类型变暗。入场时先露出星纹 + 3D Joker（同原版），砸中时列表滑进来。
- 原版构图（frame 194–206 取中值描的边）：立绘 CAMPITEM_2D01 0.5 倍贴左上角；撞色斜线 x = 874 − 0.419 (y − 560) 右边，立绘的黑身体画成浅灰、白描边变黑；
  黑块顶上一个红三角、右边一条斜边外是红底；右下一条浅灰斜带（原版放「消耗品 ●○○ Q/E」切分类）→ 我们放排列切换。
  坐标按立绘对齐到我们的排版（缩放 0.977、平移 (−80, 0)，立绘大小和消耗堆一致，上边缘贴屏幕顶）。牌 3 列放进黑块，只在黑色窗口里露出来，往下滚时从浅灰身体 / 斜带下面钻过去。
