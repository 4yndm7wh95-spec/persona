# P4Yu（暂定名）计划

## 用户决定
- 2026-11-26：第三个换皮 mod 把**铁甲战士**换成 **P4 鸣上悠**，Persona 是伊邪那岐。
- 2026-11-26：P4G 已经装好，但游戏比较老，资料只作部分参考。主要参考是 **P4R（Persona 4 Revival，2027-02-18 发售）**的公开视频和图片，交给云电脑 Claude 去找。

## 命名（暂定，开工前可以改）
| 项 | 名字 |
|---|---|
| mod 目录 | `p4mod/` |
| mod id、程序集 | `P4Yu`，`P4Yu.dll` |
| 资源命名空间 | `res://p4yu/` |
| 工具 / 美术 / 文档 | `tools/p4/`、`art/p4/`、`docs/p4/` |
| 资料 | 原版铁甲战士 `research/ironclad_assets/`，P4G `research/p4g/`，P4R `research/p4r/` |

## 第 0 阶段：准备资料（进行中）
- Codex：`docs/codex/codex_p4_stage0_rip.md`。导出原版铁甲战士资源，解包 P4G 的立绘、Persona、塔罗牌、UI、特效贴图、音效、语音、模型和过场截图。
- 云电脑 Claude：`docs/p4/P4R_RESEARCH_TASK.md`。收集 P4R 视频截图和短片段，内容包括悠的动作、召唤、伊邪那岐、特效、UI 和场景。

## 第 1 阶段：能加载，进游戏能看到替换（等资料到了再做）
1. 搭 `p4mod/` 骨架。入口复制 P3 的兼容层和 P5 的挂载、抢路径写法，再复制 SkinChanger 接管判断，只复制，不改原文件。
2. 写 `tools/p4/cloud/check.sh`，同时用 `research/libs` 和 `research/libs_0.107.1` 编译。
3. 用 `tools/spine/skel_reader.py` 读原版 `ironclad.skel`，拿到动画名、事件和挂点骨骼。先用一张待机全身图做骨骼：所有动作都是同一张图加呼吸。
4. 商店改指向战斗骨骼，休息处先用同一张图；左上角头像、地图标记、选人按钮头像也换掉。
5. 交给 Codex 打包，用户进游戏确认。

## 待用户定
- 造型：冬季校服？电视世界的眼镜戴不戴？
- 武器用哪一把；要不要做伊邪那岐大神。
- 画风：先出候选图让用户挑，还是用户直接给参考图。
