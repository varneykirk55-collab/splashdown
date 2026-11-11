#!/usr/bin/env python3
"""Generates the stylized arena textures for Splashdown (all original, procedural, reproducible).

Usage:  python3 Tools/generate_arena_textures.py      Needs: numpy, Pillow
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "BikiniWipeout", "Textures", "Arena")
rng = np.random.default_rng(3)


def save(name, img):
    img.save(os.path.join(OUT, name + ".png"))
    print(name, img.size)


def water(size=512):
    """Tileable turquoise caustics: sum of periodic sine ridges, so it wraps seamlessly."""
    y, x = np.mgrid[0:size, 0:size] / size * 2 * np.pi
    v = np.zeros((size, size))
    for fx, fy, ph in [(3, 2, 0.0), (-2, 4, 1.3), (5, -1, 2.1), (1, 5, 4.0), (-4, -3, 0.7), (6, 3, 5.2)]:
        v += np.sin(fx * x + fy * y + ph + 0.9 * np.sin(fy * x - fx * y + ph))
    v = np.abs(v / 6.0)
    ridges = np.clip(1.0 - v * 3.2, 0, 1) ** 2.2          # thin bright lines where the waves cancel
    base = np.array([18, 196, 214], dtype=float)           # bright turquoise
    deep = np.array([8, 150, 200], dtype=float)
    foam = np.array([225, 252, 255], dtype=float)
    shade = (np.sin(2 * x + y) * 0.5 + 0.5)[..., None]
    rgb = deep * (1 - shade) * 0.5 + base * (0.5 + 0.5 * shade)
    rgb = rgb * (1 - ridges[..., None] * 0.75) + foam * ridges[..., None] * 0.75
    save("Water_Caustics", Image.fromarray(np.clip(rgb, 0, 255).astype("uint8"), "RGB"))


def chevron(size=256, bg=(255, 60, 150), fg=(255, 255, 255), name="Chevron_PinkWhite"):
    """Arrow pointing +V (texture up). Tiles vertically."""
    img = Image.new("RGB", (size, size), bg)
    d = ImageDraw.Draw(img)
    w = size
    t = size * 0.26
    for off in (0, size):                                   # second copy makes the vertical wrap seamless
        top = size * 0.18 - off + size * 0.5
        pts = [(w * 0.12, top + size * 0.30), (w * 0.5, top), (w * 0.88, top + size * 0.30),
               (w * 0.88, top + size * 0.30 + t), (w * 0.5, top + t), (w * 0.12, top + size * 0.30 + t)]
        d.polygon(pts, fill=fg)
        pts = [(px, py - size) for px, py in pts]
        d.polygon(pts, fill=fg)
    arr = np.roll(np.asarray(img.transpose(Image.FLIP_TOP_BOTTOM)), size // 2 - int(size * 0.1), axis=0)   # centre the arrow
    save(name, Image.fromarray(arr, "RGB"))


def target(size=256):
    img = Image.new("RGB", (size, size), (70, 110, 235))
    d = ImageDraw.Draw(img)
    c = size / 2
    for r, col in [(0.47, (255, 255, 255)), (0.38, (255, 60, 150)), (0.27, (255, 255, 255)), (0.17, (255, 60, 150)),
                   (0.07, (255, 255, 255))]:
        d.ellipse([c - r * size, c - r * size, c + r * size, c + r * size], fill=col)
    save("Target_Rings", img)


def checker(size=256, n=8):
    a = np.indices((size, size)).sum(axis=0) * 0
    yy, xx = np.indices((size, size))
    a = ((xx // (size // n)) + (yy // (size // n))) % 2
    rgb = np.where(a[..., None] == 1, np.array([250, 250, 250]), np.array([25, 25, 35])).astype("uint8")
    save("Checker", Image.fromarray(rgb, "RGB"))


def panels(name, colors, size=256, n=2):
    """Inflatable 'quilted' panels: soft-shaded colour blocks with darker seams. Tileable."""
    img = np.zeros((size, size, 3), dtype=float)
    cell = size // n
    yy, xx = np.indices((size, size))
    u = (xx % cell) / cell
    v = (yy % cell) / cell
    puff = (np.sin(u * np.pi) * np.sin(v * np.pi)) ** 0.35     # bright in the middle, dark at the seams
    idx = ((xx // cell) + (yy // cell)) % len(colors)
    for i, col in enumerate(colors):
        mask = (idx == i)[..., None]
        img += mask * np.array(col, dtype=float)
    img = img * (0.72 + 0.34 * puff[..., None])
    save(name, Image.fromarray(np.clip(img, 0, 255).astype("uint8"), "RGB"))


def stripes(name, a, b, size=256, n=4):
    yy, xx = np.indices((size, size))
    band = ((xx + yy) // (size // n)) % 2
    rgb = np.where(band[..., None] == 1, np.array(a), np.array(b)).astype("uint8")
    save(name, Image.fromarray(rgb, "RGB"))


def crowd(size=512, rows=16, cols=40):
    """Abstract spectators: rows of colourful round heads/shoulders on dark seating. Tileable horizontally."""
    img = Image.new("RGB", (size, size), (52, 44, 92))
    d = ImageDraw.Draw(img)
    palette = [(255, 80, 150), (255, 210, 50), (70, 160, 255), (255, 140, 40), (120, 230, 120), (240, 240, 250),
               (180, 110, 255), (255, 110, 110)]
    skin = [(240, 200, 170), (205, 150, 115), (150, 100, 75), (250, 220, 195)]
    rh = size / rows
    cw = size / cols
    for r in range(rows):
        d.rectangle([0, r * rh + rh * 0.82, size, (r + 1) * rh], fill=(38, 32, 70))
        for c in range(cols):
            if rng.random() < 0.08:
                continue
            x = (c + 0.5 + (0.5 if r % 2 else 0)) * cw % size
            y = r * rh + rh * 0.5 + rng.uniform(-1.5, 1.5)
            col = palette[rng.integers(len(palette))]
            d.ellipse([x - cw * 0.46, y - rh * 0.02, x + cw * 0.46, y + rh * 0.62], fill=col)
            d.ellipse([x - cw * 0.26, y - rh * 0.40, x + cw * 0.26, y + rh * 0.06], fill=skin[rng.integers(len(skin))])
    save("Crowd", img.filter(ImageFilter.GaussianBlur(0.6)))


def lights(size=128, n=4):
    img = Image.new("RGB", (size, size), (40, 44, 60))
    d = ImageDraw.Draw(img)
    cell = size / n
    for i in range(n):
        for j in range(n):
            d.ellipse([i * cell + cell * 0.14, j * cell + cell * 0.14, (i + 1) * cell - cell * 0.14, (j + 1) * cell - cell * 0.14],
                      fill=(255, 252, 235))
    save("Floodlight", img)


def soft_dot(size=64):
    yy, xx = np.indices((size, size))
    r = np.hypot(xx - size / 2 + 0.5, yy - size / 2 + 0.5) / (size / 2)
    a = np.clip(1 - r, 0, 1) ** 1.5
    rgba = np.dstack([np.full_like(a, 255), np.full_like(a, 255), np.full_like(a, 255), a * 255]).astype("uint8")
    save("SoftDot", Image.fromarray(rgba, "RGBA"))


def foam_ring(size=256):
    yy, xx = np.indices((size, size))
    r = np.hypot(xx - size / 2 + 0.5, yy - size / 2 + 0.5) / (size / 2)
    ang = np.arctan2(yy - size / 2, xx - size / 2)
    wob = 0.04 * np.sin(ang * 9) + 0.03 * np.sin(ang * 17 + 1.0)
    a = np.clip(1 - np.abs(r - 0.80 - wob) / 0.16, 0, 1) ** 1.3
    a *= (r < 0.99)
    rgba = np.dstack([np.full_like(a, 255), np.full_like(a, 255), np.full_like(a, 255), a * 235]).astype("uint8")
    save("Foam_Ring", Image.fromarray(rgba, "RGBA"))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    water()
    chevron()
    chevron(bg=(70, 110, 235), fg=(255, 214, 40), name="Chevron_BlueYellow")
    target()
    checker()
    panels("Panels_PinkPurple", [(255, 70, 160), (150, 90, 240)])
    panels("Panels_BlueTeal", [(60, 130, 245), (40, 200, 215)])
    panels("Panels_YellowOrange", [(255, 214, 40), (255, 150, 30)])
    panels("Panels_Rainbow", [(255, 70, 160), (255, 214, 40), (60, 130, 245), (255, 140, 30)], n=4)
    stripes("Stripes_RedWhite", (235, 40, 50), (250, 250, 250))
    stripes("Stripes_BlueYellow", (50, 110, 240), (255, 214, 40))
    crowd()
    lights()
    soft_dot()
    foam_ring()
