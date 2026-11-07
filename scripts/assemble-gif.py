"""Joins the PNG frames written by `WindowsIsland.exe --frames <folder>` into the README animation.

Usage: python scripts/assemble-gif.py <frames folder> <output.gif>
Needs Python 3 with Pillow (pip install pillow). Only used when the README animation is regenerated.
"""
import glob
import os
import sys

from PIL import Image

FPS = 20


def main():
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    folder, output = sys.argv[1], sys.argv[2]
    files = sorted(glob.glob(os.path.join(folder, "frame*.png")))
    if not files:
        sys.exit("No frames found in " + folder)

    # Each frame gets its own 256-color palette: the backdrop gradient takes many shades, and a shared
    # palette would wash out the blue rings and the Codex icon.
    frames = [Image.open(f).convert("RGB").quantize(colors=256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE) for f in files]
    frames[0].save(
        output,
        save_all=True,
        append_images=frames[1:],
        duration=int(1000 / FPS),
        loop=0,
        optimize=True,
        disposal=1,
    )
    print("%s: %d frames, %.1f MB" % (output, len(frames), os.path.getsize(output) / 1048576))


main()
