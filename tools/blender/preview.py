"""Sets up a look at the tile from inside and renders it, in the open Blender.

    exec(open("tools/blender/preview.py").read())
    shoot("/tmp/shot")            # writes /tmp/shot_couloir.png &c.

Neutral light, not the game's: the point here is to judge the rock's shape, so a
plain matte grey and one warm lamp where an Explorer's torch would be. What the
tile looks like in the temple is Godot's business.
"""

import math

import bpy

ROCK = "Preview_Rock"
TORCH = "Preview_Torch"
CAMERA = "Preview_Camera"

# Where an Explorer would actually stand: the middle of a tile, eyes at 1.05 m.
# Not with their nose against the rock — a wide lens at arm's length turns a 10 cm
# ledge into a flying shelf, and three good hours were lost to that.
SHOTS = {
    # name: (where the camera stands, where it looks)
    "couloir": ((0.0, -0.9, 1.05), (0.0, 1.0, 1.0)),
    "mur": ((0.0, 0.0, 1.05), (-1.0, 0.0, 0.9)),
    "voute": ((0.0, -0.3, 0.5), (0.0, 0.4, 1.5)),
    "epaule": ((0.45, -0.45, 1.05), (-0.45, 0.45, 0.95)),
}

# The whole tile in section, from above, with the rock over the vault clipped away.
PLAN_HEIGHT = 4.0
PLAN_CLIP = 2.55


def material():
    if ROCK in bpy.data.materials:
        return bpy.data.materials[ROCK]

    rock = bpy.data.materials.new(ROCK)
    rock.use_nodes = True
    shader = rock.node_tree.nodes["Principled BSDF"]
    shader.inputs["Base Color"].default_value = (0.34, 0.33, 0.35, 1.0)
    shader.inputs["Roughness"].default_value = 1.0
    return rock


def light():
    if TORCH in bpy.data.objects:
        return bpy.data.objects[TORCH]

    torch = bpy.data.objects.new(TORCH, bpy.data.lights.new(TORCH, type="POINT"))
    torch.data.energy = 30.0
    torch.data.color = (1.0, 0.72, 0.5)
    torch.data.shadow_soft_size = 0.3
    torch.location = (0.0, 0.0, 1.05)
    bpy.context.collection.objects.link(torch)
    return torch


def camera():
    if CAMERA in bpy.data.objects:
        return bpy.data.objects[CAMERA]

    cam = bpy.data.objects.new(CAMERA, bpy.data.cameras.new(CAMERA))
    cam.data.lens = 24.0
    bpy.context.collection.objects.link(cam)
    return cam


def aim(cam, where, at):
    from mathutils import Vector

    cam.location = where
    direction = Vector(at) - Vector(where)
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def setup():
    rock = material()

    for obj in bpy.data.objects:
        if obj.type == "MESH" and obj.name.startswith("Cave"):
            obj.data.materials.clear()
            obj.data.materials.append(rock)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 540
    scene.render.image_settings.file_format = "PNG"

    if scene.world is None:
        scene.world = bpy.data.worlds.new("Preview_World")

    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes["Background"]
    background.inputs[0].default_value = (0.03, 0.03, 0.04, 1.0)
    background.inputs[1].default_value = 0.5

    light()
    scene.camera = camera()
    return scene


def plan(out):
    """Looks straight down on the tile with everything above the vault clipped
    off — the one view that shows all four passages and how the rock reads as a
    whole. Perspective, not orthographic, so the blocks still have relief."""
    scene = setup()
    cam = scene.camera
    cam.data.lens = 35.0
    cam.data.clip_start = PLAN_CLIP
    aim(cam, (0.0, 0.0, PLAN_HEIGHT), (0.0, 0.0, 0.0))

    # Workbench, not EEVEE: the camera clips the rock over the vault away but a
    # lamp still can't shine through it, so a lit render of this view comes back
    # black. Workbench lights the surfaces themselves, which is what a section
    # drawing wants anyway.
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "SINGLE"
    scene.display.shading.single_color = (0.45, 0.44, 0.46)
    scene.display.shading.show_cavity = True

    scene.render.filepath = f"{out}_plan.png"
    bpy.ops.render.render(write_still=True)

    scene.render.engine = "BLENDER_EEVEE"
    cam.data.clip_start = 0.1
    cam.data.lens = 24.0
    return scene.render.filepath


def shoot(out="/tmp/tile", shots=None):
    scene = setup()
    cam = scene.camera
    written = []

    for name in (shots or list(SHOTS)):
        where, at = SHOTS[name]
        aim(cam, where, at)
        scene.render.filepath = f"{out}_{name}.png"
        bpy.ops.render.render(write_still=True)
        written.append(scene.render.filepath)

    return written
