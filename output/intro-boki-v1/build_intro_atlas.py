#!/usr/bin/env python3
"""Derives the intro-only Boki atlas from the shared player atlas.

The player atlas paints Boki's tired eyes, eyebrows and mouth into the face tile. The intro
Boki animates those as separate 3D parts (see Assets/Scripts/Intro/IntroBoki.cs), so this copy
blanks the painted features: the eye whites become the dark eye-bag colour (the socket the 3D
eyes sit in) and the eyebrows, mouth and chin line become skin. Everything else (bags, nose
shading, stubble, ears, jacket, trousers, watch) is untouched, so the intro model still reads as
the same Boki.

Usage: python3 output/intro-boki-v1/build_intro_atlas.py
Requires Pillow.
"""
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "output/player-model-v1/Textures/T_Boki_BaseColor.png"
TARGET = ROOT / "Game/Assets/Art/Intro/T_Boki_Intro_BaseColor.png"

TILE = 128
FACE_TILE = 12  # painted face island, same numbering as build_player.py
SKIN_TILE = 4   # plain skin: same base colour and noise as the face tile
SOCKET = (0x93, 0x73, 0x7B)  # eye-bag colour from build_player.py

# Eye whites, iris and pupil sit in these rectangles (face-tile UV: x0, y0, x1, y1, v from the bottom).
EYE_WHITES = [(0.18, 0.54, 0.42, 0.61), (0.58, 0.54, 0.82, 0.61)]

# Colours build_player.py paints for the brows, mouth line, lip highlight and chin line. Matching by
# colour leaves the stubble pixels, which share the same rows, untouched.
PAINTED = {(0x34, 0x28, 0x20), (0x70, 0x5B, 0x57), (0xAD, 0x8E, 0x7C), (0x51, 0x49, 0x40)}


def tile_origin(tile):
    """Top-left pixel of a tile. The atlas is authored bottom-up, PNG rows run top-down."""
    atlas_rows = 4
    return (tile % 4) * TILE, (atlas_rows - 1 - tile // 4) * TILE


def box(rect):
    x0, y0, x1, y1 = rect
    return int(x0 * TILE), int(TILE - y1 * TILE), int(x1 * TILE), int(TILE - y0 * TILE)


def main():
    atlas = Image.open(SOURCE).convert("RGB")
    face_x, face_y = tile_origin(FACE_TILE)
    skin_x, skin_y = tile_origin(SKIN_TILE)
    skin = atlas.crop((skin_x, skin_y, skin_x + TILE, skin_y + TILE))

    for rect in EYE_WHITES:
        left, top, right, bottom = box(rect)
        atlas.paste(Image.new("RGB", (right - left, bottom - top), SOCKET), (face_x + left, face_y + top))
    for y in range(TILE):
        for x in range(TILE):
            if atlas.getpixel((face_x + x, face_y + y)) in PAINTED:
                atlas.putpixel((face_x + x, face_y + y), skin.getpixel((x, y)))

    TARGET.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(TARGET)
    print(f"Wrote {TARGET.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
