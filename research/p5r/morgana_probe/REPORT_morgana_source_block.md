# 摩尔加纳原版模型参考：第1步停止报告（2026-10-27）

已fetch + merge同步，当前基线f95c1828。未启动游戏、未使用Computer Use、未修改游戏原文件。

## 结果：指定目录与角色不符，未进入第2步

用户指定先试MODEL/CHARACTER/0009/C0009_051_00.GMD。实际渲染为白色制服、红色长鼻面具的人形角色，不是黄领巾、腰带、两足吉祥物形态的摩尔加纳。0009目录另一个模型C0009_099_00.GMD包含纹理名0099_mouse_akechi_t01.dds，支持该目录为明智相关资源，而非摩尔加纳目录。

按要求又提取并解析0009下全部43个GMD的纹理清单（见0009_texture_inventory.json），43个均可解析，未发现可确认的摩尔加纳候选。没有把纹理扫描冒充全部43个外观均已渲染；实际只渲染051一套，其他候选仅解析资源名称。没有擅自扩大到其他角色目录，也没有凭印象生成摩尔加纳。

因此按“形象不对就停下报告，不要做第2步”停止，rest_sit_d_A和idle_blink_a均未生成。art/p5/ref/morgana_ref.png未创建，避免把错误角色作为绘画输入。需先更正真实摩尔加纳模型来源，再恢复任务。

## 已执行工具链

1. 按research/p5r/index.csv定位BASE.CPK中的MODEL/CHARACTER/0009/。P5RAssetTool选择性提取051，之后提取目录43个候选；输出仅在本机p5r_rip资源库，游戏文件只读。库总量约12.65GB，低于15GiB。
2. GFDExport将C0009_051_00.GMD转为本机model/morgana_ref/morgana.glb，22网格、16材质、导出报告287 bones（各网格累计），不是摩尔加纳身份证明。
3. 原仓库tools/p5r/fix_gfd_glb.py执行--drop Blur --flip-v；rebased16、dropped[]、翻转21个UV accessor、修复14个IBM accessor。未修改修复脚本。
4. Blender4.5.9后台渲染正面、3/4朝右、侧面，各1024×1024正交相机、透明RGBA，Workbench原纹理平光、无阴影。三张及3072×1024一行三格仅作为错误来源证据，文件名明确NOT_morgana。GLB与原GMD只留本机，不提交。

## 文件与停止范围

- c0009_051_front/threequarter/side.png、c0009_051_NOT_morgana.png：错误候选的原始三视角与拼图。
- 0009_texture_inventory.json、render_records.json：43项纹理名与相机数据。
- extract/export/fix.log及三份脚本：复现证据。

art/p5/ref/rest_pose_ref.png本机仓库不存在，未打开参考，也无需删除。没有把上一轮未保存的休息坐姿A误当本轮结果提交。未生成rest_sit_d_A，没有补眨眼，不进入第2步。

art/cards另一会话11个已修改文件原样保留，不提交、不stash、不还原；guard_a及其他无关资源保留。未打开结城理mod图片，未读取obs.json或密码，没有rebase/reset/强推。此报告和诊断证据提交推送后核对origin。
