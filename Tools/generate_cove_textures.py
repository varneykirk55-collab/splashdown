#!/usr/bin/env python3
"""Generates the stylized textures for Course 2 - Chaos Cove (all original, procedural, reproducible).

Usage:  python3 Tools/generate_cove_textures.py      Needs: numpy, Pillow
"""
import os
import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "BikiniWipeout", "Textures", "Cove")
rng = np.random.default_rng(5)


def save(name, rgb):
    Image.fromarray(np.clip(rgb, 0, 255).astype("uint8"), "RGB").save(os.path.join(OUT, name + ".png"))
    print(name, rgb.shape[:2])


def periodic_noise(size, octaves=((2, 1.0), (4, 0.5), (8, 0.25), (16, 0.12))):
    """Smooth tileable noise built from sines with random phase and direction (wraps seamlessly)."""
    y, x = np.mgrid[0:size, 0:size] / size * 2 * np.pi
    v = np.zeros((size, size))
    for freq, amp in octaves:
        for _ in range(3):
            fx, fy = rng.integers(-freq, freq + 1, 2)
            if fx == 0 and fy == 0:
                fx = freq
            v += amp * np.sin(fx * x + fy * y + rng.uniform(0, 2 * np.pi))
    return (v - v.min()) / (v.max() - v.min())


def rock(size=256):
    """Chunky stylized rock: broad light/dark facets with darker cracks."""
    n = periodic_noise(size)
    facets = np.round(n * 5) / 5                                   # posterised = flat painted planes
    cracks = np.clip(1 - np.abs(periodic_noise(size, ((6, 1.0), (12, 0.5))) - 0.5) * 9, 0, 1) ** 3
    light = np.array([196, 190, 200], dtype=float)
    dark = np.array([118, 112, 132], dtype=float)
    rgb = dark + (light - dark) * facets[..., None]
    save("Rock", rgb * (1 - 0.35 * cracks[..., None]))


def sand(size=256):
    n = periodic_noise(size, ((3, 1.0), (9, 0.4), (27, 0.15)))
    ripples = 0.5 + 0.5 * np.sin(np.mgrid[0:size, 0:size][0] / size * 2 * np.pi * 10 + n * 4)
    base = np.array([250, 222, 150], dtype=float)
    save("Sand", base * (0.9 + 0.1 * n[..., None]) * (0.96 + 0.04 * ripples[..., None]))


def waterfall(size=256):
    """Vertical streaks of white foam on turquoise. Tiles in both directions; scrolled downwards in game."""
    y, x = np.mgrid[0:size, 0:size] / size * 2 * np.pi
    streak = np.zeros((size, size))
    for k in range(9):
        fx = int(rng.integers(3, 22))
        fy = int(rng.integers(1, 4))
        streak += np.sin(fx * x + rng.uniform(0, 6.28) + 0.6 * np.sin(fy * y + rng.uniform(0, 6.28))) * rng.uniform(0.5, 1.0)
    streak = (streak - streak.min()) / (streak.max() - streak.min())
    foam = np.clip((streak - 0.45) * 3.2, 0, 1)[..., None]
    water = np.array([70, 200, 235], dtype=float)
    white = np.array([245, 253, 255], dtype=float)
    save("Waterfall", water * (1 - foam) + white * foam)


def stripes(name, a, b, size=256, n=4):
    yy, xx = np.indices((size, size))
    band = ((xx + yy) // (size // n)) % 2
    save(name, np.where(band[..., None] == 1, np.array(a), np.array(b)))


def deck(size=256, n=2):
    """Blue inflatable deck panels with soft shading and pale seams, matching the Course 2 reference sheet."""
    cell = size // n
    yy, xx = np.indices((size, size))
    u = (xx % cell) / cell
    v = (yy % cell) / cell
    puff = (np.sin(u * np.pi) * np.sin(v * np.pi)) ** 0.3
    idx = ((xx // cell) + (yy // cell)) % 2
    colour = np.where(idx[..., None] == 1, np.array([58, 128, 240]), np.array([76, 150, 250])).astype(float)
    save("Deck_Blue", colour * (0.74 + 0.32 * puff[..., None]))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    rock()
    sand()
    waterfall()
    stripes("Stripes_PinkYellow", (255, 60, 150), (255, 214, 40))
    stripes("Stripes_PinkWhite", (255, 70, 160), (250, 250, 250))
    deck()
