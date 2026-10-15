# /// script
# requires-python = ">=3.11"
# dependencies = ["numpy"]
# ///
"""Synthesise the game's sound effects (no purchased or recorded audio).

Every sound is built from noise bursts, decaying partials and simple filters
so it can be tweaked here and regenerated. Loops are an exact number of
cycles (or crossfaded noise) so they repeat without clicks.

Run: uv run tools/make_sounds.py
"""
import pathlib
import wave

import numpy as np

SR = 44100
OUT = pathlib.Path(__file__).resolve().parent.parent / "game" / "Assets" / "Art" / "Audio"
rng = np.random.default_rng(7)


def t(dur):
    return np.arange(int(dur * SR)) / SR


def env(dur, attack=0.001, decay=0.05):
    x = t(dur)
    a = np.clip(x / max(attack, 1e-6), 0, 1)
    return a * np.exp(-np.maximum(x - attack, 0) / decay)


def lowpass(x, cutoff):
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc
        y[i] = acc
    return y


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def partials(dur, freqs, decays, amps):
    x = t(dur)
    out = np.zeros_like(x)
    for f, d, a in zip(freqs, decays, amps):
        out += a * np.sin(2 * np.pi * f * x + rng.uniform(0, 2 * np.pi)) * np.exp(-x / d)
    return out


def noise_burst(dur, decay, hp=0, lp=None):
    n = rng.standard_normal(int(dur * SR))
    if lp:
        n = lowpass(n, lp)
    if hp:
        n = highpass(n, hp)
    return n * env(dur, 0.0003, decay)


def mix(dur, *parts):
    out = np.zeros(int(dur * SR))
    for offset, sig in parts:
        i = int(offset * SR)
        n = min(len(sig), len(out) - i)
        out[i:i + n] += sig[:n]
    return out


def save(name, x, peak=0.9):
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    fade = min(len(x), int(0.004 * SR))
    if not name.endswith("_loop"):
        x[-fade:] *= np.linspace(1, 0, fade)
    OUT.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUT / f"{name}.wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())
    print(f"{name}.wav  {len(x) / SR:.2f}s")


def click(dur, metal_hz, body_hz, bright=1.0):
    return mix(dur,
               (0, noise_burst(0.01, 0.0012, hp=2500) * bright),
               (0, partials(dur, [metal_hz, metal_hz * 1.58, metal_hz * 2.31], [0.012, 0.008, 0.005], [0.5, 0.3, 0.2])),
               (0, partials(dur, [body_hz], [0.018], [0.6])))


def loop_noise(dur, lp, hp=0):
    n = rng.standard_normal(int(dur * SR) + int(0.5 * SR))
    n = lowpass(n, lp)
    if hp:
        n = highpass(n, hp)
    xf = int(0.5 * SR)
    body, tail = n[:-xf], n[-xf:]
    ramp = np.linspace(0, 1, xf)
    body[:xf] = body[:xf] * ramp + tail * (1 - ramp)
    return body


def mains(dur, base, harmonics):
    x = t(dur)
    return sum(a * np.sin(2 * np.pi * base * k * x) for k, a in harmonics)


def main():
    # toggle switches: two-stage snap, "on" slightly brighter
    save("toggle_on", mix(0.08, (0, click(0.08, 4200, 210)), (0.006, 0.4 * click(0.05, 5200, 260))))
    save("toggle_off", mix(0.08, (0, click(0.08, 3600, 180)), (0.005, 0.35 * click(0.05, 4400, 220))))
    # big button: plastic thunk down, lighter return
    save("button_down", mix(0.15, (0, noise_burst(0.12, 0.012, lp=1800) * 1.4),
                            (0, partials(0.15, [140, 310, 720], [0.04, 0.025, 0.012], [1.0, 0.5, 0.25]))))
    save("button_up", mix(0.1, (0, noise_burst(0.06, 0.004, hp=800) * 0.7),
                          (0, partials(0.1, [380, 950], [0.02, 0.01], [0.6, 0.3]))))
    # selector detent: tiny tick
    save("knob_detent", mix(0.05, (0, noise_burst(0.02, 0.0008, hp=3000)),
                            (0, partials(0.05, [2900, 6100], [0.006, 0.003], [0.6, 0.3]))))
    # knife switch: heavy copper clunk plus contact sparks
    sparks = np.zeros(int(0.12 * SR))
    for _ in range(14):
        i = rng.integers(0, len(sparks) - 400)
        sparks[i:i + 400] += noise_burst(400 / SR, 0.0015, hp=4000) * rng.uniform(0.3, 1.0)
    save("knife_close", mix(0.6, (0, noise_burst(0.05, 0.006, lp=3000) * 1.5),
                            (0, partials(0.6, [190, 487, 1031, 2213], [0.12, 0.09, 0.05, 0.03], [1.0, 0.6, 0.4, 0.25])),
                            (0.01, sparks * 0.8)))
    save("knife_open", mix(0.45, (0, noise_burst(0.15, 0.04, hp=1500, lp=6000) * 0.5),
                           (0.03, partials(0.4, [240, 611, 1290], [0.08, 0.05, 0.03], [0.8, 0.5, 0.3])),
                           (0.03, noise_burst(0.03, 0.004, lp=2500))))
    # desk lamp push switch
    save("lamp_click", mix(0.08, (0, click(0.08, 3000, 160, bright=0.8))))
    # hanging tools: free-bar modes (1 : 2.76 : 5.40 : 8.93)
    for name, f0, dec in (("clink_large", 820, 0.35), ("clink_small", 1250, 0.25), ("clink_hammer", 610, 0.2)):
        save(name, mix(0.8, (0, noise_burst(0.01, 0.001, hp=2000) * 0.6),
                       (0, partials(0.8, [f0, f0 * 2.76, f0 * 5.40, f0 * 8.93], [dec, dec * 0.6, dec * 0.35, dec * 0.2],
                                    [1.0, 0.55, 0.3, 0.15]))))
    # screwdrivers rattling in the rack
    save("rattle_plastic", mix(0.2, *[(i * 0.035, noise_burst(0.03, 0.003, lp=2500) * (1 - i * 0.18)) for i in range(5)],
                               (0, partials(0.2, [700, 1650], [0.03, 0.02], [0.4, 0.2]))))
    # padlock: a few metallic knocks against the hasp
    knocks = [(i * rng.uniform(0.03, 0.05), (0.8 ** i) * mix(0.12, (0, noise_burst(0.01, 0.001, hp=1500)),
                                                            (0, partials(0.12, [1900, 3300, 5100], [0.04, 0.025, 0.015],
                                                                         [0.6, 0.4, 0.2])))) for i in range(6)]
    save("padlock_rattle", mix(0.45, *[(sum(k[0] for k in knocks[:j + 1]), k[1]) for j, k in enumerate(knocks)]))
    # mug: ceramic tink
    save("mug_tink", mix(0.5, (0, noise_burst(0.01, 0.0008, hp=3000) * 0.5),
                         (0, partials(0.5, [2150, 4380, 6020], [0.18, 0.09, 0.05], [1.0, 0.5, 0.25]))))
    # gauge glass tap
    save("glass_tap", mix(0.25, (0, noise_burst(0.01, 0.0006, hp=4000) * 0.6),
                          (0, partials(0.25, [3100, 5300, 7900], [0.06, 0.04, 0.02], [0.8, 0.4, 0.2]))))
    # loops: mains hum while powered, harsh buzz while overloaded, room tone, fluorescent tube
    save("hum_loop", mains(2.0, 50, [(1, 0.5), (2, 1.0), (3, 0.35), (4, 0.25), (6, 0.08)]) + 0.02 * loop_noise(2.0, 400)[:int(2.0 * SR)], peak=0.6)
    buzz = np.sign(mains(1.0, 100, [(1, 1.0)])) * 0.5 + mains(1.0, 100, [(2, 0.4), (3, 0.3), (5, 0.2)])
    save("buzz_loop", lowpass(buzz, 3500) + 0.05 * loop_noise(1.0, 6000, 1000)[:SR], peak=0.6)
    save("roomtone_loop", loop_noise(8.0, 300) + 0.15 * loop_noise(8.0, 2500, 600), peak=0.5)
    save("fluoro_loop", mains(2.0, 100, [(1, 1.0), (2, 0.5), (3, 0.3), (4, 0.1)]) * 0.6 + 0.05 * loop_noise(2.0, 8000, 2000), peak=0.5)


if __name__ == "__main__":
    main()
