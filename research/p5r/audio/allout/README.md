# 总攻击音频元数据

读取本机现有的 667 个非 BGM ACB。通过 CRI UTF 表读取 CueName/Cue，按 Sequence、Track、Event、Synth 定位 Waveform。时长根据 `NumSamples / SamplingRate` 计算，按波形分别列出。协议字段的解析依据见项目源码。 [vgmstream ACB 解析器](https://github.com/vgmstream/vgmstream/blob/master/src/meta/acb.c)

- `catalog.csv` 有 20 条关键词匹配，日英 `SYSTEM.ACB` 各 10 条。其中各 7 条名称明确关联总攻击，另外各 3 条只是匹配 `finish`。
- `joker_voice_cues.csv` 有日英 `BP01.ACB` 各 293 条命名 cue。每条均找到至少一个引用波形。一个 cue 可对应多个随机或分轨波形，所以 cue 数量和 AWB 流数不同。两种语言的语音名称均无本轮关键词命中。
- 波形 ID 字段采用 `Streaming 标记:AWB ID`。ACB cue ID、CueIndex、波形行号和 AWB ID 是不同字段。已有音频的编号口径未经核对，不能据此认定哪些素材此前已经导出。

本目录仅含元数据。没有从 cue 名推断 Joker 的具体台词，也没有转码或上传 OGG。
