# 大招召唤直接收手回待机：down1/2/3_b（2026-10-27）

开始前fetch+merge成功，快进到aa1c1ef01，保留Claude更新后的release_b=1.045/front、settle_b=1.033/front及既有final版本。按用户最新收尾路线，从settle_b直接放手回idle，不倒回扶脸帧。

## 三张各生成一次并立即推送

| 新图 | 编辑底图 | 提示词中的动作要求 | 原图提交 |
|---|---|---|---|
| summon_down1_b | summon_settle_b | 右手落向身侧但未完全放松，后脚撤约120px，身体转回1/3 | bf7d9b653 |
| summon_down2_b | summon_down1_b | 双手垂下，后脚累计撤约230px，身体转回2/3，衣摆落回 | 92fdf4bf4 |
| summon_down3_b | idle_a3 | 几乎待机，后脚比待机靠前约110px，衣摆微动 | bab1e75b4 |

使用内置image_gen，每张一次，没有审图、没有重画或追加帧。三张1024×1536 RGBA，透明残点只清理主体外alpha1～2，不改主体颜色；每张完整提示词、输入、原始输出路径和清理统计见对应*_generation.json。各次推送后均已ls-remote确认origin收到提交。

不画面具和特效是每张提示词约束；实际画面直接交给用户与Claude判断，没有把生成结果写成已经视觉验收通过。

## 归一化与一次序列检查

三张登记1.0/front，没有新增缩放或额外nudge。现有normalize_poses.main限定本批三张运行一次并合并normalize.json，既有PNG和配置保持不变；新final尺寸1536×1632，沿用PAD256/TOP96。不另外跑头部比例复验或为了检查数字返工。

`python tools/p5/sequence_check.py summon_release_b summon_settle_b summon_down1_b summon_down2_b summon_down3_b idle_a3`

```text
summon_release_b -> summon_settle_b  overlap 0.67  face moved    15  feet moved [99, 0]  ok
 summon_settle_b -> summon_down1_b   overlap 0.71  face moved    40  feet moved [131, 0]  JUMP - add a frame
  summon_down1_b -> summon_down2_b   overlap 0.71  face moved    21  feet moved [15, 0]  ok
  summon_down2_b -> summon_down3_b   overlap 0.42  face moved   183  feet moved [28, 0]  JUMP - add a frame
  summon_down3_b -> idle_a3          overlap 0.84  face moved    11  feet moved [197, 0]  JUMP - add a frame
3 jump(s); overlays: art\p5\poses\seq_summon_release_b_idle_a3.png
```

当前检查仍3处JUMP，如实记录，不自动补图、不放宽阈值、不加--allow、不调整顺序绕过结果。具体是否重画、补帧、缩放或交给游戏淡入，由用户与Claude决定。本次只按要求生成3张，没有在已有序列里增加第四张。

## 归一化登记

- summon_down1_b: {"scale": 1.0, "offset": [389, 79], "anchor_foot": "front"}; 清理残点396像素。
- summon_down2_b: {"scale": 1.0, "offset": [366, 81], "anchor_foot": "front"}; 清理残点45像素。
- summon_down3_b: {"scale": 1.0, "offset": [266, 96], "anchor_foot": "front"}; 清理残点190像素。

## 保留与提交

提交三张原图、对应生成记录、三张final、归一化登记、验收状态、本报告及summon_down_sequence.json。检查用叠色图不提交。art/cards另一会话11个修改原样保留，不提交/不stash/不还原；既有guard_a修改及其他无关未跟踪资源保留。未打开结城理mod图片，未读取obs.json或密码，未启动游戏，不rebase/reset/强推。最终报告提交推送后再核实origin。
