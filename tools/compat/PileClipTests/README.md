# 牌堆固定裁切回归

原生 Godot 诊断工程，创建固定裁切包装和模拟 holder，不启动游戏。检查边界内颜色完整、越界像素裁掉、特写解除与恢复裁切、裁掉区域不命中鼠标，以及 holder 没有退出场景树。

```powershell
& '<Godot原生工具路径>' --path tools/compat/PileClipTests --rendering-method mobile --rendering-driver d3d12 --script check.gd -- '<结果JSON绝对路径>'
```

预期输出 3202 项通过，holder_tree_exits = 0。
