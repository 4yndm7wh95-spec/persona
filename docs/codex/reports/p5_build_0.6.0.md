# P5Joker 0.6.0 编译安装报告

- 执行日期：2026-10-31（America/Chicago）。
- 副本：C:\Users\kkk\sts2_tools\p5_build\repo。
- 分支：claude/p5r-ren-slay-the-spire-mod-6tk3f3。
- 开始前游戏进程检查无输出；未启动游戏，未结束任何进程，未做实机验收。

## 1. 更新副本

fetch、checkout、pull --ff-only 均成功。最新提交 log 输出：

```text
ec495c7 P5Joker 0.6.0: Persona summon with impact - red halftone stage, eye cut-in slams in and is torn apart
```

P5Joker.json 的 version 为 0.6.0，src/ModEntry.cs 的 Version 为 0.6.0。p5mod/data 已存在，未修改 sparse-checkout。

## 2. 编译 DLL

执行：

```powershell
& 'C:\Users\kkk\sts2_tools\dotnet\dotnet.exe' build -c Release -p:GameDir="C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2"
```

关键输出：

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:04.11
```

产物 bin/Release/net9.0/P5Joker.dll 存在，大小 333312 字节。

## 3. 打包 PCK

将 p5mod/pck_stage 全部内容复制到新目录：
C:\Users\kkk\sts2_tools\p5_build\stage_20261031_230500。

使用 GDRE headless，以 PCK 版本 3、引擎版本 4.5.1 打包；通过 Start-Process -Wait 等待完成，标准输出和错误输出分别保存到 p5_build/gdre_20261031_230500_out.txt、gdre_20261031_230500_err.txt。

生成文件：C:\Users\kkk\sts2_tools\p5_build\P5Joker_20261031_230500.pck。
大小：31737924 字节，大于 5 MB，满足打包成功标准。

## 4. 备份与安装

覆盖安装前已将原 mods/P5Joker 整个目录复制备份至：
C:\Users\kkk\sts2_tools\backup\P5Joker_20261031_230500。

安装目录：C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\mods\P5Joker。
仅覆盖 P5Joker.json、P5Joker.dll、P5Joker.pck。

安装后文件列表：

| 文件名 | 大小（字节） | 修改时间（America/Chicago） |
| --- | ---: | --- |
| P5Joker.dll | 333312 | 2026-10-31 23:04:52 |
| P5Joker.json | 541 | 2026-10-31 23:04:29 |
| P5Joker.pck | 31737924 | 2026-10-31 23:05:15 |

## 结果

编译、打包、备份和复制安装全部完成，无命令报错。未修改代码或特效，未生图，未启动游戏，未使用 Computer Use。