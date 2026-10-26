#!/usr/bin/env python3
"""FreeReturn README hero banners and GitHub social previews.

Writes to docs/media:

    banner-ru-light.png  banner-ru-dark.png     1600 x 720  README hero
    banner-en-light.png  banner-en-dark.png
    social-preview-ru.png  social-preview-en.png 1280 x 640  GitHub social card

Pillow only.  Inputs:

* the app icon, drawn by build/icon.py  (draw(size) -> RGBA)
* the real window captures in docs/screenshots/<lang>-<theme>/
* Segoe UI Variable (C:\\Windows\\Fonts\\SegUIVar.ttf)

Usage:  python build/generate-readme-art.py [--repo PATH] [--out DIR]
"""
from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont, ImageMath

DEFAULT_REPO = Path(__file__).resolve().parent.parent
FONT_PATH = r"C:\Windows\Fonts\SegUIVar.ttf"

SS = 4  # supersampling factor for masks (rounded corners, hairlines)

# --------------------------------------------------------------------------
# copy
# --------------------------------------------------------------------------
COPY = {
    "ru": dict(
        tagline="Перенос приложений из App Store на другой iPhone",
        tagline_lines=["Перенос приложений", "из App Store на другой iPhone"],
        meta="Windows 10 и 11 · Бесплатно · Открытый исходный код",
    ),
    "en": dict(
        tagline="Move your App Store apps to another iPhone",
        tagline_lines=["Move your App Store apps", "to another iPhone"],
        meta="Windows 10 & 11 · Free · Open source",
    ),
}

# --------------------------------------------------------------------------
# palettes (Apple system colours)
# --------------------------------------------------------------------------
THEMES = {
    "light": dict(
        bg_top=(255, 255, 255),
        bg_bottom=(240, 240, 243),
        # (centre x, centre y, radius x, radius y, colour, peak alpha) in 1600x720 units
        glows=[
            (1180, 400, 660, 430, (0, 122, 255), 0.14),
            (1420, 170, 420, 300, (88, 86, 214), 0.07),
            (230, 250, 380, 300, (90, 200, 250), 0.05),
        ],
        title=(29, 29, 31),
        tagline=(66, 66, 69),
        meta=(110, 110, 115),
        border=(0, 0, 0, 34),
        shadow=(18, 28, 60),
        # (blur radius, y offset, alpha) for the front window
        shadow_layers=[(46, 34, 0.20), (14, 10, 0.10), (3, 1, 0.06)],
        back_shadow_layers=[(36, 24, 0.13), (10, 6, 0.07)],
        back_veil=(245, 245, 247, 40),
        icon_shadow=((10, 30, 110), 0.22, 18, 10),
    ),
    "dark": dict(
        bg_top=(11, 11, 12),
        bg_bottom=(26, 26, 29),
        glows=[
            (1180, 400, 660, 440, (88, 86, 214), 0.30),
            (1000, 560, 520, 300, (0, 122, 255), 0.16),
            (1440, 150, 380, 260, (94, 92, 230), 0.12),
            (230, 250, 380, 300, (0, 122, 255), 0.06),
        ],
        title=(245, 245, 247),
        tagline=(210, 210, 215),
        meta=(142, 142, 147),
        border=(255, 255, 255, 38),
        shadow=(0, 0, 0),
        shadow_layers=[(50, 34, 0.62), (14, 10, 0.40), (3, 1, 0.30)],
        back_shadow_layers=[(36, 24, 0.50), (10, 6, 0.30)],
        back_veil=(11, 11, 12, 70),
        icon_shadow=((0, 0, 0), 0.55, 20, 10),
    ),
}

# --------------------------------------------------------------------------
# helpers
# --------------------------------------------------------------------------
_font_cache: dict = {}


def font(px: float, weight: int = 400, opsz: float | None = None) -> ImageFont.FreeTypeFont:
    """Segoe UI Variable at `px` pixels with explicit weight and optical size."""
    if opsz is None:
        opsz = px * 0.75  # px -> pt
    opsz = max(5.0, min(36.0, opsz))
    key = (px, weight, opsz)
    if key not in _font_cache:
        f = ImageFont.truetype(FONT_PATH, size=round(px))
        f.set_variation_by_axes([weight, opsz])
        _font_cache[key] = f
    return _font_cache[key]


def ink_left(f: ImageFont.FreeTypeFont, text: str) -> float:
    return f.getbbox(text, anchor="ls")[0]


def cap_height(f: ImageFont.FreeTypeFont) -> float:
    return -f.getbbox("H", anchor="ls")[1]


def text_width(f, text: str, tracking: float = 0.0) -> float:
    if not tracking:
        return f.getbbox(text, anchor="ls")[2] - ink_left(f, text)
    xs = _char_positions(f, text, tracking)
    last = f.getbbox(text[-1], anchor="ls")[2]
    return xs[-1] + last - ink_left(f, text)


def _char_positions(f, text, tracking):
    return [f.getlength(text[:i]) + i * tracking for i in range(len(text))]


def draw_text(d: ImageDraw.ImageDraw, x: float, baseline: float, text: str, f, fill, tracking: float = 0.0):
    """Draw `text` so that its ink starts exactly at x, sitting on `baseline`."""
    x0 = x - ink_left(f, text)
    if not tracking:
        d.text((x0, baseline), text, font=f, fill=fill, anchor="ls")
        return
    for ch, cx in zip(text, _char_positions(f, text, tracking)):
        d.text((x0 + cx, baseline), ch, font=f, fill=fill, anchor="ls")


def _bayer(n: int = 8):
    """n x n Bayer matrix with values 0 .. n*n-1 (n a power of two)."""
    m = [[0]]
    while len(m) < n:
        k = len(m)
        quad = (0, 2, 3, 1)  # top-left, top-right, bottom-left, bottom-right
        m = [[4 * m[y % k][x % k] + quad[(y // k) * 2 + (x // k)] for x in range(2 * k)] for y in range(2 * k)]
    return m


_dither_cache: dict = {}


def dither_field(size) -> Image.Image:
    """Ordered-dither thresholds in [0, 1) as a float image (8x8 Bayer)."""
    if size not in _dither_cache:
        W, H = size
        b = _bayer(8)
        rows = [[(b[y][x] + 0.5) / 64 for x in range(8)] * (W // 8 + 1) for y in range(8)]
        data = []
        for y in range(H):
            data.extend(rows[y % 8][:W])
        f = Image.new("F", size)
        f.putdata(data)
        _dither_cache[size] = f
    return _dither_cache[size]


def background(size, theme, glows=None, step=4) -> Image.Image:
    """Vertical gradient + soft elliptical glows, computed in float and
    ordered-dithered to 8 bit so the dark gradients do not band."""
    W, H = size
    w, h = W // step, H // step
    top, bot = theme["bg_top"], theme["bg_bottom"]
    glows = theme["glows"] if glows is None else glows
    e0 = math.exp(-3.2)
    chans = ([], [], [])
    for j in range(h):
        Y = (j + 0.5) * step
        t = Y / H
        for i in range(w):
            X = (i + 0.5) * step
            c = [top[k] + (bot[k] - top[k]) * t for k in range(3)]
            for cx, cy, rx, ry, col, alpha in glows:
                r2 = ((X - cx) / rx) ** 2 + ((Y - cy) / ry) ** 2
                if r2 < 1:
                    a = alpha * (math.exp(-3.2 * r2) - e0) / (1 - e0)
                    c = [c[k] + (col[k] - c[k]) * a for k in range(3)]
            for k in range(3):
                chans[k].append(c[k])
    d = dither_field(size)
    bands = []
    for k in range(3):
        f = Image.new("F", (w, h))
        f.putdata(chans[k])
        f = f.resize(size, Image.BICUBIC)
        bands.append(ImageMath.lambda_eval(lambda a: a["f"] + a["d"], f=f, d=d).convert("L"))
    return Image.merge("RGB", bands).convert("RGBA")


def rounded_mask(w, h, r) -> Image.Image:
    big = Image.new("L", (w * SS, h * SS), 0)
    ImageDraw.Draw(big).rounded_rectangle((0, 0, w * SS - 1, h * SS - 1), radius=r * SS, fill=255)
    return big.resize((w, h), Image.LANCZOS)


def hairline_mask(w, h, r, width=1.0) -> Image.Image:
    big = Image.new("L", (w * SS, h * SS), 0)
    d = ImageDraw.Draw(big)
    d.rounded_rectangle((0, 0, w * SS - 1, h * SS - 1), radius=r * SS, fill=255)
    i = round(width * SS)
    d.rounded_rectangle((i, i, w * SS - 1 - i, h * SS - 1 - i), radius=max(0, r * SS - i), fill=0)
    return big.resize((w, h), Image.LANCZOS)


def load_shot(path: Path) -> Image.Image:
    im = Image.open(path).convert("RGB")
    # real window captures carry a 1 px black frame: drop it, we draw our own
    w, h = im.size
    return im.crop((1, 1, w - 1, h - 1))


def window_image(shot: Image.Image, width: int, theme: dict, radius: int = 12, crop_h: int | None = None):
    """Scaled screenshot with rounded corners and a hairline border -> RGBA."""
    sw, sh = shot.size
    h = round(sh * width / sw)
    im = shot.resize((width, h), Image.LANCZOS)
    im = im.filter(ImageFilter.UnsharpMask(radius=0.6, percent=35, threshold=1))
    im = im.convert("RGBA")
    mask = rounded_mask(width, h, radius)
    ring = hairline_mask(width, h, radius, 1.0)
    bc = theme["border"]
    border = Image.new("RGBA", (width, h), bc[:3] + (0,))
    border.putalpha(ring.point(lambda v: round(v * bc[3] / 255)))
    im.alpha_composite(border)
    im.putalpha(ImageChops.multiply(im.getchannel("A"), mask))
    return im


def drop_shadow(canvas: Image.Image, alpha_mask: Image.Image, pos, color, layers):
    """Composite soft shadows of `alpha_mask` (placed at pos) onto canvas."""
    W, H = canvas.size
    for blur, dy, a in layers:
        m = Image.new("L", (W, H), 0)
        m.paste(alpha_mask, (pos[0], pos[1] + dy))
        if blur:
            m = m.filter(ImageFilter.GaussianBlur(blur))
        m = m.point(lambda v, a=a: round(v * a))
        layer = Image.new("RGBA", (W, H), color + (0,))
        layer.putalpha(m)
        canvas.alpha_composite(layer)


def place_window(canvas, win: Image.Image, pos, theme, layers, veil=None):
    drop_shadow(canvas, win.getchannel("A"), pos, theme["shadow"], layers)
    if veil:
        v = Image.new("RGBA", win.size, veil)
        win = win.copy()
        a = win.getchannel("A")
        win.alpha_composite(v)
        win.putalpha(a)
    canvas.alpha_composite(win, pos)


_icon_cache: dict = {}


def app_icon(visible: int) -> Image.Image:
    """The app icon cropped to its squircle, `visible` px tall."""
    if visible not in _icon_cache:
        size = round(visible * 16 / 14)  # draw() keeps a 1/16 margin per side
        im = draw_icon(size)
        im = im.crop(im.getbbox())
        if im.size != (visible, visible):
            im = im.resize((visible, visible), Image.LANCZOS)
        _icon_cache[visible] = im
    return _icon_cache[visible]


def place_icon(canvas, x, y, visible, theme):
    icon = app_icon(visible)
    color, a, blur, dy = theme["icon_shadow"]
    drop_shadow(canvas, icon.getchannel("A"), (x, y), color, [(blur, dy, a), (4, 2, a * 0.35)])
    canvas.alpha_composite(icon, (x, y))


def save(img: Image.Image, path: Path):
    img.convert("RGB").save(path, optimize=True)
    print(f"wrote {path.name}  {img.size[0]}x{img.size[1]}  {path.stat().st_size // 1024} KB")


# --------------------------------------------------------------------------
# README hero banner, 1600 x 720
# --------------------------------------------------------------------------
BANNER = dict(
    size=(1600, 720),
    left=104,
    icon=200,
    title_px=86,
    title_track=-1.2,
    tag_px=40,
    tag_weight=450,
    tag_lh=54,
    meta_px=24,
    gap_icon_title=44,     # icon bottom -> wordmark cap top
    gap_title_tag=70,      # wordmark baseline -> tagline baseline 1
    gap_tag_meta=58,       # tagline baseline 2 -> meta baseline
    main_w=610,
    back_scale=0.86,
    # The window behind peeks out above and to the right of the front one.
    # How far is given in screenshot pixels of the back window, so that only
    # whole, uncut UI shows: its title bar and page dots above (y < 92, the
    # status circle starts at 101) and the empty right margin of its card
    # (x > 748, the check marks end at 741 and the ru subtitle at 728).
    back_reveal_x=750,
    back_reveal_y=92,
    right=100,             # right margin of the window group
    second="7-done.png",
)


def make_banner(repo: Path, lang: str, theme_name: str, out: Path):
    L = BANNER
    W, H = L["size"]
    th = THEMES[theme_name]
    cp = COPY[lang]
    img = background((W, H), th)

    # ---- product shot (right) -----------------------------------------
    shots = repo / "docs" / "screenshots" / f"{lang}-{theme_name}"
    main = window_image(load_shot(shots / "2-choose-apps.png"), L["main_w"], th)
    back_shot = load_shot(shots / L["second"])
    back = window_image(back_shot, round(L["main_w"] * L["back_scale"]), th, radius=11)
    mw, mh = main.size
    bw, bh = back.size
    k = bw / back_shot.size[0]
    back_dx = bw - round(L["back_reveal_x"] * k)   # sticks out right of the front window
    back_dy = round(L["back_reveal_y"] * k)        # sticks out above it
    group_w = mw + back_dx
    group_h = mh + back_dy
    gx1 = W - L["right"]
    gy0 = (H - group_h) // 2 - 6          # shadows hang below: lift slightly
    main_pos = (gx1 - group_w, gy0 + back_dy)
    back_pos = (gx1 - bw, gy0)
    place_window(img, back, back_pos, th, th["back_shadow_layers"], veil=th["back_veil"])
    place_window(img, main, main_pos, th, th["shadow_layers"])

    # ---- text column (left) -------------------------------------------
    f_title = font(L["title_px"], 600, 36)
    f_tag = font(L["tag_px"], L["tag_weight"])
    f_meta = font(L["meta_px"], 400)
    cap = cap_height(f_title)
    block = L["icon"] + L["gap_icon_title"] + cap + L["gap_title_tag"] + L["tag_lh"] + L["gap_tag_meta"]
    y0 = round((H - block) / 2) - 4
    x = L["left"]

    place_icon(img, x, y0, L["icon"], th)
    d = ImageDraw.Draw(img)
    base = y0 + L["icon"] + L["gap_icon_title"] + cap
    draw_text(d, x, base, "FreeReturn", f_title, th["title"], L["title_track"])
    base += L["gap_title_tag"]
    for i, line in enumerate(cp["tagline_lines"]):
        draw_text(d, x, base + i * L["tag_lh"], line, f_tag, th["tagline"])
    base += L["tag_lh"] + L["gap_tag_meta"]
    draw_text(d, x, base, cp["meta"], f_meta, th["meta"])

    # sanity: text column must clear the front window
    right_edge = x + max(
        text_width(f_title, "FreeReturn", L["title_track"]),
        *(text_width(f_tag, s) for s in cp["tagline_lines"]),
        text_width(f_meta, cp["meta"]),
    )
    gap = main_pos[0] - right_edge
    if gap < 60:
        print(f"  warning: {lang}-{theme_name}: text column only {gap:.0f}px from the window")

    save(img, out / f"banner-{lang}-{theme_name}.png")


# --------------------------------------------------------------------------
# GitHub social preview, 1280 x 640 (light)
# --------------------------------------------------------------------------
SOCIAL = dict(
    size=(1280, 640),
    safe=(1100, 540),
    icon=112,
    title_px=84,
    title_track=-1.2,
    gap_icon_title=30,     # horizontal gap icon -> wordmark
    tag_px=40,
    top=86,                # top of the icon / wordmark row
    gap_row_tag=74,        # icon bottom -> tagline baseline
    win_w=860,
    win_top=312,
    glows=[
        (640, 560, 760, 330, (0, 122, 255), 0.12),
        (1080, 250, 380, 260, (88, 86, 214), 0.06),
        (220, 260, 360, 240, (90, 200, 250), 0.05),
    ],
)


def make_social(repo: Path, lang: str, out: Path):
    S = SOCIAL
    W, H = S["size"]
    th = THEMES["light"]
    cp = COPY[lang]
    img = background((W, H), th, glows=SOCIAL["glows"])

    # ---- product shot: centred, bleeding off the bottom ----------------
    shots = repo / "docs" / "screenshots" / f"{lang}-light"
    win = window_image(load_shot(shots / "2-choose-apps.png"), S["win_w"], th, radius=14)
    wx = (W - win.size[0]) // 2
    place_window(img, win, (wx, S["win_top"]), th, [(40, 18, 0.20), (12, 6, 0.10), (3, 1, 0.06)])

    # ---- icon + wordmark row, centred ------------------------------------
    f_title = font(S["title_px"], 600, 36)
    f_tag = font(S["tag_px"], 400)
    tw = text_width(f_title, "FreeReturn", S["title_track"])
    row_w = S["icon"] + S["gap_icon_title"] + tw
    x0 = round((W - row_w) / 2)
    y0 = S["top"]
    place_icon(img, x0, y0, S["icon"], th)
    d = ImageDraw.Draw(img)
    cap = cap_height(f_title)
    base = round(y0 + S["icon"] / 2 + cap / 2)  # wordmark caps centred on the icon
    draw_text(d, x0 + S["icon"] + S["gap_icon_title"], base, "FreeReturn", f_title, th["title"], S["title_track"])

    # ---- tagline, one line, centred --------------------------------------
    tag_w = text_width(f_tag, cp["tagline"])
    tag_base = y0 + S["icon"] + S["gap_row_tag"]
    draw_text(d, round((W - tag_w) / 2), tag_base, cp["tagline"], f_tag, th["tagline"])

    sw, sh = S["safe"]
    sx0, sy0 = (W - sw) // 2, (H - sh) // 2
    assert x0 >= sx0 and x0 + row_w <= sx0 + sw, "wordmark row outside safe area"
    assert (W - tag_w) / 2 >= sx0, "tagline outside safe area"
    assert y0 >= sy0, "icon outside safe area"

    path = out / f"social-preview-{lang}.png"
    save(img, path)
    if path.stat().st_size >= 1024 * 1024:
        print(f"  warning: {path.name} is over 1 MB")


# --------------------------------------------------------------------------
def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--repo", type=Path, default=DEFAULT_REPO, help="FreeReturn repository root")
    ap.add_argument("--out", type=Path, default=DEFAULT_REPO / "docs" / "media", help="output folder (default: docs/media)")
    args = ap.parse_args()

    sys.path.insert(0, str(args.repo / "build"))
    global draw_icon
    from icon import draw as draw_icon  # noqa: E402

    args.out.mkdir(parents=True, exist_ok=True)
    for lang in ("ru", "en"):
        for theme in ("light", "dark"):
            make_banner(args.repo, lang, theme, args.out)
    for lang in ("ru", "en"):
        make_social(args.repo, lang, args.out)


if __name__ == "__main__":
    main()
