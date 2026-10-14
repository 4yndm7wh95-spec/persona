# 结城理面部睁眼动画素材

## 文件与对齐
- face_src.png：原版 allout_finish.png 的面部裁剪，起点 (800, 0)，500×640，RGBA。
- face_half.png：双眼半睁、下视。
- face_closed.png：双眼自然完全闭合。
- preview_face.png：原图 / 半睁 / 闭眼并排，下方分别给出左右眼 3 倍像素放大。
- 叠回原立绘时左上角使用 (800, 0)，不缩放。
- 源图实际宽 1299；从 x=800 到右边界只有 499 列。为满足指定 500×640，末列（源坐标 x=1299）透明补齐。其余源图裁剪像素原样保存。

## 编辑方式与次数
使用内置 imagegen 的编辑功能，两种状态均直接以 face_src.png 为编辑输入；闭眼 1 次、半睁 1 次，共 2 次，1 轮生成。未使用 API/CLI 替代。
模型输出实际为 1108×1419，处理时缩回 500×640 后，仅合入眼部局部；源图与最终帧的画布位置固定。

### 闭眼提示词
Use case: precise-object-edit. Edit target: the supplied original 500×640 RGBA crop of Makoto Yuki from Persona 3 Reload. This is a frame replacement in the SAME illustration. Change ONLY BOTH EYES to naturally, peacefully FULLY CLOSED eyes: remove every visible iris, pupil and white sclera, replace them with matching pale face skin and a delicate curved closed upper eyelid line with the original dark eyelashes, relaxed serene expression. Preserve exact head position, face shape, eyebrows, nose, mouth, hair strands (especially hair occluding the left eye), colors, shading, clothing, hand, silhouette and transparent alpha background. Do not redraw the image or change style, zoom, framing, orientation or positions. Keep the exact 500×640 canvas and registration. Closed eyes must be truly shut, with no cyan iris remaining. No text.

### 半睁提示词
Use case: precise-object-edit. Edit target: supplied original 500×640 RGBA crop of Makoto Yuki from Persona 3 Reload. Make a matching in-between eye-opening animation frame of this EXACT illustration. Change ONLY BOTH EYES to HALF-OPEN, looking downward: relaxed upper eyelids lowered across the upper half of the cyan irises/pupils; small narrow visible lower portions of cyan irises and sclera, clearly halfway between fully open and fully shut, calm peaceful expression. Preserve exact original eye corners and eye locations, head position, face shape, eyebrows, nose, mouth, every hair strand especially bangs crossing the left eye, original flat pale skin color and crisp cel-shaded linework, clothing, hand, silhouette and transparency. Do not redraw, restyle, add soft painterly shading, shift, zoom or reframe. Same 500×640 canvas and pixel registration. No text. Keep all areas outside the eyelids unchanged.

## 第 4 步还原
已执行。编辑像素仅允许落在两个约 160×90 椭圆中：裁图坐标中心 (205,255)、(345,273)。在其中进一步使用贴合眼睑的局部轮廓、羽化边缘，并保护遮住眼睛的原版头发。
眼外使用 face_src.png 像素还原；全图 alpha 直接使用源图 alpha。局部生成肤色按原版浅肤色作中位数色差校正（闭眼 RGB +4/+5/+7，半睁 +3/+3/+4；深色线条不作同等提亮）。
经过 3 次局部蒙版调整，消除眉下硬边并恢复遮挡发丝。

## 自检与已知问题
- 三张面部 PNG 均为 500×640 RGBA。
- 两个椭圆以外逐像素比较：半睁 0 差异、闭眼 0 差异。
- 全图 alpha 与源图逐像素相同。
- 闭眼未保留可见瞳孔、虹膜或眼白；半睁露出下部蓝色虹膜，目光下垂。
- 预览检查：头发、脸型、鼻口、服装和构图保持原版；眼部为同一插画的局部编辑。
- 已知限制：眼睑线条来自生成编辑，并非 P3R 官方闭眼帧；局部线条粗细与原睁眼帧可能有细微差别。未在游戏中验证换帧效果（按要求未启动游戏）。

