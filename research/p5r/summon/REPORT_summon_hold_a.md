# summon_hold_a：第一张召唤关键帧与检查问题报告

## 当前状态

开始前fetch+merge成功，快进到83adc2aa8，已读POSE_PLAN“召唤（用户2026-10-27更新，以此为准）”。停用key_rip_open和key_stance的半蹲架势，不再作为本组输入，不删除历史参考。

本轮仅生成summon_hold_a.png。以research/p5r/summon/key_mask_hold.png为编辑底图重绘；idle_a3控制人物/服装/比例/头部大小，face_standard控制脸。手扶裸脸，不画面具、蓝焰或特效。内置image_gen，1024×1536 RGBA，未使用CLI或其他生图模型。第二次原图作为当前summon_hold_a，完整提示词、输入和上一轮记录见summon_hold_a_generation.json。

## 比例检查：未通过，不能宣称比例一致

`python tools/p5/pose_consistency.py idle_a3 summon_hold_a`

第一次生成：

```text
summon_hold_a      head x0.796  body x1.032 (check)  boot height x1.007  boot width x0.939  feet moved [222, 166] px  FAIL
sheet: art\p5\poses\check_summon_hold_a.png | overall: FAIL
```

按规则从同一原版底图重做一次，没有手工缩放：

```text
summon_hold_a      head x0.760  body x1.041 (check)  boot height x0.971  boot width x0.921  feet moved [204, 179] px  FAIL
sheet: art\p5\poses\check_summon_hold_a.png | overall: FAIL
```

脚本head_height以最大连通肤色区域的最下方像素作为下巴，计算发顶到该点的距离。扶脸红手套遮挡下脸，会分割可见肤色区域；两次指标可能受到遮挡影响，无法仅凭当前脚本确认实际头部是否偏小。当前结果如实保留FAIL，不把它改为ok，不修改脚本、阈值、肤色或手套颜色，也不手工放大头部凑数。

第二次提示词明确保留待机头部尺寸及圆下巴，希望下巴能在手套下露出，但结果仍FAIL。继续用这一数值强行改大头部可能偏离用户标准；遮挡导致的测量处理方式没有写在规则里，按用户“遇到没写到的情况停下来报告”暂停，不继续反复改头部或姿势。

## 保存与未执行项

当前图保存art/p5/poses/summon_hold_a.png，第二次仅清理远离主体的alpha1～2残点423个，其余像素不改；alpha=0像素1024603个。第一次图仍在内置生图原始保存位置，元数据记录来源；未额外提交多个同名候选。

未通过比例检查，因此未将summon_hold_a加入normalize_poses、未生成final版本、未做动画连续性补帧。没有做额外主观审图。summon_rip_a与summon_release_a尚未生成，严格等待第一张用户验收；release以后也要直立，不能用旧半蹲参考。

本次提交当前图、生成记录、验收状态及本报告。art/cards的11个已有修改原样保留且不提交/不stash/不还原；既有guard_a修改与其他无关未跟踪资源也保留。未打开结城理mod图片，未读obs.json或密码，未启动游戏，不reset/rebase/强推。推送后通过ls-remote确认origin收到提交。
