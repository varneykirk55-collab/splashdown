"""Build one Splashdown asset per run: blender -b -P build_assets.py -- <AssetName>
Each asset is saved to src/<name>.blend, exported to export/<name>.fbx and described in src/<name>.json.
Pivot / axis conventions are noted per asset (Blender Z-up -> Unity Y-up on export).
"""
import sys, os, json, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from sd_lib import *

REG = {}


def asset(fn):
    REG[fn.__name__] = fn
    return fn


def poly_slab(name, pts, thick, z, material):
    """Flat extruded polygon (pts in XY, counter-clockwise), bottom at z."""
    bm = bmesh.new()
    vs = [bm.verts.new((x, y, z)) for x, y in pts]
    f = bm.faces.new(vs)
    res = bmesh.ops.extrude_face_region(bm, geom=[f])
    vv = [v for v in res["geom"] if isinstance(v, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, vec=(0, 0, thick), verts=vv)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name + "_mesh")
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    me.materials.append(mat(material))
    return o


def chevron(name, cx, cy, w, h, t, z, thick, material="SD_White", flip=False):
    """Chevron pointing +Y (or -Y when flip). w = width, h = total height, t = arm thickness."""
    s = -1 if flip else 1
    pts = [(-w / 2, 0), (0, h / 2 + 0), (w / 2, 0), (w / 2, -t), (0, h / 2 - t), (-w / 2, -t)]
    pts = [(cx + x, cy + s * (y - h / 4 + t / 2)) for x, y in pts]
    if flip:
        pts = pts[::-1]
    return poly_slab(name, pts, thick, z, material)


def banded_bar(name, length, radius, bands, band_w, loc, rot_z=0.0, body="SD_Red", band_mat="SD_Blue"):
    """Red capsule bar along X with blue bands; origin at loc. Returns objects (unjoined)."""
    parts = [capsule_x(name + "_body", length, radius, (0, 0, 0), body)]
    for bx in bands:
        parts.append(cyl(name + "_band", radius * 1.025, band_w, (bx, 0, 0), band_mat, bevel=0.01, segments=1, verts=24,
                         rot=(0, math.radians(90), 0)))
    return parts


# ------------------------------------------------------------------ Priority A: gameplay obstacles

@asset
def SD_BallMount():
    """Stationary oversized ball on a truss mount. Ball r=1.1 m (matches BouncingBall collider radius)."""
    R = 1.1
    base = box("base", (1.5, 1.5, 0.35), (0, 0, 0.175), "SD_Blue", 0.08, 4)
    tower = truss("tw", 0.9, 0.9, 1.25, z0=0.35, bays=2)
    cap = box("cap", (1.15, 1.15, 0.22), (0, 0, 1.71), "SD_Blue", 0.07, 4)
    collar = cyl("collar", 0.5, 0.06, (0, 0, 1.85), "SD_Chrome", 0.01, 2)
    mount = join([base, cap, collar] + tower, "BallMount")
    finalize(mount, pivot=(0, 0, 0))
    ball = sphere("Ball", R, (0, 0, 1.82 + R - 0.12), "SD_Red")
    finalize(ball, pivot=(0, 0, 1.82 + R - 0.12))
    objs = [mount, ball]
    return objs, dict(pivot="Mount origin = base centre on floor. Ball origin = sphere centre (z=%.2f m above mount base)." % (1.82 + R - 0.12),
                      gameplay="Ball radius 1.10 m = existing BouncingBall sphere collider. Ball is a separate object so a Rigidbody can drive it; "
                               "as supplied it is STATIONARY. Existing course balls bounce and patrol (BouncingBall.cs); that behaviour is untouched.",
                      colliders="Mount: box 1.5x1.5xH1.9. Ball: SphereCollider r=1.1.")


def make_sweeper(reach, high=False, swap=False):
    """Rotating sweeper. high=True adds two bars at 2.35 m (prefab 'SweeperDouble'); swap=True swaps bar colours (blue bars, red bands)."""
    hubR = max(0.45, min(0.85, reach * 0.12))   # same formula as Foundation1Builder.CreateSweeperVariant
    THK = 0.42
    ARM_Z = 0.5
    HIGH_Z = 2.35
    hubH = HIGH_Z + 0.5 if high else 1.35     # prefab hub height
    body_m, band_m = ("SD_Blue", "SD_Red") if swap else ("SD_Red", "SD_Blue")
    # static base
    skirt = cyl("skirt", hubR + 0.30, 0.12, (0, 0, 0.06), "SD_Blue", 0.04, 3, 48)
    drum = cyl("drum", hubR + 0.22, 0.16, (0, 0, 0.20), "SD_Red", 0.05, 3, 48)
    col = cyl("col", hubR, hubH - 0.28, (0, 0, 0.28 + (hubH - 0.28) / 2), "SD_Red", 0.05, 3, 48)
    band = cyl("band", hubR * 1.05, 0.22, (0, 0, 0.62), "SD_Blue", 0.02, 2, 48)
    collar = cyl("collar", hubR * 0.9, 0.08, (0, 0, hubH - 0.05), "SD_Chrome", 0.01, 2, 48)
    dome = sphere("dome", hubR * 1.05, (0, 0, hubH), "SD_Red", 48, 24)
    parts_b = [skirt, drum, col, band, collar, dome]
    if high:
        parts_b.append(cyl("band2", hubR * 1.05, 0.22, (0, 0, hubH - 0.55), "SD_Blue", 0.02, 2, 48))
    base = join(parts_b, "Base")
    finalize(base, pivot=(0, 0, 0))
    # rotating arm: origin at the rotation axis, height 0.5 (matches prefab 'Arm' child)
    length = reach - hubR
    specs = [(0, ARM_Z), (180, ARM_Z)] + ([(90, HIGH_Z), (270, HIGH_Z)] if high else [])
    bars, pads = [], []
    for i, (ang, z) in enumerate(specs):
        tube_len = length + THK
        centre = hubR + length / 2
        nb = max(2, int(tube_len // 1.0))
        bands = [-tube_len / 2 + tube_len * (k + 0.5) / nb for k in range(nb) if k % 2 == 1]
        parts = banded_bar("bar", tube_len, THK / 2, bands, 0.34, (0, 0, 0), body=body_m, band_mat=band_m)
        bar = join(parts, "bar%d" % i)
        bar.location = (0, 0, 0)
        bpy.context.view_layer.update()
        bar.data.transform(Matrix.Translation((centre, 0, z)))
        if ang:
            bar.data.transform(Matrix.Rotation(math.radians(ang), 4, "Z"))
        bars.append(bar)
        r = math.radians(ang)
        pads.append(sphere("pad", 0.30, (math.cos(r) * reach, math.sin(r) * reach, z), band_m, 24, 12))
    arm = join(bars + pads, "Arm")
    finalize(arm, pivot=(0, 0, ARM_Z))
    return [base, arm], dict(
        pivot="Base origin = axis on floor. Arm origin = rotation axis at z=0.5 m (matches the prefab 'Arm' child at local y 0.5). Rotate Arm about Unity Y.",
        gameplay="reach %.2f m, hub radius %.2f m, hub height %.2f, tube thickness 0.42 m, tip pads r 0.30 (prefab 0.34). %s Rotation axis Y." % (reach, hubR, hubH, "Two low bars at y 0.5 + two high bars at y 2.35 (90/270 deg)." if high else "Two opposed low bars."),
        colliders="Hub: cylinder r %.2f, H %.2f. Bars: capsule r 0.21 along the bar, length %.2f. Pads: sphere r 0.34. Keep the prefab colliders." % (hubR, hubH, length + THK))


@asset
def SD_Sweeper_Small():
    return make_sweeper(3.6)


@asset
def SD_Sweeper_Large():
    return make_sweeper(6.6)


@asset
def SD_Sweeper_Double():
    return make_sweeper(7.2, high=True)


@asset
def SD_RotatingBeam():
    return make_sweeper(2.8, swap=True)


def make_jump_pad(radius, name_hint):
    """Origin at the centre of the walkable top face (z=0). Pillar and feet extend below into the water."""
    thick = 0.45
    cushion = cyl("cushion", radius, thick, (0, 0, -thick / 2), "SD_Red", 0.12, 5, 64)
    inlay = cyl("inlay", radius * 0.8, 0.07, (0, 0, -0.025), "SD_Blue", 0.02, 2, 64)
    # inlay stands 0.01 above the red top face; keep the whole walkable top at ~0
    pillar = cyl("pillar", min(0.3, radius * 0.25), 1.6, (0, 0, -thick - 0.8 + 0.02), "SD_Chrome", 0.015, 2, 32)
    foot = box("foot", (radius * 0.75, radius * 0.75, 0.3), (0, 0, -thick - 1.6 - 0.13), "SD_Blue", 0.06, 3)
    o = join([cushion, inlay, pillar, foot], name_hint)
    finalize(o, pivot=(0, 0, 0))
    return [o], dict(pivot="Origin = centre of walkable top face (z=0); mesh extends ~2.4 m below.",
                     gameplay="Walkable disc radius %.2f m, top at pivot height. Replace primitive cylinder only; keep the existing MeshCollider/CylinderCollider." % radius,
                     colliders="Cylinder r %.2f, thickness 0.45 below pivot." % radius)


@asset
def SD_JumpPad_Small():
    return make_jump_pad(1.2, "JumpPad_Small")


@asset
def SD_JumpPad_Large():
    return make_jump_pad(2.0, "JumpPad_Large")


@asset
def SD_BalanceBeam():
    L, W, H = 6.0, 0.5, 0.36
    body = box("body", (L, W, H), (0, 0, -H / 2), "SD_Red", 0.14, 5)
    bands = []
    for k in range(-2, 3):
        if k % 2 == 0:
            bands.append(box("bandb", (0.42, W + 0.02, H + 0.02), (k * 1.2, 0, -H / 2), "SD_Blue", 0.12, 4))
    parts = [body] + bands
    for sx in (-2.3, 2.3):
        parts += truss("lg", 0.4, 0.4, 1.3, z0=-H - 1.3, loc=(sx, 0, 0), bays=2)
        parts.append(box("ft", (0.8, 0.8, 0.28), (sx, 0, -H - 1.3 - 0.12), "SD_Blue", 0.06, 3))
        parts.append(box("hd", (0.55, 0.55, 0.06), (sx, 0, -H - 0.02), "SD_Chrome", 0.01, 1))
    o = join(parts, "BalanceBeam")
    finalize(o, pivot=(0, 0, 0))
    return [o], dict(pivot="Origin = centre of walkable top surface (z=0). Beam runs along local X (Unity X).",
                     gameplay="6.0 m long, 0.5 m wide, 0.36 m thick; two truss legs at x=+-2.3.", colliders="Box 6.0 x 0.36 x 0.5 with top at pivot.")


@asset
def SD_Platform_4x4():
    return make_platform(4.0, 4.0, "Platform_4x4")


@asset
def SD_Platform_8x4():
    return make_platform(8.0, 4.0, "Platform_8x4")


def make_platform(wx, wy, name):
    """Landing / competition platform: red cushion frame, blue inlay, white chevrons, truss + feet beneath. Origin = top-face centre."""
    th = 0.4
    frame = box("frame", (wx, wy, th), (0, 0, -th / 2), "SD_Red", 0.14, 5)
    inlay = box("inlay", (wx - 0.7, wy - 0.7, 0.06), (0, 0, -0.02), "SD_Blue", 0.02, 2)
    parts = [frame, inlay]
    n = max(1, int(wx // 3.0)) if wx > wy else 1
    for k in range(n):
        cx = (k - (n - 1) / 2) * 3.0 if n > 1 else 0
        for j in (0, 1):
            parts.append(chevron("chev", cx, -0.45 + j * 0.85, 1.7, 0.95, 0.28, 0.0, 0.012))
    # truss underneath: two bays of lattice under the long sides, chrome rails
    for sy in (-1, 1):
        y = sy * (wy / 2 - 0.35)
        parts.append(strut("rail", (-wx / 2 + 0.4, y, -th - 0.05), (wx / 2 - 0.4, y, -th - 0.05), 0.07))
        parts.append(strut("rail2", (-wx / 2 + 0.4, y, -th - 0.95), (wx / 2 - 0.4, y, -th - 0.95), 0.07))
        nb = max(2, int(wx // 1.0))
        xs = [-wx / 2 + 0.4 + (wx - 0.8) * i / nb for i in range(nb + 1)]
        for i in range(nb):
            a, b = xs[i], xs[i + 1]
            parts.append(strut("d", (a, y, -th - 0.05), (b, y, -th - 0.95), 0.03))
            parts.append(strut("d", (b, y, -th - 0.05), (a, y, -th - 0.95), 0.03))
        for x in xs[::max(1, nb // 2)]:
            parts.append(strut("v", (x, y, -th - 0.05), (x, y, -th - 0.95), 0.04))
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(box("ft", (0.9, 0.9, 0.3), (sx * (wx / 2 - 0.5), sy * (wy / 2 - 0.5), -th - 0.95 - 0.1), "SD_Blue", 0.07, 3))
            parts.append(cyl("pl", 0.1, 0.9, (sx * (wx / 2 - 0.5), sy * (wy / 2 - 0.5), -th - 0.5), "SD_Chrome", 0.01, 1, 16))
    o = join(parts, name)
    finalize(o, pivot=(0, 0, 0))
    return [o], dict(pivot="Origin = centre of walkable top face (z=0). Chevrons point +Y in Blender = Unity +Z (forward).",
                     gameplay="%.1f x %.1f m top, 0.4 m cushion. Chevrons are flat, decorative (no collision)." % (wx, wy),
                     colliders="Box %.1f x 0.4 x %.1f below pivot." % (wx, wy))


# ------------------------------------------------------------------ moving obstacles already in the game

@asset
def SD_PistonWall():
    """Matches PistonWall prefab: 4.0 (X, push axis) x 1.3 deep x 1.7 tall block, base at y=0.1."""
    body = box("body", (4.0, 1.3, 1.7), (0, 0, 0.95), "SD_Red", 0.2, 6)
    parts = [body]
    for k in (-1, 0, 1):
        parts.append(box("seam", (3.9, 1.33, 0.035), (0, 0, 0.95 + k * 0.55), "SD_White", 0.005, 1))
    for sx in (-1, 1):
        parts.append(box("endcap", (0.34, 1.4, 1.8), (sx * 1.93, 0, 0.95), "SD_Blue", 0.14, 5))
    o = join(parts, "PistonWall")
    finalize(o, pivot=(0, 0, 0))
    return [o], dict(pivot="Origin = ground level under block centre. Push axis = local X (MovingPlatform).",
                     gameplay="Block 4.0x1.3x1.7 m (+ 0.3 m end caps), identical to the existing primitive's footprint.",
                     colliders="Box 4.0 x 1.7 x 1.3 centred at y=0.95; trigger 4.25 x 1.7 x 1.5.")


@asset
def SD_PendulumBob():
    R = 0.9
    ball = sphere("bob", R, (0, 0, 0), "SD_Red")
    band = cyl("band", R * 1.012, 0.16, (0, 0, 0), "SD_White", 0.01, 1, 48)
    cap = cyl("cap", 0.16, 0.18, (0, 0, R + 0.02), "SD_Chrome", 0.01, 1, 24)
    o = join([ball, band, cap], "PendulumBob")
    finalize(o, pivot=(0, 0, 0))
    return [o], dict(pivot="Origin = sphere centre (rod hangs 5.5 m above).", gameplay="Radius 0.9 m (matches Pendulum weight).",
                     colliders="Sphere r 0.9; trigger r 1.0.")


# ------------------------------------------------------------------ Priority B: environment

@asset
def SD_PaddedBarrier():
    """Modular 3 m barrier section: three stacked red pads between blue posts. Origin = base centre."""
    W, D, H = 3.0, 0.55, 1.4
    parts = []
    for k in range(3):
        parts.append(box("pad", (W - 0.5, D - 0.08, H / 3 - 0.04), (0, 0, H / 6 + k * H / 3), "SD_Red", 0.08, 4))
    for sx in (-1, 1):
        parts.append(box("post", (0.34, D, H), (sx * (W / 2 - 0.17), 0, H / 2), "SD_Blue", 0.11, 5))
    o = join(parts, "PaddedBarrier")
    finalize(o, pivot=(0, 0, 0))
    return [o], dict(pivot="Origin = base centre. Tiles along local X with 3.0 m spacing.", gameplay="3.0 x 0.55 x 1.4 m.",
                     colliders="Box 3.0 x 1.4 x 0.55.")


def make_flag(name, flag_mat, chevron_flip):
    base = cyl("base", 0.28, 0.07, (0, 0, 0.035), "SD_Chrome", 0.02, 3, 32)
    base2 = cyl("base2", 0.17, 0.09, (0, 0, 0.115), "SD_Chrome", 0.02, 3, 32)
    pole = cyl("pole", 0.032, 2.6, (0, 0, 1.4), "SD_Chrome", 0.0, 0, 16)
    knob = sphere("knob", 0.06, (0, 0, 2.72), "SD_Chrome", 16, 8)
    # pennant hanging from a crossbar at the top, in the XZ plane (faces -Y / +Y), notch at the bottom
    top = 2.55
    w, h = 0.7, 1.15
    pts = [(0.04, top), (0.04 + w, top), (0.04 + w, top - h), (0.04 + w / 2, top - h + 0.26), (0.04, top - h)]
    me = bmesh.new()
    vs = [me.verts.new((x, 0, z)) for x, z in pts]
    me.faces.new(vs)
    res = bmesh.ops.extrude_face_region(me, geom=list(me.faces))
    vv = [v for v in res["geom"] if isinstance(v, bmesh.types.BMVert)]
    bmesh.ops.translate(me, vec=(0, 0.03, 0), verts=vv)
    bmesh.ops.recalc_face_normals(me, faces=me.faces)
    mesh = bpy.data.meshes.new("flag_m")
    me.to_mesh(mesh)
    me.free()
    flag = bpy.data.objects.new("flag", mesh)
    bpy.context.collection.objects.link(flag)
    mesh.materials.append(mat(flag_mat))
    ch = []
    for j in range(2):
        cz = top - 0.38 - j * 0.3
        pts_c = [(0.04 + w / 2 - 0.27, cz + 0.0), (0.04 + w / 2, cz - 0.2), (0.04 + w / 2 + 0.27, cz + 0.0),
                 (0.04 + w / 2 + 0.27, cz + 0.11), (0.04 + w / 2, cz - 0.09), (0.04 + w / 2 - 0.27, cz + 0.11)]
        bm = bmesh.new()
        vs = [bm.verts.new((x, -0.002, z)) for x, z in pts_c]
        bm.faces.new(vs[::-1])
        r = bmesh.ops.extrude_face_region(bm, geom=list(bm.faces))
        v2 = [v for v in r["geom"] if isinstance(v, bmesh.types.BMVert)]
        bmesh.ops.translate(bm, vec=(0, -0.012, 0), verts=v2)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        mm = bpy.data.meshes.new("chev_m")
        bm.to_mesh(mm)
        bm.free()
        co = bpy.data.objects.new("chev", mm)
        bpy.context.collection.objects.link(co)
        mm.materials.append(mat("SD_White"))
        ch.append(co)
    for o in [flag] + ch:  # objects created via data API need no op-based finish; just smooth none (flat banner)
        pass
    bar = strut("bar", (0, 0, top), (0.8, 0, top), 0.022)
    o = join([base, base2, pole, knob, bar, flag] + ch, name)
    finalize(o, pivot=(0, 0, 0))
    return [o], dict(pivot="Origin = base centre on ground. Flag extends +X.", gameplay="Decorative competition marker, 2.75 m tall. No collider needed (optional capsule r 0.1).",
                     colliders="Optional: base cylinder r 0.28 h 0.2.")


@asset
def SD_Flag_Red():
    return make_flag("Flag_Red", "SD_Red", False)


@asset
def SD_Flag_Blue():
    return make_flag("Flag_Blue", "SD_Blue", True)


def make_edge_straight(name, mat_name):
    L, W, H = 2.0, 0.7, 0.42
    top = box("top", (L - 0.04, W, H), (0, 0, H / 2 + 0.06), mat_name, 0.12, 5)
    lip = box("lip", (L, W + 0.14, 0.1), (0, 0, 0.05), "SD_Chrome", 0.02, 2)
    o = join([top, lip], name)
    finalize(o, pivot=(0, 0, 0))
    return [o], dict(pivot="Origin = base centre, water-facing side is local -Y. Tiles along X at 2.0 m.",
                     gameplay="2.0 x 0.84 x 0.54 m, alternate Red/Blue segments for the reference stripe.", colliders="Box 2.0 x 0.54 x 0.84.")


@asset
def SD_WaterEdge_Red():
    return make_edge_straight("WaterEdge_Red", "SD_Red")


@asset
def SD_WaterEdge_Blue():
    return make_edge_straight("WaterEdge_Blue", "SD_Blue")


@asset
def SD_WaterEdge_Corner():
    """90-degree outer corner: inner radius 0.6, profile 0.7 wide, spans quarter turn. Origin = arc centre on ground."""
    R, W, H = 0.9, 0.7, 0.42
    bm = bmesh.new()
    prof = [(R, 0.06), (R + W, 0.06), (R + W, 0.06 + H), (R, 0.06 + H)]
    vs = [bm.verts.new((x, 0, z)) for x, z in prof]
    f = bm.faces.new(vs)
    res = bmesh.ops.spin(bm, geom=[f] + list(f.edges) + vs, cent=(0, 0, 0), axis=(0, 0, 1), angle=math.pi / 2, steps=12, use_merge=False, use_duplicate=False)
    last = [g for g in res["geom_last"] if isinstance(g, bmesh.types.BMVert)]
    if len(last) == 4:
        try:
            bm.faces.new(last)
        except ValueError:
            pass
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("corner_m")
    bm.to_mesh(mesh)
    bm.free()
    o = bpy.data.objects.new("corner", mesh)
    bpy.context.collection.objects.link(o)
    o = _finish_obj(o, 0.1, 4, "SD_Red")
    lip = bpy.data.objects.new("lipx", bpy.data.meshes.new("lip_m"))
    bpy.context.collection.objects.link(lip)
    bm = bmesh.new()
    prof = [(R - 0.07, 0.0), (R + W + 0.07, 0.0), (R + W + 0.07, 0.1), (R - 0.07, 0.1)]
    vs = [bm.verts.new((x, 0, z)) for x, z in prof]
    f = bm.faces.new(vs)
    res = bmesh.ops.spin(bm, geom=[f] + list(f.edges) + vs, cent=(0, 0, 0), axis=(0, 0, 1), angle=math.pi / 2, steps=12, use_merge=False, use_duplicate=False)
    last = [g for g in res["geom_last"] if isinstance(g, bmesh.types.BMVert)]
    if len(last) == 4:
        try:
            bm.faces.new(last)
        except ValueError:
            pass
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(lip.data)
    bm.free()
    lip = _finish_obj(lip, 0.02, 2, "SD_Chrome")
    j = join([o, lip], "WaterEdge_Corner")
    finalize(j, pivot=(0, 0, 0))
    return [j], dict(pivot="Origin = centre of the arc on the ground; arc spans +X..+Y quadrant.", gameplay="Inner radius 0.9 m, width 0.84 m, height 0.54 m.",
                     colliders="Use 3 short box colliders or a mesh collider (convex split).")


def _finish_obj(o, bevel, segs, material):
    from sd_lib import _finish
    return _finish(o, bevel, segs, o.name, material)


@asset
def SD_Support_Truss():
    """Generic structural support: 1.0 x 1.0 x 3.0 m chrome truss on a blue foot with a blue cap. Origin = base centre."""
    foot = box("foot", (1.4, 1.4, 0.3), (0, 0, 0.15), "SD_Blue", 0.07, 3)
    tw = truss("tw", 0.9, 0.9, 2.4, z0=0.3, bays=3)
    cap = box("cap", (1.1, 1.1, 0.3), (0, 0, 2.85), "SD_Blue", 0.07, 3)
    o = join([foot, cap] + tw, "Support_Truss")
    finalize(o, pivot=(0, 0, 0))
    return [o], dict(pivot="Origin = base centre; stack vertically at 3.0 m.", gameplay="1.4 x 1.4 foot, 3.0 m tall.", colliders="Box 1.0 x 3.0 x 1.0 (cap/foot are decorative).")


# ------------------------------------------------------------------ driver

def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not argv:
        print("ASSETS", ",".join(REG))
        return
    name = argv[0]
    reset_scene()
    objs, meta = REG[name]()
    blend, fbx = save_and_export(name, objs)
    meta.update(name=name, blend=os.path.relpath(blend, ROOT), fbx=os.path.relpath(fbx, ROOT),
                objects=[o.name for o in objs])
    with open(os.path.join(SRC, name + ".json"), "w") as fh:
        json.dump(meta, fh, indent=2)
    print("BUILT", name)


main()
