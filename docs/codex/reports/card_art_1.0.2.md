# P3CardArt 1.0.2 编译、打包与安装报告

基于 claude/admiring-knuth-lqj1x7 的 db2b094fbb39136bf2028a6b95340f7cc7f90f21，按 codex_card_art_1.0.2_soft.md 执行。未修改 cardart/src、mod/src 或图片，未重新导出图片。主模组两处已安装文件的 SHA-256 与操作前完全相同。

## 构建与补丁检查

分别对桌面 v0.111.0 和 Steam v0.113.0 Release 重新编译，均 0 警告、0 错误。使用指定 GameDir 的实际程序集。独立 .NET 9 进程核对游戏 DLL 哈希并实际安装 Harmony 补丁：0.111 的 Portrait / NCard.UpdatePortrait，0.113 的 Portrait / BigPortrait / NCard.UpdatePortrait 均通过；插画字段存在。日志附于同名目录。

## PCK 检查

从 cardart/pck_stage 的独立副本使用 GDRE Tools 打包，格式 v3，引擎 4.5.1。

- p3cardart/card_art/ 恰好 **189 个文件**：94 张普通 JPG、94 张 _big.jpg 和 index.txt；包内无其他文件、无 .import。
- 独立解析 PCK，189 个文件与源目录逐字节相同，MD5 校验通过。
- 188 张图片全部可解码。普通图尺寸为 250×190，远古牌为 250×351；大图分别为 500×380 / 500×702。
- index.txt 恰好 94 个唯一条目，各条目普通图和大图均存在。
- PCK SHA-256：11362d3dbbf89c751802b98585f1a81ef157ebff020cbef5c82339a93e89e977。

清单见 card102_files.txt；逐文件尺寸、字节数和 SHA-256 见 card102_manifest.json。

## 安装与 ZIP

确认游戏未运行后，备份两处旧 P3CardArt 的 DLL/PCK/清单，再安装 1.0.2。安装文件及两份 ZIP 的三个成员哈希均与对应构建产物一致。ZIP 已复制到桌面 P3RMOD。

- v0.111.0：`C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\mods\P3CardArt`
  - 备份：`C:\Users\kkk\Documents\Codex\2026-10-21\p3makoto-c-users-kkk-documents-codex\work\hammer_furnace_v5_8339f23a\cardart\_build\1.0.2\install\backup_20261113_224556_111\P3CardArt`
  - ZIP：`C:\Users\kkk\Documents\Codex\2026-10-21\p3makoto-c-users-kkk-documents-codex\work\hammer_furnace_v5_8339f23a\cardart\dist\P3CardArt_1.0.2_game0.111.0.zip`
- v0.113.0：`G:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\P3CardArt`
  - 备份：`C:\Users\kkk\Documents\Codex\2026-10-21\p3makoto-c-users-kkk-documents-codex\work\hammer_furnace_v5_8339f23a\cardart\_build\1.0.2\install\backup_20261113_224556_113\P3CardArt`
  - ZIP：`C:\Users\kkk\Documents\Codex\2026-10-21\p3makoto-c-users-kkk-documents-codex\work\hammer_furnace_v5_8339f23a\cardart\dist\P3CardArt_1.0.2.zip`

## 游戏内检查由用户进行

用户明确说“我来看就行”，本次未启动游戏。手牌、奖励和牌组的柔和程度，以及 0.113 放大查看时的大图清晰度，均待用户验收。未采集本版运行时 init v1.0.2 / card art: 94 illustrations in pck 日志；补丁安装检查不能代替显示效果确认。

补齐稀疏检出时，192 个本地缺失对象按固定提交下载并验证 Git blob SHA-1 后物化；没有改写资源内容。
