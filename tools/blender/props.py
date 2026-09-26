"""Builds the props: the pieces on the board and what dresses the tiles.

Headless:

    blender --background --python tools/blender/props.py -- --out resources/models/props
    blender --background --python tools/blender/props.py -- --only bridge altar

Three kinds of output, for three uses:

  * pieces (key, artefact) — .glb whose materials are named for what they are
    (Gold, Ember…) and painted by the game like the figures, B+ included;
  * tile dressing (bridge, altar, dart heads…) — .glb with their colours set here,
    in the tone of the rock rather than of the painted pieces;
  * loose meshes (rock chunks, a spike) — .obj, for tile scenes whose nodes swap
    their mesh in place and are animated by name (Debris_*, Spike_*).

Axes and origin follow the tiles (tools/blender/cave_tile.py): Blender Z-up, +Y is
the tile's North, the tile's centre at the origin, the floor at Z = 0. A gallery is
1.5 m wide (CORRIDOR_HALF = 0.75); walls wander up to ~0.2 m inwards, so what hangs
on a wall is sunk into it rather than laid against it.
"""

import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import figure as kit  # noqa: E402  — the same workshop as the cast


# ── Colours of the tile dressing ─────────────────────────────────────────────

TINTS = {
    "Stone": ((0.3, 0.26, 0.23), 0.0, 0.9, None),
    "DarkStone": ((0.12, 0.1, 0.09), 0.0, 0.95, None),
    "Wood": ((0.34, 0.21, 0.11), 0.0, 0.85, None),
    "Rope": ((0.58, 0.47, 0.3), 0.0, 1.0, None),
    "Iron": ((0.3, 0.29, 0.28), 0.8, 0.5, None),
    "Rust": ((0.36, 0.2, 0.12), 0.5, 0.75, None),
    "Gold": ((0.95, 0.72, 0.3), 1.0, 0.3, None),
    "GoldGlow": ((1.0, 0.78, 0.35), 0.0, 0.5, ((1.0, 0.7, 0.25), 3.0)),
    "Rune": ((0.55, 0.22, 0.95), 0.0, 0.6, ((0.55, 0.2, 0.95), 0.8)),
    "Lava": ((1.0, 0.35, 0.05), 0.0, 1.0, ((1.0, 0.35, 0.05), 6.0)),
    "Daylight": ((0.85, 0.9, 1.0), 0.0, 1.0, ((0.85, 0.9, 1.0), 4.0)),
    "Canvas": ((0.62, 0.55, 0.4), 0.0, 1.0, None),
}


DUSK = 0.4


def tint():
    """Gives every dressing material its colour, now the shapes are built."""
    for mat in bpy.data.materials:
        if mat.name not in TINTS:
            continue
        colour, metal, rough, glow = TINTS[mat.name]
        # Written as they would read in daylight, then brought down to the rock's own
        # value: the temple's stone is nearly black, and a prop a torch's length from
        # its flame would otherwise burn white next to it.
        # What glows keeps its colour in the emission, which the torch cannot bleach.
        colour = tuple(c * DUSK for c in colour)
        shader = mat.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = (*colour, 1.0)
        shader.inputs["Metallic"].default_value = metal
        shader.inputs["Roughness"].default_value = rough
        if glow:
            shader.inputs["Emission Color"].default_value = (*glow[0], 1.0)
            shader.inputs["Emission Strength"].default_value = glow[1]


part, rod, root = kit.part, kit.rod, kit.root


def rock(name, mat, parent, location, size, seed, flat=True):
    """An irregular chunk of rock: a lumpy sphere, its underside cut flat to sit."""
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1.0, location=(0, 0, 0))
    obj = bpy.context.active_object
    obj.name = name
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    rng = random.Random(seed)
    stretch = Vector((rng.uniform(0.8, 1.25), rng.uniform(0.8, 1.25), rng.uniform(0.55, 0.85)))
    offset = Vector((rng.uniform(0, 50), rng.uniform(0, 50), rng.uniform(0, 50)))
    for vert in mesh.verts:
        bump = 1.0 + 0.28 * noise.noise(vert.co * 1.7 + offset)
        vert.co = Vector((vert.co.x * stretch.x, vert.co.y * stretch.y, vert.co.z * stretch.z)) * bump * size
        if flat and vert.co.z < -0.25 * size:
            vert.co.z = -0.25 * size
    mesh.to_mesh(obj.data)
    mesh.free()
    for poly in obj.data.polygons:
        poly.use_smooth = False
    obj.location = (location[0], location[1], location[2] + 0.25 * size)
    obj.data.materials.append(kit.material(mat))
    obj.parent = parent
    return obj


# ── Pieces ───────────────────────────────────────────────────────────────────

def key():
    """A great old key, standing on its bit: bow, shank and wards."""
    f = root("Key")
    part("torus", "Bow", "Gold", f, (0, 0, 0.42), rotation=(math.pi / 2, 0, 0), major_radius=0.09, minor_radius=0.022)
    part("sphere", "BowGem", "Ember", f, (0, 0, 0.42), scale=(0.03, 0.02, 0.03))
    rod("Shank", "Gold", f, (0, 0, 0.33), (0, 0, 0.04), 0.02)
    part("torus", "Collar", "Gold", f, (0, 0, 0.32), major_radius=0.028, minor_radius=0.01)
    part("cube", "Bit", "Gold", f, (0.045, 0, 0.09), scale=(0.045, 0.012, 0.05))
    part("cube", "Ward", "Gold", f, (0.075, 0, 0.14), scale=(0.015, 0.012, 0.025))
    return f


def artefact():
    """The Artefact: a golden idol, hands folded, eyes still burning."""
    f = root("Artefact")
    kit.skinned("Idol", [((0, 0, 0.02), (0, 0, 0.22)), ((0, 0, 0.22), (0, 0, 0.32))],
                {(0, 0, 0.02): (0.1, 0.08), (0, 0, 0.22): (0.08, 0.065), (0, 0, 0.32): 0.04}, "Gold", f)
    part("sphere", "Head", "Gold", f, (0, 0, 0.4), scale=(0.075, 0.07, 0.085))
    part("cone", "Crown", "Gold", f, (0, 0, 0.5), scale=(0.07, 0.07, 0.05), radius1=1, radius2=0.6, depth=2, vertices=8)
    for side in (1, -1):
        part("sphere", f"Eye{side}", "Ember", f, (0.028 * side, -0.065, 0.41), scale=(0.016, 0.01, 0.01))
    part("torus", "Hands", "Gold", f, (0, -0.06, 0.2), rotation=(math.pi / 2, 0, 0), scale=(1, 1, 0.6), major_radius=0.045, minor_radius=0.018)
    return f


# ── Tile dressing ────────────────────────────────────────────────────────────

def bridge():
    """The Bridge: the floor falls away into a glowing chasm, crossed on planks and rope."""
    f = root("Bridge")
    # Ledges at either end, where the rock still holds.
    for end in (1, -1):
        part("cube", f"Ledge{end}", "Stone", f, (0, 1.2 * end, -0.11), scale=(0.75, 0.3, 0.11), bevel=False)
    # The chasm: its walls, and far below, the heat of the mountain.
    for side in (1, -1):
        part("cube", f"ChasmWall{side}", "DarkStone", f, (0.8 * side, 0, -1.2), scale=(0.05, 0.9, 1.2), bevel=False)
    part("cube", "ChasmFloor", "Lava", f, (0, 0, -2.3), scale=(0.8, 0.9, 0.05), bevel=False)
    # Planks, each a little askew, on two stringers.
    for side in (1, -1):
        rod(f"Stringer{side}", "Wood", f, (0.42 * side, -1.05, -0.04), (0.42 * side, 1.05, -0.04), 0.03)
    rng = random.Random(7)
    for i in range(12):
        y = -0.99 + i * 0.18
        part("cube", f"Plank{i}", "Wood", f, (rng.uniform(-0.02, 0.02), y, -0.005), rotation=(0, 0, rng.uniform(-0.05, 0.05)),
             scale=(0.5, 0.075, 0.02))
    # Posts and hand ropes, sagging between them.
    for side in (1, -1):
        for end in (1, -1):
            rod(f"Post{side}{end}", "Wood", f, (0.5 * side, 1.02 * end, -0.1), (0.5 * side, 1.02 * end, 0.95), 0.035)
        for rail, height in (("Top", 0.9), ("Low", 0.5)):
            points = [(0.5 * side, -1.02 + t * 2.04 / 6, height - 0.12 * math.sin(math.pi * t / 6)) for t in range(7)]
            for i in range(6):
                rod(f"Rope{rail}{side}{i}", "Rope", f, points[i], points[i + 1], 0.012, vertices=6)
    return f


def dart_heads():
    """The Dart Trap: two carved faces in the walls of the bend, mouths bored through."""
    f = root("DartHeads")
    # On the west wall of the north arm (facing +X), and the south wall of the east arm (facing +Y).
    for name, at, facing in (("West", (-0.72, 0.45, 1.05), 0.0), ("South", (0.45, -0.72, 1.05), math.pi / 2)):
        head = root(f"Head{name}")
        head.parent = f
        part("sphere", "Face", "Stone", head, (0, 0, 0), scale=(0.08, 0.22, 0.24))
        part("cube", "Brow", "Stone", head, (0.06, 0, 0.1), scale=(0.05, 0.2, 0.035))
        for side in (1, -1):
            part("sphere", f"Eye{side}", "DarkStone", head, (0.075, 0.08 * side, 0.06), scale=(0.02, 0.03, 0.02))
        part("cylinder", "Mouth", "DarkStone", head, (0.07, 0, -0.08), rotation=(0, math.pi / 2, 0), scale=(0.05, 0.05, 0.03), vertices=12)
        for i in range(3):
            rod(f"Dart{i}", "Rust", head, (0.06, -0.025 + 0.025 * i, -0.08), (0.12, -0.025 + 0.025 * i, -0.08), 0.005, vertices=4)
        head.location = at
        head.rotation_euler = (0, 0, facing)
    return f


def pressure_plate():
    """The plate in the floor that sets the darts off."""
    f = root("PressurePlate")
    part("cube", "Plate", "Stone", f, (0, 0, 0.012), scale=(0.3, 0.3, 0.012))
    part("cube", "Seam", "DarkStone", f, (0, 0, 0.004), scale=(0.33, 0.33, 0.004), bevel=False)
    for i, (x, y) in enumerate(((0.15, 0.15), (-0.15, 0.15), (0.15, -0.15), (-0.15, -0.15))):
        part("cylinder", f"Hole{i}", "DarkStone", f, (x, y, 0.025), scale=(0.03, 0.03, 0.002), vertices=10)
    return f


def spike_grate():
    """The iron grate the spikes of a Spike Trap come up through."""
    f = root("SpikeGrate")
    part("cube", "Frame", "Iron", f, (0, 0, 0.01), scale=(0.72, 0.72, 0.01), bevel=False)
    for i in range(7):
        t = -0.6 + i * 0.2
        rod(f"BarX{i}", "Rust", f, (-0.7, t, 0.025), (0.7, t, 0.025), 0.012, vertices=6)
        rod(f"BarY{i}", "Rust", f, (t, -0.7, 0.03), (t, 0.7, 0.03), 0.012, vertices=6)
    return f


def rune_circle():
    """A Guardian's pocket: a circle of runes set into the floor, burning violet."""
    f = root("RuneCircle")
    part("cylinder", "Slab", "DarkStone", f, (0, 0, 0.01), scale=(0.62, 0.62, 0.01), vertices=40)
    part("torus", "Outer", "Rune", f, (0, 0, 0.022), scale=(1, 1, 0.3), major_radius=0.55, minor_radius=0.02, major_segments=48)
    part("torus", "Inner", "Rune", f, (0, 0, 0.022), scale=(1, 1, 0.3), major_radius=0.3, minor_radius=0.015, major_segments=40)
    for i in range(8):
        a = i * math.tau / 8
        part("cube", f"Rune{i}", "Rune", f, (0.43 * math.cos(a), 0.43 * math.sin(a), 0.022), rotation=(0, 0, a),
             scale=(0.02, 0.06 if i % 2 else 0.035, 0.006), bevel=False)
    for i in range(3):
        a = i * math.tau / 3 + 0.3
        rod(f"Spoke{i}", "Rune", f, (0.3 * math.cos(a), 0.3 * math.sin(a), 0.022), (0.55 * math.cos(a), 0.55 * math.sin(a), 0.022), 0.008, vertices=4)
    return f


def key_niche():
    """The Key tile: a keyhole carved into the back wall of the dead end, lit gold."""
    f = root("KeyNiche")
    part("cube", "Frame", "Stone", f, (0, -0.72, 1.0), scale=(0.28, 0.1, 0.42))
    part("cylinder", "HoleTop", "GoldGlow", f, (0, -0.64, 1.1), rotation=(math.pi / 2, 0, 0), scale=(0.08, 0.08, 0.03), vertices=20)
    part("cone", "HoleFoot", "GoldGlow", f, (0, -0.64, 0.93), rotation=(math.pi / 2, 0, 0), scale=(0.075, 0.13, 0.03), radius1=1, radius2=0.35, depth=2, vertices=4)
    part("cube", "Step", "Stone", f, (0, -0.6, 0.08), scale=(0.35, 0.12, 0.08))
    return f


def key_pillar():
    """One of the Sanctuary's three locks: a short pillar with a keyhole on its face."""
    f = root("KeyPillar")
    part("cylinder", "Shaft", "Stone", f, (0, 0, 0.45), scale=(0.13, 0.13, 0.45), vertices=12)
    part("cube", "Cap", "Stone", f, (0, 0, 0.93), scale=(0.17, 0.17, 0.04))
    part("cube", "Foot", "Stone", f, (0, 0, 0.04), scale=(0.18, 0.18, 0.04))
    part("cylinder", "Lock", "Gold", f, (0, -0.125, 0.68), rotation=(math.pi / 2, 0, 0), scale=(0.05, 0.05, 0.015), vertices=16)
    part("cube", "Slot", "DarkStone", f, (0, -0.14, 0.66), scale=(0.012, 0.005, 0.03), bevel=False)
    return f


def altar():
    """The Sanctuary's vault: a stepped altar the Artefact rests on."""
    f = root("Altar")
    part("cube", "StepLow", "Stone", f, (0, 0, 0.06), scale=(0.6, 0.5, 0.06))
    part("cube", "StepHigh", "Stone", f, (0, 0, 0.18), scale=(0.48, 0.38, 0.06))
    part("cube", "Block", "Stone", f, (0, 0, 0.42), scale=(0.34, 0.26, 0.18))
    part("cube", "Top", "Stone", f, (0, 0, 0.63), scale=(0.4, 0.32, 0.03))
    for side in (1, -1):
        part("cube", f"Inlay{side}", "GoldGlow", f, (0.345 * side, 0, 0.42), scale=(0.004, 0.18, 0.1), bevel=False)
    part("cube", "InlayFront", "GoldGlow", f, (0, -0.265, 0.42), scale=(0.24, 0.004, 0.1), bevel=False)
    return f


def entrance_stairs():
    """The way out: from the gallery's mouth, steps climb to the back of the dead end."""
    f = root("EntranceStairs")
    # Eight low steps, the last just under the vaults the view from above slices off.
    for i in range(8):
        part("cube", f"Step{i}", "Stone", f, (0, 1.2 - i * 0.24, 0.07 * (i + 1)), scale=(0.66, 0.12, 0.07 * (i + 1)))
    # The daylight itself is the tile scene's lamp: anything drawn up there would float
    # above the vaults the view from above slices off.
    return f


def camp():
    """Where the expedition starts: crates, a rolled tent, a rope — the last of the outside."""
    f = root("Camp")
    part("cube", "Crate", "Wood", f, (0.5, 0.52, 0.18), rotation=(0, 0, 0.2), scale=(0.18, 0.15, 0.18))
    part("cube", "CrateSmall", "Wood", f, (0.52, 0.5, 0.45), rotation=(0, 0, -0.3), scale=(0.11, 0.1, 0.09))
    part("cylinder", "Tent", "Canvas", f, (-0.5, 0.5, 0.1), rotation=(0, math.pi / 2, 0.4), scale=(0.1, 0.1, 0.3), vertices=12)
    part("torus", "Coil", "Rope", f, (-0.52, -0.5, 0.03), scale=(1, 1, 0.5), major_radius=0.13, minor_radius=0.03)
    rod("Pick", "Wood", f, (0.55, -0.45, 0.02), (0.35, -0.6, 0.02), 0.018)
    return f


# ── Loose meshes ─────────────────────────────────────────────────────────────

def export_obj(obj, path):
    for other in bpy.context.scene.objects:
        other.select_set(other is obj)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.wm.obj_export(filepath=path, export_selected_objects=True, forward_axis="NEGATIVE_Z", up_axis="Y",
                          export_normals=True, export_uv=False, export_materials=False, export_triangulated_mesh=True)


def loose(out):
    for i, size in enumerate((0.3, 0.25, 0.2)):
        kit.reset()
        chunk = rock(f"RockChunk{i}", "Stone", None, (0, 0, 0), size, seed=11 + i)
        chunk.location = (0, 0, 0)
        export_obj(chunk, os.path.join(out, f"rock_chunk_{i}.obj"))
        print(f"wrote rock_chunk_{i}.obj")

    # A spike: four-sided and leaf-bladed rather than a round cone, origin at its base.
    kit.reset()
    bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=0.055, radius2=0.0, depth=0.42, location=(0, 0, 0.21))
    spike = bpy.context.active_object
    spike.name = "Spike"
    spike.scale = (1.0, 0.45, 1.0)
    bpy.ops.object.transform_apply(scale=True)
    export_obj(spike, os.path.join(out, "spike.obj"))
    print("wrote spike.obj")


GLB = {
    "key": key,
    "artefact": artefact,
    "bridge": bridge,
    "dart_heads": dart_heads,
    "pressure_plate": pressure_plate,
    "spike_grate": spike_grate,
    "rune_circle": rune_circle,
    "key_niche": key_niche,
    "key_pillar": key_pillar,
    "altar": altar,
    "entrance_stairs": entrance_stairs,
    "camp": camp,
}


def main():
    import argparse
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", default="resources/models/props")
    parser.add_argument("--only", nargs="*", default=None)
    args = parser.parse_args(argv)
    os.makedirs(args.out, exist_ok=True)

    for name, build in GLB.items():
        if args.only and name not in args.only:
            continue
        kit.reset()
        prop = build()
        tint()
        kit.export(prop, os.path.join(args.out, f"{name}.glb"))
        print(f"wrote {name}.glb — {kit.triangles(prop)} triangles")

    if not args.only or "loose" in args.only:
        loose(args.out)


if __name__ == "__main__":
    main()
