# 召唤后两张关键帧向人物右侧调整（2026-10-26）

用户反馈“再多转15度”。本次按相机观察角度调整处理，采用15°的小幅调整；只改key_rip_open（16帧14）与key_stance（17帧0），前两张key_mask_hold和key_rip_mid保持不变。

开始前fetch+merge，最新仍721837b2。以角色右肩减左肩的世界水平向量确定人物自身右侧，比较相机绕竖直轴正负15°哪一边更靠向右肩侧；两张均为从336.856095851°转至351.856095851°（本次+15°，累计+30°）。不修改人物原始动作、模型、手部或身体旋转。

相机目标、距离、高度及正交大小285.331708331保持不变；1536×1536 RGBA透明底，统一水平镜像。不挂枪。两张四边alpha均0，未裁切衣摆。前两张文件SHA256在修改前后完全一致，记录见right30_review/postprocessed.json。

## 文件

- key_rip_open.png、key_stance.png：当前调整后的后两张关键帧。
- key_contact_sheet.png：前两张321.86°、后两张351.86°的新四张对比。
- right30_review/comparison.png：每个后段姿势的改前/改后并排，角度均写明。
- right30_review/before_key_*.png：保留本次调整前的两张，不改动历史。
- right30_review/camera_records.json：精确机位及人物右侧方向向量。
- scripts/summon_right30_render.py、summon_right30_finish.py：实际脚本。

这次仅供确认后两张观察角度，未重新渲完整16/17段；根目录15/16/17逐帧PNG、GIF、联系表及render_records仍是上轮321.86°固定相机结果，不能将其当作351.86°版本。key_selection已为后两张标明新角度与来源，避免混淆。等待用户确认后再决定后续整段统一机位/立绘步骤；没有调用生图工具。

art/cards另一会话11个已修改文件未触碰/提交；既有guard_a本地修改及其他无关未跟踪资源保留。不stash、还原、rebase、reset、强推，未打开结城理mod图片，未读obs.json或密码，未启动游戏。推送后用ls-remote确认origin收到提交。
