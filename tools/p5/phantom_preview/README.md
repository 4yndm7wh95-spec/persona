# Joker 第一批特效试作

用户 2026-10-29：旧 P3 mod 只研究「为什么华丽」，不要沿袭它的风格。莲的特效稍贴 P5R 和角色即可。先做容易展示的效果，用 Godot 渲染图给用户看。

## 范围

- `p5mod/src/Vfx/PhantomFx.cs` 是游戏的实际实现：背景暗场、召唤释放。`Aim.PlayCast` 接上召唤。用户随后明确要求打击、枪击保留原效果，本轮没有修改 HitFx。
- `tools/p5/fxpreview/Preview.cs` 的 `phantom_*` 场景直接编译这些 C# 源文件，并组合现有 ReadyFx、Eiha。需要 Godot .NET 4.5.1。
- 本机 Godot .NET 下载失败，使用已有标准版 4.5.1 运行 `capture.gd` 的独立视觉对应实现。它复用仓库的 Joker 图集、动作数据、原版眼部几何、原样读取的 Flame / Glow 着色器，以及 P5R 原版火星、星芒、烟雾。**这批图验证新增渲染层、大小、配色、节奏；不代表 C# 在游戏内已经验收。**
- 独立预览没有复制现有 Eiha、BodyAura、GPU 火星；Joker 待机的加权变形使用原始静态图，动作区间按仓库的区域附件换图。完整组合保留在 C# 预览场景，不能把独立试作 GIF 当成完整实机录像。

## 渲染

在仓库根目录执行（`<Godot>` 指独立标准版工具，不是游戏程序）：

```powershell
New-Item -ItemType Directory -Force tools/p5/phantom_preview/out/summon
& '<Godot>' --path tools/p5/phantom_preview --script capture.gd --rendering-method gl_compatibility --position -3000,-3000 --write-movie out/summon/f.png --fixed-fps 60 --quit-after 180 -- --scenario=summon
```

`--scenario=cutin` 渲染 120 帧，写到 `out/cutin/f.png`。然后执行 `make_previews.py` 生成 `docs/p5/preview/phantom_studies/` 的 GIF 和总览。

C# 预览使用 `phantom_summon` / `phantom_direct`。本机仅有 .NET 10，已用 `-p:TargetFramework=net10.0` 编译通过；Godot .NET 运行未验证。其他已有 .NET 8 的环境保留项目默认目标。

最终边界：打击、枪击、刀扇、毒不改；新增召唤不用代码绘制的几何纸片。特效由游戏内着色器和原版素材实时渲染，GIF 仅用于审看。

原版纹理映射见 `tools/p5/stage_p5.py::FX_TEX`：`blue_flame` 来自 PERSONA_C、`star4` 来自 0325、`smoke` 来自 0098。P3Makoto 工具来源为 `mod/src/Vfx/Fx.cs` / `Shaders.cs` / `P3Fx.cs`，P5 现有 `Fx`、`Glow.Flash`、`Glow.Ripple` 已有相应移植，本轮直接调用这些工具。
