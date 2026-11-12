"""Validate all staged Splashdown assets: blender -b -P validate_assets.py
Writes ArtStaging/Blender/validation.json and prints a table. Exit code is not used; read the report.
"""
import sys, os, json, glob, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from sd_lib import ROOT, SRC, EXPORT

TRI_BUDGET = 25000          # per asset (mobile-agnostic PC/console target; chrome trusses dominate)
results = []


def mesh_stats(objs):
    tris = verts = 0
    nonman = 0
    dup = 0
    for o in objs:
        me = o.data
        me.calc_loop_triangles()
        tris += len(me.loop_triangles)
        verts += len(me.vertices)
        bm = bmesh.new()
        bm.from_mesh(me)
        nonman += sum(1 for e in bm.edges if not e.is_manifold)
        before = len(bm.verts)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        dup += before - len(bm.verts)
        bm.free()
    return tris, verts, nonman, dup


def check_blend(path):
    bpy.ops.wm.open_mainfile(filepath=path)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    r = dict(name=os.path.basename(path)[:-6], checks={})
    c = r["checks"]
    c["reopens"] = True
    c["has_meshes"] = len(meshes) > 0
    tris, verts, nonman, dup = mesh_stats(meshes)
    r.update(tris=tris, verts=verts, non_manifold_edges=nonman, duplicate_verts=dup)
    c["tri_budget"] = tris <= TRI_BUDGET
    c["no_duplicate_verts"] = dup == 0
    c["manifold"] = nonman == 0
    c["uv_layers"] = all(len(o.data.uv_layers) > 0 for o in meshes)
    c["materials_assigned"] = all(len(o.material_slots) > 0 and all(s.material for s in o.material_slots) for o in meshes)
    c["all_faces_have_valid_material"] = all(p.material_index < len(o.material_slots) for o in meshes for p in o.data.polygons)
    c["scale_applied_positive"] = all(all(abs(abs(s) - 1) < 1e-5 and s > 0 for s in o.scale) for o in meshes)
    c["no_rotation"] = all(all(abs(a) < 1e-5 for a in o.rotation_euler) for o in meshes)
    c["names_ok"] = all(" " not in o.name and not o.name[0].islower() for o in meshes) and r["name"].startswith("SD_")
    # no loose/unused materials, and only SD_* palette used
    c["palette_only"] = all(m.name.startswith("SD_") for m in bpy.data.materials if m.users)
    # normals: flag any mesh whose signed volume is negative (inverted)
    inv = []
    for o in meshes:
        bm = bmesh.new()
        bm.from_mesh(o.data)
        vol = sum(f.calc_area() * (f.calc_center_median().dot(f.normal)) for f in bm.faces) / 3.0
        bm.free()
        if vol < 0:
            inv.append(o.name)
    c["normals_outward"] = not inv
    r["inverted"] = inv
    dims = {}
    for o in meshes:
        d = o.dimensions
        dims[o.name] = dict(size=[round(d.x, 3), round(d.y, 3), round(d.z, 3)], pivot=[round(v, 3) for v in o.location],
                            materials=[s.material.name for s in o.material_slots])
    r["objects"] = dims
    return r


def check_fbx(name, expect_objs):
    path = os.path.join(EXPORT, name + ".fbx")
    ok = os.path.isfile(path) and os.path.getsize(path) > 1000
    info = dict(exists=ok, bytes=os.path.getsize(path) if os.path.isfile(path) else 0)
    if ok:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        try:
            bpy.ops.import_scene.fbx(filepath=path)
            ms = [o for o in bpy.data.objects if o.type == "MESH"]
            info["reimport_meshes"] = len(ms)
            info["reimport_ok"] = len(ms) >= len(expect_objs)
            # Unity-oriented import: Blender converts Y-up FBX back to Z-up, so heights read on Z again.
            tot = [0, 0, 0]
            info["reimport_dims"] = {o.name: [round(v, 3) for v in o.dimensions] for o in ms}
        except Exception as e:
            info["reimport_ok"] = False
            info["error"] = str(e)[:200]
    else:
        info["reimport_ok"] = False
    return info


def main():
    blends = sorted(glob.glob(os.path.join(SRC, "*.blend")))
    for b in blends:
        r = check_blend(b)
        meta_path = b[:-6] + ".json"
        expect = []
        if os.path.isfile(meta_path):
            expect = json.load(open(meta_path))["objects"]
        r["checks"]["metadata_documented"] = os.path.isfile(meta_path)
        r["fbx"] = check_fbx(r["name"], expect)
        r["checks"]["fbx_exported"] = r["fbx"]["exists"]
        r["checks"]["fbx_reimports"] = r["fbx"].get("reimport_ok", False)
        r["pass"] = all(r["checks"].values())
        results.append(r)
    with open(os.path.join(ROOT, "validation.json"), "w") as fh:
        json.dump(dict(budget_tris=TRI_BUDGET, results=results), fh, indent=1)
    print("\n%-22s %7s %5s %5s  %s" % ("asset", "tris", "nonM", "dupV", "result"))
    for r in results:
        failed = [k for k, v in r["checks"].items() if not v]
        print("%-22s %7d %5d %5d  %s" % (r["name"], r["tris"], r["non_manifold_edges"], r["duplicate_verts"], "PASS" if r["pass"] else "FAIL " + ",".join(failed)))


main()
