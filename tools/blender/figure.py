"""Builds the two sketch figures of the style board: an Explorer and a Guardian.

Headless, to write the models Godot loads:

    blender --background --python tools/blender/figure.py -- --out resources/models/figures

Sketches, not the final cast: each is sculpted from a stick skeleton wrapped in a
Skin modifier and smoothed, plus a few hard-surface props. They exist so the same
two figures can be shown in each candidate art direction — the style is the
material's business in Godot, so every part carries a material named for what it
is (Skin, Cloth, Leather, Metal…) and the board swaps the look by that name.

Axes: Blender is Z-up; the glTF export converts to Godot's Y-up. The figure faces
-Y in Blender, which lands facing +Z (towards a default camera) in Godot.
"""

import argparse
import math
import os
import sys

import bpy
from mathutils import Vector


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name):
    found = bpy.data.materials.get(name)
    if found:
        return found
    made = bpy.data.materials.new(name)
    made.use_nodes = True
    return made


def finish(obj, mat, subdivisions=2):
    """Smooths a skinned part, applies everything and gives it its material."""
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    if subdivisions:
        sub = obj.modifiers.new("Subdivision", "SUBSURF")
        sub.levels = subdivisions
        sub.render_levels = subdivisions
    for modifier in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.ops.object.shade_smooth()
    obj.data.materials.clear()
    obj.data.materials.append(material(mat))
    obj.select_set(False)
    return obj


def skinned(name, bones, radii, mat, parent, subdivisions=2):
    """A limb or a body out of a stick skeleton: vertices, edges, a radius each."""
    points = []
    index = {}
    edges = []
    for a, b in bones:
        for p in (a, b):
            if p not in index:
                index[p] = len(points)
                points.append(p)
        edges.append((index[a], index[b]))

    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([Vector(p) for p in points], edges, [])
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent

    skin = obj.modifiers.new("Skin", "SKIN")
    skin.use_smooth_shade = True
    for i, p in enumerate(points):
        r = radii.get(p, 0.05)
        r = r if isinstance(r, tuple) else (r, r)
        obj.data.skin_vertices[0].data[i].radius = r
    # One root per connected piece, or the modifier refuses to build.
    obj.data.skin_vertices[0].data[0].use_root = True
    return finish(obj, mat, subdivisions)


def primitive(kind, name, mat, parent, location, scale=(1, 1, 1), rotation=(0, 0, 0), smooth=True, **kwargs):
    ops = {
        "sphere": bpy.ops.mesh.primitive_uv_sphere_add,
        "cylinder": bpy.ops.mesh.primitive_cylinder_add,
        "cone": bpy.ops.mesh.primitive_cone_add,
        "cube": bpy.ops.mesh.primitive_cube_add,
        "torus": bpy.ops.mesh.primitive_torus_add,
    }
    ops[kind](location=location, rotation=rotation, **kwargs)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if kind == "cube":
        bevel = obj.modifiers.new("Bevel", "BEVEL")
        bevel.width = 0.02
        bevel.segments = 3
        bpy.ops.object.modifier_apply(modifier="Bevel")
    if smooth:
        bpy.ops.object.shade_smooth()
    obj.data.materials.append(material(mat))
    # Keep the world position while hanging it off the figure's root.
    world = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = world
    obj.select_set(False)
    return obj


def root(name):
    empty = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(empty)
    return empty


def mirrored(points):
    """The same stick drawn on both sides of the figure."""
    return [tuple(p) for p in points] + [(-p[0], p[1], p[2]) for p in points]


def explorer():
    """An adventurer of the 1920s expeditions: hat, satchel, torch held high."""
    figure = root("Explorer")

    legs = []
    radii = {}
    for side in (1, -1):
        hip, knee, ankle = (0.1 * side, 0, 0.9), (0.11 * side, -0.03, 0.5), (0.12 * side, 0, 0.12)
        legs += [(hip, knee), (knee, ankle)]
        radii.update({hip: 0.085, knee: 0.065, ankle: 0.055})
    pelvis = (0, 0, 0.95)
    legs += [(pelvis, (0.1, 0, 0.9)), (pelvis, (-0.1, 0, 0.9))]
    radii[pelvis] = (0.16, 0.11)
    skinned("Legs", legs, radii, "Cloth", figure)

    for side in (1, -1):
        skinned(f"Boot{'L' if side > 0 else 'R'}",
                [((0.12 * side, 0, 0.2), (0.12 * side, 0, 0.06)), ((0.12 * side, 0, 0.06), (0.12 * side, -0.14, 0.04))],
                {(0.12 * side, 0, 0.2): 0.07, (0.12 * side, 0, 0.06): 0.07, (0.12 * side, -0.14, 0.04): 0.05},
                "Leather", figure)

    chest, neck = (0, 0, 1.32), (0, 0, 1.5)
    skinned("Jacket", [((0, 0, 0.92), chest), (chest, neck)],
            {(0, 0, 0.92): (0.17, 0.12), chest: (0.2, 0.13), neck: 0.06}, "Coat", figure)

    arms = []
    radii = {}
    left = [(0.19, 0, 1.44), (0.3, -0.04, 1.2), (0.3, -0.16, 1.02)]
    right = [(-0.19, 0, 1.44), (-0.3, -0.12, 1.6), (-0.26, -0.2, 1.84)]  # torch arm, raised
    for chain in (left, right):
        arms += [(chain[0], chain[1]), (chain[1], chain[2])]
        radii.update({chain[0]: 0.065, chain[1]: 0.05, chain[2]: 0.045})
    arms += [((0, 0, 1.44), left[0]), ((0, 0, 1.44), right[0])]
    radii[(0, 0, 1.44)] = 0.08
    skinned("Sleeves", arms, radii, "Coat", figure)

    primitive("sphere", "HandL", "Skin", figure, (0.3, -0.18, 0.99), scale=(0.05, 0.05, 0.055), segments=16, ring_count=8)
    primitive("sphere", "HandR", "Skin", figure, (-0.26, -0.21, 1.87), scale=(0.05, 0.05, 0.055), segments=16, ring_count=8)
    primitive("sphere", "Head", "Skin", figure, (0, -0.01, 1.66), scale=(0.11, 0.115, 0.13), segments=24, ring_count=12)
    primitive("sphere", "Nose", "Skin", figure, (0, -0.12, 1.66), scale=(0.02, 0.03, 0.025), segments=12, ring_count=6)
    primitive("cylinder", "HatBrim", "Leather", figure, (0, -0.01, 1.76), scale=(0.2, 0.2, 0.012), vertices=32)
    primitive("cone", "HatCrown", "Leather", figure, (0, -0.01, 1.83), scale=(0.12, 0.12, 0.07), vertices=24, radius1=1, radius2=0.8, depth=2)
    primitive("torus", "HatBand", "Accent", figure, (0, -0.01, 1.78), scale=(1, 1, 0.5), major_radius=0.118, minor_radius=0.012)
    primitive("torus", "Belt", "Leather", figure, (0, 0, 0.97), scale=(1, 0.72, 1.4), major_radius=0.16, minor_radius=0.022)
    primitive("cube", "Buckle", "Metal", figure, (0, -0.125, 0.97), scale=(0.03, 0.01, 0.025))
    primitive("cube", "Satchel", "Leather", figure, (0.2, 0.02, 0.95), scale=(0.035, 0.1, 0.08))
    primitive("cube", "Pack", "Leather", figure, (0, 0.17, 1.28), scale=(0.14, 0.07, 0.17))
    primitive("cylinder", "Bedroll", "Cloth", figure, (0, 0.17, 1.48), rotation=(0, math.pi / 2, 0), scale=(0.05, 0.05, 0.16), vertices=16)
    primitive("cylinder", "TorchHandle", "Wood", figure, (-0.26, -0.21, 1.98), scale=(0.018, 0.018, 0.17), vertices=10)
    primitive("cone", "TorchFlame", "Flame", figure, (-0.26, -0.21, 2.2), scale=(0.05, 0.05, 0.1), vertices=12, radius1=1, radius2=0, depth=2)
    return figure


def guardian():
    """One of the Ashen Legion: taller than a man, horned, cased in cooled lava."""
    figure = root("Guardian")

    legs = []
    radii = {}
    for side in (1, -1):
        hip, knee, ankle = (0.14 * side, 0, 1.05), (0.17 * side, -0.06, 0.58), (0.16 * side, 0, 0.12)
        legs += [(hip, knee), (knee, ankle)]
        radii.update({hip: 0.12, knee: 0.1, ankle: 0.1})
    pelvis = (0, 0, 1.1)
    legs += [(pelvis, (0.14, 0, 1.05)), (pelvis, (-0.14, 0, 1.05))]
    radii[pelvis] = (0.2, 0.14)
    skinned("Legs", legs, radii, "Ash", figure)

    chest, neck = (0, 0.02, 1.6), (0, 0.04, 1.85)
    skinned("Torso", [((0, 0, 1.08), chest), (chest, neck)],
            {(0, 0, 1.08): (0.2, 0.14), chest: (0.3, 0.19), neck: 0.09}, "Ash", figure)

    arms = []
    radii = {}
    left = [(0.3, 0.02, 1.76), (0.44, -0.06, 1.44), (0.42, -0.2, 1.2)]  # spear hand
    right = [(-0.3, 0.02, 1.76), (-0.42, 0.02, 1.42), (-0.4, -0.02, 1.1)]
    for chain in (left, right):
        arms += [(chain[0], chain[1]), (chain[1], chain[2])]
        radii.update({chain[0]: 0.1, chain[1]: 0.085, chain[2]: 0.08})
    arms += [((0, 0.04, 1.78), left[0]), ((0, 0.04, 1.78), right[0])]
    radii[(0, 0.04, 1.78)] = 0.12
    skinned("Arms", arms, radii, "Ash", figure)

    # Plates of cooled rock over the body, with the ember showing in the seams.
    for side in (1, -1):
        primitive("sphere", f"Pauldron{side}", "Armor", figure, (0.33 * side, 0.02, 1.84), scale=(0.16, 0.15, 0.1), segments=16, ring_count=8)
        primitive("cube", f"Greave{side}", "Armor", figure, (0.17 * side, -0.1, 0.4), scale=(0.07, 0.03, 0.18))
        primitive("cone", f"Horn{side}", "Bone", figure, (0.1 * side, 0.02, 2.2), rotation=(0.35, 0.5 * side, 0), scale=(0.035, 0.035, 0.14), vertices=12, radius1=1, radius2=0, depth=2)
    primitive("cube", "Breastplate", "Armor", figure, (0, -0.12, 1.55), scale=(0.2, 0.05, 0.2))
    primitive("sphere", "Head", "Ash", figure, (0, 0.0, 2.02), scale=(0.13, 0.14, 0.15), segments=20, ring_count=10)
    for side in (1, -1):
        primitive("sphere", f"Eye{side}", "Ember", figure, (0.05 * side, -0.125, 2.03), scale=(0.022, 0.012, 0.012), segments=10, ring_count=6)
    primitive("cylinder", "SpearShaft", "Wood", figure, (0.42, -0.2, 1.3), scale=(0.022, 0.022, 1.0), vertices=10)
    primitive("cone", "SpearBlade", "Metal", figure, (0.42, -0.2, 2.42), scale=(0.05, 0.015, 0.14), vertices=4, radius1=1, radius2=0, depth=2)
    primitive("torus", "Belt", "Ember", figure, (0, 0, 1.12), scale=(1, 0.72, 1), major_radius=0.2, minor_radius=0.02)
    return figure


def export(figure, path):
    bpy.ops.object.select_all(action="DESELECT")
    figure.select_set(True)
    for child in figure.children_recursive:
        child.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_apply=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", default="resources/models/figures")
    args = parser.parse_args(argv)
    os.makedirs(args.out, exist_ok=True)

    for build, name in ((explorer, "explorer_sketch"), (guardian, "guardian_sketch")):
        reset()
        figure = build()
        export(figure, os.path.join(args.out, f"{name}.glb"))
        print(f"wrote {name}.glb")


if __name__ == "__main__":
    main()
