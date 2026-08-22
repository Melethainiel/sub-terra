extends Node3D

## Dev tool: renders one tile scene on its own and quits.
## Usage: SUBTERRA_TILE=res://scenes/board/X.tscn SUBTERRA_SHOT=/tmp/x.png godot res://scenes/_preview.tscn

func _ready() -> void:
	var tile_path := OS.get_environment("SUBTERRA_TILE")
	if tile_path != "":
		add_child((load(tile_path) as PackedScene).instantiate())

	($Camera3D as Camera3D).look_at_from_position(
		Vector3(1.7, 6.2, 2.1), Vector3(0, 0.35, 0), Vector3.UP)

	for _i in range(4):
		await RenderingServer.frame_post_draw

	get_viewport().get_texture().get_image().save_png(OS.get_environment("SUBTERRA_SHOT"))
	get_tree().quit()
