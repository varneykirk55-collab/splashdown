#!/usr/bin/env python3
"""Generates the temporary sound effects and music for Bikini Wipeout.

Everything is synthesized from scratch (sine waves, noise, envelopes) with a fixed random seed, so
the output is reproducible and carries no third-party licence. These files are placeholders: swap the
clips in Assets/BikiniWipeout/Settings/AudioLibrary.asset when real audio arrives.

Usage:  python3 Tools/generate_placeholder_audio.py
Needs:  numpy
"""
import os
import wave

import numpy as np

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "BikiniWipeout", "Audio", "Placeholder")
rng = np.random.default_rng(20261110)


def t(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def env(n, attack=0.005, decay=6.0):
    """Fast attack, exponential decay."""
    x = np.arange(n) / RATE
    a = np.clip(x / max(attack, 1e-5), 0, 1)
    return a * np.exp(-decay * x)


def lowpass(x, cutoff):
    """One-pole low-pass filter."""
    alpha = 1.0 - np.exp(-2.0 * np.pi * cutoff / RATE)
    y = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc += alpha * (v - acc)
        y[i] = acc
    return y


def noise(seconds):
    return rng.uniform(-1.0, 1.0, int(RATE * seconds))


def tone(freq, seconds, shape="sine"):
    x = t(seconds)
    phase = 2 * np.pi * freq * x if np.isscalar(freq) else 2 * np.pi * np.cumsum(freq) / RATE
    if shape == "sine":
        return np.sin(phase)
    if shape == "tri":
        return 2 / np.pi * np.arcsin(np.sin(phase))
    if shape == "soft":  # sine with a little second harmonic
        return 0.8 * np.sin(phase) + 0.2 * np.sin(2 * phase)
    raise ValueError(shape)


def fade_edges(x, seconds=0.004):
    n = min(len(x) // 2, int(RATE * seconds))
    if n > 0:
        ramp = np.linspace(0, 1, n)
        x[:n] *= ramp
        x[-n:] *= ramp[::-1]
    return x


def write(name, samples, peak=0.85):
    samples = np.asarray(samples, dtype=np.float64)
    m = np.max(np.abs(samples))
    if m > 0:
        samples = samples / m * peak
    data = (samples * 32767).astype("<i2")
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data.tobytes())
    print(f"{name}.wav  {len(data) / RATE:.2f}s  {os.path.getsize(path) / 1024:.0f} KB")


def footstep(seed_cut):
    n = noise(0.11)
    s = lowpass(n, seed_cut) * env(len(n), 0.002, 38)
    s += 0.5 * tone(95, 0.11) * env(len(n), 0.002, 45)
    return fade_edges(s)


def jump():
    d = 0.22
    f = np.linspace(260, 620, int(RATE * d))
    return fade_edges(tone(f, d, "soft") * env(len(f), 0.01, 9))


def land():
    d = 0.26
    f = np.linspace(150, 55, int(RATE * d))
    s = tone(f, d) * env(len(f), 0.002, 16)
    s += 0.35 * lowpass(noise(d), 900) * env(len(f), 0.001, 30)
    return fade_edges(s)


def impact():
    d = 0.42
    f = np.linspace(210, 48, int(RATE * d))
    s = tone(f, d, "tri") * env(len(f), 0.001, 10)
    s += 0.6 * lowpass(noise(d), 2200) * env(len(f), 0.001, 22)
    return fade_edges(np.tanh(1.6 * s))


def body_thud(pitch):
    d = 0.2
    f = np.linspace(pitch, pitch * 0.45, int(RATE * d))
    s = tone(f, d) * env(len(f), 0.002, 22)
    s += 0.3 * lowpass(noise(d), 700) * env(len(f), 0.001, 35)
    return fade_edges(s)


def splash():
    d = 1.1
    n = noise(d)
    body = lowpass(n, 2600) - 0.6 * lowpass(n, 350)
    e = np.clip(t(d) / 0.03, 0, 1) * np.exp(-3.6 * t(d))
    s = body * e
    plunge = tone(np.linspace(180, 60, int(RATE * 0.25)), 0.25) * env(int(RATE * 0.25), 0.002, 12)
    s[: len(plunge)] += 0.8 * plunge
    return fade_edges(s, 0.01)


def chime(notes, note_len, gap, decay=5.0, shape="soft"):
    total = gap * (len(notes) - 1) + note_len
    s = np.zeros(int(RATE * total) + 1)
    for i, f in enumerate(notes):
        seg = tone(f, note_len, shape) * env(int(RATE * note_len), 0.004, decay)
        start = int(RATE * gap * i)
        s[start:start + len(seg)] += seg
    return fade_edges(s, 0.01)


def blip(freq, seconds, decay):
    return fade_edges(tone(freq, seconds, "soft") * env(int(RATE * seconds), 0.002, decay))


NOTE = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(name):
    """'A4' -> 440.0, supports sharps ('F#3')."""
    octave = int(name[-1])
    semitone = NOTE[name[0]] + (1 if "#" in name else 0)
    return 440.0 * 2 ** ((semitone - 9) / 12 + (octave - 4))


def music(bpm, chords, melody, bright):
    """Seamless loop: one bar per chord, eighth-note arpeggio, bass on beats, simple lead."""
    beat = 60.0 / bpm
    bar = beat * 4
    total = bar * len(chords)
    n = int(round(RATE * total))
    s = np.zeros(n)

    def add(seg, start_time, gain):
        start = int(round(start_time * RATE)) % n
        end = start + len(seg)
        if end <= n:
            s[start:end] += gain * seg
        else:  # wrap the tail around so the loop point is seamless
            first = n - start
            s[start:] += gain * seg[:first]
            s[: end - n] += gain * seg[first:]

    for b, chord in enumerate(chords):
        root, third, fifth = (hz(x) for x in chord)
        t0 = b * bar
        for beat_index in range(4):
            bass = tone(root / 2, beat * 0.9, "tri") * env(int(RATE * beat * 0.9), 0.005, 5)
            add(bass, t0 + beat_index * beat, 0.5)
        pattern = [root, fifth, third * 2, fifth, root * 2, fifth, third * 2, fifth]
        for i, f in enumerate(pattern):
            pluck = tone(f, beat * 0.5, "soft") * env(int(RATE * beat * 0.5), 0.004, 9 if bright else 6)
            add(pluck, t0 + i * beat * 0.5, 0.28)
        if bright:
            for i in range(4):  # off-beat shaker
                sh = lowpass(noise(0.05), 6000) - lowpass(noise(0.05), 2500)
                add(sh * env(len(sh), 0.001, 60), t0 + (i + 0.5) * beat, 0.16)

    for start_beat, length_beats, name in melody:
        lead = tone(hz(name), beat * length_beats, "soft") * env(int(RATE * beat * length_beats), 0.01, 3.2 / length_beats)
        add(lead, start_beat * beat, 0.3)

    return s


def main():
    os.makedirs(OUT, exist_ok=True)
    write("sfx_footstep_01", footstep(1400), 0.6)
    write("sfx_footstep_02", footstep(1900), 0.6)
    write("sfx_jump", jump(), 0.7)
    write("sfx_land", land(), 0.8)
    write("sfx_obstacle_impact", impact(), 0.9)
    write("sfx_body_thud_01", body_thud(140), 0.75)
    write("sfx_body_thud_02", body_thud(110), 0.75)
    write("sfx_water_splash", splash(), 0.9)
    write("sfx_checkpoint", chime([hz("E5"), hz("A5")], 0.45, 0.12), 0.7)
    write("sfx_finish", chime([hz("C5"), hz("E5"), hz("G5"), hz("C6"), hz("E6")], 0.9, 0.13, decay=3.0), 0.8)
    write("sfx_ui_click", blip(880, 0.07, 45), 0.6)
    write("sfx_ui_hover", blip(1320, 0.04, 70), 0.35)
    write("sfx_countdown_tick", blip(660, 0.16, 16), 0.7)
    write("sfx_countdown_go", blip(990, 0.5, 6), 0.8)

    menu_chords = [("C4", "E4", "G4"), ("A3", "C4", "E4"), ("F3", "A3", "C4"), ("G3", "B3", "D4")]
    menu_melody = [(0, 2, "E5"), (2, 2, "G5"), (4, 3, "E5"), (8, 2, "C5"), (10, 2, "A4"), (12, 3, "D5")]
    write("music_menu_loop", music(96, menu_chords, menu_melody, bright=False), 0.5)

    course_chords = [("D4", "F#4", "A4"), ("G3", "B3", "D4"), ("B3", "D4", "F#4"), ("A3", "C#4", "E4")] * 2
    course_melody = [(0, 1, "F#5"), (1, 1, "A5"), (2, 2, "D6"), (4, 1, "B5"), (5, 1, "G5"), (6, 2, "D5"),
                     (8, 1, "F#5"), (9, 1, "B5"), (10, 2, "D6"), (12, 1, "C#6"), (13, 1, "A5"), (14, 2, "E5"),
                     (16, 1, "A5"), (17, 1, "F#5"), (18, 2, "D5"), (20, 1, "G5"), (21, 1, "B5"), (22, 2, "D6"),
                     (24, 2, "F#5"), (26, 2, "D5"), (28, 1, "E5"), (29, 1, "A5"), (30, 2, "C#6")]
    write("music_course_loop", music(132, course_chords, course_melody, bright=True), 0.5)


def arena_sounds():
    """Phase 3 additions. Appended after the original sounds so those stay byte-identical."""
    # Ball bounce: rubbery 'boing' with a pitch drop.
    d = 0.32
    f = np.concatenate([np.linspace(170, 320, int(RATE * 0.04)), np.linspace(320, 120, int(RATE * d) - int(RATE * 0.04))])
    s = tone(f, d, "soft") * env(len(f), 0.003, 9)
    s += 0.25 * lowpass(noise(d), 500) * env(len(f), 0.001, 30)
    write("sfx_ball_bounce", fade_edges(s), 0.8)

    # Mechanism whoosh: band of noise that swells and fades as an arm passes.
    d = 0.55
    n = noise(d)
    x = t(d)
    swell = np.sin(np.pi * x / d) ** 2
    body = lowpass(n, 1800) - lowpass(n, 300)
    write("sfx_mechanism_whoosh", fade_edges(body * swell, 0.02), 0.6)

    # Confetti pop.
    d = 0.5
    s = (lowpass(noise(d), 5000) - lowpass(noise(d), 900)) * env(int(RATE * d), 0.001, 14)
    s += tone(np.linspace(900, 300, int(RATE * d)), d) * env(int(RATE * d), 0.001, 30) * 0.6
    write("sfx_confetti_pop", fade_edges(s), 0.75)

    # Crowd cheer: broadband roar that swells, with a few whistle-like chirps.
    d = 3.2
    n = noise(d)
    x = t(d)
    roar = (lowpass(n, 2600) - lowpass(n, 350)) * (np.clip(x / 0.5, 0, 1) * np.exp(-0.75 * np.clip(x - 1.2, 0, None)))
    wobble = 0.75 + 0.25 * np.sin(2 * np.pi * 5.3 * x + np.sin(2 * np.pi * 0.7 * x) * 3)
    s = roar * wobble
    for start, f0 in [(0.35, 1900), (0.9, 2300), (1.5, 2050)]:
        seg = tone(np.linspace(f0, f0 * 1.35, int(RATE * 0.28)), 0.28) * env(int(RATE * 0.28), 0.03, 7) * 0.12
        i = int(RATE * start)
        s[i:i + len(seg)] += seg
    write("sfx_crowd_cheer", fade_edges(s, 0.03), 0.8)

    # Arena ambience loop: steady crowd murmur. Built from periodic modulation so the loop point is seamless.
    d = 8.0
    n = noise(d)
    x = t(d)
    murmur = lowpass(n, 1100) - lowpass(n, 180)
    mod = 0.7 + 0.15 * np.sin(2 * np.pi * x / d * 3) + 0.15 * np.sin(2 * np.pi * x / d * 7 + 1.1)
    s = murmur * mod
    k = int(RATE * 0.5)                      # crossfade the tail into the head
    ramp = np.linspace(0, 1, k)
    s[:k] = s[:k] * ramp + s[-k:] * (1 - ramp)
    write("amb_arena_crowd_loop", s[:-k], 0.35)


def cove_sounds():
    """Course 2 (Chaos Cove) additions. Appended after everything else so earlier sounds stay byte-identical."""
    # Spring bounce: a quick rising 'sproing' with a metallic flutter.
    d = 0.42
    n = int(RATE * d)
    f = np.linspace(180, 520, n) * (1 + 0.08 * np.sin(2 * np.pi * 34 * t(d)))
    s = tone(f, d, "soft") * env(n, 0.002, 8)
    s += 0.3 * tone(np.linspace(360, 1040, n), d) * env(n, 0.002, 14)
    write("sfx_spring_bounce", fade_edges(s), 0.8)

    # Water jet: a hissing burst that opens fast and tails off.
    d = 1.1
    x = t(d)
    nz = noise(d)
    body = lowpass(nz, 4200) - lowpass(nz, 700)
    shape = np.clip(x / 0.06, 0, 1) * np.exp(-1.6 * np.clip(x - 0.5, 0, None))
    write("sfx_water_jet", fade_edges(body * shape, 0.02), 0.7)

    # Bucket warning: two wooden creaks.
    d = 0.7
    n = int(RATE * d)
    x = t(d)
    creak = tone(np.linspace(240, 330, n) * (1 + 0.05 * np.sin(2 * np.pi * 23 * x)), d, "tri")
    gate = (np.sin(2 * np.pi * 2.9 * x) > 0).astype(float)
    write("sfx_bucket_creak", fade_edges(lowpass(creak * gate, 1500) * env(n, 0.01, 2.5), 0.01), 0.6)

    # Bucket dump: a heavy slosh of water hitting the deck.
    d = 1.3
    x = t(d)
    nz = noise(d)
    slosh = (lowpass(nz, 3000) - 0.5 * lowpass(nz, 250)) * np.clip(x / 0.12, 0, 1) * np.exp(-2.6 * x)
    thump = tone(np.linspace(130, 45, int(RATE * 0.35)), 0.35) * env(int(RATE * 0.35), 0.004, 9)
    slosh[int(RATE * 0.1):int(RATE * 0.1) + len(thump)] += thump * 0.8
    write("sfx_bucket_dump", fade_edges(slosh, 0.01), 0.9)

    # Slide whoosh: soft rushing water, swelling and fading so repeats overlap smoothly.
    d = 0.9
    x = t(d)
    nz = noise(d)
    write("sfx_slide_whoosh", fade_edges((lowpass(nz, 2400) - lowpass(nz, 500)) * np.sin(np.pi * x / d) ** 1.5, 0.03), 0.55)

    # Bridge crack: a short dry snap, the warning before a plank lets go.
    d = 0.22
    n = int(RATE * d)
    s = (lowpass(noise(d), 6000) - lowpass(noise(d), 1200)) * env(n, 0.0005, 40)
    s += tone(np.linspace(700, 260, n), d, "tri") * env(n, 0.001, 30) * 0.5
    write("sfx_bridge_crack", fade_edges(s), 0.7)

    # Bridge collapse: a falling tone with a rattle.
    d = 0.7
    n = int(RATE * d)
    s = tone(np.linspace(300, 70, n), d, "tri") * env(n, 0.003, 5)
    s += 0.4 * lowpass(noise(d), 1500) * env(n, 0.002, 7) * (0.6 + 0.4 * np.sin(2 * np.pi * 19 * t(d)))
    write("sfx_bridge_collapse", fade_edges(s), 0.75)

    # Inflatable boing: a big, low, rubbery thump for landing on a giant ball.
    d = 0.5
    n = int(RATE * d)
    f = np.concatenate([np.linspace(90, 210, int(RATE * 0.05)), np.linspace(210, 70, n - int(RATE * 0.05))])
    s = tone(f, d, "soft") * env(n, 0.004, 7) + 0.15 * lowpass(noise(d), 350) * env(n, 0.001, 20)
    write("sfx_inflatable_boing", fade_edges(s), 0.85)

    # Tropical water-park ambience loop: rushing water, slow swells and a few bird-like chirps. Seamless loop.
    d = 10.0
    x = t(d)
    nz = noise(d)
    water = (lowpass(nz, 3200) - lowpass(nz, 400)) * (0.55 + 0.12 * np.sin(2 * np.pi * x / d * 2) + 0.08 * np.sin(2 * np.pi * x / d * 5 + 0.7))
    surf = lowpass(noise(d), 500) * (0.25 + 0.25 * np.sin(2 * np.pi * x / d * 1 - 1.2) ** 2)
    s = water + surf
    for start, f0 in [(1.3, 2600), (1.55, 3100), (4.2, 2900), (6.8, 2400), (7.0, 2750), (7.2, 3200)]:
        seg = tone(np.linspace(f0, f0 * 1.25, int(RATE * 0.12)), 0.12) * env(int(RATE * 0.12), 0.02, 14) * 0.09
        i = int(RATE * start)
        s[i:i + len(seg)] += seg
    k = int(RATE * 0.6)
    ramp = np.linspace(0, 1, k)
    s[:k] = s[:k] * ramp + s[-k:] * (1 - ramp)
    write("amb_tropical_waterpark_loop", s[:-k], 0.35)


if __name__ == "__main__":
    main()
    arena_sounds()
    cove_sounds()
