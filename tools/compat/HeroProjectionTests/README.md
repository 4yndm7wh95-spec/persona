# 牌堆鼻线投影 GPU 回归

Godot 4.5 Camera3D.UnprojectPosition 的 Frustum 分支未采用 KeepAspect。此检查在真实原生渲染器中绘制三个不同深度的彩色 3D 探针，用像素中心核对显式 KeepWidth 投影（与 HeroStage 相同的 CreateFrustumAspect 参数），避免再把仅居中透视的截图当作偏移视锥的验证。

覆盖五种视口尺寸、七个入场位置，共 105 项；容许最多 1px 的像素栅格误差。工具只创建诊断场景，不启动或加载游戏。

在仓库根目录执行（将 Godot 路径替换为本机地址）：

```powershell
& '<Godot原生工具路径>' --path tools/compat/HeroProjectionTests --rendering-method mobile --rendering-driver d3d12 --script check.gd -- '<输出JSON的绝对路径>'
```

OpenGL 检查将渲染参数替换为 `--rendering-method gl_compatibility`。
