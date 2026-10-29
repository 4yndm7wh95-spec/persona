extends SceneTree
# Visual mirror of PhantomFx.cs. Original P5R sprites plus runtime flame shaders.
const INK = Color("09090e")
const RED = Color("eb1233")
const WHITE = Color("fff8ed")
const BLUE = Color("4fc6ff")
var world: Node2D
var back: Node2D
var front: Node2D
var actor: Node2D
var atlas: Texture2D
var rig: Dictionary
var regions = {}
var bone_nodes = {}
var slot_nodes = {}
var updates: Array[Callable] = []
var frames = 0
var clock = 0.0
var scenario = "summon"
var repo: String
var flame_shader: Shader
var flash_shader: Shader
var pulse_shader: Shader
var eye_nodes = []
var eye_parts = []
var phase = "idle_loop"
var phase_time = 0.0
var triggered = false
const FEET = Vector2(430,830)
const FACE = Vector2(479.2,475)
const HIT = Vector2(1420,600)

func _initialize():
	repo = ProjectSettings.globalize_path("res://../../../")
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--scenario="): scenario = a.get_slice("=",1)
	world = Node2D.new()
	world.scale = Vector2.ONE * (2.0/3.0)
	root.add_child(world)
	poly(world,[Vector2.ZERO,Vector2(1920,0),Vector2(1920,1080),Vector2(0,1080)],Color("34303a"))
	# A lit architectural test backdrop makes the requested dimming visible.
	for i in 8:
		var x = 90+i*250
		poly(world,[Vector2(x,80),Vector2(x+115,80),Vector2(x+115,830),Vector2(x,830)],Color("49404a"))
		poly(world,[Vector2(x+12,95),Vector2(x+22,95),Vector2(x+22,805),Vector2(x+12,805)],Color("72616a"))
	poly(world,[Vector2(0,830),Vector2(1920,830),Vector2(1920,1080),Vector2(0,1080)],Color("51414a"))
	for i in 8:
		line(world,[Vector2(960+(i-4)*300,830),Vector2(960+(i-4)*650,1080)],Color("6a5259"),2)
	back=Node2D.new()
	world.add_child(back)
	build_actor()
	for at in [Vector2(1420,600),Vector2(1690,640)]:
		var p=PackedVector2Array()
		for i in 24:
			var a=i*TAU/24
			p.append(at+Vector2(cos(a)*115,sin(a)*160+maxf(0,sin(a))*28))
		poly(world,p,Color("322638"))
		poly(world,star(14,14,8,0),Color("e3ca8c")).position=at+Vector2(-40,-60)
		poly(world,star(14,14,8,0),Color("e3ca8c")).position=at+Vector2(25,-64)
	front=Node2D.new()
	world.add_child(front)
	flame_shader=Shader.new()
	var source=FileAccess.get_file_as_string(repo.path_join("p5mod/src/Vfx/Flame.cs"))
	flame_shader.code=source.get_slice('Code = @"',1).get_slice('" };',0)
	source=FileAccess.get_file_as_string(repo.path_join("p5mod/src/Vfx/Glow.cs"))
	flash_shader=Shader.new()
	flash_shader.code=source.get_slice('Code = @"',2).get_slice('" };',0)
	pulse_shader=Shader.new()
	pulse_shader.code=source.get_slice('Code = @"',3).get_slice('" };',0)
	print("PHANTOM_STUDY: ",scenario,"; actual Godot shaders, atlas, authored eye meshes")

func poly(parent: Node, pts, color: Color) -> Polygon2D:
	var p=Polygon2D.new()
	p.polygon=PackedVector2Array(pts)
	p.color=color
	parent.add_child(p)
	return p
func line(parent: Node, pts, color: Color, width: float) -> Line2D:
	var p=Line2D.new()
	p.points=PackedVector2Array(pts)
	p.default_color=color
	p.width=width
	p.antialiased=true
	parent.add_child(p)
	return p
func node(parent: Node, at=Vector2.ZERO) -> Node2D:
	var n=Node2D.new()
	n.position=at
	parent.add_child(n)
	return n
func out3(t: float) -> float: return 1-pow(1-clampf(t,0,1),3)
func expo(t: float) -> float: return 1-pow(2,-10*clampf(t,0,1)) if t<1 else 1
func back_ease(t: float) -> float:
	t=clampf(t,0,1)-1
	return 1+2.70158*t*t*t+1.70158*t*t
func star(outer: float, inner: float, teeth: int, offset: float):
	var p=PackedVector2Array()
	for i in teeth*2:
		var r=(outer if i%2==0 else inner)*(1+0.16*sin(i*5.13))
		p.append(Vector2.RIGHT.rotated(offset+i*TAU/(teeth*2))*r)
	return p
func run(owner: CanvasItem, start: float, duration: float, step: Callable):
	owner.visible=false
	updates.append(func(t):
		var s=t-start
		owner.visible=s>=0 and s<=duration
		if owner.visible: step.call(clampf(s/duration,0,1)))

func build_actor():
	var base=repo.path_join("p5mod/pck_stage/animations/characters/silent/silent")
	rig=JSON.parse_string(FileAccess.get_file_as_string(base+".spine-json"))
	atlas=ImageTexture.create_from_image(Image.load_from_file(base+".png"))
	var last=""
	for raw in FileAccess.get_file_as_string(base+".atlas").split("\n"):
		var s=raw.strip_edges()
		if s.begins_with("bounds:"):
			var v=s.substr(7).split(",")
			regions[last]=Rect2(float(v[0]),float(v[1]),float(v[2]),float(v[3]))
		elif s!="" and not s.contains(":"): last=s
	actor=node(world,FEET)
	actor.scale=Vector2(0.29,-0.29)
	for b in rig.bones:
		var parent=bone_nodes[b.parent] if b.has("parent") else actor
		bone_nodes[b.name]=node(parent)
	for s in rig.slots:
		var spr=Sprite2D.new()
		spr.texture=atlas
		spr.region_enabled=true
		spr.flip_v=true
		actor.add_child(spr)
		slot_nodes[s.name]=spr

func sample(keys: Array, t: float, key: String, default):
	if keys.is_empty(): return default
	var i=0
	while i+1<keys.size() and keys[i+1].get("time",0)<=t: i+=1
	var a=keys[i]
	var v=a.get(key,default)
	if i+1==keys.size(): return v
	var b=keys[i+1]
	return lerpf(v,b.get(key,default),clampf((t-a.get("time",0))/(b.get("time",0)-a.get("time",0)),0,1))
func actor_frame(anim: String,t: float):
	var a=rig.animations.get(anim,{})
	for b in rig.bones:
		var tr=a.get("bones",{}).get(b.name,{})
		var n=bone_nodes[b.name]
		n.position=Vector2(b.get("x",0)+sample(tr.get("translate",[]),t,"x",0),b.get("y",0)+sample(tr.get("translate",[]),t,"y",0))
		n.rotation=deg_to_rad(b.get("rotation",0)+sample(tr.get("rotate",[]),t,"value",0))
		n.scale=Vector2(b.get("scaleX",1)*sample(tr.get("scale",[]),t,"x",1),b.get("scaleY",1)*sample(tr.get("scale",[]),t,"y",1))
	for s in rig.slots:
		var tracks=a.get("slots",{}).get(s.name,{})
		var att=s.get("attachment","")
		for k in tracks.get("attachment",[]):
			if k.get("time",0)<=t: att=k.get("name","")
		var spr=slot_nodes[s.name]
		var data=rig.skins[0].attachments.get(s.name,{}).get(att,{}) if att!=null else {}
		spr.visible=not data.is_empty()
		if not spr.visible: continue
		var key=data.get("path",att)
		if not regions.has(key):
			spr.visible=false
			continue
		if data.get("type","")=="mesh":
			# Idle uses weighted deform; use its static approved region in the study only.
			if regions.has("idle_a3"):
				spr.region_rect=regions["idle_a3"]
				spr.transform=Transform2D(0,Vector2(2.072,2.072),0,Vector2(-30,663))
			continue
		spr.region_rect=regions[key]
		var local=Transform2D(deg_to_rad(data.get("rotation",0)),Vector2(data.get("scaleX",1),data.get("scaleY",1)),0,Vector2(data.get("x",0),data.get("y",0)))
		spr.transform=actor.global_transform.affine_inverse()*bone_nodes[s.bone].global_transform*local
		var colors=tracks.get("rgba",[])
		var alpha=1.0
		if not colors.is_empty():
			var i=0
			while i+1<colors.size() and colors[i+1].get("time",0)<=t: i+=1
			alpha=Color(colors[i].color).a
			if i+1<colors.size(): alpha=lerpf(alpha,Color(colors[i+1].color).a,clampf((t-colors[i].get("time",0))/(colors[i+1].get("time",0)-colors[i].get("time",0)),0,1))
		spr.modulate=Color(1,1,1,alpha)

func focus(start: float):
	var dim=ColorRect.new()
	dim.size=Vector2(1920,1080)
	back.add_child(dim)
	back.move_child(dim,0)
	run(dim,start,0.92,func(t):
		var s=t*0.92
		dim.color=Color(0.012,0.007,0.02,0.64*out3(s/0.065)*(1-out3((s-0.66)/0.26))))

func eyes(start: float):
	var clip=JSON.parse_string(FileAccess.get_file_as_string(repo.path_join("p5mod/data/eye_cutin.json")))
	var r=node(back,Vector2(268.8,151.2))
	r.scale=Vector2.ONE*1.08 # 1920 / 1280 * 0.72, centered without changing aspect ratio.
	var sh=Shader.new()
	sh.code="shader_type canvas_item;render_mode unshaded;void fragment(){COLOR=vec4(texture(TEXTURE,UV).rgb,1.0);}"
	var mat=ShaderMaterial.new()
	mat.shader=sh
	var texture=ImageTexture.create_from_image(Image.load_from_file(repo.path_join("p5mod/pck_stage/p5joker/fx/eyes.png")))
	for p in clip.meshes:
		var arr=[]
		arr.resize(Mesh.ARRAY_MAX)
		var verts=PackedVector3Array()
		var cols=PackedColorArray()
		var uvs=PackedVector2Array()
		for v in p.vertices:
			verts.append(Vector3(v[0],v[1],0))
			cols.append(Color(p.color[0],p.color[1],p.color[2],1))
		for uv in p.uv: uvs.append(Vector2(uv[0],uv[1]))
		arr[Mesh.ARRAY_VERTEX]=verts
		arr[Mesh.ARRAY_COLOR]=cols
		arr[Mesh.ARRAY_INDEX]=PackedInt32Array(p.indices)
		if p.textured: arr[Mesh.ARRAY_TEX_UV]=uvs
		var mesh=ArrayMesh.new()
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arr)
		var n=MeshInstance2D.new()
		n.mesh=mesh
		if p.textured:
			n.texture=texture
			n.material=mat
			n.texture_repeat=CanvasItem.TEXTURE_REPEAT_ENABLED
		r.add_child(n)
		run(n,start,clip.duration/clip.speed,func(t):
			var native_time=t*clip.duration
			var keys=p.keys
			var i=0
			while i+1<keys.size() and keys[i+1][0]<=native_time+0.000001: i+=1
			var alpha=keys[i][1]
			if p.get("linear",false) and i+1<keys.size():
				alpha=lerpf(alpha,keys[i+1][1],clampf((native_time-keys[i][0])/(keys[i+1][0]-keys[i][0]),0,1))
			n.visible=alpha>0.0001
			n.modulate.a=alpha)

func summon(start: float):
	var height=FACE.distance_to(FEET)
	var k=height/330
	var center=FACE.lerp(FEET,0.36)
	var r=node(back,center)
	r.scale=Vector2.ONE*k
	# Fire stays behind the eye strip.
	back.move_child(r,maxi(1,back.get_child_count()-2))
	for i in 3:
		var w=170 if i==1 else 115
		var h=500 if i==1 else 360
		var fire=ColorRect.new()
		fire.size=Vector2(w,h)
		fire.position=Vector2((i-1)*40-w/2.0,190-h)
		fire.pivot_offset=Vector2(w/2.0,h)
		var mat=ShaderMaterial.new()
		mat.shader=flame_shader
		fire.material=mat
		mat.set_shader_parameter("seed",3.7+i*5.1)
		r.add_child(fire)
		run(fire,start+i*0.025,0.82,func(t):
			var open=expo(t/0.15)
			mat.set_shader_parameter("clock",t*0.82)
			mat.set_shader_parameter("alpha",open*(1-out3((t-0.38)/0.62)))
			fire.scale=Vector2(0.6+0.4*open,0.2+0.8*open))
	texture_burst(FACE,center,k,start)
	var flash=shader_quad(front,FACE,height*0.75,flash_shader)
	var flash_mat=flash.material as ShaderMaterial
	flash_mat.set_shader_parameter("tint",BLUE)
	run(flash,start,0.2,func(t):
		flash_mat.set_shader_parameter("alpha",1-out3(t))
		flash.scale=Vector2.ONE*lerpf(0.5,1.25,expo(t)))
	var pulse=shader_quad(back,FACE,height*1.05,pulse_shader)
	var pulse_mat=pulse.material as ShaderMaterial
	pulse_mat.set_shader_parameter("strength",0.010)
	run(pulse,start+0.035,0.34,func(t):
		pulse_mat.set_shader_parameter("radius",lerpf(0.02,0.9,out3(t)))
		pulse_mat.set_shader_parameter("width",lerpf(0.16,0.07,out3(t)))
		pulse_mat.set_shader_parameter("alpha",(1-t)*(1-t)))

func shader_quad(parent: Node,at: Vector2,size: float,shader: Shader) -> ColorRect:
	var q=ColorRect.new()
	q.size=Vector2.ONE*size
	q.position=at-Vector2.ONE*size/2
	q.pivot_offset=Vector2.ONE*size/2
	var mat=ShaderMaterial.new()
	mat.shader=shader
	q.material=mat
	parent.add_child(q)
	return q

func fx_texture(name: String) -> Texture2D:
	return ImageTexture.create_from_image(Image.load_from_file(repo.path_join("p5mod/pck_stage/p5joker/fx/"+name+".png")))
func texture_burst(face: Vector2,center: Vector2,k: float,start: float):
	var r=node(front,face)
	var ember=fx_texture("blue_flame")
	var glint=fx_texture("star4")
	var additive=CanvasItemMaterial.new()
	additive.blend_mode=CanvasItemMaterial.BLEND_MODE_ADD
	for i in 20:
		var star=i%5==0
		var tex=glint if star else ember
		var a=-PI+fmod(i*2.39996,TAU)
		var dir=Vector2(cos(a),sin(a)*0.65-0.45)
		var reach=(65+i%5*32)*k
		var sprite=Sprite2D.new()
		sprite.texture=tex
		sprite.material=additive
		r.add_child(sprite)
		var size=(23 if star else 13+i%3*5)*k/tex.get_width()
		sprite.self_modulate=Color("eafcff") if star else BLUE
		run(sprite,start+i%3*0.012,0.72,func(t):
			sprite.position=dir*reach*out3(t)+Vector2(0,-55*k*t*t)
			sprite.rotation=t*1.2 if star else dir.angle()+PI/2+sin(t*4+i)*0.25
			sprite.scale=Vector2.ONE*size*(0.8+0.4*sin(t*PI))*(1-pow(t,3))
			sprite.modulate.a=1-pow(t,3))
	var smoke=fx_texture("smoke")
	var vapor=node(back,center)
	back.move_child(vapor,1)
	for i in 3:
		var sprite=Sprite2D.new()
		sprite.texture=smoke
		sprite.self_modulate=Color("14213e")
		vapor.add_child(sprite)
		run(sprite,start+i*0.035,0.78,func(t):
			sprite.position=Vector2((i-1)*65*k*out3(t),-70*k*t)
			sprite.scale=Vector2.ONE*(125+95*out3(t))*k/smoke.get_width()
			sprite.modulate.a=0.35*sin(t*PI))

func _process(_delta):
	clock=frames/60.0
	if not triggered:
		triggered=true
		if scenario=="summon":
			focus(1.20)
			eyes(1.20)
			summon(1.20)
		elif scenario=="cutin":
			focus(0.4)
			eyes(0.4)

	var anim="idle_loop"
	var t=clock
	if scenario=="summon":
		if clock>=1.16 and clock<1.91:
			anim="cast_from_mask"
			t=clock-1.16
		elif clock>=0.25 and clock<1.16:
			anim="mask_loop" if clock>=0.55 else "mask_in"
			t=clock-0.25
	actor_frame(anim,t)
	for u in updates: u.call(clock)
	frames+=1
	return false
