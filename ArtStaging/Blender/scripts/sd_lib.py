"""Splashdown Blender helpers. Units: 1 Blender unit = 1 m. Z-up in Blender, exported to Unity Y-up via FBX axes.
Run only inside Blender (background mode): blender -b --python build_assets.py
"""
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
SRC, EXPORT, PREVIEWS = (os.path.join(ROOT, d) for d in ("src", "export", "previews"))

# Palette follows the approved reference (bold red / blue, chrome, white trim).
PALETTE = {
    "SD_Red":    ((0.80, 0.04, 0.07, 1), 0.0, 0.28),
    "SD_Blue":   ((0.04, 0.16, 0.85, 1), 0.0, 0.28),
    "SD_White":  ((0.92, 0.93, 0.96, 1), 0.0, 0.35),
    "SD_Chrome": ((0.78, 0.80, 0.84, 1), 1.0, 0.22),
    "SD_Water":  ((0.00, 0.75, 0.85, 1), 0.0, 0.10),
    "SD_Pad":    ((0.55, 0.57, 0.62, 1), 0.0, 0.50),
}


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)


def mat(name):
    m = bpy.data.materials.get(name)
    if m:
        return m
    color, metal, rough = PALETTE[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = color
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    m.diffuse_color = color
    return m


def _finish(obj, bevel, segments, name, material, harden=False):
    obj.name = name
    obj.data.name = name + "_mesh"
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.limit_method = "ANGLE"
        mod.angle_limit = math.radians(35)
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.clear()
    obj.data.materials.append(mat(material))
    if harden:
        # Keep big flat caps (pad tops) truly flat while the bevel stays smooth: mark every edge of a cap n-gon sharp.
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        for f in bm.faces:
            f.smooth = True
            if len(f.verts) > 8:
                for e in f.edges:
                    e.smooth = False
        bm.to_mesh(obj.data)
        bm.free()
    else:
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    obj.select_set(False)
    return obj


def box(name, size, loc=(0, 0, 0), material="SD_Red", bevel=0.05, segments=3, rot=(0, 0, 0), harden=False):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = size
    return _finish(o, bevel, segments, name, material, harden)


def cyl(name, radius, depth, loc=(0, 0, 0), material="SD_Red", bevel=0.04, segments=3, verts=32, rot=(0, 0, 0), harden=False):
    bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=depth, vertices=verts, location=loc, rotation=rot)
    return _finish(bpy.context.active_object, bevel, segments, name, material, harden)


def sphere(name, radius, loc=(0, 0, 0), material="SD_Red", seg=48, rings=24):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, segments=seg, ring_count=rings, location=loc)
    return _finish(bpy.context.active_object, 0, 0, name, material)


def capsule_x(name, length, radius, loc=(0, 0, 0), material="SD_Red"):
    """Capsule lying along local X (rounded padded bar). length = tip to tip."""
    body = max(length - 2 * radius, 0.01)
    parts = [cyl(name + "_c", radius, body, loc, material, bevel=0, verts=24, rot=(0, math.radians(90), 0))]
    for s in (-1, 1):
        # Sphere centre sits 1 cm inside the cylinder end so the two closed shells overlap instead of
        # sharing a coincident ring (which merges into non-manifold edges on clean()).
        parts.append(sphere(name + "_e", radius, (loc[0] + s * (body / 2 - 0.01), loc[1], loc[2]), material, 24, 12))
    return join(parts, name)


def strut(name, a, b, radius=0.04, material="SD_Chrome"):
    a, b = Vector(a), Vector(b)
    d = b - a
    bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=d.length, vertices=12, location=(a + b) / 2)
    o = bpy.context.active_object
    o.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    return _finish(o, 0, 0, name, material)


def truss(name, w, d, h, z0=0.0, loc=(0, 0, 0), post=0.045, brace=0.025, bays=1):
    """Chrome lattice tower: four corner posts + X braces on all four faces. Origin at base centre."""
    x, y = w / 2, d / 2
    z1 = z0 + h
    pts = {(sx, sy): (loc[0] + sx * x, loc[1] + sy * y) for sx in (-1, 1) for sy in (-1, 1)}
    parts = []
    for (sx, sy), (px, py) in pts.items():
        parts.append(strut(name + "_post", (px, py, loc[2] + z0), (px, py, loc[2] + z1), post))
    zs = [z0 + h * i / bays for i in range(bays + 1)]
    faces = [((-1, -1), (1, -1)), ((1, -1), (1, 1)), ((1, 1), (-1, 1)), ((-1, 1), (-1, -1))]
    for k0, k1 in faces:
        for i in range(bays):
            for (ka, za), (kb, zb) in (((k0, zs[i]), (k1, zs[i + 1])), ((k1, zs[i]), (k0, zs[i + 1]))):
                pa, pb = pts[ka], pts[kb]
                parts.append(strut(name + "_brace", (pa[0], pa[1], loc[2] + za), (pb[0], pb[1], loc[2] + zb), brace))
    for z in zs[1:-1]:
        for k0, k1 in faces:
            pa, pb = pts[k0], pts[k1]
            parts.append(strut(name + "_ring", (pa[0], pa[1], loc[2] + z), (pb[0], pb[1], loc[2] + z), brace))
    return parts


def join(objs, name):
    """Join objects into one mesh; material slots are merged and de-duplicated."""
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    o.data.name = name + "_mesh"
    # remove duplicate material slots
    seen = {}
    for i, s in enumerate(o.material_slots):
        seen.setdefault(s.material.name if s.material else None, i)
    for poly in o.data.polygons:
        m = o.material_slots[poly.material_index].material
        poly.material_index = seen[m.name if m else None]
    for i in range(len(o.material_slots) - 1, -1, -1):
        if i not in seen.values():
            o.active_material_index = i
            bpy.ops.object.material_slot_remove()
    o.select_set(False)
    return o


def uv(obj, margin=0.003):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=margin)
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.select_set(False)


def clean(obj):
    """Merge doubles, recalc normals outward."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(obj.data)
    bm.free()


def set_origin(obj, point):
    """Move the object's origin to the world point without moving geometry."""
    mw = obj.matrix_world
    offset = Vector(point) - mw.translation
    obj.data.transform(Matrix.Translation(-offset))
    obj.location = Vector(point)


def finalize(obj, pivot=None):
    clean(obj)
    uv(obj)
    if pivot is not None:
        set_origin(obj, pivot)


def save_and_export(name, objects, meta=None):
    """Save .blend and export FBX (Unity: Y-up, -Z forward, metres) for the given objects."""
    os.makedirs(SRC, exist_ok=True)
    os.makedirs(EXPORT, exist_ok=True)
    blend = os.path.join(SRC, name + ".blend")
    for o in bpy.data.objects:
        o.select_set(False)
    for o in objects:
        o.select_set(True)
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    fbx = os.path.join(EXPORT, name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=fbx, use_selection=True, global_scale=1.0, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, object_types={"MESH", "EMPTY"}, mesh_smooth_type="FACE",
        add_leaf_bones=False, path_mode="COPY", embed_textures=False)
    return blend, fbx
