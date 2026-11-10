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


if __name__ == "__main__":
    main()
