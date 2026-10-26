"""FreeReturn app icon.

Two iPhones overlap like a "copy" symbol.  The one behind (top right) is the
source: it holds the app tile, and its outline runs down its right side,
turns at the bottom corner and carries on as the return arrow.  The arrow
crosses into the iPhone in front (bottom left) through a gap in its side, and
its head points into that phone: the app is carried out of one iPhone and
restored onto the other.  Teal -> #007AFF -> indigo superellipse tile.
Pillow only.

    draw(size, small=False) -> RGBA PIL.Image (size x size)

One design, one gradient, rendered at every size from the same proportions,
so the ICO frames 16/20/24/32/40/48/64/96/128/256 keep their weight and
drawing between DPI steps:

* every size is pixel-fitted: strokes, gaps, pills, the app tile and the
  phones are snapped to whole pixels, then box-filtered down from an exact
  integer supersample, so straight edges are pure 0/255;
* at 32 px and below the arrowhead is a solid whole-pixel staircase with a
  flat tip as wide as the shaft (8 px tall at 32, 6 at 24, 5 at 20, 3 at 16)
  instead of anti-aliased 45 deg flanks, and it always keeps >= 1 px of
  clear space to the front phone's walls; 40-48 px get a crisp 45 deg
  triangle, 64 px and up a softly rounded one;
* sizes <= 32 always get the small drawing whatever `small` says: flat
  colour, no shadows, and only the details that fit as whole pixels (the
  pills and the app tile drop out where they would turn into specks).  The
  app tile is rounded at every size where it is drawn, and its 2x2 app grid
  only appears where it fits as four crisp >= 3 px holes.  `small=True`
  forces the flat, grid-less drawing at larger sizes too;
* the squircle keeps a safe margin (1/16 of the canvas, at least 1 px), and
  everything outside it is fully transparent.

Run as a script to write a preview sheet to artifacts/icon-preview.png.
"""
from __future__ import annotations

import math
import os

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

# --------------------------------------------------------------------------
# palette (one gradient for every size)
# --------------------------------------------------------------------------
# position along the diagonal (top-left -> bottom-right). The teal end is
# deep enough to keep >= 3:1 against white backgrounds and under the glyph.
GRAD = [
    (0.00, (0, 150, 196)),    # deep teal
    (0.50, (0, 122, 255)),    # the app's #007AFF accent
    (1.00, (92, 64, 224)),    # indigo
]
GRAD_DIR = (0.58, 0.82)       # a little steeper than 45 deg: light from the top
TOP_LIGHT = 0.06              # soft sheen at the top of the tile
BOTTOM_SHADE = 0.06

GLYPH_TOP = (255, 255, 255)
GLYPH_BOTTOM = (236, 243, 255)   # barely-blue white at the bottom (large only)
SHADOW = (6, 22, 92)             # soft navy

SQUIRCLE_N = 5.0
TILE_N = 4.6

# --------------------------------------------------------------------------
# the design, in tile units (0..1 across the squircle)
# --------------------------------------------------------------------------
# A = the iPhone in front (destination, bottom left)
# B = the iPhone behind (source, top right), offset by (dx, -dy) from A
D = dict(
    w=0.07,         # stroke
    pw=0.37,        # phone width (outer), both phones
    ph=0.54,        # phone height (outer), both phones
    dx=0.285,       # B is this far right of A ...
    dy=0.19,        # ... and this far above it
    ro=0.105,       # phone corner radius (outer)
    g=0.035,        # gap around the front phone and around the arrow
    hh=0.11,        # arrowhead half-height
    ct=0.045,       # arrow tip -> inner left wall of the front phone
    cv=0.035,       # minimum clearance above/below the arrowhead inside A
    ts=0.145,       # app tile
    gf=0.028,       # tile -> floor gap (the app rests on the floor that
                    # becomes the arrow)
    pill_w=0.095,
    pill_h=0.032,
    pill_gap=0.036,
    grid_m=0.22,    # grid margin, as a fraction of the tile
    grid_g=0.12,    # grid gap, as a fraction of the tile
    head_round=0.012,
    shift_x=0.0,    # optical nudges (tile units)
    shift_y=0.0,
)


def _margin(size: int) -> int:
    """Safe margin around the squircle: 1/16 of the canvas, >= 1 px."""
    return max(1, int(size / 16 + 0.25))


# --------------------------------------------------------------------------
# layout: the design snapped to whole pixels at every size
# --------------------------------------------------------------------------
# hand-fitted exceptions to the snapped layout (values in target pixels)
PIXEL_TWEAKS = {
    16: dict(ph=7),
    20: dict(j=2),
    24: dict(j=2),       # 2-4-6 staircase: an 8 px head would touch the top
    32: dict(pw=11, dx=9, ph=14),   # 20 x 20 glyph, 4 px all round
}


def _fit_grid(ts):
    """Whole-pixel 2x2 grid for a ts px tile -> (margin, dot, gap) or None.

    Dots must be >= 3 px with >= 2 px margins and a >= 1 px bar, so the four
    holes stay distinct; among the candidates take the one closest to the
    design proportions."""
    tm, tg = D["grid_m"] * ts, D["grid_g"] * ts
    td = (ts - 2 * tm - tg) / 2
    best = None
    for d in range(3, ts // 2):
        for g in range(1, ts - 2 * d):
            m2 = ts - 2 * d - g
            if m2 % 2 or m2 // 2 < 2:
                continue
            m = m2 // 2
            err = (m - tm) ** 2 + (g - tg) ** 2 + (d - td) ** 2
            if best is None or err < best[0]:
                best = (err, m, d, g)
    return best[1:] if best else None


def _layout(size, small):
    """All geometry in target pixels; strokes, gaps and boxes on whole pixels."""
    M = _margin(size)
    T = size - 2 * M
    tiny = size <= 32
    tw = PIXEL_TWEAKS.get(size, {})

    def px(key, lo=1):
        return tw.get(key, max(lo, round(D[key] * T)))

    w = px("w")
    pw, ph, dx, dy = px("pw"), px("ph"), px("dx"), px("dy")
    g = px("g")
    stair = w <= 2 and T <= 30           # whole-pixel staircase arrowhead
    big = w >= 4                          # rounded head / round corners
    # arrowhead: w + 2j px tall
    j = tw.get("j", max(1, round(D["hh"] * T - w / 2)))
    if w >= 2:
        j = max(j, w + 1)                 # >= 8 px tall at a 2 px stroke
    if stair:
        hl = j + 1                        # columns of height w, w+2, .. w+2j
        th = 0.0
    else:
        th = 0.5 if w % 2 else 0.0        # 1 px flat tip keeps odd strokes crisp
        hl = w / 2 + j - th
    ct = px("ct")
    cv = px("cv")
    # A must hold: wall, tip clearance, head, >= 1 px of shaft, wall
    pw = max(pw, 2 * w + ct + math.ceil(hl) + 1)
    # B's left wall must disappear completely behind A (+ its gap)
    pw = max(pw, dx + w - g)
    # B shows the shaft at its floor; the head needs cv above and below it
    # inside A: below it that sets the offset, above it the head size
    dy = max(dy, j + w + cv)
    while j > 1 and ph - w - j < dy + w + cv:
        j -= 1
        dy = max(px("dy"), j + w + cv)
    if stair:
        hl = j + 1
    else:
        hl = w / 2 + j - th
    # glyph box, origin at its top-left corner
    ax0, ay0 = 0, dy
    bx0, by0 = dx, 0
    ax1, ay1 = ax0 + pw, ay0 + ph
    bx1, by1 = bx0 + pw, by0 + ph
    ys1 = by1                     # the shaft is B's floor: rows [ys0, ys1)
    ys0 = ys1 - w
    xt = ax0 + w + ct             # arrow tip (left edge of the tip column)
    xb = xt + hl                  # back of the head
    # corners
    if w == 1:
        ro, notch = 1.0, True
    else:
        ro, notch = max(w + 1.0, round(D["ro"] * T * 2) / 2), False
    # pills (Dynamic Island), only where they stay a clear separate shape
    pill_h = px("pill_h")
    pill_w = px("pill_w", 3)
    if (pw - pill_w) % 2:
        pill_w += 1
    pg = px("pill_gap")
    pills = []
    # B's pill: centred, above the front phone's knock-out
    py0 = by0 + w + pg
    if pill_w <= pw - 2 * w - 2 and py0 + pill_h <= ay0 - g and pill_w >= 3:
        pills.append((bx0 + (pw - pill_w) // 2, py0, pill_w, pill_h))
    # A's pill: centred, clear of the arrowhead
    py0 = ay0 + w + pg
    if py0 + pill_h + max(1, round(0.025 * T)) <= ys0 - j and pill_w >= 3:
        pills.append((ax0 + (pw - pill_w) // 2, py0, pill_w, pill_h))
    # app tile on B's floor, in the part of B the front phone leaves visible
    # optically centred between A's wall and B's wall, but never inside the
    # knock-out around A
    lo_x, hi_x = ax1 + g, bx1 - w - max(1, round(0.02 * T))
    ts = min(px("ts", 3), hi_x - lo_x)
    gf = px("gf")
    top_room = by0 + w + (pg + pill_h + 1 if pills and pills[0][1] < ay0 else 1)
    tile = None
    grid = None
    grid_n = None
    if ts >= 3:
        ty1 = ys0 - gf
        ts = min(ts, ty1 - top_room)
        if ts >= 3:
            if not small and not tiny:
                for cand in (ts, ts - 1):
                    fit = _fit_grid(cand) if cand >= 3 else None
                    if fit:
                        ts, grid = cand, fit
                        break
            tx0 = (ax1 + bx1 - w - ts + 1) // 2
            tx0 = max(lo_x, min(hi_x - ts, tx0))
            ty0 = ty1 - ts
            tile = (tx0, ty0, ts)
            if grid:
                m, d, gg = grid
                grid_n = 4.0 if d >= 6 else None
                grid = [(tx0 + m + i * (d + gg), ty0 + m + jj * (d + gg), d)
                        for jj in range(2) for i in range(2)]
    # place the glyph box in the tile
    W = bx1
    H = ay1
    # odd leftovers go right and below: the glyph sits on or a hair above and
    # left of centre (its centroid is a little low/right: the arrowhead and
    # the app tile)
    ox = M + int(math.floor((T - W) / 2 + D["shift_x"] * T))
    oy = M + int(math.floor((T - H) / 2 + D["shift_y"] * T))

    def sx(v):
        return v + ox

    def sy(v):
        return v + oy

    if small or tiny:
        depth = 0.0
    else:
        depth = max(0.0, min(1.0, (size - 40) / 56))
    return dict(
        M=M, T=T, w=w, g=g, ro=ro, notch=notch,
        A=(sx(ax0), sy(ay0), sx(ax1), sy(ay1)),
        B=(sx(bx0), sy(by0), sx(bx1), sy(by1)),
        ys0=sy(ys0), ys1=sy(ys1), xt=sx(xt), xb=sx(xb), j=j, th=th,
        head="round" if big else ("stair" if stair else "poly"),
        hr=D["head_round"] * T if big else 0.0,
        pills=[(sx(x), sy(y), pw_, ph_) for x, y, pw_, ph_ in pills],
        tile=None if tile is None else (sx(tile[0]), sy(tile[1]), tile[2]),
        tile_n=TILE_N,
        grid=None if grid is None else [(sx(x), sy(y), d) for x, y, d in grid],
        grid_n=grid_n,
        depth=depth,
        box=(ox, oy, ox + W, oy + H),
    )


# --------------------------------------------------------------------------
# drawing helpers (supersampled; k = supersample factor)
# --------------------------------------------------------------------------
def _superellipse(cx, cy, rx, ry, n=SQUIRCLE_N, steps=1440):
    pts = []
    e = 2.0 / n
    for i in range(steps):
        t = 2.0 * math.pi * i / steps
        c, s = math.cos(t), math.sin(t)
        pts.append((cx + rx * math.copysign(abs(c) ** e, c),
                    cy + ry * math.copysign(abs(s) ** e, s)))
    return pts


def _lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))


def _stops(t, stops):
    t = min(1.0, max(0.0, t))
    for (p0, c0), (p1, c1) in zip(stops, stops[1:]):
        if t <= p1:
            return _lerp(c0, c1, (t - p0) / (p1 - p0) if p1 > p0 else 0.0)
    return stops[-1][1]


def _gradient(n):
    """n x n diagonal gradient over the tile, with a soft top light."""
    g = 256
    img = Image.new("RGB", (g, g))
    px = img.load()
    dx, dy = GRAD_DIR
    norm = dx + dy
    for y in range(g):
        v = y / (g - 1)
        for x in range(g):
            u = x / (g - 1)
            r, gg, b = _stops((u * dx + v * dy) / norm, GRAD)
            k = TOP_LIGHT * (1.0 - v) ** 2.2 - BOTTOM_SHADE * v ** 3
            if k >= 0:
                r, gg, b = (c + (255 - c) * k for c in (r, gg, b))
            else:
                r, gg, b = (c * (1 + k) for c in (r, gg, b))
            px[x, y] = (int(r), int(gg), int(b))
    return img.resize((n, n), Image.BICUBIC)


def _rect(d, k, x0, y0, x1, y1, fill=255):
    """Fill the half-open box [x0, x1) x [y0, y1) given in target pixels."""
    X0, Y0, X1, Y1 = (int(round(v * k)) for v in (x0, y0, x1, y1))
    if X1 > X0 and Y1 > Y0:
        d.rectangle([X0, Y0, X1 - 1, Y1 - 1], fill=fill)


def _poly(d, k, pts, fill=255):
    """Polygon given in target pixels (vertices on pixel edges)."""
    d.polygon([(x * k, y * k) for x, y in pts], fill=fill)


def _disc(d, k, cx, cy, r, fill=255):
    if r <= 0:
        return
    d.ellipse([(cx - r) * k, (cy - r) * k, (cx + r) * k - 1, (cy + r) * k - 1],
              fill=fill)


def _rrect(d, k, x0, y0, x1, y1, r, fill=255, notch=False):
    """Rounded box [x0, x1) x [y0, y1): straight edges on whole pixels,
    circular corners of radius r (or one missing corner pixel if notch)."""
    if notch:
        _rect(d, k, x0 + 1, y0, x1 - 1, y1, fill)
        _rect(d, k, x0, y0 + 1, x1, y1 - 1, fill)
        return
    r = max(0.0, min(r, (x1 - x0) / 2, (y1 - y0) / 2))
    _rect(d, k, x0 + r, y0, x1 - r, y1, fill)
    _rect(d, k, x0, y0 + r, x1, y1 - r, fill)
    for cx, cy in ((x0 + r, y0 + r), (x1 - r, y0 + r),
                   (x0 + r, y1 - r), (x1 - r, y1 - r)):
        _disc(d, k, cx, cy, r, fill)


def _outline(d, k, box, ro, w, notch):
    x0, y0, x1, y1 = box
    _rrect(d, k, x0, y0, x1, y1, ro, 255, notch)
    if notch:
        _rect(d, k, x0 + w, y0 + w, x1 - w, y1 - w, 0)
    else:
        _rrect(d, k, x0 + w, y0 + w, x1 - w, y1 - w, max(0.0, ro - w), 0)


def _inset_triangle(tip, base_top, base_bot, r):
    """Move triangle vertices inward so a dilation by r restores the outline."""
    pts = [tip, base_top, base_bot]
    ax, ay = tip
    bx, by = base_top
    cx, cy = base_bot
    la = math.dist(base_top, base_bot)
    lb = math.dist(tip, base_bot)
    lc = math.dist(tip, base_top)
    p = la + lb + lc
    ix = (la * ax + lb * bx + lc * cx) / p
    iy = (la * ay + lb * by + lc * cy) / p
    area = abs((bx - ax) * (cy - ay) - (cx - ax) * (by - ay)) / 2
    inr = 2 * area / p
    f = (inr - r) / inr
    return [(ix + (x - ix) * f, iy + (y - iy) * f) for x, y in pts]


def _glyph_mask(S, k, L):
    """White glyph coverage at the supersampled size S = size * k."""
    m = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(m)
    w, g, ro, notch = L["w"], L["g"], L["ro"], L["notch"]
    A, B = L["A"], L["B"]

    # --- B, the source iPhone behind: outline, pill, app tile ------------
    _outline(d, k, B, ro, w, notch)
    pills = L["pills"]
    for px0, py0, pw_, ph_ in pills:
        if py0 < A[1]:
            _pill(d, k, px0, py0, pw_, ph_)
    if L["tile"]:
        tx0, ty0, ts = L["tile"]
        if ts >= 10:
            _poly(d, k, _superellipse(tx0 + ts / 2, ty0 + ts / 2, ts / 2, ts / 2,
                                      n=L["tile_n"], steps=360))
        elif ts >= 4:
            # smaller tiles: same rounding, straight edges on whole pixels
            _rrect(d, k, tx0, ty0, tx0 + ts, ty0 + ts, max(1.0, ts * 0.27))
        else:
            _rect(d, k, tx0, ty0, tx0 + ts, ty0 + ts)
        for gx, gy, gd in L["grid"] or ():
            if L["grid_n"]:
                _poly(d, k, _superellipse(gx + gd / 2, gy + gd / 2, gd / 2, gd / 2,
                                          n=L["grid_n"], steps=180), fill=0)
            else:
                _rect(d, k, gx, gy, gx + gd, gy + gd, fill=0)

    # --- A, the destination iPhone in front: hides B behind it -----------
    ax0, ay0, ax1, ay1 = A
    # square knock-out: B's left wall ends in a clean flat cut a gap above
    # A instead of a sliver that follows A's round corner
    _rect(d, k, ax0 - g, ay0 - g, ax1 + g, ay1 + g, 0)
    _outline(d, k, A, ro, w, notch)
    ys0, ys1 = L["ys0"], L["ys1"]
    # the doorway the arrow comes in through
    _rect(d, k, ax1 - w, ys0 - g, ax1, ys1 + g, 0)
    for px0, py0, pw_, ph_ in pills:
        if py0 >= ay0:
            _pill(d, k, px0, py0, pw_, ph_)

    # --- the return arrow: B's floor, into A -----------------------------
    xb, xt, j, th = L["xb"], L["xt"], L["j"], L["th"]
    bx1 = B[2]
    _rect(d, k, xb - 0.01, ys0, bx1 - max(ro, 1.0), ys1)
    yc = (ys0 + ys1) / 2
    hh = (ys1 - ys0) / 2 + j
    if L["head"] == "stair":
        for i in range(j + 1):
            _rect(d, k, xt + i, ys0 - i, xt + i + 1, ys1 + i)
    elif L["head"] == "poly":
        _poly(d, k, [(xt, yc - th), (xb, yc - hh), (xb, yc + hh), (xt, yc + th)])
    else:  # round: a softly rounded triangle (large sizes)
        r = L["hr"]
        tri = _inset_triangle((xt, yc), (xb, yc - hh), (xb, yc + hh), r)
        _poly(d, k, tri)
        for a, b in zip(tri, tri[1:] + tri[:1]):
            d.line([(a[0] * k, a[1] * k), (b[0] * k, b[1] * k)], fill=255,
                   width=max(1, int(round(2 * r * k))))
            _disc(d, k, a[0], a[1], r)
        # square the back of the head off against the shaft
        _rect(d, k, xb - r, yc - hh + r * 2.5, xb, yc + hh - r * 2.5)
    return m


def _pill(d, k, x0, y0, w, h):
    if h >= 2:
        _rrect(d, k, x0, y0, x0 + w, y0 + h, h / 2)
    else:
        _rect(d, k, x0, y0, x0 + w, y0 + h)


# --------------------------------------------------------------------------
# public
# --------------------------------------------------------------------------
def draw(size: int, small: bool = False) -> Image.Image:
    size = int(size)
    k = max(4, -(-1024 // size))          # exact integer supersample factor
    S = size * k
    L = _layout(size, small)
    M, T = L["M"], L["T"]
    depth = L["depth"]

    # background: gradient over the tile box (RGB filled edge to edge so the
    # box-filter downsample never pulls in a foreign colour at the rim)
    grad = _gradient(T * k)
    img = Image.new("RGBA", (S, S))
    img.paste(grad.resize((S, S), Image.BICUBIC), (0, 0))
    img.paste(grad, (M * k, M * k))

    mask = Image.new("L", (S, S), 0)
    # Pillow fills polygons inclusively: pull the radius in by half a sample
    # so the tile covers exactly [M, size - M), symmetric on all four sides
    R = T * k / 2 - 0.5
    ImageDraw.Draw(mask).polygon(_superellipse(S / 2, S / 2, R, R), fill=255)

    if depth > 0:
        # glassy rim light along the top edge, gentle inner shade at the bottom
        inner = Image.new("L", (S, S), 0)
        ri = T * k / 2 - S * 0.008
        ImageDraw.Draw(inner).polygon(_superellipse(S / 2, S / 2, ri, ri),
                                      fill=255)
        ring = ImageChops.subtract(mask, inner).filter(
            ImageFilter.GaussianBlur(S * 0.003))
        col = [int(32 * depth * max(0.0, 1 - y / (S * 0.45)) ** 1.5)
               for y in range(S)]
        top_w = Image.new("L", (1, S))
        top_w.putdata(col)
        top_w = top_w.resize((S, S), Image.NEAREST)
        col = [int(46 * depth * max(0.0, (y - S * 0.6) / (S * 0.4)) ** 1.6)
               for y in range(S)]
        bot_w = Image.new("L", (1, S))
        bot_w.putdata(col)
        bot_w = bot_w.resize((S, S), Image.NEAREST)
        hi = Image.new("RGBA", (S, S), (255, 255, 255, 0))
        hi.putalpha(ImageChops.multiply(ring, top_w))
        img = Image.alpha_composite(img, hi)
        lo = Image.new("RGBA", (S, S), SHADOW + (0,))
        lo.putalpha(ImageChops.multiply(ring, bot_w))
        img = Image.alpha_composite(img, lo)

    glyph = _glyph_mask(S, k, L)

    if depth > 0:
        # soft navy drop shadow + a tight contact shadow
        for blur, dy, op in ((0.020, 0.016, 0.36), (0.006, 0.005, 0.15)):
            sh = glyph.filter(ImageFilter.GaussianBlur(S * blur))
            sh = ImageChops.offset(sh, 0, int(round(S * dy)))
            sh = sh.point(lambda v, op=op * depth: int(v * op))
            layer = Image.new("RGBA", (S, S), SHADOW + (0,))
            layer.putalpha(sh)
            img = Image.alpha_composite(img, layer)
        g0, g1 = L["box"][1] * k, L["box"][3] * k
        col = []
        for y in range(S):
            t = min(1.0, max(0.0, (y - g0) / max(1.0, g1 - g0))) * depth
            col.append(_lerp(GLYPH_TOP, GLYPH_BOTTOM, t))
        fill = Image.new("RGB", (1, S))
        fill.putdata(col)
        fill = fill.resize((S, S), Image.NEAREST).convert("RGBA")
    else:
        fill = Image.new("RGBA", (S, S), GLYPH_TOP + (255,))
    fill.putalpha(glyph)
    img = Image.alpha_composite(img, fill)

    img.putalpha(mask)
    # exact box-filter reduce: whole-pixel edges stay hard, curves get
    # true area coverage, and nothing rings outside the squircle
    return img.reduce(k) if k > 1 else img


# --------------------------------------------------------------------------
# preview sheet
# --------------------------------------------------------------------------
def _font(px):
    try:
        return ImageFont.load_default(size=px)
    except TypeError:  # very old Pillow
        return ImageFont.load_default()


def _preview(path):
    big = draw(512)
    i128 = draw(128)
    i64 = draw(64)
    i32 = draw(32, small=True)
    i16 = draw(16, small=True)
    z32 = i32.resize((128, 128), Image.NEAREST)
    z16 = i16.resize((64, 64), Image.NEAREST)

    items = [(big, "512"), (i128, "128"), (i64, "64"), (i32, "32 small"),
             (i16, "16 small"), (z32, "32 @4x"), (z16, "16 @4x")]
    pad = 32
    gaps = [pad, pad, pad, pad, pad * 2, pad]
    width = pad + sum(im.width for im, _ in items) + sum(gaps) + pad
    strip_h = 512 + 2 * pad + 24
    sheet = Image.new("RGBA", (width, strip_h * 2), (255, 255, 255, 255))
    dr = ImageDraw.Draw(sheet)
    dr.rectangle([0, strip_h, width, strip_h * 2], fill=(28, 28, 30, 255))
    f = _font(13)
    for row in range(2):
        x = pad
        ink = (110, 110, 115) if row == 0 else (160, 160, 168)
        for kx, (im, label) in enumerate(items):
            y = row * strip_h + pad + (512 - im.height) // 2
            sheet.alpha_composite(im, (x, y))
            dr.text((x, row * strip_h + pad + 512 + 6), label, fill=ink, font=f)
            x += im.width + (gaps[kx] if kx < len(gaps) else 0)
    sheet.convert("RGB").save(path)


if __name__ == "__main__":
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    os.makedirs(os.path.join(root, "artifacts"), exist_ok=True)
    out = os.path.join(root, "artifacts", "icon-preview.png")
    _preview(out)
    print(out)
