# 鼠标光标样式开关（2026-10-20）

设置 → 常规 → P3MAKOTO 新增「鼠标光标（立即生效）」，可在 P3R / 游戏原版之间切换；保存至现有 p3makoto_settings.json。旧配置缺少此项时默认保留 P3R，极速模式不影响选择。

按 NCursorManager 节点缓存原版普通 / 按下图片；关闭换皮时恢复原始图片，再由原版 UpdateCursor 决定当前状态。查看光标、临时覆盖、热点与手柄隐藏逻辑不变。首次启动关闭时不读取 Mod 光标图片；切换设置只刷新一次，不增加逐帧遍历。

验证：Release 编译 0 警告、0 错误；ApiCheck 对 0.111 与 0.107.1 各检查 1174 处引用，均「全部能找到」。只读核对两个版本 NCursorManager 的图片字段和 UpdateCursor。未启动游戏，实际外观与反复切换由用户验收。

已备份并替换两个现有安装位置的 P3Makoto.dll（.pck 和清单保持原样）：
- Desktop/slaythespire/Slay the Spire 2/mods/P3Makoto
- Downloads/Slay the Spire 2/mods/P3Makoto/P3Makoto

用户验收：切成「游戏原版」后立即恢复原版；按下 / 释放正常；切回 P3R 正常；重启后保持选择；手柄仍可正常隐藏光标。
