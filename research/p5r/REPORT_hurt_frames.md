# 受击三张过渡帧（2026-10-27）

开始前fetch + merge快进至41125c15，使用仓库里Claude放大头部后的hurt_a4，保留其原图及final。三张都直接以该hurt_a4为底图，face_standard为脸部参考，没有以另一张新图串联作底图。

## 本轮生成

| 帧 | 提示词动作要求 | 原图提交 | 清理残点 |
|---|---|---|---|
| hurt_flinch_a | 双臂张开半程、斜下45度；头肩后收半程、眯眼咬牙；衣摆飘起一半 | 4fdd7304 | 95 |
| hurt_recover_b1 | 双臂比水平低约30度，肘微弯指开始放松；头回正下巴降低、仍皱眉朝右；衣摆回落一些 | f06273e0 | 461 |
| hurt_recover_b2 | 双臂几乎下垂、手指放松；头完全回正、朝右、眉微皱嘴闭；衣摆几乎垂下轻向左摆 | dbee306d | 221 |

每张提示词强调双脚位置、头部大小和身体比例完全照修正后的hurt_a4，头不缩小、手不放大；不画特效、伤口和血。以上为提示词要求，实际画面交给用户验收。内置image_gen各生成一次，未审图重画、未追加帧。均保存1024×1536 RGBA，仅清理主体外alpha1～2透明残点。完整提示词、输入与原始输出路径见各自_generation.json。每张保存后立即提交推送，并以ls-remote确认origin包含提交。

## 归一化与一次检查

三张均登记1.0/rear，现有归一化函数仅对本批三张运行一次、合并normalize.json，保留其他final和参数。没有额外缩放、位移、改变阈值或加--allow。

`python tools/p5/pose_consistency.py idle_a3 hurt_flinch_a hurt_recover_b1 hurt_recover_b2`

```text
hurt_flinch_a      head x1.049  body x0.961 (check)  boot height x1.016  boot width x1.033  feet moved [20, 44] px  ok
hurt_recover_b1    head x1.062  body x0.964 (check)  boot height x1.053  boot width x1.045  feet moved [19, 31] px  FAIL
hurt_recover_b2    head x1.062  body x0.975  boot height x1.032  boot width x1.065  feet moved [17, 11] px  FAIL
sheet: art\p5\poses\check_hurt_flinch_a.png | overall: FAIL
```

`python tools/p5/sequence_check.py idle_a3 hurt_flinch_a hurt_a4 hurt_recover_b1 hurt_recover_b2 idle_a3`

```text
         idle_a3 -> hurt_flinch_a    overlap 0.62  face moved    50  feet moved [0, 37]  JUMP - add a frame
   hurt_flinch_a -> hurt_a4          overlap 0.70  face moved    19  feet moved [0, 4]  ok
         hurt_a4 -> hurt_recover_b1  overlap 0.77  face moved    13  feet moved [0, 2]  ok
 hurt_recover_b1 -> hurt_recover_b2  overlap 0.72  face moved    23  feet moved [0, 32]  ok
 hurt_recover_b2 -> idle_a3          overlap 0.77  face moved    33  feet moved [0, 9]  ok
1 jump(s); overlays: art\p5\poses\seq_idle_a3_idle_a3.png
```

以上输出原样记录，不根据结果重画或追加帧。检查拼图存本机outputs/p5_hurt_frames，不提交。等待用户与Claude判断。

art/cards另一会话11个已修改文件原样保留，未提交、未stash、未还原；guard_a及其他无关资源保留。未打开结城理mod图片，未读取obs.json或密码。没有rebase/reset/强推。最终登记和报告推送后核对origin。
