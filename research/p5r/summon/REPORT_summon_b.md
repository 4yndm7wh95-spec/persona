# 召唤后半段B组：每张仅生成一次（2026-10-27）

## 执行范围

开始前fetch+merge成功，快进到aad27f2a5。合并后读取README新增“生成完就推送，不要自己反复检查重画”，它覆盖旧的自动重画规则；用户最新消息也明确要求生成完直接推送。本轮四张各调用内置image_gen一次，没有输出审图、没有自动重画、没有为数字追加帧。

open1_b、open2_b、release_b全部仅以已通过的summon_rip_a2为编辑输入；settle_b以本轮release_b为编辑底图，rip_a2只补充原始脸/比例参考。提示词锁定rip头部大小、肩宽、腿长、脸和服装，限制后脚不超过约100px，站直不半蹲。无面具、无特效。原图均1024×1536 RGBA真透明；仅清理主体外alpha1～2残点，不改主体RGB。

每张生成保存并跑一次比例脚本后立即提交推送：open1 f56006b2d、open2 a7201cbd6、release bb33474af、settle 2c4323e8a，各次均用ls-remote确认origin已收到。没有使用旧summon_fling2_a/fling_a/release_a/settle_a作为生图输入。

## 比例检查原样输出

`python tools/p5/pose_consistency.py summon_rip_a2 summon_open1_b`

```text
summon_open1_b     head x0.991  body x1.001  boot height x1.004  boot width x0.998  feet moved [1, 0] px  ok
sheet: art\p5\poses\check_summon_open1_b.png | overall: ok
```

`python tools/p5/pose_consistency.py summon_rip_a2 summon_open2_b`

```text
summon_open2_b     head x0.991  body x0.992  boot height x1.014  boot width x1.005  feet moved [66, 0] px  ok
sheet: art\p5\poses\check_summon_open2_b.png | overall: ok
```

`python tools/p5/pose_consistency.py summon_rip_a2 summon_release_b`

```text
summon_release_b   head x0.951  body x0.964 (check)  boot height x1.024  boot width x0.996  feet moved [114, 20] px  ok
sheet: art\p5\poses\check_summon_release_b.png | overall: ok
```

`python tools/p5/pose_consistency.py summon_rip_a2 summon_settle_b`

```text
summon_settle_b    head x0.969  body x0.967 (check)  boot height x1.068  boot width x1.037  feet moved [30, 30] px  ok
sheet: art\p5\poses\check_summon_settle_b.png | overall: ok
```

脚本的overall:ok只以头部5%判定；身体超2.5%只标(check)，并不触发FAIL。**release_b的body x0.964、settle_b的body x0.967超出身体范围，不能因为脚本打印ok就宣称头身全部一致。** release_b原始后脚位移114px也超出用户约100px目标。按用户最新要求只如实记录，判断/是否重画由用户和Claude决定，不自动生成第二版。

## 归一化与序列

四张登记1.0/front，不加第三项额外位移nudge；既有登记保持Claude合并后的值，没有重新调整旧图。使用现有normalize_poses.main只处理本批四张，再合并normalize.json，保留既有final PNG文件编码不变。final沿用最新PAD256/TOP96，为1536×1632。生成的四张final用于本次序列比较，仍是待用户/Claude验收的候选，尤其上述身体警告未解决，不当作正式通过。

`python tools/p5/sequence_check.py summon_hold_a summon_rip_a2 summon_open1_b summon_open2_b summon_release_b summon_settle_b idle_a3`

```text
   summon_hold_a -> summon_rip_a2    overlap 0.89  face moved    35  feet moved [2, 0]  ok
   summon_rip_a2 -> summon_open1_b   overlap 0.86  face moved     1  feet moved [1, 0]  ok
  summon_open1_b -> summon_open2_b   overlap 0.75  face moved     1  feet moved [68, 0]  ok
  summon_open2_b -> summon_release_b overlap 0.69  face moved    30  feet moved [67, 0]  ok
summon_release_b -> summon_settle_b  overlap 0.69  face moved    10  feet moved [92, 0]  ok
 summon_settle_b -> idle_a3          overlap 0.35  face moved   221  feet moved [351, 0]  JUMP - add a frame
1 jump(s); overlays: art\p5\poses\seq_summon_hold_a_idle_a3.png
```

剩余1处JUMP：settle_b→idle_a3（overlap0.35、脸221px、后脚351px）。其他相邻步均ok。没有添加--allow豁免、没有改阈值，按最新“不要为数字追加更多帧”到此停止。本次四张全部只生成一次，脚本是记录而非自动返工门禁。

## 文件和保留

对应*_generation.json包含完整提示词、输入、原始生图保存位置、清理统计及原始检查输出。summon_b_sequence.json标记本轮顺序、剩余跳变与身体警告。旧A组文件保留历史，验收状态将指定四张标作被B组替换，不删除/覆盖它们。

art/cards另一会话11个已修改文件原样保留，不提交/不stash/不还原；既有guard_a本地修改及无关未跟踪资源也保留。未打开结城理mod图片、未读obs.json或密码、未启动游戏，不rebase/reset/强推。最终提交归一化登记、final候选与本报告，推送后再次确认origin。
