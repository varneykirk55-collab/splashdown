#!/usr/bin/env python3
"""Draws a top-down map of Course 1 from Course1Layout.cs (the same data the game and tests use)
and prints the gap table. Usage: python3 Tools/render_course1_map.py   Needs: Pillow"""
import math, os, re
from PIL import Image, ImageDraw

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
SRC = open(os.path.join(ROOT, "Assets/BikiniWipeout/Scripts/Core/Course1Layout.cs")).read()
num = lambda s: float(s.strip().rstrip("f"))
LEGS = [(num(a), num(b), int(c)) for a, b, c in re.findall(r"new CourseLeg\(([^,]+),([^,]+),\s*(-?\d+)\)", SRC)]
S = []
for m in re.finditer(r'(Box|Moving|Disc)\("(\w+)", \'(\w)\', (\d), (?:SurfaceKind\.(\w+), )?([^)]*)\)', SRC):
    kind, name, sec, leg, sk, rest = m.groups()
    v = [num(x) for x in rest.split(",")]
    if kind == "Box": S.append(dict(n=name, s=sec, leg=int(leg), k=sk, x=v[0], z=v[1], w=v[2], l=v[3], h=0, mx=0, mz=0))
    elif kind == "Moving": S.append(dict(n=name, s=sec, leg=int(leg), k="MovingPlatform", x=v[0], z=v[1], w=v[2], l=v[3], h=0, mx=v[4], mz=v[5]))
    else: S.append(dict(n=name, s=sec, leg=int(leg), k=sk, x=v[0], z=v[1], r=v[2], h=v[3], mx=0, mz=0))
m = re.search(r'Name = "FinishPlatform".*?X = ([\d.f-]+), Z = ([\d.f-]+), Width = ([\d.f-]+), Length = ([\d.f-]+), Height = ([\d.f-]+)', SRC, re.S)
S.append(dict(n="FinishPlatform", s="E", leg=2, k="Finish", x=num(m[1]), z=num(m[2]), w=num(m[3]), l=num(m[4]), h=num(m[5]), mx=0, mz=0))

def world(leg, x, z):
    ox, oz, yaw = LEGS[leg]; c, s = round(math.cos(math.radians(yaw))), round(math.sin(math.radians(yaw)))
    return ox + x * c + z * s, oz - x * s + z * c

# Obstacles as placed by Foundation1Builder.Course1Scene.cs (leg, x, z, reach/radius, label)
OBST = [(0,0,43,3.6,"sweeper"),(0,0,52.4,3.6,"sweeper"),(0,0,70.2,1.1,"ball"),(0,0,74.7,1.1,"ball"),(0,0,79.2,1.1,"ball"),
        (0,0,87.8,1.5,"giant"),(0,0,93.2,1.5,"giant"),(1,0,34.5,6.6,"sweeper"),(1,0,52.5,2.8,"beam"),(1,0,59.5,2.8,"beam"),
        (2,0,4.5,0.9,"pend"),(2,0,8.5,0.9,"pend"),(2,0,12.5,0.9,"pend"),(2,-6,25.5,0,"piston"),(2,6,29.5,0,"piston"),(2,-6,33.5,0,"piston"),
        (2,0,41.5,0.9,"pend"),(2,0,46,2.8,"beam"),(2,0,50.2,0.9,"pend"),(2,0,71.1,7.2,"double")]
CP = [(0,0,3),(0,0,61.5),(0,0,110.3),(1,0,70.5),(2,0,56)]

SC = 7; X0, Z0, X1, Z1 = -30, -36, 108, 140
W, H = int((X1 - X0) * SC), int((Z1 - Z0) * SC)
P = lambda x, z: ((x - X0) * SC, H - (z - Z0) * SC)
img = Image.new("RGB", (W, H), (22, 190, 210)); d = ImageDraw.Draw(img)
d.rectangle([P(-24, 134), P(101, -30)], outline=(255, 255, 255), width=2)
COL = dict(A=(255, 90, 170), B=(70, 130, 245), C=(255, 205, 50), D=(150, 100, 240), E=(255, 140, 40))
for s in S:
    for t in ((0, 1) if s["mx"] or s["mz"] else (0,)):
        cx, cz = world(s["leg"], s["x"] + s["mx"] * t, s["z"] + s["mz"] * t)
        col = COL[s["s"]] if s["k"] not in ("Checkpoint", "Start", "Finish") else (245, 245, 245)
        if "r" in s:
            a, b = P(cx - s["r"], cz + s["r"]), P(cx + s["r"], cz - s["r"]); d.ellipse([a, b], fill=col, outline=(40, 40, 60))
        else:
            hw, hl = (s["l"] / 2, s["w"] / 2) if abs(LEGS[s["leg"]][2]) % 180 == 90 else (s["w"] / 2, s["l"] / 2)
            d.rectangle([P(cx - hw, cz + hl), P(cx + hw, cz - hl)], fill=col if t == 0 else None, outline=(40, 40, 60))
for leg, x, z, r, label in OBST:
    cx, cz = world(leg, x, z)
    if label == "piston":
        ex, ez = world(leg, x + (7 if x < 0 else -7), z); d.line([P(cx, cz), P(ex, ez)], fill=(30, 60, 200), width=6)
    elif label == "pend":
        a, b = world(leg, -3.7, z), world(leg, 3.7, z); d.line([P(*a), P(*b)], fill=(200, 20, 30), width=3)
        d.ellipse([P(cx - r, cz + r), P(cx + r, cz - r)], fill=(220, 30, 40))
    elif label in ("ball", "giant"):
        d.ellipse([P(cx - r, cz + r), P(cx + r, cz - r)], fill=(220, 30, 40))
    else:
        d.ellipse([P(cx - r, cz + r), P(cx + r, cz - r)], outline=(200, 20, 30), width=3)
for i, (leg, x, z) in enumerate(CP):
    cx, cz = world(leg, x, z); px, py = P(cx, cz); d.ellipse([px - 9, py - 9, px + 9, py + 9], fill=(40, 220, 110), outline=(0, 0, 0)); d.text((px - 3, py - 6), str(i), fill=(0, 0, 0))
fx, fz = world(2, 0, 106.5); d.line([P(fx - 7, fz), P(fx + 7, fz)], fill=(0, 0, 0), width=5)
for name, (leg, x, z) in dict(A=(0, -9, 30), B=(0, -9, 85), C=(1, -9, 35), D=(2, -9, 25), E=(2, -9, 85)).items():
    d.text(P(*world(leg, x, z)), name, fill=(255, 255, 255))
d.text(P(-8, -16), "START", fill=(0, 0, 0)); d.text(P(70, -16), "FINISH", fill=(0, 0, 0))
os.makedirs(os.path.join(ROOT, "Docs"), exist_ok=True)
img.save(os.path.join(ROOT, "Docs", "course1_map.png")); print("surfaces:", len(S), "legs:", LEGS)
