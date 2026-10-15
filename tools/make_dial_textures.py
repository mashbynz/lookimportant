# /// script
# requires-python = ">=3.11"
# dependencies = ["pillow"]
# ///
"""Draw the printed faces for the pegboard gauge and the bench selector dial.

Numbers and tick marks only - the game has no written instructions.
The angle conventions here must match the Gauge and RotaryKnob scripts:
angles are degrees clockwise from 12 o'clock.

Run: uv run tools/make_dial_textures.py
"""
import math
import pathlib

from PIL import Image, ImageDraw, ImageFont

ROOT = pathlib.Path(__file__).resolve().parent.parent
OUT = ROOT / "game" / "Assets" / "Art" / "Textures"
SIZE = 1024
INK = (24, 22, 20, 255)
CREAM = (232, 224, 200, 255)
RED = (190, 30, 20, 255)

GAUGE_SWEEP = (-135.0, 135.0)   # value 0 .. 1
SELECTOR_ANGLES = [-60, -30, 0, 30, 60]


def polar(cx, cy, r, deg):
    a = math.radians(deg)
    return cx + r * math.sin(a), cy - r * math.cos(a)


def font(px):
    return ImageFont.load_default(size=px)


def gauge_dial():
    img = Image.new("RGBA", (SIZE, SIZE), CREAM)
    d = ImageDraw.Draw(img)
    c = SIZE / 2
    lo, hi = GAUGE_SWEEP
    # red zone, last fifth of the sweep
    r0, r1 = SIZE * 0.40, SIZE * 0.44
    for i in range(200):
        t = 0.8 + 0.2 * i / 199
        a = lo + (hi - lo) * t
        d.line([polar(c, c, r0, a), polar(c, c, r1, a)], fill=RED, width=6)
    # ticks
    for i in range(51):
        a = lo + (hi - lo) * i / 50
        major = i % 5 == 0
        inner = SIZE * (0.355 if major else 0.385)
        d.line([polar(c, c, inner, a), polar(c, c, SIZE * 0.445, a)],
               fill=INK, width=10 if major else 4)
        if major:
            x, y = polar(c, c, SIZE * 0.29, a)
            d.text((x, y), str(i // 5), font=font(64), fill=INK, anchor="mm")
    # outer printed ring + small maker's mark (a circle, not words)
    d.ellipse([c - SIZE * 0.47, c - SIZE * 0.47, c + SIZE * 0.47, c + SIZE * 0.47], outline=INK, width=6)
    d.ellipse([c - 34, c + 150, c + 34, c + 218], outline=INK, width=6)
    d.line([c - 22, c + 184, c + 22, c + 184], fill=INK, width=6)
    return img


def selector_scale():
    img = Image.new("RGBA", (SIZE, SIZE), CREAM)
    d = ImageDraw.Draw(img)
    c = SIZE / 2
    # thin arc behind the detent marks
    box = [c - SIZE * 0.36, c - SIZE * 0.36, c + SIZE * 0.36, c + SIZE * 0.36]
    d.arc(box, start=-90 + SELECTOR_ANGLES[0], end=-90 + SELECTOR_ANGLES[-1], fill=INK, width=8)
    for i, a in enumerate(SELECTOR_ANGLES):
        d.line([polar(c, c, SIZE * 0.32, a), polar(c, c, SIZE * 0.40, a)], fill=INK, width=14)
        x, y = polar(c, c, SIZE * 0.45, a)
        d.text((x, y), str(i + 1), font=font(78), fill=INK, anchor="mm")
    # border
    d.rectangle([12, 12, SIZE - 12, SIZE - 12], outline=INK, width=10)
    return img


def main():
    for name, img in (("gauge_dial", gauge_dial()), ("dial_scale", selector_scale())):
        d = OUT / name
        d.mkdir(parents=True, exist_ok=True)
        img.save(d / f"{name}_albedo.png", optimize=True)
        print(name)


if __name__ == "__main__":
    main()
