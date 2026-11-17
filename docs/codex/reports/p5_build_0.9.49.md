# P5Joker 0.9.49 编译安装报告

- 构建源码：`b0a930cf P5Joker 0.9.49: drop the old "blade cut" transition, stage char select + embark assets`。
- 构建前确认游戏未运行。快进合并成功，两处版本号均为 `0.9.49`，13 个指定资源文件齐全，两份旧过场源码已移除。
- Release 编译成功，4 个 warning、0 个 error。
- PCK 打包成功，大小为 78,617,700 字节。
- 已安装到桌面游戏目录和 G 盘的 Slay the Spire 2 `v0.113.1`。两处的 DLL、JSON 和 PCK 均与构建产物通过 SHA-256 比对。
- 桌面版备份：`C:\Users\kkk\sts2_tools\backup\P5Joker_20261117_055346`。
- G 盘版备份：`C:\Users\kkk\sts2_tools\backup\P5Joker_0113_20261117_055433`。
- 未启动游戏，未做实机验收。

两处 `mods/P5Joker` 的文件清单一致：

| 文件 | 大小（字节） | 修改时间（UTC−06:00） |
| --- | ---: | --- |
| P5Joker.dll | 1,518,592 | 2026-11-17 05:53:04 |
| P5Joker.json | 682 | 2026-11-17 05:52:08 |
| P5Joker.pck | 78,617,700 | 2026-11-17 05:53:32 |
