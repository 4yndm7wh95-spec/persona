你在用户的 Windows 电脑上工作（姿势图那个窗口）。任务：用 P5R 原版的开枪动作作参考，画 Joker 开枪这组四张图。不改 mod 代码，不启动游戏，不用 Computer Use。

找仓库、拉取、提交推送、「绝对不要做」清单和 docs/codex/codex_p5_poses4.md 开头一样：不要 rebase / reset / amend / --force；不要改全局 git config；不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码；不要改 P3R、P5R、杀戮尖塔 2 的游戏原文件；不要绕过 imagegen 的安全拒绝；不要 sparse-checkout disable / set；art/cards 等其他会话的文件原样保留；不要打开、参考、编辑结城理 mod 的任何图片；遇到没写到的情况停下来报告。

【用户确认的原版开枪动作】（research/p5r/pose/index.csv 的序号）
| 序号 | 原名 | 内容 | 用于 |
|---|---|---|---|
| 51 | BB0001_051.GAP#main[53] | 拔枪：手从脸前划过、转成侧身、右臂平伸 | gun_raise |
| 52 | BB0001_051.GAP#main[54] | 瞄准保持（循环）：侧身站直、双脚并拢、左手垂在大腿旁、右臂笔直平伸 | gun |
| 53 | BB0001_051.GAP#main[55] | 开枪后坐：手腕大幅上挑（几乎到头顶）、大衣下摆被带起、再落回平伸 | gun_recoil |
| 54 | BB0001_051.GAP#main[56] | 收枪：手往上翻经过脸前收回、转回正面站姿 | gun_lower |

【1. 渲原版参考（挂枪、我们的视角）】
- 模型和方法同 research/p5r/pose/README.md（joker_thief_nomask，C 方案只套 rotation；能看出位移的话加根节点 translation 的 D 方案）。
- **挂上原版手枪**：在 index.csv / MODEL/WEAPON 下找 Joker 怪盗服默认的手枪模型，导出（tools/p5r/fix_gfd_glb.py 同样的修复），挂到右手武器骨骼（找不到就挂右手掌骨骼，写明用的哪个）。找不到枪模型就先不挂，报告里写明。
- **相机改成我们立绘的视角**：正交相机，人物 3/4 朝画面右方、身体偏正面朝镜头（和 art/p5/poses/idle_a3.png 的朝向一致），不要原来那种偏背对镜头的角度。固定相机，不逐帧重新取景。
- 四段各**每 2 帧渲一张**（30fps 基准），透明底 1024×1536；每段拼一张联系表 research/p5r/gun/<序号>_sheet.png（格子上写帧号），再各做一个原速 GIF research/p5r/gun/<序号>.gif（320 宽）。
- 从里面挑四张关键帧，单独存 1024×1536：research/p5r/gun/key_gun_raise.png（51 里手刚过脸、转成侧身、手臂举到一半那一帧）、key_gun.png（52 任一帧）、key_gun_recoil.png（53 里手挑到最高那一帧）、key_gun_lower.png（54 里手翻上来、经过脸前那一帧）。
- 先提交推送这一步（只 add research/p5r/gun），给用户看。

【2. 画 gun_a.png（关键帧）】
- 输入：idle_a3（人物、比例、服装、头的大小）+ face_standard.png + key_gun.png（**只学姿势**：侧身、站直、双脚并拢、左手垂在大腿旁、右臂笔直平伸；不要学原版模型的画风和脸）。
- 右手单手握黑色手枪，食指伸直贴在扳机护圈外侧（不扣扳机），手腕直，枪口朝画面右方；头顺着手臂看出去，表情冷静。左手掌贴在左大腿外侧。真透明底 1024×1536，不画火光和特效。
- 提示词按游戏角色立绘、拿着道具手枪摆姿势来写，不写 shoot / fire / aim at / enemy / kill，不画血、不画目标。被拒就改成「右臂平伸、手掌张开指向前方」再试一次并写明；第二次还被拒就停下来报告。
- 出图后直接给用户看并推送。**用户说 gun_a 通过后**，再用同样的方法（输入 idle_a3 + face_standard + 对应的 key 图 + 已通过的 gun_a）画 gun_raise_a、gun_recoil_a、gun_lower_a，每张出图后给用户看。

每次推送后确认 origin 上有这次提交。报告写在 research/p5r/gun/REPORT.md：枪模型来源和挂点、用的变体（C / D）、关键帧取自第几帧、提交号。
