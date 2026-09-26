"""Builds the cast: one miniature per Explorer sheet, the Guardian, and the sketches.

Headless, to write the models Godot loads:

    blender --background --python tools/blender/figure.py -- --out resources/models/figures
    blender --background --python tools/blender/figure.py -- --only guide

Each figure is sculpted from stick skeletons wrapped in a Skin modifier and
smoothed, plus hard-surface props. They are made to be told apart by silhouette —
a hat, a tool, a stance — since the coat's colour belongs to the player's seat.
Detail is not the goal here: a better-crafted cast replaces these files later,
under the same names and conventions (docs/art/direction-artistique.md).

Every part carries a material named for what it is (Skin, Coat, Metal…) and
nothing else: the game paints by that name (scripts/Presentation/Miniature.cs).

Axes: Blender is Z-up, the figure faces -Y, origin on the ground between the feet;
the glTF export turns that into Godot's Y-up, facing +Z.
"""

import argparse
import math
import os
import sys

import bpy
from mathutils import Vector


# ── Plumbing ─────────────────────────────────────────────────────────────────

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name):
    found = bpy.data.materials.get(name)
    if found:
        return found
    made = bpy.data.materials.new(name)
    made.use_nodes = True
    return made


def root(name):
    empty = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(empty)
    return empty


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
    """A limb or a body out of a stick skeleton: segments, and a radius per joint."""
    points, index, edges = [], {}, []
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
    obj.data.skin_vertices[0].data[0].use_root = True
    return finish(obj, mat, subdivisions)


def part(kind, name, mat, parent, location, scale=(1, 1, 1), rotation=(0, 0, 0), bevel=True, **kwargs):
    """A hard-surface prop: sphere, cylinder, cone, cube or torus."""
    ops = {
        "sphere": bpy.ops.mesh.primitive_uv_sphere_add,
        "cylinder": bpy.ops.mesh.primitive_cylinder_add,
        "cone": bpy.ops.mesh.primitive_cone_add,
        "cube": bpy.ops.mesh.primitive_cube_add,
        "torus": bpy.ops.mesh.primitive_torus_add,
    }
    if kind == "sphere":
        kwargs.setdefault("segments", 20)
        kwargs.setdefault("ring_count", 10)
    if kind in ("cylinder", "cone"):
        kwargs.setdefault("vertices", 20)
    if kind == "torus":
        kwargs.setdefault("major_segments", 24)
        kwargs.setdefault("minor_segments", 8)
    ops[kind](location=location, rotation=rotation, **kwargs)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if kind == "cube" and bevel:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = min(0.015, min(scale) * 0.4)
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier="Bevel")
    bpy.ops.object.shade_smooth()
    obj.data.materials.append(material(mat))
    world = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = world
    obj.select_set(False)
    return obj


def between(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def rod(name, mat, parent, a, b, radius, vertices=10):
    """A straight shaft from a to b — a spear, a rifle barrel, a cane."""
    a, b = Vector(a), Vector(b)
    direction = b - a
    obj = part("cylinder", name, mat, parent, tuple((a + b) / 2), scale=(radius, radius, direction.length / 2), vertices=vertices)
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(direction.normalized())
    return obj


# ── The body ─────────────────────────────────────────────────────────────────

class Body:
    """
    Where a figure's joints sit, for its props to hang off. Built by `body`, which
    sculpts legs (or a robe), torso and sleeves, hands, head and neck.
    """

    def __init__(self, **joints):
        self.__dict__.update(joints)


def body(figure, height=1.0, bulk=1.0, hips=1.0, legs="trousers", lean=0.0,
         left=((0.3, -0.04, 1.2), (0.3, -0.12, 1.0)), right=((-0.3, -0.04, 1.2), (-0.3, -0.12, 1.0)),
         stance=0.1, skin="Skin", hair=True):
    """
    One Explorer's body. `left` and `right` give each arm's elbow and wrist, at
    the reference height of 1.70 m; everything scales with `height`. `lean` tips
    the torso forwards (a crouch), `stance` spreads the feet. `legs` is trousers,
    a knee-length skirt over them, or a robe to the ground; `hair` caps the head
    for those who wear nothing on it.
    """
    s = height

    def at(p):
        return (p[0] * s, p[1] * s, p[2] * s)

    pelvis = at((0, 0, 0.95))
    chest = at((0, -0.12 * lean, 1.3 - 0.03 * lean))
    neck = at((0, -0.22 * lean, 1.46 - 0.06 * lean))
    shoulders = at((0, -0.17 * lean, 1.4 - 0.05 * lean))
    head = at((0, -0.27 * lean - 0.01, 1.62 - 0.08 * lean))

    if legs == "skirt":
        skinned("Skirt", [(pelvis, at((0, 0, 0.5)))],
                {pelvis: (0.18 * hips * s, 0.13 * s), at((0, 0, 0.5)): (0.25 * s, 0.2 * s)},
                "Cloth", figure)
        legs = "trousers"

    if legs == "robe":
        skinned("Robe", [(pelvis, at((0, 0, 0.5))), (at((0, 0, 0.5)), at((0, 0, 0.06)))],
                {pelvis: (0.18 * hips * s, 0.13 * s), at((0, 0, 0.5)): (0.22 * s, 0.18 * s), at((0, 0, 0.06)): (0.28 * s, 0.24 * s)},
                "Cloth", figure)
    else:
        # Each leg on its own, from the hip down: joined at one pelvis vertex, the
        # Skin modifier knots the three branches into a jagged point.
        for side, tag in ((1, "L"), (-1, "R")):
            hip = at((0.09 * side * hips, 0, 0.93))
            knee = at(((0.1 + stance * 0.4) * side, -0.03 - 0.05 * lean, 0.5 - 0.05 * lean))
            ankle = at(((0.1 + stance) * side, 0, 0.12))
            skinned(f"Leg{tag}", [(hip, knee), (knee, ankle)],
                    {hip: 0.09 * bulk * s, knee: 0.065 * bulk * s, ankle: 0.055 * s}, "Cloth", figure)
        part("sphere", "Hips", "Cloth", figure, at((0, 0, 0.92)), scale=(0.17 * hips * bulk * s, 0.12 * bulk * s, 0.11 * s))
        for side, tag in ((1, "L"), (-1, "R")):
            ankle = at(((0.1 + stance) * side, 0, 0.2))
            foot = at(((0.1 + stance) * side, 0, 0.06))
            toe = at(((0.1 + stance) * side, -0.14, 0.04))
            skinned(f"Boot{tag}", [(ankle, foot), (foot, toe)], {ankle: 0.07 * s, foot: 0.07 * s, toe: 0.05 * s}, "Leather", figure)

    # Down past the hips: where the legs meet, the skin modifier leaves a point that
    # the coat's hem has to cover.
    hem = at((0, 0, 0.8))
    skinned("Jacket", [(hem, chest), (chest, neck)],
            {hem: (0.19 * hips * bulk * s, 0.14 * bulk * s), chest: (0.2 * bulk * s, 0.13 * bulk * s), neck: 0.06 * s},
            "Coat", figure)

    arm_bones, arm_radii = [], {shoulders: 0.08 * bulk * s}
    wrists = []
    for side, (elbow, wrist) in ((1, left), (-1, right)):
        shoulder = at((0.19 * side * bulk, -0.17 * lean, 1.42 - 0.05 * lean))
        e, w = at(elbow), at(wrist)
        arm_bones += [(shoulders, shoulder), (shoulder, e), (e, w)]
        arm_radii.update({shoulder: 0.065 * bulk * s, e: 0.05 * bulk * s, w: 0.045 * s})
        wrists.append(w)
    # Thin enough to stay smooth on one subdivision: the triangles go where the
    # silhouette needs them, the legs and the coat.
    skinned("Sleeves", arm_bones, arm_radii, "Coat", figure, subdivisions=1)

    hands = []
    for tag, w in zip("LR", wrists):
        hand = (w[0], w[1] - 0.01 * s, w[2] - 0.03 * s)
        part("sphere", f"Hand{tag}", skin, figure, hand, scale=(0.05 * s, 0.05 * s, 0.055 * s), segments=14, ring_count=8)
        hands.append(hand)

    part("sphere", "Head", skin, figure, head, scale=(0.11 * s, 0.115 * s, 0.13 * s), segments=24, ring_count=12)
    part("sphere", "Nose", skin, figure, (head[0], head[1] - 0.11 * s, head[2]), scale=(0.02 * s, 0.03 * s, 0.025 * s), segments=10, ring_count=6)
    if hair:
        part("sphere", "Hair", "Hair", figure, (head[0], head[1] + 0.015 * s, head[2] + 0.025 * s), scale=(0.117 * s, 0.12 * s, 0.12 * s))

    return Body(s=s, pelvis=pelvis, chest=chest, neck=neck, head=head, left=hands[0], right=hands[1],
                back=(0, 0.16 * bulk * s, 1.28 * s), waist=at((0, 0, 0.97)), bulk=bulk)


# ── Props ────────────────────────────────────────────────────────────────────

def belt(f, b, mat="Leather"):
    s = b.s
    part("torus", "Belt", mat, f, b.waist, scale=(1, 0.72, 1.4), major_radius=0.16 * b.bulk * s, minor_radius=0.022 * s)
    part("cube", "Buckle", "Metal", f, (0, -0.125 * b.bulk * s, b.waist[2]), scale=(0.03 * s, 0.01 * s, 0.025 * s))


def brimmed_hat(f, b, brim=0.2, crown=0.12, tall=0.07, band="Accent", mat="Leather"):
    x, y, z = b.head
    s = b.s
    part("cylinder", "HatBrim", mat, f, (x, y, z + 0.1 * s), scale=(brim * s, brim * s, 0.012 * s), vertices=32)
    part("cone", "HatCrown", mat, f, (x, y, z + 0.1 * s + tall * s), scale=(crown * s, crown * s, tall * s), radius1=1, radius2=0.8, depth=2, vertices=24)
    part("torus", "HatBand", band, f, (x, y, z + 0.12 * s), scale=(1, 1, 0.5), major_radius=crown * 0.98 * s, minor_radius=0.012 * s)


def helmet(f, b, mat="Metal", lamp=False):
    x, y, z = b.head
    s = b.s
    part("sphere", "Helmet", mat, f, (x, y, z + 0.05 * s), scale=(0.135 * s, 0.14 * s, 0.1 * s))
    part("cylinder", "HelmetRim", mat, f, (x, y, z + 0.02 * s), scale=(0.16 * s, 0.17 * s, 0.01 * s), vertices=28)
    if lamp:
        part("cylinder", "Lamp", "Metal", f, (x, y - 0.13 * s, z + 0.08 * s), rotation=(math.pi / 2, 0, 0), scale=(0.035 * s, 0.035 * s, 0.03 * s))
        part("sphere", "LampGlow", "Flame", f, (x, y - 0.165 * s, z + 0.08 * s), scale=(0.028 * s, 0.01 * s, 0.028 * s))


def hood(f, b, mat="Cloth"):
    x, y, z = b.head
    s = b.s
    part("sphere", "Hood", mat, f, (x, y + 0.02 * s, z + 0.02 * s), scale=(0.14 * s, 0.15 * s, 0.155 * s))
    part("cone", "HoodPeak", mat, f, (x, y + 0.06 * s, z + 0.14 * s), rotation=(-0.4, 0, 0), scale=(0.06 * s, 0.06 * s, 0.05 * s), radius1=1, radius2=0, depth=2)


def pack(f, b, mat="Leather", size=1.0, roll=True):
    s = b.s
    x, y, z = b.back
    part("cube", "Pack", mat, f, (x, y, z), scale=(0.14 * size * s, 0.07 * size * s, 0.17 * size * s))
    if roll:
        part("cylinder", "Bedroll", "Cloth", f, (x, y, z + 0.2 * size * s), rotation=(0, math.pi / 2, 0), scale=(0.05 * s, 0.05 * s, 0.16 * size * s), vertices=16)


def satchel(f, b, side=1, mat="Leather", cross=False):
    s = b.s
    x = 0.21 * side * b.bulk * s
    part("cube", "Satchel", mat, f, (x, -0.02 * s, 0.93 * s), scale=(0.04 * s, 0.11 * s, 0.09 * s))
    strap = rod("SatchelStrap", mat, f, (x, -0.02 * s, 1.0 * s), (-0.15 * side * s, -0.05 * s, 1.44 * s), 0.012 * s, vertices=6)
    if cross:
        part("cube", "CrossV", "Accent", f, (x + 0.042 * side * s, -0.02 * s, 0.94 * s), scale=(0.004 * s, 0.018 * s, 0.05 * s), bevel=False)
        part("cube", "CrossH", "Accent", f, (x + 0.042 * side * s, -0.02 * s, 0.94 * s), scale=(0.004 * s, 0.05 * s, 0.018 * s), bevel=False)
    return strap


def torch(f, hand, s):
    x, y, z = hand
    rod("TorchHandle", "Wood", f, (x, y, z - 0.08 * s), (x, y, z + 0.22 * s), 0.018 * s)
    part("cone", "TorchFlame", "Flame", f, (x, y, z + 0.3 * s), scale=(0.05 * s, 0.05 * s, 0.1 * s), radius1=1, radius2=0, depth=2, vertices=12)


def lantern(f, hand, s):
    x, y, z = hand
    rod("LanternBail", "Metal", f, (x, y, z), (x, y, z - 0.08 * s), 0.006 * s, vertices=6)
    part("cube", "LanternCap", "Metal", f, (x, y, z - 0.1 * s), scale=(0.05 * s, 0.05 * s, 0.015 * s))
    part("cylinder", "LanternGlass", "Flame", f, (x, y, z - 0.17 * s), scale=(0.04 * s, 0.04 * s, 0.055 * s), vertices=12)
    part("cube", "LanternFoot", "Metal", f, (x, y, z - 0.235 * s), scale=(0.05 * s, 0.05 * s, 0.012 * s))


def book(f, hand, s, open_=False):
    x, y, z = hand
    part("cube", "Book", "Leather", f, (x, y - 0.03 * s, z + 0.02 * s), rotation=(0.5, 0, 0), scale=(0.08 * s, 0.02 * s, 0.1 * s))
    part("cube", "Pages", "Bone", f, (x, y - 0.045 * s, z + 0.025 * s), rotation=(0.5, 0, 0), scale=(0.07 * s, 0.012 * s, 0.09 * s))


def blade(f, hand, s, length=0.45, up=True, name="Blade", width=0.03):
    x, y, z = hand
    d = 1 if up else -1
    part("cube", f"{name}Guard", "Metal", f, (x, y, z + 0.03 * d * s), scale=(0.06 * s, 0.015 * s, 0.012 * s))
    part("cube", name, "Metal", f, (x, y, z + (0.05 + length / 2) * d * s), scale=(width * s, 0.006 * s, length / 2 * s))
    rod(f"{name}Grip", "Leather", f, (x, y, z + 0.03 * d * s), (x, y, z - 0.07 * d * s), 0.014 * s, vertices=8)


# ── The cast ─────────────────────────────────────────────────────────────────

def archeologue():
    """L'Archéologue: wide hat, spectacles, a notebook open in one hand, a trowel in the other."""
    f = root("Archeologue")
    b = body(f, height=0.97, hips=1.15, legs="skirt", hair=False,
             left=((0.26, -0.14, 1.15), (0.14, -0.26, 1.12)),
             right=((-0.3, -0.06, 1.18), (-0.3, -0.16, 0.98)))
    brimmed_hat(f, b, brim=0.23, crown=0.115, tall=0.06, band="Accent")
    x, y, z = b.head
    for side in (1, -1):
        part("torus", f"Lens{side}", "Metal", f, (x + 0.045 * side * b.s, y - 0.11 * b.s, z + 0.01 * b.s), rotation=(math.pi / 2, 0, 0), major_radius=0.022 * b.s, minor_radius=0.004 * b.s)
    part("sphere", "Bun", "Hair", f, (x, y + 0.1 * b.s, z - 0.02 * b.s), scale=(0.06 * b.s, 0.05 * b.s, 0.06 * b.s))
    book(f, b.left, b.s)
    rx, ry, rz = b.right
    part("cone", "Trowel", "Metal", f, (rx, ry - 0.02 * b.s, rz - 0.12 * b.s), rotation=(math.pi, 0, 0), scale=(0.035 * b.s, 0.008 * b.s, 0.06 * b.s), radius1=1, radius2=0, depth=2, vertices=4)
    belt(f, b)
    satchel(f, b, side=1)
    return f


def guide():
    """Le Guide: a lantern held out in front, a coil of rope over the shoulder, a big pack."""
    f = root("Guide")
    b = body(f, height=1.02, stance=0.14, hair=False,
             left=((0.32, -0.08, 1.24), (0.34, -0.16, 1.2)),
             right=((-0.26, -0.2, 1.36), (-0.2, -0.42, 1.4)))
    brimmed_hat(f, b, brim=0.16, crown=0.1, tall=0.05, band="Leather", mat="Cloth")
    lantern(f, b.right, b.s)
    lx, ly, lz = b.left
    rod("Staff", "Wood", f, (lx, ly, 0.02), (lx, ly, lz + 0.55 * b.s), 0.02 * b.s)
    part("torus", "Rope", "Wood", f, (0.06 * b.s, -0.02 * b.s, 1.3 * b.s), rotation=(0.1, 0.5, 0.2), major_radius=0.17 * b.s, minor_radius=0.025 * b.s)
    pack(f, b, size=1.35)
    belt(f, b)
    return f


def gredin():
    """Le Gredin: hooded, face masked, crouched forward with a dagger in each hand."""
    f = root("Gredin")
    b = body(f, height=0.95, lean=1.0, stance=0.2, hair=False,
             left=((0.32, -0.16, 1.12), (0.26, -0.34, 1.0)),
             right=((-0.32, -0.16, 1.12), (-0.26, -0.34, 1.0)))
    hood(f, b)
    skinned("Cape", [((0, 0.1 * b.s, 1.38 * b.s), (0, 0.24 * b.s, 0.6 * b.s))],
            {(0, 0.1 * b.s, 1.38 * b.s): (0.2 * b.s, 0.06 * b.s), (0, 0.24 * b.s, 0.6 * b.s): (0.3 * b.s, 0.05 * b.s)}, "Cloth", f)
    x, y, z = b.head
    part("cube", "Mask", "Accent", f, (x, y - 0.1 * b.s, z - 0.05 * b.s), scale=(0.1 * b.s, 0.03 * b.s, 0.05 * b.s))
    for hand, name in ((b.left, "DaggerL"), (b.right, "DaggerR")):
        blade(f, hand, b.s, length=0.2, up=False, name=name, width=0.018)
    belt(f, b)
    part("cube", "Pouch", "Leather", f, (0.14 * b.s, -0.1 * b.s, 0.93 * b.s), scale=(0.04 * b.s, 0.025 * b.s, 0.04 * b.s))
    return f


def aristocrate():
    """L'Aristocrate: top hat, a long frock coat to the knees, a cane planted beside."""
    f = root("Aristocrate")
    b = body(f, height=1.04, hair=False,
             left=((0.3, -0.04, 1.2), (0.3, -0.12, 1.0)),
             right=((-0.3, -0.1, 1.2), (-0.33, -0.2, 1.0)))
    x, y, z = b.head
    s = b.s
    part("cylinder", "TopBrim", "Leather", f, (x, y, z + 0.1 * s), scale=(0.16 * s, 0.16 * s, 0.01 * s), vertices=32)
    part("cylinder", "TopCrown", "Leather", f, (x, y, z + 0.22 * s), scale=(0.1 * s, 0.1 * s, 0.12 * s), vertices=28)
    part("torus", "TopBand", "Accent", f, (x, y, z + 0.13 * s), scale=(1, 1, 0.8), major_radius=0.1 * s, minor_radius=0.012 * s)
    skinned("Tails", [((0, 0.02 * s, 0.95 * s), (0, 0.06 * s, 0.55 * s))], {(0, 0.02 * s, 0.95 * s): (0.18 * s, 0.13 * s), (0, 0.06 * s, 0.55 * s): (0.2 * s, 0.1 * s)}, "Coat", f)
    rx, ry, rz = b.right
    rod("Cane", "Wood", f, (rx, ry, rz + 0.02 * s), (rx - 0.06 * s, ry - 0.02 * s, 0.02), 0.014 * s)
    part("sphere", "CaneKnob", "Metal", f, (rx, ry, rz + 0.04 * s), scale=(0.025 * s, 0.025 * s, 0.025 * s))
    part("torus", "Monocle", "Metal", f, (x + 0.045 * s, y - 0.11 * s, z + 0.01 * s), rotation=(math.pi / 2, 0, 0), major_radius=0.024 * s, minor_radius=0.004 * s)
    part("cube", "Cravat", "Accent", f, (0, -0.12 * s, 1.43 * s), rotation=(0.2, 0, 0), scale=(0.035 * s, 0.02 * s, 0.05 * s))
    return f


def contremaitre():
    """Le Contremaître: stocky, a lamp on his hard hat, a pickaxe over the shoulder."""
    f = root("Contremaitre")
    b = body(f, height=0.96, bulk=1.25, stance=0.14, hair=False,
             left=((0.32, -0.02, 1.2), (0.22, -0.06, 1.46)),
             right=((-0.34, -0.04, 1.18), (-0.34, -0.12, 0.98)))
    helmet(f, b, mat="Accent", lamp=True)
    lx, ly, lz = b.left
    s = b.s
    rod("PickHaft", "Wood", f, (lx - 0.05 * s, ly, lz - 0.1 * s), (lx + 0.12 * s, ly + 0.25 * s, lz + 0.35 * s), 0.02 * s)
    head = (lx + 0.12 * s, ly + 0.25 * s, lz + 0.35 * s)
    rod("PickHead", "Metal", f, (head[0] - 0.02 * s, head[1] - 0.2 * s, head[2] - 0.05 * s), (head[0] + 0.02 * s, head[1] + 0.2 * s, head[2] + 0.05 * s), 0.018 * s)
    for side in (1, -1):
        rod(f"Brace{side}", "Leather", f, (0.08 * side * s, -0.15 * s, 0.98 * s), (0.1 * side * s, -0.12 * s, 1.45 * s), 0.012 * s, vertices=6)
    belt(f, b)
    return f


def tireuse():
    """La Tireuse d'élite: a beret, and a long scoped rifle held across the body."""
    f = root("Tireuse")
    b = body(f, height=0.98, hips=1.1,
             left=((0.26, -0.18, 1.22), (0.1, -0.3, 1.3)),
             right=((-0.28, -0.1, 1.16), (-0.16, -0.22, 1.1)))
    x, y, z = b.head
    s = b.s
    part("cylinder", "Beret", "Accent", f, (x + 0.02 * s, y, z + 0.1 * s), rotation=(0, 0.25, 0), scale=(0.13 * s, 0.13 * s, 0.03 * s), vertices=28)
    part("sphere", "Braid", "Hair", f, (x, y + 0.1 * s, z - 0.08 * s), scale=(0.035 * s, 0.035 * s, 0.1 * s))
    stock, muzzle = (b.right[0] - 0.08 * s, b.right[1] + 0.12 * s, b.right[2] - 0.04 * s), (b.left[0] + 0.28 * s, b.left[1] - 0.2 * s, b.left[2] + 0.22 * s)
    rod("RifleStock", "Wood", f, stock, between(stock, muzzle, 0.45), 0.03 * s, vertices=8)
    rod("RifleBarrel", "Metal", f, between(stock, muzzle, 0.4), muzzle, 0.014 * s)
    scope = between(stock, muzzle, 0.42)
    rod("RifleScope", "Metal", f, (scope[0], scope[1], scope[2] + 0.05 * s), (scope[0] + 0.07 * s, scope[1] - 0.05 * s, scope[2] + 0.1 * s), 0.018 * s)
    belt(f, b)
    return f


def sapeur():
    """Le Sapeur: a round helmet with goggles, sticks of dynamite across the chest."""
    f = root("Sapeur")
    b = body(f, height=1.0, bulk=1.1, hair=False,
             left=((0.3, -0.06, 1.18), (0.26, -0.18, 1.02)),
             right=((-0.3, -0.04, 1.2), (-0.3, -0.14, 1.0)))
    helmet(f, b, mat="Metal")
    x, y, z = b.head
    s = b.s
    for side in (1, -1):
        part("cylinder", f"Goggle{side}", "Metal", f, (x + 0.045 * side * s, y - 0.1 * s, z + 0.03 * s), rotation=(math.pi / 2, 0, 0), scale=(0.03 * s, 0.03 * s, 0.015 * s), vertices=14)
    rod("Bandolier", "Leather", f, (0.16 * s, -0.13 * s, 0.98 * s), (-0.16 * s, -0.13 * s, 1.44 * s), 0.018 * s, vertices=6)
    for i in range(5):
        p = between((0.13 * s, -0.16 * s, 1.03 * s), (-0.13 * s, -0.16 * s, 1.39 * s), i / 4)
        part("cylinder", f"Dynamite{i}", "Accent", f, p, rotation=(0, 0.6, 0), scale=(0.018 * s, 0.018 * s, 0.05 * s), vertices=10)
    part("cube", "Charge", "Leather", f, (b.left[0], b.left[1] - 0.02 * s, b.left[2] - 0.08 * s), scale=(0.06 * s, 0.05 * s, 0.07 * s))
    belt(f, b)
    return f


def combattante():
    """La Combattante chevronnée: broad, a round shield on one arm, a sword raised in the other."""
    f = root("Combattante")
    b = body(f, height=1.03, bulk=1.2, hips=1.05, stance=0.15,
             left=((0.3, -0.14, 1.22), (0.24, -0.26, 1.2)),
             right=((-0.32, -0.02, 1.52), (-0.3, -0.08, 1.74)))
    x, y, z = b.head
    s = b.s
    part("sphere", "Ponytail", "Hair", f, (x, y + 0.13 * s, z - 0.04 * s), scale=(0.04 * s, 0.07 * s, 0.1 * s))
    lx, ly, lz = b.left
    part("cylinder", "Shield", "Wood", f, (lx + 0.02 * s, ly - 0.05 * s, lz), rotation=(math.pi / 2, 0, 0.2), scale=(0.2 * s, 0.2 * s, 0.02 * s), vertices=28)
    part("cylinder", "ShieldRim", "Metal", f, (lx + 0.02 * s, ly - 0.06 * s, lz), rotation=(math.pi / 2, 0, 0.2), scale=(0.21 * s, 0.21 * s, 0.012 * s), vertices=28)
    part("sphere", "ShieldBoss", "Metal", f, (lx + 0.02 * s, ly - 0.085 * s, lz), scale=(0.05 * s, 0.03 * s, 0.05 * s))
    blade(f, b.right, s, length=0.55, up=True, name="Sword", width=0.028)
    for side in (1, -1):
        part("sphere", f"Pauldron{side}", "Metal", f, (0.2 * side * b.bulk * s, 0, 1.45 * s), scale=(0.09 * s, 0.09 * s, 0.06 * s))
    belt(f, b)
    return f


def guerisseuse():
    """La Guérisseuse: a headscarf, an apron, a medic's bag with its red cross, bandages ready."""
    f = root("Guerisseuse")
    b = body(f, height=0.96, hips=1.12, legs="robe", hair=False,
             left=((0.28, -0.14, 1.24), (0.22, -0.3, 1.26)),
             right=((-0.3, -0.04, 1.2), (-0.3, -0.14, 1.0)))
    x, y, z = b.head
    s = b.s
    part("sphere", "Scarf", "Accent", f, (x, y + 0.02 * s, z + 0.04 * s), scale=(0.125 * s, 0.13 * s, 0.12 * s))
    part("cube", "Apron", "Bone", f, (0, -0.12 * s, 0.9 * s), scale=(0.13 * s, 0.01 * s, 0.22 * s))
    lx, ly, lz = b.left
    part("cylinder", "Bandage", "Bone", f, (lx, ly - 0.03 * s, lz), rotation=(0, math.pi / 2, 0), scale=(0.035 * s, 0.035 * s, 0.04 * s), vertices=14)
    satchel(f, b, side=-1, cross=True)
    belt(f, b)
    return f


def pretre():
    """Le Prêtre: a cassock to the ground, a stole, a book held up and a censer swinging."""
    f = root("Pretre")
    b = body(f, height=1.01, legs="robe", hair=False,
             left=((0.26, -0.14, 1.22), (0.14, -0.26, 1.28)),
             right=((-0.3, -0.06, 1.18), (-0.32, -0.14, 0.98)))
    s = b.s
    book(f, b.left, s)
    rx, ry, rz = b.right
    rod("CenserChain", "Metal", f, (rx, ry, rz - 0.02 * s), (rx, ry, rz - 0.3 * s), 0.005 * s, vertices=6)
    part("sphere", "Censer", "Metal", f, (rx, ry, rz - 0.35 * s), scale=(0.05 * s, 0.05 * s, 0.045 * s))
    part("sphere", "CenserEmber", "Ember", f, (rx, ry - 0.03 * s, rz - 0.34 * s), scale=(0.02 * s, 0.01 * s, 0.015 * s))
    for side in (1, -1):
        part("cube", f"Stole{side}", "Accent", f, (0.06 * side * s, -0.13 * s, 1.1 * s), scale=(0.025 * s, 0.01 * s, 0.35 * s))
    x, y, z = b.head
    part("cylinder", "SaturnoBrim", "Cloth", f, (x, y, z + 0.1 * s), scale=(0.24 * s, 0.24 * s, 0.012 * s), vertices=32)
    part("sphere", "SaturnoCrown", "Cloth", f, (x, y, z + 0.11 * s), scale=(0.11 * s, 0.11 * s, 0.07 * s))
    return f


def explorer_sketch():
    """The stand-in for any Explorer whose own figure is not made yet."""
    f = root("Explorer")
    b = body(f, hair=False, left=((0.3, -0.04, 1.2), (0.3, -0.16, 1.02)), right=((-0.3, -0.12, 1.6), (-0.26, -0.2, 1.84)))
    brimmed_hat(f, b)
    torch(f, b.right, b.s)
    pack(f, b)
    belt(f, b)
    satchel(f, b, side=1)
    return f


def guardian():
    """One of the Ashen Legion: taller than a man, horned, cased in cooled lava."""
    f = root("Guardian")
    legs, radii = [], {}
    for side in (1, -1):
        hip, knee, ankle = (0.14 * side, 0, 1.05), (0.17 * side, -0.06, 0.58), (0.16 * side, 0, 0.12)
        legs += [(hip, knee), (knee, ankle)]
        radii.update({hip: 0.12, knee: 0.1, ankle: 0.1})
    pelvis = (0, 0, 1.1)
    legs += [(pelvis, (0.14, 0, 1.05)), (pelvis, (-0.14, 0, 1.05))]
    radii[pelvis] = (0.2, 0.14)
    skinned("Legs", legs, radii, "Ash", f)

    chest, neck = (0, 0.02, 1.6), (0, 0.04, 1.85)
    skinned("Torso", [((0, 0, 1.08), chest), (chest, neck)], {(0, 0, 1.08): (0.2, 0.14), chest: (0.3, 0.19), neck: 0.09}, "Ash", f)

    arms, radii = [], {}
    left = [(0.3, 0.02, 1.76), (0.44, -0.06, 1.44), (0.42, -0.2, 1.2)]
    right = [(-0.3, 0.02, 1.76), (-0.42, 0.02, 1.42), (-0.4, -0.02, 1.1)]
    for chain in (left, right):
        arms += [(chain[0], chain[1]), (chain[1], chain[2])]
        radii.update({chain[0]: 0.1, chain[1]: 0.085, chain[2]: 0.08})
    arms += [((0, 0.04, 1.78), left[0]), ((0, 0.04, 1.78), right[0])]
    radii[(0, 0.04, 1.78)] = 0.12
    skinned("Arms", arms, radii, "Ash", f)

    for side in (1, -1):
        part("sphere", f"Pauldron{side}", "Armor", f, (0.33 * side, 0.02, 1.84), scale=(0.16, 0.15, 0.1), segments=16, ring_count=8)
        part("cube", f"Greave{side}", "Armor", f, (0.17 * side, -0.1, 0.4), scale=(0.07, 0.03, 0.18))
        part("cone", f"Horn{side}", "Bone", f, (0.1 * side, 0.02, 2.2), rotation=(0.35, 0.5 * side, 0), scale=(0.035, 0.035, 0.14), vertices=12, radius1=1, radius2=0, depth=2)
        part("sphere", f"Eye{side}", "Ember", f, (0.05 * side, -0.125, 2.03), scale=(0.022, 0.012, 0.012), segments=10, ring_count=6)
    part("cube", "Breastplate", "Armor", f, (0, -0.12, 1.55), scale=(0.2, 0.05, 0.2))
    part("sphere", "Head", "Ash", f, (0, 0.0, 2.02), scale=(0.13, 0.14, 0.15), segments=20, ring_count=10)
    part("cylinder", "SpearShaft", "Wood", f, (0.42, -0.2, 1.3), scale=(0.022, 0.022, 1.0), vertices=10)
    part("cone", "SpearBlade", "Metal", f, (0.42, -0.2, 2.42), scale=(0.05, 0.015, 0.14), vertices=4, radius1=1, radius2=0, depth=2)
    part("torus", "Belt", "Ember", f, (0, 0, 1.12), scale=(1, 0.72, 1), major_radius=0.2, minor_radius=0.02)
    return f


CAST = {
    "archeologue": archeologue,
    "guide": guide,
    "gredin": gredin,
    "aristocrate": aristocrate,
    "contremaitre": contremaitre,
    "tireuse": tireuse,
    "sapeur": sapeur,
    "combattante": combattante,
    "guerisseuse": guerisseuse,
    "pretre": pretre,
    "guardian": guardian,
    "explorer_sketch": explorer_sketch,
}


def export(figure, path):
    bpy.ops.object.select_all(action="DESELECT")
    figure.select_set(True)
    for child in figure.children_recursive:
        child.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_apply=True)


def triangles(figure):
    count = 0
    for obj in figure.children_recursive:
        if obj.type == "MESH":
            count += sum(len(p.vertices) - 2 for p in obj.data.polygons)
    return count


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", default="resources/models/figures")
    parser.add_argument("--only", nargs="*", default=None)
    args = parser.parse_args(argv)
    os.makedirs(args.out, exist_ok=True)

    for name, build in CAST.items():
        if args.only and name not in args.only:
            continue
        reset()
        figure = build()
        export(figure, os.path.join(args.out, f"{name}.glb"))
        print(f"wrote {name}.glb — {triangles(figure)} triangles")


if __name__ == "__main__":
    main()
