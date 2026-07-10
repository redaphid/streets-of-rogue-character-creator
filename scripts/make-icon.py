#!/usr/bin/env python3
"""Generate a simple 64x64 ability icon PNG for a custom character.

It's a rounded badge in a chosen color with a bold letter and a few sparkles -
a clean placeholder the kids can keep or replace with their own art. Everything
is drawn from scratch (no third-party images), matching the "no ripped assets"
rule the mods follow.

Usage:
    scripts/make-icon.py OUT.png --letter W --color "#5E0094"
    scripts/make-icon.py characters/wizard/assets/ability.png -l F -c "#ff6a00"
"""
import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def hex_to_rgb(s: str):
    s = s.lstrip("#")
    if len(s) == 3:
        s = "".join(c * 2 for c in s)
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))


def blend(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


def load_font(size):
    for name in ("DejaVuSans-Bold.ttf", "Arial Bold.ttf", "arialbd.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except Exception:
            continue
    return ImageFont.load_default()


def make(out: Path, letter: str, color: str):
    N = 64
    base = hex_to_rgb(color)
    light = blend(base, (255, 255, 255), 0.45)
    dark = blend(base, (0, 0, 0), 0.35)

    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # Rounded badge with a subtle top-to-bottom shade (drawn as rows).
    pad = 6
    radius = 16
    for y in range(pad, N - pad):
        t = (y - pad) / (N - 2 * pad)
        row = blend(light, dark, t)
        d.rounded_rectangle([pad, y, N - pad, y + 1], radius=radius, fill=row + (255,))
    # Crisp outline.
    d.rounded_rectangle([pad, pad, N - pad - 1, N - pad - 1], radius=radius,
                        outline=dark + (255,), width=2)

    # Big centered letter.
    ch = (letter or "?").strip()[:1].upper() or "?"
    font = load_font(38)
    bbox = d.textbbox((0, 0), ch, font=font)
    tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
    tx = (N - tw) / 2 - bbox[0]
    ty = (N - th) / 2 - bbox[1]
    d.text((tx + 1, ty + 2), ch, font=font, fill=dark + (180,))   # shadow
    d.text((tx, ty), ch, font=font, fill=(255, 255, 255, 255))    # letter

    # A couple of sparkles for flavor.
    for (sx, sy, r) in ((14, 15, 2), (50, 20, 1), (48, 47, 2)):
        d.ellipse([sx - r, sy - r, sx + r, sy + r], fill=(255, 255, 255, 230))

    out.parent.mkdir(parents=True, exist_ok=True)
    img.save(out)
    print(f"wrote {out} ({letter!r}, {color})")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out", type=Path)
    ap.add_argument("-l", "--letter", default="?")
    ap.add_argument("-c", "--color", default="#5E0094")
    a = ap.parse_args()
    make(a.out, a.letter, a.color)


if __name__ == "__main__":
    main()
