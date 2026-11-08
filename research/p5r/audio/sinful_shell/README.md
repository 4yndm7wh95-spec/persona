# 大罪穿甲弹与撒旦耶尔原版音频检索

已交付 2 个日英语音文件和 8 个低置信度音效候选。**尚未确认大罪穿甲弹开枪、蓄力、飞行、命中或特殊召唤的原版声音。**

## 语音结果

- `BP01_stream370_bp01_p_170_ja.ogg`，日语，0.872333 秒。
- `BP01_stream370_bp01_p_170_en.ogg`，英语，0.878542 秒。

来源分别为 SOUND_J.CPK 和 SOUND_E.CPK 的 `SOUND/BATTLE/BP01.AWB`。在对应 ACB 里解析 UTF 表得到同一条引用链：`bp01_p_170` → CueId 1170 / CueIndex 180 → Sequence 180 → Track 292 → Synth 292 → Waveform 279 → StreamAwbId 369 → vgmstream 第 370 流。vgmstream 原始元数据也直接返回流名 `bp01_p_170; bp01_p_199`。两种语言都是 24 kHz 单声道。

对「该语音被 PS0170 的呼名 cue 引用」把握高；该声音也被 `bp01_p_199` 共用。未进行听写，因此不把它的实际台词宣称为已确认的“サタナエル / Satanael”。`voice_cue_mapping.json` 保存原始表字段。

## 音效候选

`catalog.csv` 列出每个输出的来源包、包内路径、vgmstream 的 **1 起始流序号**、可读取的流名、原长度和两个目标的相关分数。流序号不是 ACB 的 CueIndex，二者不能混用。无法确认的 cue 名留空。

- `SKILLSE_0020_stream01.ogg`、`SKILLSE_0113_stream01.ogg`、`SKILLSE_0390_stream01.ogg`、`SKILLSE_0096_stream01.ogg` 是技能音效范围内按剧情目标分数挑出的不同候选。
- `EVENT_E113_001_SE_stream04.ogg`、`EVENT_E202_001_SE_stream11.ogg`、`EVENT_E419_001_SE_stream04.ogg`、`EVENT_E132_002_SE_stream01.ogg` 是事件范围内按战斗目标分数挑出的 0.8～12 秒候选。
- 以上八个文件都只供试听筛选，置信度低，不能直接认定为开枪或特殊召唤。部分流名如 `cut3`、`0326_move` 也没有提供可靠的大罪穿甲弹语义。
- `top20.csv` 分别列出对战斗目标和剧情目标得分最高的 20 个 SFX 流，包含同声异包。短促通用音效容易得到较高分，这种得分不是识别概率。

## 四条检索路线

1. **技能表**。检查本机已展开的 `BATTLE/TABLE/NAME.TBL`、`SKILL.TBL` 和 SKILL 的四个子块。NAME 采用游戏自定义字符编码，直接搜索 UTF-8、UTF-16LE、Shift-JIS 的英文/日文/中文名称均未命中，未完成可信的本机技能编号解码。外部原始 ID 清单给出 `0x0296 = 662`（下附来源），作为检索线索。全 CPK 音频目录中没有 `SOUND/SKILLSE/0662.ACB`，不能把技能 ID 直接当作声音文件号。
2. **0170 和动作引用**。全音频索引按 0170 / satanael 检索，找到 `SOUND/SKILLSE/0170.ACB` 及 `BE_BOSS_0170`，后者是敌人命名空间，不能据此当作 PS0170。探测 0170.ACB，只有一条 `cut3` 流，没有足以确认的匹配。未解码 GAP 中的音效指令，未宣称建立了技能—声音映射。
3. **全部流与事件包**。扫描本机 BASE 音效的 647 个 ACB，其中 642 个可读，共 725 流；包括完整 SOUND/SKILLSE 和已选 SOUND/MOTIONSE。另从 SOUND_J / SOUND_E 提取全部 16 个语言专用 SKILLSE 包，共 48 流。从 BASE 选择性提取 SOUND/EVENT 全部 4,359 个 ACB/AWB 文件，按 ACB 优先、必要时读配对 AWB，得到 4,335 个可读事件流。共比较 5,108 个 SFX 流。新增提取物约 430 MB，全部只留在本机资源库。未全量提取日英 EVENT 对白（各约 2 GB）或 EVENT_BATTLE，故这些范围仍可能藏有目标。
4. **Joker 语音**。解析日英 BP01 的全部 293 个 cue、410 个 waveform；发现 `bp01_p_170` 显式引用链。另将两种语言的 AWB 1～410 流完整比较，未依赖旧版从 0 开始的文件编号。旧脚本使用 0 作为默认首流，会重复第一流并漏最后一流，本次语音导出直接读取原 AWB 第 370 流。

## 比对与转码

- 目标是仓库现有 `target_battle_11.0-16.2s.wav` 和 `target_event_full.wav`，原文件未改。
- 比对副本转单声道 11,025 Hz，FFT 1024、hop 256，使用按对数频率划分并去重后的 30 个实际频带，计算 log(1+1000×平均功率)，各频带去时间均值后滑动归一化相关。仅分析时去掉极低电平首尾，候选交付文件不裁切。
- vgmstream r2117 解码原始流，FFmpeg 9.0.2 以 libvorbis q4 转 OGG，保持原采样率与通道；没有剪辑、增益、EQ、音色或时长变更。
- 已验证输出能被 FFmpeg 解码，时长与流元数据一致（允许 Vorbis 封装的样本舍入）。

## 失败与边界

5 个 BASE 容器无法被 vgmstream 探测：`0117.ACB`、`0269.ACB`、`0304.ACB`、`0379.ACB`、`0735.ACB`。保留原文件，未修改工具。README 旧游戏路径少了 `CPK/` 一层，最初一次提取尝试返回 FileNotFoundException，改用实际存在的 `G:/Gamebox/P5R/ZS6425/P5R/CPK/BASE.CPK` 后正常。初版 UTF 解析器把新版表头的版本字段计入 row offset，断言失败；按原始表头改正本机分析脚本后完成读取。上述均未修改游戏文件或仓库代码。

本机工作目录 `C:/Users/kkk/sts2_tools/p5r_rip/audio/sinful_shell_work/` 留有 `container_metadata.json`、`extra_metadata.json`、`rankings.json`、`extra_rankings.json`、`voice_rankings.json`、解析表和提取日志/辅助脚本，便于后续复查。原始提取物在其 `extracted/` 子目录。仓库仅新增本目录 OGG 和说明。

外部编号线索来自 [Ruimusume/P5R 原始 Skill ID 清单](https://raw.githubusercontent.com/Ruimusume/P5R/main/Skill%20ID.txt)。该来源仅辅助编号检索，声音身份以原版 ACB 引用为证据。
