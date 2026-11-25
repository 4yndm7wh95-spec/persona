# P5R 日替わり（日历切换）原版音效

录屏中有三个可辨认的声音，对应两个原版 cue。飞刀插入的低频闷响和随后持续的金属声都在 `stab_knife` 里。这里交付每个 cue 的完整原音。

| 录屏中的声音 | 游戏包内流序号（从 1 开始） | Cue 名 | 文件 | 源音频 |
| --- | --- | --- | --- | --- |
| 「30」砸下的重击 | 37 | `calendar_change` | `calendar_change.ogg` | 48,000 Hz，双声道，33,540 样本/声道 |
| 飞刀钉住的金属余响 | 82 | `stab_knife` | `stab_knife.ogg` | 44,100 Hz，单声道，61,910 样本 |
| 飞刀插入的低频闷响 | 82 | `stab_knife` | 同上 | 同上 |

两个文件源自 `SOUND_J.CPK → SOUND/SYSTEM.AWB`，同目录 `SYSTEM.ACB` 提供 cue 名。按相同流序号从 `SOUND_E.CPK` 中解码后，两条 PCM 均与日版逐字节一致。

## 核对依据

- 用 vgmstream 枚举日版和英语版 SYSTEM 音效包。每版 AWB 494 条流，ACB 内嵌 259 条。优先搜索 `calendar / higawari / day / date / knife / naifu`。也搜索了 `audio_catalog.csv` 中的资源路径，包括 TITLE、FIELD、MOTIONSE 等候选包，索引没有单列日历音效包。
- 把本机原版录屏的音轨解码到内存做互相关。流 37 在约 4.553 秒开始匹配，85–7000 Hz 归一化互相关约 0.821。流 36 `calendar_change_long` 不是这次录屏中「30」的重击。
- 流 82 在约 5.803 秒开始匹配，宽频瞬态约 5.893 秒。25–320 Hz 闷响与 3.8–6.6 kHz 金属余响在同一流起点匹配，归一化互相关分别约 0.980 与 0.952。流 82 余响峰为约 4268 Hz 与 6128 Hz。流 83 `stab_newyear` 的低频部分相近，但高频匹配低于流 82（约 0.852），是不同版本。
- 发布的 OGG 只使用游戏包解码的完整 WAV。沿用 vgmstream 忽略循环单次解码（`-i -s N`），再用 FFmpeg `libvorbis -q:a 4` 转码。采样率和声道数与源一致，没有剪裁、音量调整或频段分离。日期重击的 OGG 在 FFmpeg 回读时比源 PCM 多出约 20 毫秒编码尾部填充。
