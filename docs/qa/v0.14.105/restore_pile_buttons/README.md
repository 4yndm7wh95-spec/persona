# 恢复牌堆按钮装饰

基线b15dd8db。截图中的DRAW/DISCARD/EXHAUST由HudSkin.Back共用一个427×600的卡背装饰；上一轮清理错误地删除该资源并改用游戏run_history/card_back（128×128），造成白灰牌片。本次恢复原始资源三文件并将HudSkin指回原路径。清理脚本不再删除该HUD资源。

仅增加原PNG、.import及CTEX；原615文件逐字节不变。卡面/卡框代码和资源不恢复，PileFx仍使用原版背面。设置位置、文案和高清门保持。

构建0警告0错误；0.111.0和0.107.1各1198处引用通过；最终资源包原生Godot载入427×600装饰及936×1680门，并确认四个卡面替换资源目录缺席。没有启动游戏/Computer Use。安装前用户已说明退出，仍需检测进程并备份三文件后覆盖。完整记录mod/_build/restore_pile_buttons_20261021。

PCK SHA256: 40e7ef38024c2f572a7202c1d50aac2bfba268936328c0426f4b35c5542d4e66

DLL SHA256: d537b4ed9145aef10c7dc50c82f916470e67365f096074c98d673cdf82977012
