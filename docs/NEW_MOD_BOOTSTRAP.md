# 新 MOD 开工手册（给新开的 Claude 会话）

这个仓库已经做了两个《杀戮尖塔 2》换皮 mod：P3Makoto（`mod/`，储君 → 结城理）和 P5Joker（`p5mod/`，静默猎手 → Joker）。
新 mod 照着它们的结构和踩过的坑做，下面是必须知道的东西。细节以代码为准，这里只给方向和出处。

## 1. 先读这些
- `docs/COLLAB_RULES.md`：多会话协作规则（分支、交接、用户决定清单）。
- `docs/BUILD.md`：P3 的构建和 PCK 流程；`tools/p5/cloud/build_package.sh`、`check.sh`：P5 的云端编译和打包。
- `docs/HANDOFF.md` 最新一节、`docs/p5/RELEASE_0.9.84_0.15.46.md`：最近的修复和玩家日志里查到的问题。
- 入口和兼容层抄这两个文件：`p5mod/src/ModEntry.cs`、`mod/src/ModEntry.cs`（挂资源、SkinChanger、平台判断、版本兼容）。

## 2. 构建方式（最容易踩坑）
- 云端容器只有 .NET 8 SDK，用 `csc` + `research/libs/` 里的游戏程序集编译（见 `tools/p5/cloud/check.sh`）。**没有 Godot 源生成器**：
  - Node 的虚方法 `_Ready` / `_Process` 在游戏里**不会被调用**。每帧逻辑挂 `SceneTree.ProcessFrame`，或者 Harmony 补丁游戏自己的方法。
  - Node 子类仍然要写 `partial`：特效预览工具（`tools/p5/fxpreview`）会跑源生成器，不写会报 GD0001。
- Harmony 补丁按类逐个 `PatchAll`，每个类单独 try/catch：一个目标方法在某个游戏版本不存在时，只关掉这一个类，日志打印 `harmony patches applied: N ok, M failed`。
- 游戏版本差异：`research/libs`（当前版本）和 `research/libs_0.107.1`（旧版）都要能编过。旧版没有的字段（如 `Mod.workshopId`）用反射读，签名不同的方法放进 `[MethodImpl(NoInlining)]` 的小函数里，并捕获 `MissingMethodException`。
- 游戏加载器会吞掉初始化异常：出错表现是"mod 没生效"而不是闪退，所以关键步骤都要打日志。

## 3. 资源和 PCK
- `*/pck_stage/` 是完整的 `res://` 目录结构（入 git），贴图用 `tools/pck/godot_tex.py` 生成无损 WebP 的 `.ctex` 和 `.import`；Spine 资源用导入后格式（`.spatlas`、`.spjson`）。
- 打包由用户本地的 codex 完成（GDRE Tools 或 `build_package.sh`）。我们只负责把 `pck_stage` 准备好、推送。
- mod 自己的资源放在自己的命名空间下（如 `res://p5joker/...`），只有替换原版的资源才用原版路径。
- 手机性能：大图集准备一份半分辨率的手机包（参考 `tools/p5/stage_mobile.py` 和 `P5Joker.mobile.pck`）；离屏 3D 渲染在手机上降分辨率（`p5mod/src/Vfx/Perf.cs`）。

## 4. 和其他 mod 共存
- **SkinChanger**（创意工坊 3787302680，id `Gurio.SkinChanger`）：json 里 `has_pck` 必须是 `true`，它才会把我们当皮肤接管；接管时它会隔离我们的 pck，选中皮肤时才挂载资源，不选时关掉我们的补丁。我们的入口里要判断"是否被它接管"，接管时不要自己全局挂 pck。抄 `MountLocalContent` 和 `JokerCostume.ManagedSelection` 的写法。
- 别的皮肤 mod 可能替换同一个原版资源路径：参考 P5 0.9.85 的 `JokerRig.cs`（私有副本 + `TakeOverPath`）。
- 判断"这个角色是不是我们的皮肤"要看实际的模型（骨骼签名），不要只看角色类型：多人局里队友、或者装了别的皮肤时会出错。
- 特效里推动敌人或角色模型时，统一用 `p5mod/src/Vfx/BodyRest.cs` 记录原位（击退叠加曾把敌人推出屏幕）。

## 5. 平台
- PC、安卓、iOS 都要能加载。iOS 不支持动态生成代码：只在 Web 或 `RuntimeFeature.IsDynamicCodeSupported == false` 时禁用 Harmony，不要要求 `IsDynamicCodeCompiled`（见 `mod/src/ModEntry.cs` 的 `CanPatch`）。
- Windows 智能应用控制会拦截未签名的 DLL（错误码 `0x800711C7`），这不是我们的 bug，让玩家解除锁定或关闭它。

## 6. 清单文件
- `<Mod>.json` 里的 `author`、`description` 启动时会被校验，不要改；版本号同时改 json 和 `ModEntry.Version`。

## 7. 预览和交付
- 特效、动画先在 `tools/p5/fxpreview` 里渲染预览视频再给用户看：宽 854、60fps、无声音，放 `docs/<mod>/preview/`，旧版本删掉；发之前自己逐帧对比。
- 每次推送前 `git fetch` 并合并远端分支；不要 rebase，不要 force push。
- 交给 codex 的事项写在发布说明里（打包注意、需要实机确认的项）。

## 8. 用户习惯
- 全程用中文回复。
- 用户会把 prompt 转给本地的 codex：给 codex 的指令要可直接复制，并带上安全要求——不要 rebase 或 force push；不要读取 `C:\Users\kkk\.p3record\obs.json` 或任何密钥文件；只往游戏的 mods 文件夹里复制文件。
- 不要往用户本地堆文件（用户磁盘很紧）：中间产物放仓库或云端，临时文件用完就删。
- 玩家日志里的报错很多来自别的 mod：先分清是不是我们的，再动代码，并向用户说明修复把握程度和是否实机验证过。
