# 音频候选（待试听确认）

打开 listen.html 分组试听；candidates.csv 保留文件、来源、流序号、流名、时长、候选解释、可信度和实际OGG时长。

枪声、斩击命中、挥空和 persona_flame 有原始流名支持，可信度中。技能名表尚未解码，因此 Eiha/Eigaon/Maeiha/Sukukaja 目录只是对应纹理候选编号的初筛，不能当作已确认的技能音。
BP01 的日语语音按流序号和时长筛选，尚未试听转写；「ペルソナ！」「アルセーヌ！」与攻击喊声均未确认。分组只是试听目标，不是台词标签。没有把英语语音混入BP01候选。

原库已有音频直接复制其OGG，不重复有损转码；新EM0201候选通过vgmstream WAV管道转libvorbis q4。所有候选均以FFprobe核验为Vorbis并记录实际时长。旧库cue_index为原表编号照录；新EM0201明确使用vgmstream -s 1..8，没有改变旧目录。
C0001_051_B.DAT 原表标记metadata_error，未将其猜作可播放音频。
