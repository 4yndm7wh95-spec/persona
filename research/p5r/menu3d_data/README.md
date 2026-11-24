# 菜单 3D Joker：逐帧蒙皮顶点

数据由现有 GFDLibrary 导出器烘焙，保留怪盗服、051B 黑手套，以及可见性表指定的道具和效果网格。坐标沿用原 `.bin` 的模型空间，未缩放或转换轴。每个 `.f16` 为小端 IEEE 754 binary16，按 [帧][顶点][xyz] 存放；`meshes.json` 的 `offset` 以顶点为单位，`clips.json` 的 `frames` 为帧数。原动作未提供速度时 `speed` 保留为 null。

- 可见网格：23 个材质表面，每帧 26,693 个顶点。
- 29 段、343 帧：27 段有效短 main、blend_0000，以及 main_0113 的完整 101 帧。30 fps，含片尾；跳过零时长动作，未因体积删减短片。
- 数据文件合计 55,291,535 字节（55.29 MB）；最大文件 main_0113.f16 为 16,175,958 字节（16.18 MB）。
- Godot 4.5.1 临时自检：以同一机位用 capture.gd 渲染 main_0015 f0005，第二次仅把可见网格换成从实际交付文件读回的顶点。1920×1080 RGBA 最大单通道绝对差 217/255，平均 0.0527/255，7,751 个像素有变化（0.374%）。最大差没有达到预期的接近零；量化后仍有少量像素变化较大。两张检查图都不入库。

复现：用 Export.dll 加 `--fullbody` 把原 GMD/GAP 烘焙到 `tools/p5/menu3d/scratch`，然后运行：

```powershell
py tools/p5/menu3d/pack_f16.py tools/p5/menu3d/scratch research/p5r/menu3d_data --check-plan tools/p5/menu3d/scratch/verify.json
```

云端读取可用 `tools/p5/menu3d/render_f16.gd`。
