"""Regenerates FreeReturn's icon and installer artwork.

Usage: python build/generate-icons.py   (requires Pillow)
Everything is drawn at high resolution and downsampled, so all sizes stay crisp.
"""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
APP_ASSETS = ROOT / "src" / "FreeReturn" / "Assets"
INSTALLER = ROOT / "installer"
DOCS = ROOT / "assets"

TOP = (72, 136, 255)
BOTTOM = (98, 60, 234)
WHITE = (255, 255, 255, 255)


def gradient(size: int, top=TOP, bottom=BOTTOM) -> Image.Image:
    """Diagonal gradient from top-left to bottom-right."""
    base = Image.new("RGBA", (size, size))
    px = base.load()
    for y in range(size):
        for x in range(size):
            t = (x + y) / (2 * (size - 1))
            px[x, y] = tuple(round(a + (b - a) * t) for a, b in zip(top, bottom)) + (255,)
    return base


def tile(size: int = 1024, bold: bool = False) -> Image.Image:
    """The app icon: rounded tile, white phone outline and a download arrow inside it."""
    s = size
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))

    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, s - 1, s - 1), radius=round(s * 0.23), fill=255)
    img.paste(gradient(256).resize((s, s), Image.BICUBIC), (0, 0), mask)

    # Soft highlight on the upper half for depth.
    gloss = Image.new("L", (s, s), 0)
    ImageDraw.Draw(gloss).ellipse((-s * 0.3, -s * 0.9, s * 1.3, s * 0.45), fill=26)
    gloss = gloss.filter(ImageFilter.GaussianBlur(s * 0.09))
    white_layer = Image.new("RGBA", (s, s), (255, 255, 255, 0))
    white_layer.putalpha(Image.composite(gloss, Image.new("L", (s, s), 0), mask))
    img = Image.alpha_composite(img, white_layer)

    d = ImageDraw.Draw(img)
    stroke = round(s * (0.085 if bold else 0.062))
    pw, ph = s * 0.42, s * 0.66
    left, top = (s - pw) / 2, (s - ph) / 2
    d.rounded_rectangle((left, top, left + pw, top + ph), radius=round(s * 0.09), outline=WHITE, width=stroke)

    # Dynamic-island style pill at the top of the phone.
    if not bold:
        pill_w, pill_h = s * 0.11, s * 0.035
        d.rounded_rectangle((s / 2 - pill_w / 2, top + s * 0.06, s / 2 + pill_w / 2, top + s * 0.06 + pill_h),
                            radius=round(pill_h / 2), fill=WHITE)

    # Arrow pointing down into a tray: "install".
    cx = s / 2
    shaft_w = s * (0.075 if bold else 0.06)
    a_top, a_tip = top + ph * (0.30 if not bold else 0.24), top + ph * 0.70
    head_w, head_h = s * (0.24 if bold else 0.2), s * (0.13 if bold else 0.11)
    d.rectangle((cx - shaft_w / 2, a_top, cx + shaft_w / 2, a_tip - head_h + 2), fill=WHITE)
    d.polygon([(cx - head_w / 2, a_tip - head_h), (cx + head_w / 2, a_tip - head_h), (cx, a_tip)], fill=WHITE)
    tray_y = top + ph * 0.80
    d.rounded_rectangle((cx - s * 0.11, tray_y, cx + s * 0.11, tray_y + stroke * 0.9), radius=round(stroke * 0.45), fill=WHITE)
    return img


def save_ico(path: Path) -> None:
    sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
    big, bold = tile(1024), tile(1024, bold=True)
    frames = [(bold if n <= 32 else big).resize((n, n), Image.LANCZOS) for n in sizes]
    frames[-1].save(path, format="ICO", sizes=[(n, n) for n in sizes], append_images=frames[:-1])


def installer_images() -> None:
    icon = tile(1024)
    # Small image (top-right of wizard pages); Inno picks the best match for the DPI.
    for n in (55, 83, 110, 138):
        canvas = Image.new("RGB", (n, n), (255, 255, 255))
        canvas.paste(icon.resize((n, n), Image.LANCZOS), (0, 0), icon.resize((n, n), Image.LANCZOS))
        canvas.save(INSTALLER / f"wizard-small-{n}.bmp")
    # Large image (welcome and finish pages).
    for w, h in ((164, 314), (246, 471), (328, 628)):
        bg = gradient(256).resize((w, h), Image.BICUBIC)
        logo_size = round(w * 0.62)
        logo = icon.resize((logo_size, logo_size), Image.LANCZOS)
        # Draw a white rounded halo so the tile reads on the gradient.
        halo = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        ImageDraw.Draw(halo).rounded_rectangle(
            ((w - logo_size) / 2 - w * 0.03, h * 0.3 - w * 0.03, (w + logo_size) / 2 + w * 0.03, h * 0.3 + logo_size + w * 0.03),
            radius=round(logo_size * 0.26), fill=(255, 255, 255, 60))
        bg = Image.alpha_composite(bg, halo)
        bg.paste(logo, (round((w - logo_size) / 2), round(h * 0.3)), logo)
        bg.convert("RGB").save(INSTALLER / f"wizard-large-{w}.bmp")


def main() -> None:
    APP_ASSETS.mkdir(parents=True, exist_ok=True)
    INSTALLER.mkdir(parents=True, exist_ok=True)
    DOCS.mkdir(parents=True, exist_ok=True)
    save_ico(APP_ASSETS / "FreeReturn.ico")
    tile(1024).resize((256, 256), Image.LANCZOS).save(APP_ASSETS / "AppIcon.png")
    tile(1024).resize((512, 512), Image.LANCZOS).save(DOCS / "logo.png")
    installer_images()
    print("Icons written.")


if __name__ == "__main__":
    main()
