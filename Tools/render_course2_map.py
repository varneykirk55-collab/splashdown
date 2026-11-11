#!/usr/bin/env python3
"""Draws a top-down map of Course 2 (Chaos Cove) from Course2Layout.cs - the same data the game and
the tests use - and prints the gap table. Usage: python3 Tools/render_course2_map.py   Needs: Pillow"""
import math, os, re
from PIL import Image, ImageDraw

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
SRC = open(os.path.join(ROOT, "Assets/BikiniWipeout/Scripts/Core/Course2Layout.cs")).read()
num = lambda s: float(s.strip().rstrip("f"))
LEGS = [(num(a), num(b), int(c)) for a, b, c in re.findall(r"new CourseLeg\(([^,]+),([^,]+),\s*(-?\d+)\)", SRC)]

S = []
for m in re.finditer(r'^\s*(Box|Moving|Disc)\("(\w+)", \'(\w)\', (\d), (?:SurfaceKind\.(\w+), )?([^)]*)\),', SRC, re.M):
    kind, name, sec, leg, sk, rest = m.groups()
    v = [num(x) for x in rest.split(",")] + [0, 0, 0, 0]
    s = dict(n=name, s=sec, leg=int(leg), k=sk or "MovingPlatform", x=v[0], z=v[1], h=0, mx=0, mz=0, drop=0, sag=0, launch=0, body=0)
    if kind == "Box": s.update(w=v[2], l=v[3], h=v[4], drop=v[5], sag=v[6])
    elif kind == "Moving": s.update(w=v[2], l=v[3], mx=v[4], mz=v[5])
    else: s.update(r=v[2], h=v[3], launch=v[4], body=v[5], sag=v[6])
    S.append(s)
O = [dict(n=n, k=k, leg=int(l), x=num(x), z=num(z), a=num(a), b=num(b), c=num(c)) for n, k, l, x, z, y, a, b, c in
     re.findall(r'Ob\("(\w+)", ObstacleKind\.(\w+), (\d), ([^,]+), ([^,]+), ([^,]+), ([^,]+), ([^,]+), ([^)]+)\)', SRC)]
CP = [(int(l), num(x), num(z)) for l, x, z in re.findall(r'Cp\("\w+", "\w+", (\d), ([^,]+), ([^,]+),', SRC)]
FINISH_Z = num(re.search(r"FinishGateZ = ([\d.f]+)", SRC)[1])


def world(leg, x, z):
    ox, oz, yaw = LEGS[leg]
    c, s = round(math.cos(math.radians(yaw))), round(math.sin(math.radians(yaw)))
    return ox + x * c + z * s, oz - x * s + z * c


def half(s):
    return (s["l"] / 2, s["w"] / 2) if abs(LEGS[s["leg"]][2]) % 180 == 90 else (s["w"] / 2, s["l"] / 2)


SC = 6
X0, Z0, X1, Z1 = -42, -46, 138, 192
W, H = int((X1 - X0) * SC), int((Z1 - Z0) * SC)
P = lambda x, z: ((x - X0) * SC, H - (z - Z0) * SC)
img = Image.new("RGB", (W, H), (22, 190, 210))
d = ImageDraw.Draw(img)
d.rectangle([P(-36, 186), P(132, -40)], outline=(255, 255, 255), width=2)
COL = dict(A=(70, 130, 245), B=(255, 90, 170), C=(255, 205, 50), D=(90, 150, 250), E=(240, 110, 190), F=(255, 150, 40))
KINDCOL = dict(Seesaw=(150, 220, 255), BouncePad=(255, 60, 150), SlideRamp=(120, 230, 255), Ramp=(255, 220, 120),
               CollapsingBridge=(255, 120, 60), FloatingArena=(60, 110, 230), BigBall=(225, 25, 35), MovingPlatform=(255, 60, 150))

for s in S:
    for t in ((0, 1) if s["mx"] or s["mz"] else (0,)):
        cx, cz = world(s["leg"], s["x"] + s["mx"] * t, s["z"] + s["mz"] * t)
        col = (245, 245, 245) if s["k"] in ("Checkpoint", "Start", "Finish") else KINDCOL.get(s["k"], COL[s["s"]])
        if "r" in s:
            r = max(s["r"], s["body"])
            d.ellipse([P(cx - r, cz + r), P(cx + r, cz - r)], fill=col, outline=(40, 40, 60))
            if s["body"]:
                d.ellipse([P(cx - s["r"], cz + s["r"]), P(cx + s["r"], cz - s["r"])], outline=(255, 255, 255))
        else:
            hw, hl = half(s)
            d.rectangle([P(cx - hw, cz + hl), P(cx + hw, cz - hl)], fill=col if t == 0 else None, outline=(40, 40, 60))

REACH = dict(Sweeper=3.8, SpinningArms=2.9, GiantArms=5.5, DoubleSweeper=7.2)
for o in O:
    cx, cz = world(o["leg"], o["x"], o["z"])
    k = o["k"]
    if k in REACH:
        r = REACH[k]
        d.ellipse([P(cx - r, cz + r), P(cx + r, cz - r)], outline=(200, 20, 30), width=3)
    elif k == "SwingingBall":
        a, b = world(o["leg"], -3.9, o["z"]), world(o["leg"], 3.9, o["z"])
        d.line([P(*a), P(*b)], fill=(200, 20, 30), width=3)
        d.ellipse([P(cx - 1.1, cz + 1.1), P(cx + 1.1, cz - 1.1)], fill=(255, 60, 150), outline=(120, 0, 40))
    elif k == "Geyser":
        d.ellipse([P(cx - 1, cz + 1), P(cx + 1, cz - 1)], fill=(235, 250, 255), outline=(0, 90, 200), width=2)
    elif k == "SideJet":
        yaw = math.radians(LEGS[o["leg"]][2] + o["c"])
        ex, ez = cx + math.sin(yaw) * 6.5, cz + math.cos(yaw) * 6.5
        d.line([P(cx, cz), P(ex, ez)], fill=(0, 90, 200), width=5)
    elif k == "TippingBucket":
        d.rectangle([P(cx - 1.2, cz + 1.2), P(cx + 1.2, cz - 1.2)], fill=(255, 205, 30), outline=(120, 80, 0), width=2)
    elif k == "BouncingBall":
        d.ellipse([P(cx - 1.1, cz + 1.1), P(cx + 1.1, cz - 1.1)], fill=(220, 30, 40))

for i, (leg, x, z) in enumerate(CP):
    px, py = P(*world(leg, x, z))
    d.ellipse([px - 9, py - 9, px + 9, py + 9], fill=(40, 220, 110), outline=(0, 0, 0))
    d.text((px - 3, py - 6), str(i), fill=(0, 0, 0))
a, b = world(3, -7, FINISH_Z), world(3, 7, FINISH_Z)
d.line([P(*a), P(*b)], fill=(0, 0, 0), width=5)
for name, (leg, x, z) in dict(A=(0, -11, 30), B=(0, -11, 100), C=(1, -11, 45), D=(2, -13, 25), E=(2, -13, 90), F=(3, -11, 30)).items():
    d.text(P(*world(leg, x, z)), name, fill=(255, 255, 255))
d.text(P(-8, -8), "START", fill=(0, 0, 0))
d.text(P(15, 26), "FINISH", fill=(0, 0, 0))
os.makedirs(os.path.join(ROOT, "Docs"), exist_ok=True)
img.save(os.path.join(ROOT, "Docs", "course2_map.png"))
print("surfaces:", len(S), "obstacles:", len(O), "checkpoints:", len(CP), "legs:", LEGS)
