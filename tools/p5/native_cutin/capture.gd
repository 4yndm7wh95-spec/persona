extends SceneTree
var parts: Array
var nodes: Array[MeshInstance2D] = []
var frame := 0
var canvas: Node2D
func _initialize():
	var repo = ProjectSettings.globalize_path("res://../../../")
	var clip = JSON.parse_string(FileAccess.get_file_as_string(repo.path_join("p5mod/data/eye_cutin.json")))
	parts = clip.meshes
	canvas = Node2D.new()
	root.add_child(canvas)
	var shader = Shader.new()
	shader.code = "shader_type canvas_item; render_mode unshaded; void fragment(){COLOR=vec4(texture(TEXTURE, UV).rgb,1.0);}"
	var opaque = ShaderMaterial.new()
	opaque.shader = shader
	var texture = ImageTexture.create_from_image(Image.load_from_file(repo.path_join("p5mod/pck_stage/p5joker/fx/eyes.png")))
	for part in parts:
		var vertices := PackedVector3Array()
		var colors := PackedColorArray()
		var uv := PackedVector2Array()
		for p in part.vertices:
			vertices.append(Vector3(p[0],p[1],0))
			colors.append(Color(part.color[0],part.color[1],part.color[2],1))
		for p in part.uv: uv.append(Vector2(p[0],p[1]))
		var arrays := []
		arrays.resize(Mesh.ARRAY_MAX)
		arrays[Mesh.ARRAY_VERTEX]=vertices
		arrays[Mesh.ARRAY_COLOR]=colors
		arrays[Mesh.ARRAY_INDEX]=PackedInt32Array(part.indices)
		if part.textured: arrays[Mesh.ARRAY_TEX_UV]=uv
		var mesh := ArrayMesh.new()
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
		var node := MeshInstance2D.new()
		node.mesh=mesh
		if part.textured:
			node.texture=texture
			node.material=opaque
			node.texture_repeat=CanvasItem.TEXTURE_REPEAT_ENABLED
		node.visible=false
		canvas.add_child(node)
		nodes.append(node)
	print("NATIVE_CUTIN: ", nodes.size(), " meshes prepared")
func _process(_delta):
	# Fixed authored time permits reproducible stills and frame-by-frame review.
	var time: float = maxf(0.0, (frame / 60.0 - 0.20) * 2.5)
	for j in nodes.size():
		var keys = parts[j].keys
		var i:=0
		while i+1<keys.size() and keys[i+1][0]<=time+0.000001: i+=1
		var alpha: float = keys[i][1]
		if parts[j].get("linear",false) and i+1<keys.size():
			alpha=lerpf(alpha,keys[i+1][1],clampf((time-keys[i][0])/(keys[i+1][0]-keys[i][0]),0,1))
		nodes[j].visible=frame>=12 and alpha>0.0001
		nodes[j].modulate=Color(1,1,1,alpha)
	frame+=1
	return false
