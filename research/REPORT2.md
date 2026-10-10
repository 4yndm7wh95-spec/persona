# 第二轮收集报告

## 准备
成功。切换指定分支并拉取，无错误。未修改 Git 配置或游戏本体文件。

## 第1步：完整源码
成功。复制全部 .cs 和 .csproj，排除 System.Text.RegularExpressions.Generated 目录。
research/src_full 文件数：3529；总大小：18893207 字节。

## 第2步：本地化文本
成功。复制 zhs 和 eng 全部文件，保持语言目录相对路径。
research/localization 文件数：219。

## 第3步：版本信息

### C:/Users/kkk/Desktop/slaythespire/Slay the Spire 2/SlayTheSpire2.exe
成功

~~~text

FileVersionRaw     : 1.0.0.0
ProductVersionRaw  : 1.0.0.0
Comments           : 
CompanyName        : 
FileBuildPart      : 0
FileDescription    : 
FileMajorPart      : 1
FileMinorPart      : 0
FileName           : C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\SlayTheSpire2.exe
FilePrivatePart    : 0
FileVersion        : 1.0.0.0
InternalName       : 
IsDebug            : False
IsPatched          : False
IsPrivateBuild     : False
IsPreRelease       : False
IsSpecialBuild     : False
Language           : English (United States)
LegalCopyright     : 
LegalTrademarks    : 
OriginalFilename   : 
PrivateBuild       : 
ProductBuildPart   : 0
ProductMajorPart   : 1
ProductMinorPart   : 0
ProductName        : 
ProductPrivatePart : 0
ProductVersion     : 1.0.0.0
SpecialBuild       : 

~~~

### C:/Users/kkk/Desktop/slaythespire/Slay the Spire 2/data_sts2_windows_x86_64/GodotSharp.dll
成功

~~~text

FileVersionRaw     : 4.5.1.0
ProductVersionRaw  : 4.5.1.0
Comments           : Godot C# Core API.
CompanyName        : Godot Engine contributors
FileBuildPart      : 1
FileDescription    : GodotSharp
FileMajorPart      : 4
FileMinorPart      : 5
FileName           : C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\data_sts2_windows_x86_64\GodotSharp.dll
FilePrivatePart    : 0
FileVersion        : 4.5.1.0
InternalName       : GodotSharp.dll
IsDebug            : False
IsPatched          : False
IsPrivateBuild     : False
IsPreRelease       : False
IsSpecialBuild     : False
Language           : Language Neutral
LegalCopyright     : Copyright (c) Godot Engine contributors
LegalTrademarks    : 
OriginalFilename   : GodotSharp.dll
PrivateBuild       : 
ProductBuildPart   : 1
ProductMajorPart   : 4
ProductMinorPart   : 5
ProductName        : GodotSharp
ProductPrivatePart : 0
ProductVersion     : 4.5.1+f62fdbde15035c5576dad93e586201f4d41ef0cb
SpecialBuild       : 

~~~

### C:/Users/kkk/sts2_extract/addons/spine/spine_godot_extension.gdextension
成功

### C:/Users/kkk/sts2_extract/project.godot
成功

## 第4步：参考 mod

### 清单复制
成功

### mesugaki反编译
成功
退出码：0

### mesugaki源码复制
成功

### liuchuan反编译
失败
退出码：1

~~~text
The Assembly file name(s) field is required.
Specify --help for a list of available options and commands.
~~~

### liuchuan源码复制
失败

~~~text
Get-ChildItem: 
Line |
   2 |  … ch($file in Get-ChildItem -LiteralPath $temp -Recurse -File -ErrorAct …
     |                ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
     | Cannot find path 'C:\Users\kkk\sts2_round2_liuchuan' because it does not exist.
~~~

### liuchuan PCK样本复制
成功

刘川反编译时，指定目录顶层未找到 .dll，命令未收到程序集路径。按要求记录失败后继续。PCK 样本来自 C:/Users/kkk/sts2_extract/mods/刘川 战士 动画测试版 20260914 新待机与打击特效 v2/liuchuan，按指定扩展名、文件名及大小规则复制。

## 第5步：报告
成功。生成 REPORT2.md。

## 第6步：提交推送
待执行；最终结果以回复中的命令原始输出及哈希核对为准。

本轮新增内容统计（含统计前的报告）：3789 个文件，21011162 字节，未超过 400 MiB。
