# P3R 营地菜单（设置界面）照原版重做用的工具

原版素材在 research/p3r_camp/（Codex 从本机 P3R 导出，见那里的 README）。

| 工具 | 作用 |
|---|---|
| `psa_to_bin.py` | 原版动作 PSA → 逐帧骨骼局部变换（glTF 坐标系，和 GLB 静止姿势逐项核对过）。分块的 PSA 先 `cat *.part* > x.psa` 拼回，按 sha256.txt 校验。 |
| `hero_render/render.gd` | 原版模型 + 原版动作 + 原版材质参数离线渲染：每帧 color_NNN.png（画出来的部分）+ mask_NNN.png（原版 CaRtOp 材质 = 显示水的区域）。镜头参数等原版 SceneCapture 数据导出后填进 config。 |
| `cut_splash.py` | 原版入水水花图集 T_UI_Camp_03/04/05 → 10 帧 art/p3menu_system/p3r/splash_NN.png。 |

关键发现（原版材质）：营地菜单里结城理的外套、裤子和大部分头发用的是 CaRtOp 材质 —— 不画颜色，直接显示后面的水面（RenderTarget），
所以人和水之间没有边界；真正画出来的只有脸、手、衬衫领口（灰度贴图映射成蓝色调 + 平涂色），头发是平涂青 #00E7F8，描边浅灰蓝 #B6C4D4。
