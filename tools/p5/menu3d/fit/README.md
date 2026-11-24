# 菜单 3D Joker 入场：逐帧对原版拟合（消耗堆 SKILL 页已用这套做完，弃牌堆 ITEM / 牌组 EQUIP 照着做）

数据：`research/p5r/menu3d_data`（Codex 从 MENU_C0001 GMD/GAP 蒙皮导出的逐帧顶点，float16）；`../m3d.py` 读数据、按 Godot Camera3D 的约定投影；
`../render_f16.gd` 云端用 Godot 渲（支持 `"verts"` 直接给混合后的顶点）；`../skill_entrance.py` 是 SKILL 页的最终渲染脚本（结论写在它的文件头）。
原版截图：`docs/p5/preview/p5r-20261121-223943-frames-02-12/`，**序号 = 文件帧号 − 1**（`common.orig(序号)`）。中间文件默认写到 `/tmp/p5fit`（`P5FIT_OUT` 改）。

## 步骤
1. **立绘在原版里的位置**：`python3 reg2d.py <立绘 png> <原版切立绘之后的序号> [比较区域]` → 缩放、左上角。
   SKILL：CAMPSKILL_2D01，序号 124，0.50795、(0.47, 18.05)；ITEM：CAMPITEM_2D01，序号 196，0.5、(0, 0)。
2. **定格姿势 + 机位**：原版 2D 立绘就是从 3D 定格画面描的。`artfit.py` 用立绘剪影对候选片段拟合（先把所有片段最后一帧都扔进去，4 进程并行）。
   SKILL 的结果：**main_0016 + C2**（位置 (-57, 44, 73.6)，旋转 (41.2, -48.9, 11.1)，fov 40.5），IoU 0.85，比第二名高一截。最后一帧用它，切 2D 立绘时不跳。
3. **前面每一帧**：`grid.py` 放大原版帧量双眼（、嘴、下巴）的屏幕坐标；机位基本固定在第 2 步的机位，每帧只解小的俯仰 / 偏航偏移（`skill_example/seqdesign.py`）；
   动作挑片段帧或往定格姿势线性混合，撞色区里露出来的白色部分（SKILL 是白腿）用 `skill_example/fitB.py` 那样对轮廓。`rast.py` 是 Python 光栅化（快），`render.py` 是 Godot 真渲 + 叠原版看。
   注意：动作数据只有形状，**没有表情**（原版 113–117 的闭眼做不出来）。
4. **出最终帧**：照 `../skill_entrance.py`（画布四边多渲 60px、裁到不透明区域、frames.json 记每帧在原版屏幕里的位置）。
5. **进游戏**：`stage_p5.py` 的 `pile_textures()` 打包（只写 pile_* 那几张，别整个重跑 stage）；代码里用同一个变换把原版屏幕坐标换到我们的排版（`PileExhaust.Map`：立绘配准对齐）。
6. **交付前对照**：预览 `tools/p5/fxpreview/render.sh <场景> 6` + `mix.py`，再 `compare.py` 出「原版 vs 我们同一时刻」逐帧对照图，**放到 100% 看局部**（重影、截边、红缝）。

## 弃牌堆 / 牌组的线索（未验证）
- ITEM 入场原版大约序号 177–186（178 黑底白剪影、181 手臂扫进白色区域变白、184 低头特写、187 定格）；Codex v2 猜的片段 main_0010（`research/p5r/menu3d_pages/cameras.json`）。
- EQUIP 大约序号 233–243（Codex：0236 手过头顶、0240 枪斜举、0242 枪口朝右下），立绘 CAMPEQUIP_2D01/02/03，Codex 猜的 main_0005。
- 这两个都还要按上面第 1–3 步重新确认（SKILL 页 Codex 的猜测就是错的）。
