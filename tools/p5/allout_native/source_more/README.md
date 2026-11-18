# 跳入、碎玻璃和乱斗索引

- `package_index.csv` 对照现有 CPK 索引，记录 7 条请求相关的包内路径。
- `resource_references.csv` 记录从本机已有文件中扫描到的资源路径和字节偏移。出现路径表示文本引用，资源边界可能还需通过实际容器目录验证。
- `dds_headers.csv` 记录 16 个通过固定头尺寸检查的 DDS 头，包括尺寸、偏移和 FourCC。相同贴图可能重复出现。

`BKSK_JUMP_CTE.EPL` 可定位三组 `glassBr_anim` GFS 名称和 `black_jmp_bksk` 贴图名称。`BKSK_ATTACK_CTE.EPL` 可定位 `be0cmn_bokosuka_matte_attack.GFS`、`bksk_attack_kira.GMD` 和 `bksk_attack_do.EPL`。本机扫描范围内已有 `BKSK_HERO.BCD`，读到其收尾与喷溅引用。另两份 BCD 只核对了包内路径，暂未确认声音指令或编号。未纳入原文件、转换贴图和完整轨道数据。
