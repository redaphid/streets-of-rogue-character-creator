#!/usr/bin/env python3
"""Draw a purple pointy WIZARD HAT as 5 directional pixel sprites for SoR's
head/hat (armor-head) layer: WizardHat{S,SE,E,NE,N}. The game renders
`<ItemName><dir>` on the head every frame and flips the whole agent for the
left-facing directions, so N/NE/E/SE/S cover all 8 facings.

Sprites are sized/anchored to sit on the head like a vanilla headpiece
(center-pivot; brim a few px below the sprite centre, cone rising above).
"""
import os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "characters", "wizard", "assets", "headpiece")

W, H = 28, 40
PURPLE = (94, 0, 148, 255)
PURPLE_D = (60, 0, 100, 255)     # shaded side
GOLD = (224, 185, 58, 255)
STAR = (255, 228, 92, 255)
OUTLINE = (24, 0, 40, 255)

# Vertical layout. The sprite is CENTRE-pivoted at the head attach point, so any
# content ABOVE the centre (y=H/2=20) renders above the head. Keep the whole hat
# in the TOP HALF: brim just above centre (sits at the hairline), cone rising from
# there, empty padding below so it doesn't spill over the face.
TIP_Y, BASE_Y, BRIM_Y = 2, 17, 18


def base(draw_star, lean=0, dark_side=None, brim_w=13):
    """A pointy hat. lean shifts the tip horizontally (for 3/4 + side views);
    dark_side shades one half; brim_w is half the brim ellipse width (front wide,
    side foreshortened)."""
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx = W // 2
    tip = (cx + lean, TIP_Y)
    # cone body (filled triangle tip -> base corners), with outline
    left = (cx - 10, BASE_Y)
    right = (cx + 10, BASE_Y)
    d.polygon([tip, left, right], fill=PURPLE, outline=OUTLINE)
    if dark_side == "L":
        d.polygon([tip, left, (cx, BASE_Y)], fill=PURPLE_D)
    elif dark_side == "R":
        d.polygon([tip, right, (cx, BASE_Y)], fill=PURPLE_D)
    # gold band near the base
    d.rectangle([cx - 10, BASE_Y - 5, cx + 10, BASE_Y - 2], fill=GOLD, outline=OUTLINE)
    # brim ellipse
    d.ellipse([cx - brim_w, BRIM_Y - 3, cx + brim_w, BRIM_Y + 3], fill=PURPLE, outline=OUTLINE)
    # re-draw cone outline edges over brim so the cone reads in front
    d.line([tip, left], fill=OUTLINE)
    d.line([tip, right], fill=OUTLINE)
    if draw_star:
        sx, sy = cx, 10
        for dx, dy in [(0, -2), (0, 2), (-2, 0), (2, 0), (-1, -1), (1, -1), (-1, 1), (1, 1)]:
            img.putpixel((sx + dx, sy + dy), STAR)
        img.putpixel((sx, sy), STAR)
    return img


def main():
    os.makedirs(OUT, exist_ok=True)
    variants = {
        "S":  base(True,  lean=0,  dark_side=None, brim_w=13),  # front, star, wide brim
        "SE": base(True,  lean=2,  dark_side="R",  brim_w=12),  # front-right 3/4
        "E":  base(False, lean=3,  dark_side="R",  brim_w=8),   # side profile, foreshortened brim
        "NE": base(False, lean=2,  dark_side="R",  brim_w=11),  # back-right 3/4
        "N":  base(False, lean=0,  dark_side=None, brim_w=13),  # back, no star
    }
    for d, im in variants.items():
        p = os.path.join(OUT, f"WizardHat{d}.png")
        im.save(p)
        print("wrote", os.path.relpath(p, HERE))


if __name__ == "__main__":
    main()
