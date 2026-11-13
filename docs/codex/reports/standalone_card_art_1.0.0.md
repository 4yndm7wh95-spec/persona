# 独立卡面 Mod 1.0.0 · 拆分与安装报告

用户纠正：94 张插画应制作成独立卡面替换 Mod，不能集成进 P3Makoto。本次在 de6c50c7 的整合方案及 c1065955 安装报告之后拆分；此前整合包已被本次安装替代。

## 两个独立 Mod

- **P3Makoto 0.15.32**：移除 CardArt 代码、Portrait 补丁、初始化注册、「卡图」设置及其存储属性；移出 95 个卡图资源。其余功能保留。旧设置文件里的 CardArt 字段由 JSON 读取正常忽略。
- **P3CardArt 1.0.0**：新项目、清单、DLL 和 PCK。无 P3Makoto 程序集引用，不读取其设置、骨骼或角色状态，可单独安装，亦可搭配主 Mod。只根据覆盖清单替换 CardModel.Portrait；卡框、文字和玩法不改。保留纹理缓存和牌组后台预取。
- 独立资源路径为 `res://p3cardart/card_art/`，仅 94 张 JPG 加 index.txt。原图原样迁移，未重新生成或重新编码。导出工具默认目标改为独立项目，以免以后导回主包。
- 在游戏 Mod 管理中启用/停用 P3CardArt 后重启游戏生效；主 Mod 不再含「卡图」选项。相同卡牌的其他卡面 Mod 可能覆盖彼此。

## 构建与隔离校验

两项目均针对本机 v0.111.0 与 v0.113.0 分别编译，四次均为 **0 警告、0 错误**。源码分别为 `mod/` 与 `cardart/`。

GDRE Tools 从各自 PCK 源的副本重打 v3、引擎 4.5.1 的包：

| 包 | 文件数 | 字节数 | 校验 |
|---|---:|---:|---|
| P3Makoto.pck | 659 | 135237252 | 不含任何 card_art 或 p3cardart 资源，其余文件内容与原 0.15.31 包逐个 SHA-256 完全一致 |
| P3CardArt.pck | 95 | 18973700 | 恰好 94 张 JPG + index.txt，无 .import；各文件 SHA-256 与拆分前的卡图完全一致 |

主包 SHA-256：`e228dbab7523073b3cae52fe81834ebf7cfd41311a299c4163b7d1b6ebdd6e5a`。

卡面包 SHA-256：`562351a67953239c02570a9c5c23eaf8bb48542a75dc8dca5e71c795ad612f95`。

另用独立 Godot 4.5.1 空项目只加载 P3CardArt.pck，未加载主 Mod。通过 FileAccess 和 Image.load_jpg_from_buffer 实际读取、解码 **94/94** 张 JPG 成功。这项是资源隔离检查，不代表游戏中 Harmony、菜单或卡图布局已经实测。

## 安装与交付

确认游戏进程未运行后，备份两处旧主 Mod 的三份文件，安装主 Mod 0.15.32，并新增独立 P3CardArt 1.0.0。所有安装文件均核对为对应构建产物哈希，四份 ZIP 内三份文件也均核对一致。

- v0.111.0 / P3Makoto 0.15.32
  - 安装：`C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\mods\P3Makoto`
  - 备份目录：`C:\Users\kkk\Documents\Codex\2026-10-21\p3makoto-c-users-kkk-documents-codex\work\hammer_furnace_v5_8339f23a\mod\_build\standalone_01532\backup_20261113_103040_111\P3Makoto`（新安装的卡面 Mod 原先不存在，无旧文件）
  - ZIP：`C:\Users\kkk\Documents\Codex\2026-10-21\p3makoto-c-users-kkk-documents-codex\work\hammer_furnace_v5_8339f23a\mod\dist\0.15.32\P3Makoto_0.15.32_game0.111.0.zip`
- v0.111.0 / P3CardArt 1.0.0
  - 安装：`C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\mods\P3CardArt`
  - 备份目录：`C:\Users\kkk\Documents\Codex\2026-10-21\p3makoto-c-users-kkk-documents-codex\work\hammer_furnace_v5_8339f23a\mod\_build\standalone_01532\backup_20261113_103040_111\P3CardArt`（新安装的卡面 Mod 原先不存在，无旧文件）
  - ZIP：`C:\Users\kkk\Documents\Codex\2026-10-21\p3makoto-c-users-kkk-documents-codex\work\hammer_furnace_v5_8339f23a\cardart\dist\P3CardArt_1.0.0_game0.111.0.zip`
- v0.113.0 / P3Makoto 0.15.32
  - 安装：`G:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\P3Makoto`
  - 备份目录：`C:\Users\kkk\Documents\Codex\2026-10-21\p3makoto-c-users-kkk-documents-codex\work\hammer_furnace_v5_8339f23a\mod\_build\standalone_01532\backup_20261113_103040_113\P3Makoto`（新安装的卡面 Mod 原先不存在，无旧文件）
  - ZIP：`C:\Users\kkk\Documents\Codex\2026-10-21\p3makoto-c-users-kkk-documents-codex\work\hammer_furnace_v5_8339f23a\mod\dist\0.15.32\P3Makoto_0.15.32.zip`
- v0.113.0 / P3CardArt 1.0.0
  - 安装：`G:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\P3CardArt`
  - 备份目录：`C:\Users\kkk\Documents\Codex\2026-10-21\p3makoto-c-users-kkk-documents-codex\work\hammer_furnace_v5_8339f23a\mod\_build\standalone_01532\backup_20261113_103040_113\P3CardArt`（新安装的卡面 Mod 原先不存在，无旧文件）
  - ZIP：`C:\Users\kkk\Documents\Codex\2026-10-21\p3makoto-c-users-kkk-documents-codex\work\hammer_furnace_v5_8339f23a\cardart\dist\P3CardArt_1.0.0.zip`

四份 ZIP 均有桌面 P3RMOD 副本。主 Mod 和卡面 Mod 分别打包，无合并 ZIP。Steam v0.113 使用无游戏后缀 ZIP，桌面 v0.111 使用 `_game0.111.0` 后缀。

用户此前已要求自行审查，本次未启动游戏。手牌、奖励、牌组显示以及主 Mod 和独立卡面一起使用的实机表现，均待用户验收。未采集商店三项 perf 日志。

构建日志、独立 Godot 解码结果、PCK 校验及安装/ZIP 哈希见同名附件目录。
