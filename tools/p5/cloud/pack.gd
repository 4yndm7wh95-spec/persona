# 把 p5mod/pck_stage 原样打成 PCK（Godot 4.5.1 的 PCKPacker：格式 v3，和本机 gdre_tools --pck-version=3 --pck-engine-version=4.5.1 一样）。
# 用法见 build_package.sh。参数：<pck_stage 目录> <输出.pck> <文件清单.txt（相对路径，一行一个）>
extends SceneTree
func _init():
	var a = OS.get_cmdline_user_args()
	var p = PCKPacker.new()
	var e = p.pck_start(a[1])
	if e != OK: printerr("start ", e); quit(1); return
	var n = 0
	for l in FileAccess.get_file_as_string(a[2]).split("\n"):
		if l == "": continue
		e = p.add_file("res://" + l, a[0] + "/" + l)
		if e != OK: printerr("add ", l, " ", e); quit(1); return
		n += 1
	e = p.flush(false)
	print("packed ", n, " err ", e)
	quit(0 if e == OK else 1)
