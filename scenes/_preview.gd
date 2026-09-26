extends Node3D

## Dev tool: renders one tile scene on its own and quits.
## Usage: SUBTERRA_TILE=res://scenes/board/X.tscn SUBTERRA_SHOT=/tmp/x.png godot res://scenes/_preview.tscn
## (add SUBTERRA_VIEW=floor to look down at the floor rather than along the gallery)
##
## Tiles are roofed and lit mainly by BoardView's own per-tile torchlight at
## runtime, so the "Torch" node here stands in for it — a neutral approximation,
## not the real kind-tinted glow.

func _ready() -> void:
	var tile_path := OS.get_environment("SUBTERRA_TILE")
	if tile_path != "":
		add_child((load(tile_path) as PackedScene).instantiate())

	# From inside, at an Explorer's eye height and level, looking North down the
	# passage — every shape in the catalogue opens that way, see TileShape.OpenSides.
	# Stay near the middle: that much is open on any tile, while a step south lands
	# inside solid rock on a Corner or a DeadEnd, and the shot comes back with the
	# wall's own geometry slicing across it.
	#
	# SUBTERRA_VIEW=floor looks down at the slab instead, for what lies on it.
	if OS.get_environment("SUBTERRA_VIEW") == "floor":
		($Camera3D as Camera3D).look_at_from_position(
			Vector3(0, 1.6, -0.2), Vector3(0.3, 0, 0.45), Vector3.UP)
	else:
		($Camera3D as Camera3D).look_at_from_position(
			Vector3(0, 1.6, 0.6), Vector3(0, 1.5, -3.0), Vector3.UP)

	for _i in range(4):
		await RenderingServer.frame_post_draw

	get_viewport().get_texture().get_image().save_png(OS.get_environment("SUBTERRA_SHOT"))
	get_tree().quit()
