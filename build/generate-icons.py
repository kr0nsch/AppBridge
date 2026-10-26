"""Regenerates AppBridge's icon files and installer artwork from build/icon.py.

Usage: python build/generate-icons.py   (requires Pillow)
"""
from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageFilter

sys.path.insert(0, str(Path(__file__).resolve().parent))
from icon import draw  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
APP_ASSETS = ROOT / "src" / "AppBridge" / "Assets"
INSTALLER = ROOT / "installer"
DOCS = ROOT / "assets"

ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]


def save_ico(path: Path) -> None:
    # Every size is drawn natively (the small sizes are pixel-fitted), not scaled down.
    frames = [draw(n) for n in ICO_SIZES]
    frames[-1].save(path, format="ICO", sizes=[(n, n) for n in ICO_SIZES], append_images=frames[:-1])


def installer_images() -> None:
    # Small image (top-right corner of every wizard page); Inno picks the best size for the DPI.
    for n in (55, 83, 110, 138):
        canvas = Image.new("RGBA", (n, n), (255, 255, 255, 255))
        canvas.alpha_composite(draw(n))
        canvas.convert("RGB").save(INSTALLER / f"wizard-small-{n}.bmp")
    # Large image (welcome and finish pages): the icon with a soft shadow on Apple-style light grey.
    for w, h in ((164, 314), (246, 471), (328, 628)):
        bg = Image.new("RGBA", (w, h))
        px = bg.load()
        for y in range(h):
            v = round(252 - 10 * y / (h - 1))
            for x in range(w):
                px[x, y] = (v, v, min(255, v + 3), 255)
        size = round(w * 0.62)
        pos = ((w - size) // 2, round(h * 0.30))
        icon = draw(size)
        shadow = Image.new("RGBA", (w, h), (6, 22, 92, 0))
        alpha = Image.new("L", (w, h), 0)
        alpha.paste(icon.getchannel("A").point(lambda a: a * 0.28), (pos[0], pos[1] + round(size * 0.05)))
        shadow.putalpha(alpha.filter(ImageFilter.GaussianBlur(size * 0.06)))
        bg = Image.alpha_composite(bg, shadow)
        bg.alpha_composite(icon, pos)
        bg.convert("RGB").save(INSTALLER / f"wizard-large-{w}.bmp")


def main() -> None:
    APP_ASSETS.mkdir(parents=True, exist_ok=True)
    DOCS.mkdir(parents=True, exist_ok=True)
    save_ico(APP_ASSETS / "AppBridge.ico")
    draw(256).save(APP_ASSETS / "AppIcon.png")
    draw(512).save(DOCS / "logo.png")
    draw(1024).save(DOCS / "logo-1024.png")
    installer_images()
    print("Icons written.")


if __name__ == "__main__":
    main()
