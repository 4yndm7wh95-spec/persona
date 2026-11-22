你在用户的 Windows 电脑上工作。任务：修正上一轮 research/p5r/menu3d 的渲染（任务单 docs/codex/codex_p5_menu3d_render.md，提交 c81a5eb9），重新渲染全部片段，覆盖提交。写报告。不改 mod 代码，不生图，不启动游戏。

**先读 docs/codex/README.md 和上一轮任务单 docs/codex/codex_p5_menu3d_render.md，规矩、路径、命令、推送方式全部照旧（分支 claude/p5-pile-ui）。**

上一轮的动作、蒙皮、骨骼都是对的（main_0000 就是原版主菜单入场那段），这次只改下面三件事。

## 1. 服装：用怪盗服，不用校服
原版菜单里的 3D Joker 穿的是**怪盗服**（高领黑风衣、手套），**不戴面具、不戴眼镜**。对照：
- research/p5r/art/portrait/BASE__P5_CAMPTOP_2D__e441c53ea3.png（菜单最后定格的 2D 图，高领风衣 + 红手套）；
- 参考帧 docs/p5/preview/p5r-20261121-223943-frames-02-12/frame_0049～0053（3D 黑色 Joker 的高领）。

上一轮 capture.gd 把 `c0001_body_051_UI_main`、`c0001_hand_051W_UI_main`、`c0001_hand_051B_UI_main`、`kamen01_051_main` 藏掉了，剩下的是校服 + 眼镜，**正好反了**。改成：
- 显示 `c0001_body_051_UI_main`（怪盗服身体）；
- 手套 `051W` / `051B` 两种各渲一遍 main_0000，和参考帧比，选对的那一种（报告里写依据）；
- 继续隐藏 `kamen01_051_main`（面具）、`Blur*`、`b *`、`line`；
- **隐藏校服身体、校服手、眼镜**以及其他和怪盗服重叠的替代服装网格。
- 在 README 里列出**全部网格名**，每个写「显示 / 隐藏」和理由。

## 2. 着色：贴图原样，不要乘漫反射色
原版菜单专用贴图 `c0001_t01_face01UI.png` 本身就是**白脸 + 黑线条**（实机里脸、耳朵、头发高光是白的）。上一轮用「材质漫反射色 × 贴图」，漫反射色偏暗，把脸压成了暗灰。改成：
- **有贴图的材质：颜色 = 贴图颜色（不乘漫反射色）**，透明裁切照旧；
- 没贴图的材质：用漫反射色；
- 仍然无光照、不加描边 / 阴影 / 灯光。
- 渲完和参考帧 frame_0052 比：脸部 / 耳朵 / 头发高光应该是白的、身体和头发主体是黑的。如果还是对不上，在 README 里写清楚哪里不一样。

## 3. 相机：再对准一点
上一轮 sheets/match_frame_0052.png 里头的位置接近，但肩线和头部角度有偏差。换好服装后**重新拟合一次**共用相机（还是所有片段共用一个相机），目标是 main_0000 某一帧和 frame_0052 的轮廓（头、领子、肩）尽量重合。另外出一张 main_0000 全部 12 帧依次和 frame_0049～0054 并排的对比图 sheets/compare_main_seq.png。

## 4. 输出与提交
- 覆盖 research/p5r/menu3d 下的全部片段帧、sheets、README（文件名不变，方便直接替换）；工具改动提交在 tools/p5/menu3d。
- 提交信息 "research: menu 3D Joker v2 (phantom thief outfit, texture-only shading, refit camera)"，推送到 claude/p5-pile-ui（命令同上一轮任务单第 4 步）。
- 报告（中文）：显示 / 隐藏了哪些网格；手套选了哪种、为什么；着色改了什么；新相机参数；和参考帧的差别；提交号。

【绝对不要做】同上一轮任务单的清单，全部照旧（只改 research/p5r/menu3d 和 tools/p5/menu3d；不安装、不下载、不启动游戏、不用 Computer Use、不生图、不删除目录、不碰 CPK 和游戏目录、不读 obs.json 和密码、不进入旧仓库、出错就停下报告）。
