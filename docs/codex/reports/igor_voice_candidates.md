# 伊戈尔日语语音候选

基于 `016d0cc5`，在独立工作区整理本机 P3R 音频。试听入口为 `art/igor_voice_candidates/index.html`，来源与 Cue 对照见同目录 `catalog.csv`。编号 001–193 连续，每个编号对应一个完整音轨。OGG Vorbis 保留原采样率，未裁剪对白。

## 欢迎语音

| 编号 | 日语音频来源 | 原版英文字幕线索 |
|---|---|---|
| 001 | Voice_Event_Main_100_210_B / Cue1 / 3.821 秒 | Welcome to the Velvet Room. |
| 002 | Voice_Event_Qest_600_010_C / Cue1 / 3.369 秒 | Welcome... to the Velvet Room. |
| 003 | Voice_Event_Main_100_210_B / Cue7 / 5.061 秒 | 在天鹅绒房间接待你为客人的后续对白 |

001 和 002 对应完整的欢迎开场白。003 含欢迎语义，列为备用。首场剧情在伊戈尔自我介绍前标为 Mysterious Old Gentleman，沿同一对话段落和 Cue 归入伊戈尔。

## 来源与条数

| 日语 AWB / 配套 ACB | 导出条数 |
|---|---:|
| Voice_Facility_Velvetroom | 82（全部音轨，含其他说话人） |
| Voice_Event_Main_100_210_B | 15 |
| Voice_Event_Main_100_320_C | 16 |
| Voice_Event_Main_110_100_C | 18 |
| Voice_Event_Main_170_080_C | 14 |
| Voice_Event_Main_190_160_C | 15 |
| Voice_Event_Main_210_240_B | 24 |
| Voice_Event_Qest_600_010_C | 9 |
| Voice_Event_Qest_600_210_C | 0 |
| **总计** | **193** |

搜索了本机 `Xrd777/CriData/CueSheet/Facility`、`Event` 和相应的 `CriData/Stream`，并扫描 1,052 份原版英语本地化事件及设施对白资源中的说话人表和语音调用。用对白里的 Cue ID 对照日语库，剧情只导出确认属于伊戈尔的片段。Qest_600_210_C 的对白脚本和 ACB 有 Igor Cue6，但本机日语 AWB 仅有 Cue1–Cue5，ACB 无内嵌音轨。该条未导出。

本机未发现已安装的日语转写工具或 Whisper 模型，日语转写栏留空。目录中的英文是游戏对白字幕参考，欢迎标签以其内容为依据。193 个 OGG 均通过解码检查，试听页与目录条数一致。音频共 9,020,497 字节，低于 80 MB，音频与试听页一起提交。待选定后再接入模组。

