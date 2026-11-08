# 眼神特写音效

本目录包含两段从 P5R 原版提取的音频。按用户指定，在 mod 中使用以下触发时机：

| 文件 | mod 中的用途 |
|---|---|
| battle_cutin.wav | **眼神特写出现时播放** |
| damage_critical.wav | **眼神特写被捏碎时播放** |

第二段原版内部名是 damage_critical；用于“眼神特写被捏碎”是用户指定的 mod 用法，不表示它在 P5R 原版中就是该事件的音效。

来源为 SOUND_E.CPK 中的 SOUND/SYSTEM.AWB（battle_cutin，第18个流）和 SOUND/SYSTEM.ACB（damage_critical，第21个流），流序号按 vgmstream 从1计数。使用 vgmstream CLI r2117 解码为 PCM WAV，未调整音量、未重采样、未裁切。详细采样率、声道、时长及 SHA256 见 SOURCE.json。

本次只提交音频与用途说明，尚未修改 mod 播放逻辑。
