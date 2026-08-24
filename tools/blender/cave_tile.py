"""Builds one tile's cave: the shape of the gallery, and nothing else.

In Blender, to work on it:

    exec(open("tools/blender/cave_tile.py").read())
    build("Junction")

Headless, to write the mesh Godot loads:

    blender --background --python tools/blender/cave_tile.py -- --shape Junction

Tiles used to be assembled from boxes, which reads as a brick corridor rather than
a tunnel dug through a mountain. Here the rock is one solid block with the passages
cut out of it, and what comes out is only the gallery's shape: an arched section,
walls leaning in towards a pointed vault, swelling and narrowing along its length.
The rock itself — plates, joints, grain — is the shader's business, not the mesh's
(resources/shaders/rock.gdshader). Geometry that tried to carry it ran to 25 000
triangles a tile and still read as debris.

Two things have to hold, or tiles stop sealing against each other on the table:

  * every passage is cut with the same arch profile, so any two openings that meet
    are the same hole, whatever the tiles are and however they were turned;
  * nothing changes on the four boundary planes of a tile — the roughening fades
    out as it nears an open mouth, so a passage opening is the bare arch on both
    sides of a seam.

Axes: Blender is Z-up, Godot is Y-up. Everything below is in Blender space with
+Y standing in for the tile's North, and the OBJ export does the conversion.
"""

import argparse
import math
import os
import sys

import bpy
import bmesh
from mathutils import Vector, noise

# Geometry of a tile, from docs/tuiles.md: 3 m square, 1.5 m corridor, 2.25 m of
# headroom under the vault — a gallery an adult walks down, not a crawlway. The
# block stands taller than the vault by more than anything here can lift it, so its
# top stays a flat sealed plane: that is what the overview camera looks down on,
# and what the next tile butts against.
TILE_SIZE = 3.0
HALF_TILE = TILE_SIZE / 2.0
CORRIDOR_HALF = 0.75
ARCH_APEX = 2.25
ROCK_TOP = 2.7

# How far in from the tile's edge a passage is cut: past it, so the opening reaches
# the boundary plane cleanly; and past the centre, so passages meet in one vault.
OVERSHOOT = 0.075
CENTRE_HALF = 0.75

# The arch: walls bellying out from the floor to the springing, then an ogive
# closing overhead. Wider than the 1 m the rules walk down — a passage hewn out of
# rock is roomier at the shoulder than at the feet.
CORRIDOR_SHOULDER = 0.99
SPRINGING = 0.825
WALL_SEGMENTS = 3
ARCH_SEGMENTS = 10
OGIVE_POWER = 2.3

# The cut: a swell so a passage isn't a length of pipe, a wander over it so no
# line in the tile stays straight — the groin where two vaults meet and the foot of
# a wall are the two that give a carved gallery away — and a grain on top. The mesh
# only has to bend convincingly; the rock's surface comes from the shader.
TARGET_EDGE = 0.19
SWELL_SCALE = 0.5
SWELL_DEPTH = 0.18
WANDER_SCALE = 1.35
WANDER_DEPTH = 0.08
GRAIN_SCALE = 3.0
GRAIN_DEPTH = 0.038
INTO_ROCK = 0.075
MAX_SHIFT = 0.24

# How far from a tile edge, the floor and the top of the block the roughening of
# the cut is held back to nothing.
EDGE_CALM = 0.42
FLOOR_CALM = 0.05
CEILING_CALM = 0.18

# Blender axis and direction each of the tile's sides is cut towards. North is +Y
# here and becomes -Z on export, which is where Godot's North points.
SIDES = {
    "North": ("y", 1.0),
    "East": ("x", 1.0),
    "South": ("y", -1.0),
    "West": ("x", -1.0),
}

# Mirrors TileShape.OpenSides in the rules engine.
SHAPES = {
    "Crossroads": ["North", "East", "South", "West"],
    "Junction": ["North", "East", "South"],
    "Corridor": ["North", "South"],
    "Corner": ["North", "East"],
    "DeadEnd": ["North"],
}

CAVE_NAME = "Cave"


def arch_profile():
    """The passage's cross-section, as (across, up) points from one foot to the
    other. Shared by every passage of every tile: this curve is the seam."""
    side = []

    for i in range(WALL_SEGMENTS + 1):
        t = i / WALL_SEGMENTS
        side.append((
            -CORRIDOR_HALF - (CORRIDOR_SHOULDER - CORRIDOR_HALF) * math.sqrt(t),
            SPRINGING * t,
        ))

    for i in range(1, ARCH_SEGMENTS + 1):
        t = i / ARCH_SEGMENTS
        side.append((
            -CORRIDOR_SHOULDER * (1.0 - t ** OGIVE_POWER),
            SPRINGING + (ARCH_APEX - SPRINGING) * t,
        ))

    return side + [(-across, up) for across, up in reversed(side[:-1])]


def make_block():
    """The tile's rock, before anything is cut out of it."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector((TILE_SIZE, TILE_SIZE, ROCK_TOP)), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector((0.0, 0.0, ROCK_TOP / 2.0)), verts=bm.verts)

    mesh = bpy.data.meshes.new(CAVE_NAME)
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(CAVE_NAME, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def make_passage(side):
    """A solid the shape of one passage, to be subtracted from the block. It runs
    from beyond the tile's edge to the far side of the centre — where passages
    overlap they carve a single vault — and dips below the floor so the cut leaves
    the block's own underside as the corridor floor."""
    axis, sign = SIDES[side]
    profile = arch_profile()
    near = -CENTRE_HALF * sign
    far = (HALF_TILE + OVERSHOOT) * sign

    section = [(-CORRIDOR_HALF, -0.3)] + profile + [(CORRIDOR_HALF, -0.3)]
    count = len(section)

    def place(across, up, along):
        return (along, across, up) if axis == "x" else (across, along, up)

    verts = [place(across, up, near) for across, up in section]
    verts += [place(across, up, far) for across, up in section]

    faces = [(i, (i + 1) % count, count + (i + 1) % count, count + i) for i in range(count)]
    faces.append(tuple(range(count)))
    faces.append(tuple(reversed(range(count, 2 * count))))

    mesh = bpy.data.meshes.new(f"Passage_{side}")
    mesh.from_pydata(verts, [], faces)

    obj = bpy.data.objects.new(f"Passage_{side}", mesh)
    bpy.context.collection.objects.link(obj)

    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(mesh)
    bm.free()

    return obj


def subtract(block, cutter):
    modifier = block.modifiers.new(name=cutter.name, type="BOOLEAN")
    modifier.operation = "DIFFERENCE"
    modifier.solver = "EXACT"
    modifier.object = cutter

    bpy.context.view_layer.objects.active = block
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.data.objects.remove(cutter, do_unlink=True)


def on_the_outside(face):
    """True for the six flat sides of the block itself — the tile's boundary
    planes, its top and its floor. Nobody ever sees them from inside."""
    centre = face.calc_center_median()

    return (
        abs(abs(centre.x) - HALF_TILE) < 1e-4
        or abs(abs(centre.y) - HALF_TILE) < 1e-4
        or centre.z < 1e-4
        or abs(centre.z - ROCK_TOP) < 1e-4
    )


def tessellate(bm):
    """Splits the faces the cut leaves behind into pieces small enough to take the
    roughening and to be shared out into blocks — a wall has to have vertices
    before it can have shape. Only what shows: the outside of the block stays the
    handful of big flat faces it already is."""
    bmesh.ops.triangulate(bm, faces=bm.faces[:])

    for _ in range(7):
        long_edges = [
            edge for edge in bm.edges
            if edge.calc_length() > TARGET_EDGE
            and any(not on_the_outside(face) for face in edge.link_faces)
        ]

        if not long_edges:
            break

        bmesh.ops.subdivide_edges(bm, edges=long_edges, cuts=1, use_grid_fill=True)

    bmesh.ops.triangulate(bm, faces=bm.faces[:])


def ramp(value, width):
    t = min(max(value / width, 0.0), 1.0)
    return t * t * (3.0 - 2.0 * t)


def on_a_plane(co):
    """True on one of the block's six flat sides. Those are the tile's seams with
    its neighbours: nothing there may move, ever."""
    return (
        abs(abs(co.x) - HALF_TILE) < 1e-4
        or abs(abs(co.y) - HALF_TILE) < 1e-4
        or co.z < 1e-4
        or co.z > ROCK_TOP - 1e-4
    )


def to_mouth(co, sides):
    """Distance to the nearest passage mouth. Only open sides count: the wall of a
    passage runs close to the tile's edge along its whole length, and holding it
    flat for that reason alone is what kept these galleries looking like pipes."""
    return min(
        HALF_TILE - sign * (co.x if axis == "x" else co.y)
        for axis, sign in (SIDES[side] for side in sides)
    )


def calm(co, sides):
    """How freely a vertex may move: freely deep inside the tile, not at all on a
    seam, and easing off as it nears a mouth, the floor, or the top of the block."""
    if on_a_plane(co):
        return 0.0

    return (
        ramp(to_mouth(co, sides), EDGE_CALM)
        * ramp(co.z, FLOOR_CALM)
        * ramp(ROCK_TOP - co.z, CEILING_CALM)
    )


def sample(point, scale, squash=1.0):
    return noise.noise(Vector((point.x * scale, point.y * scale, point.z * scale * squash)))


def roughen(bm, offset, sides):
    bm.normal_update()

    for vert in bm.verts:
        freedom = calm(vert.co, sides)

        if freedom <= 0.0:
            continue

        point = vert.co + offset
        swell = sample(point, SWELL_SCALE) * SWELL_DEPTH
        wander = sample(point, WANDER_SCALE, 0.7) * WANDER_DEPTH
        grain = sample(point, GRAIN_SCALE, 0.6) * GRAIN_DEPTH
        shift = min(max(swell + wander + grain - INTO_ROCK, -MAX_SHIFT), MAX_SHIFT)

        vert.co += vert.normal * (shift * freedom)


def clear():
    if CAVE_NAME in bpy.data.objects:
        bpy.data.objects.remove(bpy.data.objects[CAVE_NAME], do_unlink=True)

    for leftover in list(bpy.data.objects):
        if leftover.name.startswith("Passage_"):
            bpy.data.objects.remove(leftover, do_unlink=True)


def build(shape="Junction"):
    """Cuts one tile's gallery and leaves it in the scene as a single object."""
    clear()

    sides = SHAPES[shape]
    cave = make_block()

    for side in sides:
        subtract(cave, make_passage(side))

    bm = bmesh.new()
    bm.from_mesh(cave.data)
    tessellate(bm)
    # Each shape reads a different corner of the noise field, so a T and a
    # crossroads aren't cut out of the same rock.
    roughen(bm, Vector((sorted(SHAPES).index(shape) * 7.3, 0.0, 0.0)), sides)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(cave.data)
    bm.free()

    # Smooth: the shader bends the surface normal itself, and hard facets under it
    # would show through as a second, contradicting relief.
    cave.select_set(True)
    bpy.context.view_layer.objects.active = cave
    bpy.ops.object.shade_smooth()

    print(f"{shape}: {len(cave.data.polygons)} faces")
    return cave


def export(cave, path):
    for other in bpy.context.scene.objects:
        other.select_set(other is cave)

    bpy.context.view_layer.objects.active = cave

    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.wm.obj_export(
        filepath=path,
        export_selected_objects=True,
        forward_axis="NEGATIVE_Z",
        up_axis="Y",
        export_normals=True,
        export_uv=False,
        export_materials=False,
        export_triangulated_mesh=True,
    )

    print(f"-> {path}")


def parse_args(argv):
    tail = argv[argv.index("--") + 1:] if "--" in argv else []

    parser = argparse.ArgumentParser(prog="cave_tile.py")
    parser.add_argument("--shape", default="Junction", choices=sorted(SHAPES))
    parser.add_argument("--out", default=None)

    return parser.parse_args(tail)


def main():
    args = parse_args(sys.argv)
    out = args.out or f"resources/models/cave_{args.shape.lower()}.obj"

    # The startup file's cube, camera and lamp would end up in the export.
    for leftover in list(bpy.data.objects):
        bpy.data.objects.remove(leftover, do_unlink=True)

    export(build(args.shape), os.path.abspath(out))


if __name__ == "__main__":
    main()
