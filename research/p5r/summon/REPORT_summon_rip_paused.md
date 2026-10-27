# summon_rip_a 当前结果与用户暂停记录

本轮开始前fetch+merge成功，快进到2e7c89cb8，已读取新版pose_consistency.py的--occluded选项。用户已认可summon_hold_a，且立绘一律不画面具，蓝焰轮廓交给游戏特效。

## 当前图

仅生成一次summon_rip_a.png，内置image_gen；输入key_rip_mid为姿势编辑底图，已通过summon_hold_a提供画风和外观，idle_a3提供比例，face_standard提供脸。无面具、血或特效，1024×1536 RGBA透明底；主体外alpha1～2残点清理737像素，其余像素不改。完整提示词、输入、原始生成位置见summon_rip_a_generation.json。

## 检查输出

扶脸帧按用户新规则复查：

`python tools/p5/pose_consistency.py idle_a3 summon_hold_a --occluded summon_hold_a`

```text
summon_hold_a      (face covered) standing height x0.999  boot height x0.971  boot width x0.921  feet moved [204, 179] px  ok
sheet: art\p5\poses\check_summon_hold_a.png | overall: ok
```

撕面具帧的脸露出，因此不加--occluded：

`python tools/p5/pose_consistency.py idle_a3 summon_rip_a`

```text
summon_rip_a       head x0.880  body x1.001  boot height x1.050  boot width x1.136  feet moved [204, 173] px  FAIL
sheet: art\p5\poses\check_summon_rip_a.png | overall: FAIL
```

结果如实为FAIL，尚未重画、未修改检查脚本或阈值、未手工缩放，也未以--occluded规避脸露出帧的头部检查。没有额外主观审图。

## 暂停状态

用户在检查后明确要求“暂停 推送”，立即停止生图。当前summon_rip_a仅作为待处理结果保存，不标记通过；没有生成summon_release_a，没有继续补帧或归一化。summon_hold_a保持用户已通过状态，其原图未修改。下一步等待用户指示；若恢复，先处理summon_rip_a的检查结果，用户通过后才画直立的summon_release_a。

只提交本次当前图、生成记录、验收状态与报告。art/cards的11个已修改文件原样保留，不提交/不stash/不还原；既有guard_a修改及其他无关文件保留。未打开结城理mod图片，未读obs.json或密码，未启动游戏，不reset/rebase/强推。推送后用ls-remote确认origin收到提交。
