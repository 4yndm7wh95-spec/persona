# 0.15.31 卡图构建与安装报告

基于 `claude/admiring-knuth-lqj1x7` 的 `de6c50c72dc14c237854dbe7db1aa2f3d9d3a43d`。按 `docs/codex/codex_v0.15.31_card_art.md` 构建，不修改代码、版本号或源图片。用户随后明确“审查工作交给我就行，你装完告诉我”，因此未启动游戏，实机验收留给用户。

## 编译

使用本机 .NET SDK 10.0.401，目标 net9.0、Release。分别以桌面版游戏 v0.111.0 和 Steam 版 v0.113.0 的 GameDir 编译；两套均 **0 警告、0 错误**，生成各自 DLL。旧 Downloads 目录中的 v0.107 安装已不存在。

```powershell
dotnet build mod/P3Makoto.csproj -c Release '-p:GameDir=C:/Users/kkk/Desktop/slaythespire/Slay the Spire 2' -o mod/_build/card_art_0.15.31/build111
dotnet build mod/P3Makoto.csproj -c Release '-p:GameDir=G:/SteamLibrary/steamapps/common/Slay the Spire 2' -o mod/_build/card_art_0.15.31/build113
```

完整输出见 [0.111 构建日志](card_art_0.15.31/build111.log) 和 [0.113 构建日志](card_art_0.15.31/build113.log)。

## PCK

从 `mod/pck_stage` 创建独立副本后运行 `tools/pck/remove_card_skins.py`，删除数为 0，`--check` 通过。使用 GDRE Tools 重打 PCK，格式 v3、目标引擎 4.5.1。成品大小 154,210,724 字节，共 754 个文件。

`p3makoto/card_art/` **95 个文件 = 94 张 JPG + index.txt**，没有 `.import`。通过 GDRE `--list-files` 以及独立 PCK 目录解析两种方式确认数量。逐个读取 PCK 内的 95 个文件：与仓库源文件字节完全相同，PCK 记录的 MD5 校验也全部通过。index.txt 共 94 个非空条目。

PCK SHA-256：`39307a095955b9bdd74a46035ba3b8ea663b31d05ed9bbc9c7c987b7b1e37018`。

- [95 个文件清单](card_art_0.15.31/card_art_pck_files.txt)
- [逐文件字节数与 SHA-256](card_art_0.15.31/card_art_pck_manifest.json)

## 安装与 ZIP

确认两套游戏均未运行后，先备份原来的 DLL、PCK 和清单，再安装 0.15.31。两处安装的三份文件均与对应构建产物 SHA-256 一致。ZIP 内三份文件也逐一核对哈希一致，桌面 `P3RMOD` 存有 ZIP 副本。

### 游戏 v0.111.0

- 安装目录：`C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\mods\P3Makoto`
- 原模组版本：0.15.29
- 备份：`C:\Users\kkk\Documents\Codex\2026-10-21\p3makoto-c-users-kkk-documents-codex\work\hammer_furnace_v5_8339f23a\mod\_build\card_art_0.15.31\backup_20261113_100421_111`
- ZIP：`C:\Users\kkk\Documents\Codex\2026-10-21\p3makoto-c-users-kkk-documents-codex\work\hammer_furnace_v5_8339f23a\mod\dist\0.15.31\P3Makoto_0.15.31_game0.111.0.zip`
- ZIP SHA-256：`44ba784cb77c96c4c2112d98e9c08b79ae24299791d0579295fe55c6cbf9f59a`

### 游戏 v0.113.0

- 安装目录：`G:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\P3Makoto`
- 原模组版本：0.15.31
- 备份：`C:\Users\kkk\Documents\Codex\2026-10-21\p3makoto-c-users-kkk-documents-codex\work\hammer_furnace_v5_8339f23a\mod\_build\card_art_0.15.31\backup_20261113_100421_113`
- ZIP：`C:\Users\kkk\Documents\Codex\2026-10-21\p3makoto-c-users-kkk-documents-codex\work\hammer_furnace_v5_8339f23a\mod\dist\0.15.31\P3Makoto_0.15.31.zip`
- ZIP SHA-256：`1b4047f1ec5b2be0a912954cc9c5bcce991be4a7b2ba32940e4d728355ad9c19`

各安装文件的 SHA-256、大小及完整路径见 [安装清单](card_art_0.15.31/installation.json)。ZIP 为本机交付产物，本次 Git 提交只包含报告、构建日志及打包/安装清单。

## 实机验收：交由用户，未执行

未启动游戏，未检查手牌、选牌奖励、牌组界面和设置中的「卡图」开关。未获取运行时 `card art: 94 illustrations in pck` 或解码错误日志。包内数量与字节校验不能替代游戏内验收。

未进入商店；以下三项没有本次实测数据，待用户检查，未抄用旧日志：

```text
perf velvet shop install: 未采集
perf shop stage built: 未采集
perf shop warmup: 未采集
```

## 获取资源的说明

分支已快进至 de6c50c7。补齐稀疏检出时，Git 批量延迟拉取两次连接重置；随后对 101 个缺失文件按该提交的固定 raw URL 分文件获取，并逐个验证 Git blob SHA-1 后写入本地对象库。全部 867 个构建输入从该提交物化；后续 sparse-checkout 成功，未改动 mod/src、版本清单或 PCK 源资源。
