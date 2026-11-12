"""Render preview PNGs: blender -b -P render_previews.py [-- assembled]
Per-asset 3/4 view into previews/<name>.png, plus previews/_assembled.png laying every asset out like the reference sheet.
"""
import sys, os, glob, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector
from sd_lib import SRC, PREVIEWS, mat

os.makedirs(PREVIEWS, exist_ok=True)


def setup_render(res=(960, 720)):
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT", "BLENDER_WORKBENCH"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.image_settings.file_format = "PNG"
    sc.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    bg = w.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.55, 0.57, 0.62, 1)
    bg.inputs["Strength"].default_value = 1.0
    sc.world = w
    return sc


def add_lights(center, size):
    for loc, e, col in (((4, -5, 7), 1.7, (1, 0.97, 0.92)), ((-6, -3, 4), 0.6, (0.8, 0.9, 1.0)), ((0, 6, 5), 0.5, (1, 1, 1))):
        ld = bpy.data.lights.new("L", "SUN")
        ld.energy = e
        ld.color = col
        lo = bpy.data.objects.new("L", ld)
        bpy.context.collection.objects.link(lo)
        lo.location = Vector(center) + Vector(loc) * size / 6
        lo.rotation_euler = (center - lo.location).to_track_quat("-Z", "Y").to_euler()
    # floor
    bpy.ops.mesh.primitive_plane_add(size=size * 6, location=(center[0], center[1], 0))
    fl = bpy.context.active_object
    fl.data.materials.append(mat("SD_Pad"))


def frame(objs, az=40, el=24, pad=1.2):
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    c = (mn + mx) / 2
    size = max((mx - mn).length, 1.0)
    cam = bpy.data.cameras.new("cam")
    cam.lens = 55
    co = bpy.data.objects.new("cam", cam)
    bpy.context.collection.objects.link(co)
    d = size * pad * 1.35
    a, e = math.radians(az), math.radians(el)
    co.location = c + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * d
    co.rotation_euler = (c - co.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = co
    return c, size, mn


def render_one(path):
    bpy.ops.wm.open_mainfile(filepath=path)
    objs = [o for o in bpy.data.objects if o.type == "MESH"]
    setup_render()
    c, size, mn = frame(objs)
    add_lights(c, size)
    # keep floor at asset ground: pads/beams have origin at the top face, so drop the floor to the lowest vertex
    for o in bpy.data.objects:
        if o.name.startswith("Plane"):
            o.location.z = mn.z - 0.001
    out = os.path.join(PREVIEWS, os.path.basename(path)[:-6] + ".png")
    bpy.context.scene.render.filepath = out
    bpy.ops.render.render(write_still=True)
    return out


def render_assembled():
    """Append every asset's objects into one scene and arrange like the reference board."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    layout = {  # name: (x, y, z-offset to stand the asset on the floor, rotation_z deg)
        "SD_BallMount": (-9, 4, 0, 0), "SD_Sweeper_Large": (0, 4, 0, 20), "SD_Sweeper_Small": (-5, -3, 0, -30),
        "SD_JumpPad_Small": (12, 10, 1.95, 0), "SD_JumpPad_Large": (18, 9, 1.95, 0),
        "SD_BalanceBeam": (-1, -8, 1.7, 0), "SD_Platform_4x4": (11, -2, 1.4, 0), "SD_Platform_8x4": (17, -10, 1.4, 0),
        "SD_PaddedBarrier": (-9, -6, 0, 0), "SD_PistonWall": (-12, -1, 0.1, 0),
        "SD_Flag_Red": (3, -3, 0, 0), "SD_Flag_Blue": (5.2, -3, 0, 0), "SD_Support_Truss": (8, 11, 0, 0), "SD_PendulumBob": (-14, 6, 1.8, 0),
        "SD_WaterEdge_Corner": (-4, -13, 0, 0),
    }
    placed = []
    for n, (x, y, z, rz) in layout.items():
        p = os.path.join(SRC, n + ".blend")
        with bpy.data.libraries.load(p) as (src, dst):
            dst.objects = [o for o in src.objects]
        for o in dst.objects:
            if o is None:
                continue
            bpy.context.collection.objects.link(o)
            o.location = (o.location.x + x, o.location.y + y, o.location.z + z)
            o.rotation_euler.z = math.radians(rz)
            placed.append(o)
    # water edge run: alternating red/blue straights
    for i in range(7):
        n = "SD_WaterEdge_Red" if i % 2 == 0 else "SD_WaterEdge_Blue"
        with bpy.data.libraries.load(os.path.join(SRC, n + ".blend")) as (src, dst):
            dst.objects = list(src.objects)
        for o in dst.objects:
            bpy.context.collection.objects.link(o)
            o.location = (-1 + i * 2.0, -13, 0)
            placed.append(o)
    setup_render((1920, 1080))
    sc = bpy.context.scene
    objs = [o for o in placed if o.type == "MESH"]
    c, size, mn = frame(objs, az=28, el=30, pad=0.68)
    add_lights(c, size)
    # pool-blue water plane for context
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, 0.0))
    for o in bpy.data.objects:
        if o.name.startswith("Plane") and o.type == "MESH":
            o.location.z = -0.01 if o.dimensions.x > 100 else o.location.z
    out = os.path.join(PREVIEWS, "_assembled.png")
    sc.render.filepath = out
    bpy.ops.render.render(write_still=True)
    return out


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if argv and argv[0] == "assembled":
        print("RENDERED", render_assembled())
    elif argv:
        print("RENDERED", render_one(os.path.join(SRC, argv[0] + ".blend")))


main()
