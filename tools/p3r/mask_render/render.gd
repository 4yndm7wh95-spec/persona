extends SceneTree
var cfg
var mask_viewports=[]
var original_masks=[]
var empty_mask
func _initialize():
 var args=OS.get_cmdline_user_args()
 assert(args.size()==2,"Arguments: absolute repo path, absolute output directory")
 cfg={"repo":args[0],"out":args[1],"width":1280,"height":720}
 DirAccess.make_dir_recursive_absolute(cfg.out)
 call_deferred("run")
func run():
 await prepare_masks()
 print("ORIGINAL_MASK_CACHE_COMPLETE count=5")
 quit()
func prepare_masks():
 var data=JSON.parse_string(FileAccess.get_file_as_string(cfg.repo+"/research/p3r_camp/plg/json/Xrd777/UI/Camp/Root/Plg/PLG_UI_Camp.json"))[0].Properties.PlgData.PlgDatas
 var rows=[]
 for i in range(12,17):rows.append({"geometry":data[i]})
 var empty=Image.create(1,1,false,Image.FORMAT_RGBA8)
 empty.fill(Color.BLACK)
 empty_mask=ImageTexture.create_from_image(empty)
 for row in rows:
  var geometry=row.geometry
  var mask_vp=SubViewport.new()
  mask_vp.size=Vector2i(cfg.width,cfg.height)
  mask_vp.disable_3d=true
  mask_vp.transparent_bg=false
  mask_vp.render_target_clear_mode=SubViewport.CLEAR_MODE_ALWAYS
  mask_vp.render_target_update_mode=SubViewport.UPDATE_ALWAYS
  root.add_child(mask_vp)
  var black=ColorRect.new()
  black.size=Vector2(cfg.width,cfg.height)
  black.color=Color.BLACK
  mask_vp.add_child(black)
  var vertices=PackedVector3Array()
  var colors=PackedColorArray()
  var indices=PackedInt32Array()
  for i in geometry.Vertices.size():
   var v=geometry.Vertices[i]
   vertices.append(Vector3(float(v.X)*float(cfg.width)/1920.0,float(v.Y)*float(cfg.height)/1080.0,0))
   # Original polygon colors: 0xffffffff / 0xffffff00; low byte is alpha.
   var alpha=float(int(geometry.Colors[i])&255)/255.0
   colors.append(Color(alpha,alpha,alpha,1.0))
  for i in geometry.Indices:indices.append(int(i))
  var arrays=[]
  arrays.resize(Mesh.ARRAY_MAX)
  arrays[Mesh.ARRAY_VERTEX]=vertices
  arrays[Mesh.ARRAY_COLOR]=colors
  arrays[Mesh.ARRAY_INDEX]=indices
  var mesh=ArrayMesh.new()
  mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
  var display=MeshInstance2D.new()
  display.mesh=mesh
  var sh=Shader.new()
  sh.code="shader_type canvas_item; render_mode unshaded, blend_disabled;"
  var mat=ShaderMaterial.new()
  mat.shader=sh
  display.material=mat
  mask_vp.add_child(display)
  mask_viewports.append(mask_vp)
 for i in 3:
  await process_frame
  RenderingServer.force_draw(false,1.0/60.0)
 for i in mask_viewports.size():
  var result=mask_viewports[i].get_texture().get_image()
  result.convert(Image.FORMAT_L8)
  result.save_png(cfg.out+"/in_mask_%02d.png"%i)
  original_masks.append(ImageTexture.create_from_image(result))
  mask_viewports[i].render_target_update_mode=SubViewport.UPDATE_DISABLED
  print("NATIVE_MASK ",i," original vertices=",rows[i].geometry.Vertices.size())

