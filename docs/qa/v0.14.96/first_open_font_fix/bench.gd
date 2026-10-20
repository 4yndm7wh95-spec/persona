extends SceneTree

func _initialize():
	call_deferred("run")

func run():
	var path = OS.get_cmdline_user_args()[0]
	var start = Time.get_ticks_usec()
	var f = FontFile.new()
	f.data = FileAccess.get_file_as_bytes(path)
	if OS.get_cmdline_user_args().size() > 1:
		f.multichannel_signed_distance_field = true
		f.msdf_pixel_range = 24
		f.msdf_size = 48
	print("font_bytes=", f.data.size(), " load_ms=", (Time.get_ticks_usec()-start)/1000.0)
	for item in [[19,"抽牌顺序随机 · 按类型 → 费用排列",0], [32,"",0], [54,"",6], [20,"点击任意处返回",3], [18,"",0], [20,"攻击",0], [54,"暴政",6]]:
		start = Time.get_ticks_usec()
		var label = Label.new()
		label.add_theme_font_override("font", f)
		label.add_theme_font_size_override("font_size", item[0])
		label.add_theme_constant_override("outline_size", item[2])
		label.text = item[1]
		root.add_child(label)
		var minimum = label.get_minimum_size()
		print("size=",item[0]," text=",item[1]," cpu_ms=",(Time.get_ticks_usec()-start)/1000.0," min=",minimum)
		await process_frame
		label.queue_free()
	quit()
