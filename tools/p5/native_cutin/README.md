# P5R 原版眼神切入转换

输入是原版 `BATTLE/CUTIN/BCT_P_SUMMON.GMD` 和 `BCT_P_SUMMON_BT.GMD`，本目录保留两个只读副本。用 GFD Studio 0.10.3 的 GFDLibrary 解码，不依赖游戏运行，也不改 CPK。

主文件包含裂缝、白纸、五段面部裁切、黑边、红纸和碎屑；BT 包含双感叹号的定格、破碎及白闪。KA 是另一种字形，不用于参考视频里的双感叹号。两个输入均有 2.6666667 秒的动画时钟、Speed=2.5；可见主动画约 0.853 秒，随后是原文件的透明尾段。

导出器将原始节点世界矩阵烘焙到 1280×720 平面，按世界 Z 排序，保留 UV、材质色、三角形和显隐时间；只省去零面积三角形。`SingleAlt_2` 按阶梯显隐播放，`MaterialSingle_4` 按线性透明度播放。后者在 BT 中仅用于白闪。面部贴图槽 `btl_cutin.dds` 绑定 mod 已有的 `eyes.png`（Joker），按原始不透明材质处理 alpha。

```powershell
# 从仓库根目录运行；GfdDir 指向 GFD Studio 0.10.3 的解压目录。
dotnet build tools/p5/native_cutin/Export.csproj -c Release -p:GfdDir="<GFD Studio 目录>"
dotnet --roll-forward Major tools/p5/native_cutin/bin/Release/net9.0/Export.dll tools/p5/native_cutin p5mod/data/eye_cutin.json
dotnet build p5mod/P5Joker.csproj -c Release -p:GameDir="<杀戮尖塔 2 目录>"
```

输出应为 51 个网格、4764 个非退化三角形。JSON 包含两个原始文件的 SHA-256；DLL 将 JSON 嵌入资源 `P5Joker.EyeCutIn.json`，因此不用给玩家分发 GMD、GFDLibrary 或散装 JSON，也不用新增贴图导入。

独立 Godot 4.5.1 预览（不运行游戏，不要求 C# 版 Godot）：

```powershell
New-Item -ItemType Directory tools/p5/native_cutin/frames
& '<Godot 4.5.1 executable>' --path tools/p5/native_cutin --script capture.gd --rendering-method gl_compatibility --write-movie tools/p5/native_cutin/frames/f.png --fixed-fps 60 --quit-after 90
```

该预览读取同一份 JSON、用同样的 ArrayMesh 和显隐规则，验证引擎里的几何、UV 和材质。它没有加载游戏，也不等于 C# 模组在战斗中的实机验收。原有 C# 特效预览工程也已嵌入该资源，可继续使用 `cutin` 场景。

格式参考：[GFD Studio 源码](https://github.com/tge-was-taken/GFD-Studio)、[AnimationLayer](https://github.com/tge-was-taken/GFD-Studio/blob/master/GFDLibrary/Animations/AnimationLayer.cs)。
