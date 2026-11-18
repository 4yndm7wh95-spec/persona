# P5R 总攻击资源调查报告

## 已查证

- **喷溅**：确认 `BKSK_HERO.BCD` 在偏移 207 和 614 引用 `bksk_splatter.EPL`。喷溅文件内确认 `splatterBlood`、Plane01–Plane04、SPARK0，以及 `sozai/epd/be0cmn_smk_nml.EPD` 的字符串位置。既有 CPK 索引没有独立的同名 EPD 条目。偏移和嵌入标记记在 `splatter/README.md`。
- **碎玻璃和乱斗**：记录 7 条相关包内路径、39 个可读资源引用和 16 个 DDS 头。`BKSK_JUMP_CTE.EPL` 有三组 `glassBr_anim` 和 `black_jmp_bksk` 引用。`BKSK_ATTACK_CTE.EPL` 有乱斗底板 GFS、闪光 GMD 和 `bksk_attack_do.EPL` 引用。只在现有索引中找到 `BKSK_ATTACK.BCD` 和 `BKSK_JUMP.BCD`，未确认三份 BCD 的声音指令。
- **音效**：读了本机 667 个非 BGM ACB。日英 `SYSTEM.ACB` 分别确认 cue ID **1017** `battle_all_out_atk`、**1018** `battle_all_out_atk_finish`、**1074** `battle_all_out_atk_finish_b`，以及双叶、双子和拉雯妲的同系列变体。命名和波形表证明关联，尚不能据此确定原版 BCD 的实际触发时刻。
- **Joker 语音**：日英 `BP01.ACB` 各列出 **293 条命名 cue**，含 cue ID、行号、引用波形和波形时长。两种语言都没有总攻击关键词命中，无法仅凭名字确认开场或收尾台词。CRI 关联按 vgmstream 的代码核对。 [vgmstream ACB 解析器](https://github.com/vgmstream/vgmstream/blob/master/src/meta/acb.c)

## 提交范围与限制

主工作树有 2,445 条已暂存改动和 6 条未跟踪路径。经用户要求继续，改用从 `claude/exciting-babbage-lm8ubb` 建立的隔离工作树。本轮提交只有新建的说明、元数据 CSV 和本报告。未发布原版资源副本、完整网格和动画轨道、PNG、OGG 或预览，也未启动游戏。

## 遇到的错误

主工作树中历史清单和音频目录的读取分别返回：

```text
Get-Content: Cannot find path 'C:\Users\kkk\sts2_tools\p5_build\repo\tools\p5\allout_native\inventory.json' because it does not exist.
rg: research/p5r/audio: IO error for operation on research/p5r/audio: The system cannot find the file specified. (os error 2)
```

首次报告提交触发部分克隆补取并失败。后来在隔离索引上仅生成新文档的提交：

```text
error: RPC failed; curl 56 Recv failure: Connection was reset
error: 109 bytes of body are still expected
fetch-pack: unexpected disconnect while reading sideband packet
fatal: early EOF
fatal: index-pack failed
fatal: could not fetch 9d4752965617df2658f418f5aa0f6396f64c3843 from promisor remote
```
