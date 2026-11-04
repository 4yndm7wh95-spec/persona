# 不启动游戏的补丁兼容检查

需要 .NET 9 运行时和可编译 net9.0 的 SDK，游戏文件只读。

```powershell
dotnet build tools/p5/verify_combat_patches/Verify.csproj -c Release -p:GameDir="<游戏目录>"
dotnet tools/p5/verify_combat_patches/bin/Release/net9.0/Verify.dll "<游戏目录>/data_sts2_windows_x86_64"
```

只在独立控制台进程中检查：真实毒伤状态机的调用位置、Harmony 挂载、每次播放独立时钟/等待、异步毒伤上下文隔离。Damage 在测试中被替换为可手动完成的空任务，不执行游戏伤害、不写游戏目录。测试结束卸载测试补丁。

这些检查不能代替游戏内的目标死亡、多人同步、第三方 mod 钩子和音画验收。

## 独立 shader / 房间变换检查

`prepare_visual.py --eyes <P5 资源包的 p5joker/fx/eyes.png> --output <临时目录>` 提取当前 C# 源码中的真实 shader 和原版切入网格。
随后使用普通 Godot 4.5.1（不需要 Mono、不启动游戏）：

```
Godot --path <临时目录> --script res://check.gd --rendering-method gl_compatibility --resolution 960x540 --audio-driver Dummy
```

生成抓握、攥紧、房间缩放和光圈检查图。脚本用同样的顶层根节点和反向 canvas 变换公式测试覆盖范围；它不是运行中的 C# / 游戏完整时间轴测试。
