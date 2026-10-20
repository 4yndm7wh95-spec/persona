# 原版露出遮罩缓存

源数据为仓库的 PLG_UI_Camp[12..16]，保留顶点颜色低字节的边缘 alpha。缓存为 1280×720 L8，运行时提前解码/上传，不创建几何体。

用已有 Godot 4.5.1 运行本目录项目的 render.gd，传入仓库绝对路径和输出目录：

```powershell
godot --path <repo>/tools/p3r/mask_render --rendering-method mobile --rendering-driver d3d12 --disable-render-loop --script res://render.gd -- <repo> <output>
```

五张 in_mask_00..04.png 校验像素后可放入 art/p3menu_system/p3r/，再运行 cut_splash.py、dive_geometry.py 整理 PCK 资源。仅这是离线生成工具；不启动游戏。
