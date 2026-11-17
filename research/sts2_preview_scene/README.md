# 原版战斗背景与怪物骨骼：只读提取

## 用途与来源

供P5 Joker特效离线预览使用，保持游戏资源原始路径和场景参数。来源为本机SlayTheSpire2.pck（1,990,705,700字节），工具为已有Godot RE Tools 2.7.3。未安装工具、未修改游戏文件、未编译或启动游戏、未改mod代码。

## 内容

- overgrowth背景目录全部14个场景：1个背景入口和13个分层场景；递归补齐背景贴图、共享材质和依赖着色器。
- 四组怪物目录全部存在，无替换：cultists、chomper、fat_gremlin、inklet。
- cultists共用骨架，对应calcified_cultist.tscn和damp_cultist.tscn两个场景；其余分别对应同名场景。因此共5个怪物场景、4套Spine骨架。
- raw/：原始PCK字节，保留.import和.godot/imported下的CTEX、Spine导入资源。
- recovered/：可读场景、材质、着色器，标准Spine .skel二进制和.atlas文本，以及PNG图集；不是把骨骼重新烘成图片。
- LIST.csv沿用sts2_vfx五列格式，列出原路径、类型、字节数和依赖来源。DEPENDENCIES.json保存递归依赖图。
- contact_sheet.png包含全部68张恢复PNG，深灰底、每格图像区256px，下方标记文件名；只缩放联系表中的缩略图。

## 计数与限制

原始文件177个，共66,677,442字节；可读文件101个，共13,965,534字节，包含68张PNG、4份atlas、4份skel、19份tscn、5份tres和1份gdshader。

原始+可读资源80,642,976字节，超过最初80MB上限后暂停。用户于2026-11-02明确允许将总量上限放宽至90MB，再补齐本交付。最终体积见文末。单文件最大8,197,204字节，低于20MB上限。

## 提取和复现

工具位置：C:/Users/kkk/sts2_tools/gdre/gdre_tools.exe。游戏PCK：C:/Users/kkk/Desktop/slaythespire/Slay the Spire 2/SlayTheSpire2.pck。

1. 用 --headless --list-files=<PCK> 获取包内路径清单。
2. 初选res://scenes/backgrounds/overgrowth/、四组res://animations/monsters/目录和5个对应怪物场景，共35项。
3. 用 --headless --extract=<PCK> --output=<临时raw> --include=<精确res路径> 分批只读提取；递归追踪场景、材质和.import中的res路径。三轮补齐至177项，未解整个PCK。
4. 用 --headless --recover=<临时raw目录> --output=<临时recovered> --skip-loading-resource-strings 恢复此子集。完整命令见EXTRACTION.json。
5. 验证全部场景/材质/着色器依赖；atlas的4个图集页均存在。脚本仅记录，不解出或运行。

临时脚本和工具日志留在C:/Users/kkk/sts2_tools/tmp/sts2_preview_scene_20261102，仓库只提交research/sts2_preview_scene。

## 验证、失败和使用说明

- GDRE报告76项导入资源转换成功，0项失败、0项未转换。
- 56项源资源采用有损/VRAM压缩；PNG保留GDRE解码结果，没有再次有损压缩、裁切、缩放或改色。原始压缩前的像素不能由导入包逆推出。
- 部分PNG可能存在压缩块对齐尺寸差异，详见TEXTURE_INFO.csv和EXTRACTION.json；没有为凑尺寸改动PNG。
- 恢复子集没有project.godot，工具报告project config not loaded和无法推断版本等上下文警告，不据此判断游戏Godot版本；资源转换为0失败。日志留本机。
- 非排除范围的缺失依赖为0；4条脚本引用只记录于LIST.csv，不提取代码。未提取音频、字体、本地化。
- 场景仍保留原版脚本路径，因此这是资源集而非直接可运行的完整游戏工程。云端预览需要Spine运行支持，并由接收方处理脚本引用。
- PCK提取前后大小、修改时间一致；raw逐字节复制，recovered文本与原包文本逐字节一致，PNG与GDRE输出逐字节一致，保留Alpha。

- 本目录.gitattributes关闭raw/recovered的文本换行转换，保证Git内保留源文件字节；原版着色器自带行尾空白原样保留。

## 最终体积

共 285 个文件，总计 81873819 字节（81.87MB）；全部包含在仓库交付中，无仅本机资源。
