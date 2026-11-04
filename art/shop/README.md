# 天鹅绒商店

`velvet_room.png`：2026-11-04 通过 imagegen 生成一次的原创全屏场景。用户截图只提供对坐布局和 P3 深蓝氛围参考，没有直接使用截图。伊戈尔坐于中央桌后，画面不包含玩家、队友、前景椅子或 UI。

背景为不透明 RGBA；运行时 `velvet_shop.gdshader` 叠加轻微动态蓝光束、地面薄雾、微尘及悬停冷光。资源导入使用 `tools/pck/godot_tex.py` 的 `write_texture`，输出到 `mod/pck_stage/p3makoto/shop/velvet_room.png`。

交互区域按图像比例设定为 x=38%–62%、y=28%–65%，保留原商人按钮及其原生交互逻辑。
