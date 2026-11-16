extends SceneTree
var layers=[]
var maps=[]
var camera: Camera3D
var frame=0
var timeline={}
var holders=[]
var layer_materials=[]
var native_cull=false
var time_offset=0.0
var diagnostic_hide_matte=false
var camera_bank=false
var native_matrices=[]
var node_tables=[]
var matrix_checks=0
var max_matrix_error=0.0
func v(d): return Vector3(d.X,d.Y,d.Z)
func q(d): return Quaternion(d.X,d.Y,d.Z,d.W).normalized()
func _initialize():
	for arg in OS.get_cmdline_user_args():
		if arg=='--native-cull':native_cull=true
		if arg=='--diagnostic-hide-matte':diagnostic_hide_matte=true
		if arg=='--hide-lower-matte':diagnostic_hide_matte=true
		if arg=='--camera-bank':camera_bank=true
		if arg.begins_with('--time-offset='):time_offset=float(arg.get_slice('=',1))
	root.unresizable=true
	root.min_size=Vector2i(1280,720)
	root.max_size=Vector2i(1280,720)
	root.size=Vector2i(1280,720)
	root.content_scale_size=Vector2i(1280,720)
	root.content_scale_mode=Window.CONTENT_SCALE_MODE_VIEWPORT
	native_matrices=JSON.parse_string(FileAccess.get_file_as_string('res://native_matrices.json'))
	layers=JSON.parse_string(FileAccess.get_file_as_string('res://layers.json'))
	timeline=JSON.parse_string(FileAccess.get_file_as_string('res://timeline.json'))
	var world=Node3D.new()
	root.add_child(world)
	var placements=JSON.parse_string(FileAccess.get_file_as_string('res://placements.json'))
	for i in layers.size():
		var j=layers[i]
		var map={}
		node_tables.append([])
		maps.append(map)
		var mats={}
		for pair in j.get('Materials',[]) if j.get('Materials')!=null else []:
			var m=pair.Value
			var mat=StandardMaterial3D.new()
			mat.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED
			mat.cull_mode=BaseMaterial3D.CULL_DISABLED
			if native_cull and m.DisableBackfaceCulling==0:mat.cull_mode=BaseMaterial3D.CULL_BACK
			var c=m.LegacyParameters.DiffuseColor
			mat.albedo_color=Color(c.X,c.Y,c.Z,c.W)
			if m.DiffuseMap!=null:
				mat.set_meta('uvmatrix',m.DiffuseMap)
				var path='res://tex/'+m.DiffuseMap.Name+'.png'
				if FileAccess.file_exists(path): mat.albedo_texture=ImageTexture.create_from_image(Image.load_from_file(path))
			if c.W<0.99:mat.transparency=BaseMaterial3D.TRANSPARENCY_ALPHA
			mat.set_meta('base_color',mat.albedo_color)
			mats[pair.Key]=mat
		layer_materials.append(mats.values())
		var holder=Node3D.new()
		holders.append(holder)
		world.add_child(holder)
		var m=placements[i]
		holder.transform=Transform3D(Basis(v(m.X),v(m.Y),v(m.Z)),Vector3(m.M41,m.M42,m.M43))
		if i==5:
			# Source typography is vertical at the left edge. Preserve its authored transform.
			var vp=SubViewport.new()
			vp.size=Vector2i(1280,720)
			vp.transparent_bg=true
			vp.own_world_3d=true
			root.add_child(vp)
			world.remove_child(holder)
			vp.add_child(holder)
			var cam=Camera3D.new()
			cam.projection=Camera3D.PROJECTION_ORTHOGONAL
			cam.size=720
			cam.position=Vector3(0,0,2000)
			cam.far=5000
			vp.add_child(cam)
			var overlay=TextureRect.new()
			overlay.texture=vp.get_texture()
			overlay.size=Vector2(1280,720)
			root.add_child(overlay)
		build(j.Model.RootNode,holder,map,mats,i)
	camera=Camera3D.new()
	world.add_child(camera)
	camera.near=10
	camera.far=4000
	camera.fov=26.231289
	print('Loaded 6 original GFS layers; meshes + PRS animation')
func matrix(a):
	return Transform3D(Basis(Vector3(a[0],a[1],a[2]),Vector3(a[3],a[4],a[5]),Vector3(a[6],a[7],a[8])),Vector3(a[9],a[10],a[11]))
func scalar(keys,time,field):
	var k=0
	while k+1<keys.size() and keys[k+1].Time<=time+0.000001:k+=1
	var a=keys[k]
	var b=keys[mini(k+1,keys.size()-1)]
	var f=clampf((time-a.Time)/(b.Time-a.Time),0,1) if b.Time>a.Time else 0.0
	return lerpf(a[field],b[field],f)
func build(data,parent,map,mats,idx):
	var n=Node3D.new()
	n.name=data.Name
	n.set_meta('source_name',data.Name)
	parent.add_child(n)
	n.position=v(data.Translation)
	n.quaternion=q(data.Rotation)
	n.scale=v(data.Scale)
	map[data.Name]=n
	node_tables[idx].append(n)
	for a in data.Attachments:
		if a.get('$type','')!='NodeMeshAttachment': continue
		var m=a.Mesh
		var ar=[]
		ar.resize(Mesh.ARRAY_MAX)
		var verts=PackedVector3Array()
		for p in m.Vertices: verts.append(v(p))
		ar[Mesh.ARRAY_VERTEX]=verts
		var uv=PackedVector2Array()
		for p in m.TexCoordsChannel0 if m.TexCoordsChannel0!=null else []:
			var val=Vector2(p.X,p.Y)
			var material=mats.get(m.MaterialName)
			if material!=null and material.has_meta('uvmatrix'):
				var tm=material.get_meta('uvmatrix')
				if int(tm.Flags)&1:val=Vector2(p.X*tm.Field4C+p.Y*tm.Field5C+tm.Field7C,p.X*tm.Field50+p.Y*tm.Field60+tm.Field80)
			uv.append(val)
		if uv.size()==verts.size(): ar[Mesh.ARRAY_TEX_UV]=uv
		var ids=PackedInt32Array()
		for t in m.Triangles:ids.append_array([t.A,t.C,t.B])
		ar[Mesh.ARRAY_INDEX]=ids
		var mesh=ArrayMesh.new()
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,ar)
		var mi=MeshInstance3D.new()
		mi.mesh=mesh
		mi.material_override=mats.get(m.MaterialName)
		n.add_child(mi)
	for c in data.Children:build(c,n,map,mats,idx)
func _process(_delta):
	var t=frame/60.0+time_offset
	for i in layers.size():
		var event=timeline.layers[i]
		var local_time=maxf(0,t-event.start)
		var tint=Color.WHITE
		var opacity=1.0
		for track in event.tracks:
			if track.Keys.is_empty():continue
			if track.KeyType=='Single4Byte':
				tint=Color(scalar(track.Keys,local_time,'Field00'),scalar(track.Keys,local_time,'Field04'),scalar(track.Keys,local_time,'Field08'),1)
			if track.KeyType=='SingleByte': opacity=scalar(track.Keys,local_time,'Field00')
		# EPL end is its last native frame: 119/30 ends a 4-second event.
		holders[i].visible=t+0.000001>=event.start and t<event.end+1.0/timeline.fps_field-0.000001 and opacity>0.00001
		if diagnostic_hide_matte and i==4:holders[i].visible=false
		for mat in layer_materials[i]:
			var base=mat.get_meta('base_color')
			mat.albedo_color=Color(base.r*tint.r,base.g*tint.g,base.b*tint.b,base.a*opacity)
			mat.transparency=BaseMaterial3D.TRANSPARENCY_ALPHA if base.a*opacity<0.999 else BaseMaterial3D.TRANSPARENCY_DISABLED

		var sample_index=clampi(roundi(t*60),0,239)
		var native=native_matrices[i]
		for k in node_tables[i].size():
			assert(native.names[k]==node_tables[i][k].get_meta('source_name'))
			node_tables[i][k].transform=matrix(native.frames[sample_index][k])
		var expected=native.worldSamples.get(str(sample_index))
		if expected!=null:
			for k in node_tables[i].size():
				var actual=holders[i].global_transform.affine_inverse()*node_tables[i][k].global_transform
				var want=matrix(expected[k])
				var error=maxf(actual.origin.distance_to(want.origin),maxf(actual.basis.x.distance_to(want.basis.x),maxf(actual.basis.y.distance_to(want.basis.y),actual.basis.z.distance_to(want.basis.z))))
				max_matrix_error=maxf(max_matrix_error,error)
				matrix_checks+=1
				assert(error<0.02,"Native world transform mismatch")

	if maps[0].has('Camera01'):
		# Preserve every authored camera rotation axis instead of rebuilding a look-at basis.
		camera.global_transform=maps[0]['Camera01'].global_transform
		camera.basis=camera.basis.orthonormalized()
		if camera_bank:
			for control in layers[0].AnimationPack.Animations[0].Controllers:
				if control.TargetKind!='Camera':continue
				for track in control.Layers:
					if track.KeyType=='Single_8' and not track.Keys.is_empty():
						camera.rotate_object_local(Vector3.FORWARD,deg_to_rad(scalar(track.Keys,t,'Value')))
	if frame==239:print("WORLD_MATRIX_AUDIT: ",matrix_checks," comparisons, max error=",max_matrix_error)
	frame+=1
	return false
