extends SceneTree
## Headless Godot regression: exiting a pile must not poison pooled hitboxes.
## Run with: godot --headless --script <absolute path to this file>

var failures: int = 0

func _initialize() -> void:
	call_deferred("run")

func check(condition: bool, message: String) -> void:
	if not condition:
		failures += 1
		push_error(message)

func run() -> void:
	var reward := Control.new()
	root.add_child(reward)
	var holder := Control.new()
	var hitbox := Control.new()
	hitbox.mouse_filter = Control.MOUSE_FILTER_STOP
	holder.add_child(hitbox)
	reward.add_child(holder)

	# Reproduce the old leak: changing a pooled child's property survives reparenting.
	var old_screen := Control.new()
	root.add_child(old_screen)
	holder.reparent(old_screen)
	hitbox.mouse_filter = Control.MOUSE_FILTER_IGNORE
	holder.reparent(reward)
	check(hitbox.get_mouse_filter_with_override() == Control.MOUSE_FILTER_IGNORE,
		"Legacy setup must reproduce an ignored hitbox after reuse")
	hitbox.mouse_filter = Control.MOUSE_FILTER_STOP
	old_screen.free()

	# Exercise inherited STOP/PASS values without overwriting either on exit.
	for original_filter in [Control.MOUSE_FILTER_STOP, Control.MOUSE_FILTER_PASS]:
		hitbox.mouse_filter = original_filter
		for cycle in range(32):
			var screen := Control.new()
			var back := Control.new()
			root.add_child(screen)
			screen.add_child(back)
			holder.reparent(screen)
			check(hitbox.get_mouse_filter_with_override() == original_filter,
				"Hitbox must accept input before exit")
			# Matches PileFx exit: animate the screen, disable mouse on that ancestor only.
			screen.process_mode = Node.PROCESS_MODE_ALWAYS
			screen.mouse_behavior_recursive = Control.MOUSE_BEHAVIOR_DISABLED
			check(hitbox.get_mouse_filter_with_override() == Control.MOUSE_FILTER_IGNORE,
				"Exiting cards must ignore input")
			check(back.get_mouse_filter_with_override() == Control.MOUSE_FILTER_IGNORE,
				"Exiting back button must ignore input")
			check(hitbox.mouse_filter == original_filter,
				"Exit must preserve pooled hitbox property")
			holder.reparent(reward)
			check(hitbox.get_mouse_filter_with_override() == original_filter,
				"Reused card must regain input in reward/upgrade screen")
			screen.free()

	reward.free()
	if failures == 0:
		print("PASS: legacy leak reproduced; 64 exit/reuse cycles preserve STOP/PASS hitboxes")
	quit(0 if failures == 0 else 1)
