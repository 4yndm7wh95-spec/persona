# P3R 营地菜单（设置界面）照原版重做用的工具

原版素材在 research/p3r_camp/（Codex 从本机 P3R 导出，见那里的 README）。

| 工具 | 作用 |
|---|---|
| `psa_to_bin.py` | 原版动作 PSA → 逐帧骨骼局部变换（glTF 坐标系，和 GLB 静止姿势逐项核对过）。分块的 PSA 先 `cat *.part* > x.psa` 拼回，按 sha256.txt 校验。 |
| `hero_render/render.gd` | 原版模型 + 原版动作 + 原版材质参数离线渲染：每帧 color_NNN.png（画出来的部分）+ mask_NNN.png（原版 CaRtOp 材质 = 显示水的区域）。镜头参数等原版 SceneCapture 数据导出后填进 config。 |
| `stage_hero3d.py` | 主角 + COMMU 塔罗牌整理成 mod 运行时资源 mod/pck_stage/p3makoto/hero3d/（GLB、贴图、动作 bin、缩放轨）。 |
| `cut_splash.py` | 原版入水水花图集 T_UI_Camp_03/04/05 → 10 帧 art/p3menu_system/p3r/splash_NN.png。 |

关键发现（原版材质）：营地菜单里结城理的外套、裤子和大部分头发用的是 CaRtOp 材质 —— 不画颜色，直接显示后面的水面（RenderTarget），
所以人和水之间没有边界；真正画出来的只有脸、手、衬衫领口（灰度贴图映射成蓝色调 + 平涂色），头发是平涂青 #00E7F8，描边浅灰蓝 #B6C4D4。

原版道具隐藏：营地动作把召唤器（Jnt_L/R_AttachHand01_00）、椅子等（Jnt_C_AttachRoot_02..07）的骨骼缩放到 0.001；PSA 不带缩放，缩放轨另由 Codex 导出在 research/p3r_camp/hero3d/scale/。
COMMU 页的牌是独立道具 SK_Pp2001_000，自带同名五段动作，和主角放在同一原点、同一时间轴即可对上；牌的面单面且绕序反，要 cull_disabled。render.gd 的 config 支持 "scale" 和 "props"。
