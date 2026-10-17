# P3Makoto v0.14.52 编译风险资料

记录日期：2026-10-17。代码基线：`8e787284a62f8cd21202bf321d14f01328f70809`，分支 `claude/amazing-darwin-b00arn`。

## 实际编译结果

本机对照 StS2 v0.111.0，以 .NET Release 构建：

```text
P3Makoto: Build succeeded. 2 Warning(s), 0 Error(s)
P3MusicBox 1.1.0: Build succeeded. 0 Warning(s), 0 Error(s)
P3TestMod 1.2.2: Build succeeded. 0 Warning(s), 0 Error(s)
```

P3Makoto 两条警告原文（路径使用仓库相对路径）：

```text
mod/src/Vfx/PileFx.cs(523,19): warning CS8602: Dereference of a possibly null reference.
mod/src/Vfx/PileFx.cs(524,26): warning CS8602: Dereference of a possibly null reference.
```

重现构建（Windows PowerShell，在仓库 mod 目录）：

```powershell
& 'C:/Users/kkk/sts2_tools/dotnet/dotnet.exe' build P3Makoto.csproj -c Release '-p:GameDir=C:/Users/kkk/Desktop/slaythespire/Slay the Spire 2'
```

增量构建若跳过编译，可能不再打印已有警告；以上来自实际重新编译 v0.14.52 时的输出。本资料未再次强制重编译，也未做游戏内复现。

## 代码证据

Item 字段声明：

```csharp
47: 	private sealed class Item
48: 	{
49: 		public Control? Slot;
50: 		public NGridCardHolder? Holder;
51: 		public CardModel Card = null!;
52: 		public int Index;
53: 		public float Born = -1f;
54: 		public float Lift;
```

悬停对象及现有检查：

```csharp
434: 				if (focused != null)
435: 				{
436: 					hovered = null;
437: 				}
438: 
439: 				bool any = hovered != null && hovered.Slot != null && GodotObject.IsInstanceValid(hovered.Holder) && hovered.Slot.Visible;
440: 				if (!any)
441: 				{
442: 					hovered = null;
443: 				}
444: 				foreach (Item it in items)
445: 				{
446: 					if (it.Slot == null || !GodotObject.IsInstanceValid(it.Holder))
447: 					{
448: 						continue;
449: 					}
450: 					NGridCardHolder h = it.Holder;
```

实际警告附近的名牌定位代码：

```csharp
513: 				// 指到的那张移到最上层（绘制和鼠标判定都按节点顺序）
514: 				if (any && hovered!.Slot!.GetParent() == cards && hovered.Slot.GetIndex() != cards.GetChildCount() - 1)
515: 				{
516: 					cards.MoveChild(hovered.Slot, cards.GetChildCount() - 1);
517: 				}
518: 				// 名牌：放大那张牌的正下方
519: 				plate.Visible = any;
520: 				if (any)
521: 				{
522: 					Item it = hovered!;
523: 					Vector2 cs = it.Holder.Hitbox.Size.X > 1f ? it.Holder.Hitbox.Size : new Vector2(300f, 422f);
524: 					float half = cs.Y * it.Slot.Scale.Y * it.Holder.Scale.Y * 0.5f;
525: 					plate.Position = it.Slot.Position + new Vector2(-20f * k, half + 8f * k);
526: 					((Label)plate.GetChild(plate.GetChildCount() - 1)).Text = it.Card.Title;
527: 					float a = Ease.OutCubic(it.Lift);
528: 					plate.Modulate = new Color(1f, 1f, 1f, a);
529: 					plate.Scale = new Vector2(Mathf.Lerp(0.7f, 1f, a), 1f);
530: 				}
```

## 风险解释：已知与待确认

- 第 523 行直接访问可空字段 `it.Holder`；第 524 行直接访问可空字段 `it.Slot`。这些访问参与悬停卡牌下方名牌的位置计算。
- 上文 `any` 检查了 hovered、Slot 和 Holder 的 Godot 实例有效性。编译器不能仅凭后面的 `if (any)` 推导这些可空字段都非空；因此警告可能主要来自静态空值分析无法追踪该条件。
- `hovered!` 只抑制 hovered 的空值警告，并不会自动使 Holder、Slot 非空。
- 若运行时对象确实缺失，可能触发 NullReferenceException；若 Godot 节点被释放，则属于另一个需要检查的实例生命周期问题。**尚无本任务的运行日志或游戏复现证明这些情况实际发生。** 不应把两条警告直接称为已确认崩溃。
- 建议 Claude 检查牌堆刷新、节点释放、鼠标进入/离开及特写切换时的生命周期；用显式非空局部变量和必要的 IsInstanceValid 检查建立清晰不变量，失效时隐藏名牌或安全跳过该段。保持卡牌翻转、悬停和返回行为不变。
- 不建议仅新增 `!` 或关闭 CS8602 来掩盖问题。修复后重新编译，并手动检查悬停、快速开关牌堆、刷新和特写返回。本机此次没有修改代码或启动游戏。

## 兼容检查证据

ApiCheck 仅检查程序集 API 引用是否存在，不能证明空值和节点生命周期安全。两个游戏均检查了刚编译的三个 DLL，输出如下。

### StS2 v0.111.0

```text
游戏程序集：C:/Users/kkk/Desktop/slaythespire/Slay the Spire 2/data_sts2_windows_x86_64/sts2.dll（sts2 + GodotSharp，14586 个类型）

== P3Makoto.dll ==
检查了 903 处引用

== P3MusicBox.dll ==
检查了 241 处引用

== P3TestMod.dll ==
检查了 382 处引用
结果：全部能找到 ✓
```

### StS2 v0.107.1

```text
游戏程序集：C:/Users/kkk/Downloads/Slay the Spire 2/data_sts2_windows_x86_64/sts2.dll（sts2 + GodotSharp，14235 个类型）

== P3Makoto.dll ==
检查了 903 处引用

== P3MusicBox.dll ==
检查了 241 处引用

== P3TestMod.dll ==
检查了 382 处引用
结果：全部能找到 ✓
```

## 本次资料范围

本报告记录上一次实际构建的全部编译警告，附对应基线源码。它不是对整个 mod 的全面安全审计，也不包含新的运行时故障结论。提交只新增此资料文件，不修改代码、游戏资源或存档。
