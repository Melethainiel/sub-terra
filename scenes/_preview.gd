extends Node3D

## Dev tool: renders one tile scene on its own and quits.
## Usage: SUBTERRA_TILE=res://scenes/board/X.tscn SUBTERRA_SHOT=/tmp/x.png godot res://scenes/_preview.tscn
## (SUBTERRA_VIEW=floor looks down at the floor, =above at the whole tile from a slant)
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
	if OS.get_environment("SUBTERRA_VIEW") == "above":
		# From above at a slant, vaults sliced off as the game's own view does.
		# Lit as scenes/app/Main.tscn lights the table, so what reads here reads in play.
		(load("res://resources/materials/wall_rock.tres") as ShaderMaterial).set_shader_parameter("cutaway", 1.2)
		($Sun as DirectionalLight3D).light_energy = 0.35
		($WorldEnvironment as WorldEnvironment).environment.ambient_light_energy = 0.35
		($Camera3D as Camera3D).look_at_from_position(
			Vector3(0, 3.6, 2.6), Vector3(0, 0.2, 0), Vector3.UP)
	elif OS.get_environment("SUBTERRA_VIEW") == "floor":
		($Camera3D as Camera3D).look_at_from_position(
			Vector3(0, 1.6, -0.2), Vector3(0.3, 0, 0.45), Vector3.UP)
	else:
		($Camera3D as Camera3D).look_at_from_position(
			Vector3(0, 1.6, 0.6), Vector3(0, 1.5, -3.0), Vector3.UP)

	for _i in range(4):
		await RenderingServer.frame_post_draw

	get_viewport().get_texture().get_image().save_png(OS.get_environment("SUBTERRA_SHOT"))
	get_tree().quit()
