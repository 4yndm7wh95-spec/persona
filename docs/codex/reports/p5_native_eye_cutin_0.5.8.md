# P5Joker 0.5.8：原版眼神特写复用

## 结果

找到原版完整演出数据，已转换并接入 mod，优先复用了原版，未新画 Joker 眼神图。

- 原始主文件：`BATTLE/CUTIN/BCT_P_SUMMON.GMD`，143255 字节。
- 双感叹号及破碎：`BATTLE/CUTIN/BCT_P_SUMMON_BT.GMD`，30861 字节。
- Joker 原版贴图：`BATTLE/CUTIN/BCT_P_BC0001.DDS`；继续使用仓库现有的 `p5mod/pck_stage/p5joker/fx/eyes.png`（1920×768，是原版 3840×1536 图的缩小版本）。
- 本机来源：`C:/Users/kkk/sts2_tools/p5r_rip/BATTLE/CUTIN/`。两个 GMD 的只读副本保存在 `tools/p5/native_cutin/`，SHA-256 写入转换 JSON。

参考视频的眼神是佑介，本实现按用户要求换成 Joker。`SUMMON_KA` 是另一字形；视频里的双感叹号经实际几何预览确认在 `SUMMON_BT`，不按文件名字面猜测。

## 接入方式

原版 GMD 不能直接交给 Godot 播放。本次用本机 GFD Studio 0.10.3 的 GFDLibrary 解码，将原始节点世界矩阵、网格三角形、UV、材质颜色和关键帧转换为 `p5mod/data/eye_cutin.json`，由 DLL 嵌入并播放。

共 51 个网格、4764 个非退化三角形。原始画布 1280×720，运行时按视口缩放；按原版世界 Z 排序，保留黑边。动画源时间长 2.6666667 秒、Speed=2.5，可见演出约 0.853 秒，其后保留透明尾段。五段面部裁切、裂缝/展开、撕纸白边、红纸收拢、碎纸、双感叹号及破碎来自原始几何。没有另加随机抖动、放大或程序生成的纸片。

`SingleAlt_2` 转为阶梯显隐；BT 唯一的 `MaterialSingle_4` 白闪转为线性透明度。面部使用不透明采样，避免 PNG 的 alpha 在头发处挖出错误透明孔。保留 `Aim.cs` 现有的召唤触发及背景层，未改玩法。

转换和复现命令见 `tools/p5/native_cutin/README.md`。原始格式资料：[GFD Studio](https://github.com/tge-was-taken/GFD-Studio)、[AnimationLayer 源码](https://github.com/tge-was-taken/GFD-Studio/blob/master/GFDLibrary/Animations/AnimationLayer.cs)。

## 验证

- 转换器 Release 编译通过，0 warning、0 error；重新从两个 GMD 生成最终 JSON。
- mod Release 编译通过，0 warning、0 error；版本常量和清单均为 0.5.8。
- 对编译后的 DLL 检查：嵌入资源可实际反序列化，51 个网格完整；面部显隐的边界、连续白闪的峰值及结束时全部透明通过。
- Godot 4.5.1、Compatibility/OpenGL、RTX 4060：读取同一最终 JSON，以 ArrayMesh 播放并输出 90 帧、60fps 预览；无引擎错误。该预览使用独立 GDScript 驱动，与 mod C# 一样使用网格和关键帧数据。
- GIF：`docs/p5/preview/eye_cutin_native.gif`；定格：`docs/p5/preview/eye_cutin_native.png`。旧预览保留便于比较。
- `git diff --check` 通过。

没有启动 P5R 或杀戮尖塔 2。用户随后要求先套进 mod 试用，已备份并安装，见下文。独立 Godot 预览没有加载游戏及 C# 模组，因此战斗中的层级、召唤配合和完整 C# 运行仍待用户实机验收，不能据此承诺逐帧完全一致。

## 测试包

- ZIP：`C:/Users/kkk/sts2_tools/p5_build/P5Joker_0.5.8_native_20261029_083449.zip`，30029194 字节。
- 解压目录：`C:/Users/kkk/sts2_tools/p5_build/P5Joker_0.5.8_native_20261029_083449/`。
- DLL：319488 字节；清单：541 字节；PCK：31737924 字节。
- PCK 是从当前 `p5mod/pck_stage` 的新副本，用 GDRE 的 PCK v3、Godot 4.5.1 参数重新打包。
- DLL SHA-256：`903a77729295f9c8d05483f3d03baeb38fc4da05782cba5ee0cd2a88e7981413`。
- PCK SHA-256：`a4ea6dd3fd9158e7bec07bf9e642383fea0d0f6982b178e4676366a100eb2036`。


## 并发与安装结果

远端新增 b8aac55，同样修改眼神特写并使用版本 0.5.7。为保留双方成果，原版数据复用实现放在独立分支 codex/p5r-native-eye-cutin-20261029，并递增至 0.5.8；没有覆盖远端原分支的改动。

用户要求先套进 mod 试用后，检查杀戮尖塔 2 进程无输出；备份现有 0.5.5 至：C:\Users\kkk\sts2_tools\backup\P5Joker_before_native_20261029_083449

已将 DLL、清单和 PCK 安装到：C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\mods\P5Joker

安装后的 DLL SHA-256 与最终 0.5.8 构建相同：903a77729295f9c8d05483f3d03baeb38fc4da05782cba5ee0cd2a88e7981413。未启动游戏，等待用户实机体验。
