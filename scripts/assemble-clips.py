"""Joins the PNG frames written by `WindowsIsland.exe --clips <folder>` into one small GIF per state.

Usage: python scripts/assemble-clips.py <clips folder> <output folder> <language>
Each sub-folder of the clips folder becomes <output folder>/<name>-<language>.gif.
Needs Python 3 with Pillow (pip install pillow). Only used when the README animations are regenerated.
"""
import glob
import os
import sys

from PIL import Image

FPS = 20


def main():
    if len(sys.argv) != 4:
        sys.exit(__doc__)
    clips, output, lang = sys.argv[1], sys.argv[2], sys.argv[3]
    total = 0
    for folder in sorted(glob.glob(os.path.join(clips, "*"))):
        files = sorted(glob.glob(os.path.join(folder, "frame*.png")))
        if not files:
            continue
        # Each frame gets its own 256-color palette: the backdrop gradient takes many shades
        frames = [Image.open(f).convert("RGB").quantize(colors=256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE) for f in files]
        name = os.path.basename(folder)
        target = os.path.join(output, "%s-%s.gif" % (name, lang))
        frames[0].save(target, save_all=True, append_images=frames[1:], duration=int(1000 / FPS), loop=0, optimize=True, disposal=1)
        size = os.path.getsize(target)
        total += size
        print("%s: %d frames, %.0f KB" % (os.path.basename(target), len(frames), size / 1024))
    print("Total %.1f MB" % (total / 1048576))


main()
