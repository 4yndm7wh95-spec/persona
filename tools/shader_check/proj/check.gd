extends SceneTree
var frames = 0
func _initialize():
	var d = DirAccess.open("res://sh")
	var x = 0
	for f in d.get_files():
		if not f.ends_with(".gdshader"): continue
		var s = Shader.new()
		s.code = FileAccess.get_file_as_string("res://sh/" + f)
		var m = ShaderMaterial.new(); m.shader = s
		var r = ColorRect.new(); r.material = m; r.size = Vector2(64, 64); r.position = Vector2(x % 512, 0); x += 8

		root.add_child(r)
func _process(_d):
	frames += 1
	if frames > 5:
		print("DONE"); quit()
