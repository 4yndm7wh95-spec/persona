extends SceneTree
var vp:SubViewport
var clip:Control
var slot:Control
var card:ColorRect
var exits=0
var checks=0
func _initialize():call_deferred("run")
func draw_ready():
	await process_frame
	await process_frame
	await RenderingServer.frame_post_draw
func inspect(center:float,clipped:bool):
	clip.clip_contents=clipped
	slot.position=Vector2(120,center)-clip.position
	await draw_ready()
	var im=vp.get_texture().get_image()
	for y in range(400):
		var expected=y>=center-70 and y<center+70 and (not clipped or (y>=80 and y<320))
		var lit=im.get_pixel(120,y).r>.9
		if lit!=expected:push_error("Wrong crop at "+str([center,clipped,y]));quit(1);return false
		checks+=1
	return true
func run():
	vp=SubViewport.new();vp.size=Vector2i(400,400);vp.disable_3d=true;vp.render_target_update_mode=SubViewport.UPDATE_ALWAYS
	root.add_child(vp)
	var back=ColorRect.new();back.size=Vector2(400,400);back.color=Color.BLACK;back.mouse_filter=Control.MOUSE_FILTER_IGNORE;vp.add_child(back)
	clip=Control.new();clip.position=Vector2(0,80);clip.size=Vector2(400,240);clip.clip_contents=true;clip.mouse_filter=Control.MOUSE_FILTER_IGNORE;vp.add_child(clip)
	slot=Control.new();slot.mouse_filter=Control.MOUSE_FILTER_IGNORE;clip.add_child(slot)
	card=ColorRect.new();card.position=Vector2(-40,-70);card.size=Vector2(80,140);card.color=Color.WHITE;slot.add_child(card)
	card.tree_exiting.connect(func():exits+=1)
	for center in [40.0,80.0,120.0,280.0,320.0,360.0]:
		if not await inspect(center,true):return
	if not await inspect(80,false):return
	if not await inspect(80,true):return
	for pair in [[Vector2(120,60),false],[Vector2(120,100),true]]:
		var motion=InputEventMouseMotion.new();motion.position=pair[0];motion.global_position=pair[0];vp.push_input(motion,true)
		await process_frame
		var hit=vp.gui_get_hovered_control()==card
		if hit!=pair[1]:push_error("Hidden card input leaked: "+str(pair));quit(1);return
		checks+=1
	if exits!=0:push_error("Holder left tree during focus transition");quit(1);return
	FileAccess.open(OS.get_cmdline_user_args()[0],FileAccess.WRITE).store_string(JSON.stringify({"pass":true,"pixel_and_input_checks":checks,"holder_tree_exits":exits},"\t"))
	print("PASS ",checks," hard clip / opacity / pointer checks; holder tree exits=",exits)
	quit()
