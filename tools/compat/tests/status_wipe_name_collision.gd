extends SceneTree
## Native Godot regression: queued-for-deletion nodes still reserve their names this frame.
## This explains why TipList's old GetNode("Wipe") controlled the wrong sweep after rebuilding.

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var back := Node2D.new()
	root.add_child(back)
	var old := Node2D.new()
	old.name = "Wipe"
	back.add_child(old)
	old.queue_free()
	var replacement := Node2D.new()
	replacement.name = "Wipe"
	back.add_child(replacement)
	if back.get_node("Wipe") != old or replacement.name == "Wipe":
		push_error("Expected the old queued node to retain Wipe, forcing the replacement to be renamed")
		quit(1)
		return
	back.get_node("Wipe").hide()
	if not replacement.visible or replacement.position != Vector2.ZERO:
		push_error("Expected the new sweep to remain visible at the left edge")
		quit(1)
		return
	await process_frame
	await process_frame
	if back.has_node("Wipe") or not is_instance_valid(replacement) or not replacement.visible:
		push_error("Expected a stranded visible sweep after deferred deletion")
		quit(1)
		return
	back.free()
	print("PASS: old TipList Wipe name collision reproduced; replacement stranded visible at (0,0)")
	quit(0)
