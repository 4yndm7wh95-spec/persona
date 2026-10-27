# 直立召唤收尾 summon_release_a

开始前fetch+merge成功，快进到213a5242f，已读README新增“底图脸被挡时用idle_a3作比例基准”的规则。用户已通过summon_rip_a2，本轮只制作summon_release_a，不再使用旧半蹲key_rip_open/key_stance或旧双手举起summon_rip_a作为输入。

## 生成与交付

以内置image_gen编辑summon_rip_a2，idle_a3仅作站距和比例参考。要求身体直立、正常站距，右手顺着原扯开方向往人物自身右侧斜上方伸展并张开手指，左手自然垂下；大衣下摆和发梢被气流从下往上带起、往后飘，头微仰但视线仍向画面右方。不画面具、血或任何特效，蓝焰面具轮廓交给游戏实现。

原图1024×1536 RGBA真透明底，头/身体不做手工缩放。共生成两次，第一次比例检查未过，按规则以同一通过底图重画一次，第二次通过并作为当前_a版本。只清理主体外alpha1～2残点1807像素，其余像素不改。没有额外主观审图，用户负责视觉验收。

完整提示词、输入、原始保存路径、前次检查记录见summon_release_a_generation.json。

## 比例检查

`python tools/p5/pose_consistency.py idle_a3 summon_release_a`

第一次：

```text
summon_release_a   head x0.929  body x0.964 (check)  boot height x0.991  boot width x1.002  feet moved [119, 26] px  FAIL
sheet: art\p5\poses\check_summon_release_a.png | overall: FAIL
```

第二次（当前图）：

```text
summon_release_a   head x0.978  body x0.984  boot height x1.035  boot width x0.988  feet moved [129, 9] px  ok
sheet: art\p5\poses\check_summon_release_a.png | overall: ok
```

脸露出，不加--occluded。原命令与脚本阈值未改，不用缩放凑分。

## 归一化

按README，在normalize_poses.py的POSES登记summon_release_a=(1.0, front)并运行。选择front锚定是因为当前前脚相对待机仅移动9px，作为站距打开时保持的那只脚；没有改已有姿势的比例与锚点。

归一化配置：{"scale": 1.0, "offset": [55, 4], "anchor_foot": "front"}。final输出1152×1536；对所有alpha>0像素检查，裁掉像素数为0，衣摆/手/脚完整保留。


只为本轮新图生成final版本，没有额外改写先前hold/rip的原图或擅自新增其他动作帧。本轮是关键帧验收，尚未把它当作用户通过的成品；召唤整段连续性补帧等关键帧通过后再处理，不宣称整段已无跳变。

## 提交与保留

提交当前原图、final版本、生成记录、归一化登记、验收状态与本报告。art/cards的11个已修改文件原样保留且未提交/未stash/未还原；既有guard_a修改与其他无关资源保留。未打开结城理mod图片，未读obs.json或密码，未启动游戏，不reset/rebase/强推。推送后用ls-remote确认origin收到本次提交。
