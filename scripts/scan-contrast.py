#!/usr/bin/env python3
"""The pixel scan of the contrast round (D410, D419; docs/phase-8a-design.md 0.7, 2.6).

It finds, in a gate's pictures, every picture the round changes and where: the text boxes'
placeholders (VcTextSecondaryBrush at Fluent's opacity 0.5), the dark theme's danger colour
(#E5606B: its text, icons and dots, and the danger button) and the dark accent on a hovered or
selected row (#7C8CFF). Its detectors look for phase 7b's colours, so it runs on phase 7b's
pictures, the references of the round's comparison:

    python -I scripts/scan-contrast.py <pictures folder> <output folder> [--jobs N]

The pictures folder holds `phase-N/scripted/*.png` and `phase-N/real-window/*.png`, as
`artifacts/visual` does; a real window's `-difference` and `-with-frame` pictures are left out.
The output folder gets:
- `scan.json`: every picture's detections, with their boxes;
- the eight lists `colours-<list>.txt`, one `phase-N/<folder>/<name>.png` per line: `any`, `none`,
  `placeholder`, `placeholder-double-backdrop`, `danger`, `danger-on-row`, `danger-button` and
  `accent-on-row`;
- `report.txt`: the counts per phase and folder, and the danger buttons against plan.md's list;
- `expected.json`: per picture of `colours-any.txt`, the boxes `[left, top, right, bottom]` (in the
  picture's own pixels, right and bottom exclusive) in which the round may change it, for
  `scripts/compare-pictures.py --expected`.

How the detectors work. Every reference colour comes from src/VisualCommit.App/Theme/Tokens.axaml
as phase 7b had it. Small text is drawn with subpixel smoothing, so a single pixel says little:
each detector labels connected components and judges each by its extreme pixel. Real-window
captures are at 200 %, so every size limit doubles there. Each detector runs on the plain window
(k = 1) and again under a dialog's backdrop (VcBackdropBrush: #99000000 dark, so colours x 0.4;
#66000000 light, so x 0.6).
 1 placeholder: glyph components on a text box's background (TextControlBackground #272C36 dark,
   #FFFFFF light) whose brightest (dark) or darkest (light) channels are VcTextSecondaryBrush at
   0.5 (#616773 dark, #ADB2B8 light), not VcTextDisabledBrush (#5F6775, #A1A8B3). A second pass
   looks for it under two backdrops (a sign-in dialog over the settings screen: x 0.16 dark,
   x 0.36 light): for the lists only in pictures the first found none in, its hits of lower
   confidence; for expected.json's boxes in every picture with a backdrop.
 2 dark danger: components that hold a VcDangerBrush (#E5606B) core: glyph-sized ones (text,
   icons, dots) and the danger button (a filled area with a white label inside). Lane 3
   (#E5606B too) is left out by shape: lines, label pills and dots are taller than a glyph or
   carry on straight above or below. A danger found only under a backdrop is checked again: a
   dimmed glyph must stay as dark as the dimmed danger and lie on a dimmed background.
 3 dark accent on a row: glyph-sized VcAccentBrush (#7C8CFF) components whose surrounding pixels
   are mostly VcHoverBackgroundBrush (#2F3542) or VcSelectionBrush (#2A3150).

The corrections made by looking at the pictures (docs research, colours.md 6.2) are the constants
below, each with its reason. Needs Pillow and NumPy.
"""
import argparse
import json
import os
import sys
from collections import defaultdict
from concurrent.futures import ProcessPoolExecutor

import numpy as np
from PIL import Image


def H(h):
    return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], float)


DARK_BG = [H(x) for x in ('#14161B', '#1A1D24', '#20242C', '#272C36')]
LIGHT_BG = [H(x) for x in ('#FFFFFF', '#F5F6F8', '#EBEDF1')]
BOX = {'dark': H('#272C36'), 'light': H('#FFFFFF')}
# The placeholder as drawn: VcTextSecondaryBrush at 0.5 over the box.
PH = {'dark': (H('#9BA3B0') + H('#272C36')) / 2, 'light': (H('#5C6572') + H('#FFFFFF')) / 2}
DIS = {'dark': H('#5F6775'), 'light': H('#A1A8B3')}
DANGER, ACCENT, WHITE = H('#E5606B'), H('#7C8CFF'), H('#FFFFFF')
ROWS = {'hover': H('#2F3542'), 'selection': H('#2A3150')}
DIM = {'dark': 0.4, 'light': 0.6}

# ---- Corrections from looking at the pictures ------------------------------------------------

# A toolbar tooltip hides the upper half of the left panel's Filter placeholder, so no glyph 3 px
# high is left of it; the lower half shows, and changes. Each box was measured once by hand on
# phase 7b's picture: "Filter" lies in x 17 to 46 and y 104 to 114 of each, grown by GROW.
TOOLTIP_OVER_FILTER = {
    'phase-5/scripted/a1f-undo-tooltip.png': [[14, 101, 50, 118]],
    'phase-5/scripted/u16a-undo-tooltip.png': [[14, 101, 50, 118]],
    'phase-5/scripted/u23a-moved-since.png': [[14, 101, 50, 118]],
    'phase-5/scripted/u23c-merging-tooltip.png': [[14, 101, 50, 118]],
    'phase-6/scripted/w7d-undo-pull-tooltip.png': [[14, 101, 50, 118]],
}
# A dialog's edge cuts lane 3's line or dot into a glyph-sized piece: no danger text there.
LANE3_CUT = {'phase-6/scripted/l27g-push-dialog-dark.png', 'phase-7a/scripted/n06a-sign-in-first.png'}
# A lane-3 label pill cut by the activity log into a glyph-sized piece, by its (x, y, w, h).
LANE3_PILL = {(266, 344, 91, 5), (532, 687, 182, 11)}
# A blue fringe of grey text in the Pick dialog, not accent text.
ACCENT_FRINGE = {'phase-6/scripted/s17b-editor-pick.png'}

# How expected.json's boxes are drawn: each hit grown by GROW px, widened along its text line by
# the ink of its kind within JOIN px (an ellipsis's dots within DOT_GAP px), and joined with the
# hits of its line within JOIN px (all doubled at 200 %), so that a word whose letters the
# detector finds only in part is one box (docs/phase-8a-design.md 2.6, 2.9 item 4).
GROW = 3
JOIN = 24
# The gap across which a dot-sized piece of ink without a core joins its line (doubled at 200 %).
DOT_GAP = 4

# The plan's list of 35 dark danger-label pictures (plan.md, phase 8a's items from phase 6),
# which the danger buttons found replace (ruling 31).
PLAN_DANGER_LABELS = {
    ('scripted', 'phase-2'): '17a 18a 19a 19c',
    ('scripted', 'phase-3'): '04b 09f 10f 11b3 12g 13i 13j 16h 16k 20c 22e 25c 25g 29d 30d 31a',
    ('scripted', 'phase-4'): 'b9c c13d c13g c13k c14h c16b',
    ('scripted', 'phase-5'): 'b9i b9m u16i',
    ('real-window', 'phase-2'): '17a',
    ('real-window', 'phase-3'): '09f 10f 22e 31a',
    ('real-window', 'phase-4'): 'c16b',
}

LISTS = ['any', 'none', 'placeholder', 'placeholder-double-backdrop', 'danger', 'danger-on-row', 'danger-button', 'accent-on-row']


# ---- Connected components (8-connected) of a boolean mask, by row runs and union-find --------

def runs_of(mask):
    """The runs of True in each row: arrays y, x0, x1 (inclusive)."""
    m = mask.astype(np.int8)
    pad = np.zeros((m.shape[0], 1), np.int8)
    d = np.diff(np.concatenate([pad, m, pad], axis=1), axis=1)
    ys, xs0 = np.nonzero(d == 1)
    _, xs1 = np.nonzero(d == -1)
    # np.nonzero is row-major, so starts and ends pair up in order
    return ys, xs0, xs1 - 1


def label(mask):
    """The labels (int32, 0 the background) of the mask's components, and their number."""
    ys, x0, x1 = runs_of(mask)
    n = len(ys)
    lab = np.zeros(mask.shape, np.int32)
    if n == 0:
        return lab, 0
    parent = list(range(n))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    row_start = {}
    for i, y in enumerate(ys.tolist()):
        row_start.setdefault(y, []).append(i)
    x0l, x1l = x0.tolist(), x1.tolist()
    for y, idx in row_start.items():
        prev = row_start.get(y - 1)
        if not prev:
            continue
        j = 0
        for i in idx:
            a0, a1 = x0l[i], x1l[i]
            # the runs of the row above are sorted by x
            while j < len(prev) and x1l[prev[j]] < a0 - 1:
                j += 1
            k = j
            while k < len(prev) and x0l[prev[k]] <= a1 + 1:
                ra, rb = find(i), find(prev[k])
                if ra != rb:
                    parent[ra] = rb
                k += 1
    roots = {}
    ids = np.empty(n, np.int32)
    for i in range(n):
        r = find(i)
        if r not in roots:
            roots[r] = len(roots) + 1
        ids[i] = roots[r]
    yl = ys.tolist()
    for i in range(n):
        lab[yl[i], x0l[i]:x1l[i] + 1] = ids[i]
    return lab, len(roots)


def stats(lab, n, img):
    """Per component: area, y0, y1, x0, x1, each channel's max and min of img; index 0 unused."""
    _, W = lab.shape
    flat = lab.ravel()
    idx = np.nonzero(flat)[0]
    lv = flat[idx]
    order = np.argsort(lv, kind='stable')
    idx, lv = idx[order], lv[order]
    yy, xx = np.divmod(idx, W)
    starts = np.r_[0, np.nonzero(np.diff(lv))[0] + 1]
    labels = lv[starts]

    def red(fn, v, fill):
        out = np.full(n + 1, fill, dtype=np.int64)
        out[labels] = fn.reduceat(v, starts)
        return out

    area = np.zeros(n + 1, np.int64)
    area[labels] = np.diff(np.r_[starts, len(lv)])
    y0, y1 = red(np.minimum, yy, 0), red(np.maximum, yy, 0)
    x0, x1 = red(np.minimum, xx, 0), red(np.maximum, xx, 0)
    px = img.reshape(-1, 3)[idx]
    mx = np.stack([red(np.maximum, px[:, c], -1) for c in range(3)], axis=1)
    mn = np.stack([red(np.minimum, px[:, c], 999) for c in range(3)], axis=1)
    return area, y0, y1, x0, x1, mx, mn


# ---- The detectors -------------------------------------------------------------------------------

def near(img, c, tol):
    c = np.rint(c).astype(np.int16)
    out = np.abs(img[..., 0] - c[0]) <= tol
    out &= np.abs(img[..., 1] - c[1]) <= tol
    out &= np.abs(img[..., 2] - c[2]) <= tol
    return out


def boxsum(m, r):
    p = np.pad(m.astype(np.int32), ((r + 1, r), (r + 1, r)))
    c = p.cumsum(0).cumsum(1)
    return c[2 * r + 1:, 2 * r + 1:] - c[:-2 * r - 1, 2 * r + 1:] - c[2 * r + 1:, :-2 * r - 1] + c[:-2 * r - 1, :-2 * r - 1]


def pictures(root):
    for ph in sorted(os.listdir(root)):
        p = os.path.join(root, ph)
        if not os.path.isdir(p):
            continue
        for folder in ('scripted', 'real-window'):
            f = os.path.join(p, folder)
            if not os.path.isdir(f):
                continue
            for n in sorted(os.listdir(f)):
                if n.endswith('.png') and not n.endswith('-difference.png') and not n.endswith('-with-frame.png'):
                    yield ph, folder, n, os.path.join(f, n)


def theme(img):
    d = sum(int(near(img, c, 1).sum()) + int(near(img, c * 0.4, 1).sum()) for c in DARK_BG)
    li = sum(int(near(img, c, 1).sum()) + int(near(img, c * 0.6, 1).sum()) for c in LIGHT_BG)
    return 'dark' if d >= li else 'light'


def placeholder(img, th, s, k, line=None):
    """The placeholder's glyphs; every small ink component in a box goes into `line` when given."""
    box = BOX[th] * k
    bg = near(img, box, 0)
    if bg.sum() < 50:
        return []
    inside = boxsum(bg, 4 * s) >= 0.3 * (8 * s + 1) ** 2
    ink = inside & ~near(img, box, 4 if k == 1 else 2)
    lab, n = label(ink)
    if n == 0:
        return []
    area, y0, y1, x0, x1, mx, mn = stats(lab, n, img)
    p, d = np.rint(PH[th] * k), np.rint(DIS[th] * k)
    hits = []
    for i in range(1, n + 1):
        h, w = y1[i] - y0[i] + 1, x1[i] - x0[i] + 1
        if h <= 16 * s and w <= 120 * s and line is not None:
            line.append((int(x0[i]), int(y0[i]), int(w), int(h)))
        if not (3 * s <= h <= 16 * s and w <= 120 * s and area[i] >= 4 * s * s):
            continue
        if th == 'dark':
            # brightest channels: the placeholder's, with a red above the disabled colour's
            v = mx[i]
            ok = d[0] < v[0] <= p[0] + 2 and p[1] - 4 <= v[1] <= p[1] + 2 and p[2] - 7 <= v[2] <= p[2] + 2
        else:
            # darkest channels: the placeholder's, well above the disabled colour's red
            v = mn[i]
            ok = (p[0] - 2 <= v[0] <= p[0] + 2 and p[1] - 3 <= v[1] <= p[1] + 3
                  and p[2] - 3 <= v[2] <= p[2] + 3 and v[0] > d[0] + 3)
        if ok:
            hits.append((int(x0[i]), int(y0[i]), int(w), int(h)))
    return hits


def ring_bg(img, mask, c, pad, k):
    H_, W_ = mask.shape
    a, b = max(0, c['y'] - pad), min(H_, c['y'] + c['h'] + pad)
    left, right = max(0, c['x'] - pad), min(W_, c['x'] + c['w'] + pad)
    sub, keep = img[a:b, left:right], ~mask[a:b, left:right]
    tot = int(keep.sum()) or 1
    return {name: int((near(sub, v * k, 3) & keep).sum()) / tot for name, v in ROWS.items()}


def continues(mask, lab, c, s):
    """The colour goes on straight above or below the component's centre within 5*s pixels:
    a lane's line or the dashes under the working-changes ring, not a glyph."""
    cx = c['x'] + c['w'] // 2
    a, b = max(0, cx - 2 * s), cx + 2 * s + 1
    y0, y1 = c['y'], c['y'] + c['h'] - 1
    up = slice(max(0, y0 - 5 * s), y0)
    dn = slice(y1 + 1, y1 + 1 + 5 * s)
    above = mask[up, a:b] & (lab[up, a:b] != c['i'])
    below = mask[dn, a:b] & (lab[dn, a:b] != c['i'])
    return bool(above.any() or below.any())


def coloured(img, core, reddish, k, s=1, line=None, dots=None):
    """The components of the colour's family that hold a core pixel. For expected.json's boxes,
    the text's other ink, as (x, y, w, h): into `line` every component with a core up to 15 * s
    high, whatever its shape (a glyph the size rules leave out); into `dots` every one under 5 * s
    high without a core (an ellipsis's dots, smoothed to no core pixel)."""
    r, g, b = img[..., 0], img[..., 1], img[..., 2]
    if reddish:
        mask = (r - np.maximum(g, b) >= 35 * k) & (r >= 60 * k)
    else:
        mask = (b - r >= 50 * k) & (b >= 110 * k)
    lab, n = label(mask)
    if n == 0:
        return mask, lab, []
    area, y0, y1, x0, x1, mx, mn = stats(lab, n, img)
    corepix = near(img, core * k, 14 if k == 1 else 8)
    hascore = np.bincount(lab[corepix], minlength=n + 1) > 0
    out = []
    for i in range(1, n + 1):
        h, w = int(y1[i] - y0[i] + 1), int(x1[i] - x0[i] + 1)
        if line is not None and hascore[i] and h <= 15 * s:
            line.append((int(x0[i]), int(y0[i]), w, h))
        if dots is not None and not hascore[i] and h < 5 * s:
            dots.append((int(x0[i]), int(y0[i]), w, h))
        if not hascore[i]:
            continue
        out.append(dict(i=i, x=int(x0[i]), y=int(y0[i]), w=w, h=h, area=int(area[i]),
                        fill=round(float(area[i]) / (h * w), 2)))
    return mask, lab, out


def danger(img, s, k):
    res = dict(glyph=[], button=[], graphlike=0, tall=0, line=[], dots=[])
    mask, lab, comps = coloured(img, DANGER, True, k, s, res['line'], res['dots'])
    white = near(img, WHITE * k, 10)
    for c in comps:
        if c['h'] >= 20 * s and c['w'] >= 40 * s and c['fill'] >= 0.5:
            sub = white[c['y']:c['y'] + c['h'], c['x']:c['x'] + c['w']]
            if sub.sum() >= 5 * s * s:
                res['button'].append((c['x'], c['y'], c['w'], c['h']))
                continue
        if 5 * s <= c['h'] <= 15 * s and 3 * s <= c['w'] <= 250 * s and c['area'] >= 10 * s * s:
            if continues(mask, lab, c, s):
                res['graphlike'] += 1
                continue
            rb = ring_bg(img, mask, c, 3 * s, k)
            on = max(rb, key=rb.get) if max(rb.values()) >= 0.5 else ''
            res['glyph'].append((c['x'], c['y'], c['w'], c['h'], c['fill'], on))
        elif c['h'] > 15 * s:
            res['tall'] += 1
    return res


def accent_on_row(img, s, k, line=None, dots=None):
    mask, lab, comps = coloured(img, ACCENT, False, k, s, line, dots)
    hits = []
    for c in comps:
        # a SemiBold "R" with subpixel smoothing fills about 0.76 of its box; a ticked check box or
        # a radio's filled ring fills more, and a commit dot carries on into its lane's line
        if not (6 * s <= c['h'] <= 15 * s and 3 * s <= c['w'] <= 250 * s and c['area'] >= 10 * s * s and c['fill'] <= 0.85):
            continue
        if continues(mask, lab, c, s):
            continue
        rb = ring_bg(img, mask, c, 3 * s, k)
        if max(rb.values()) >= 0.5:
            hits.append((c['x'], c['y'], c['w'], c['h'], c['fill'], max(rb, key=rb.get)))
    return hits


def read(path):
    return np.asarray(Image.open(path).convert('RGB')).astype(np.int16)


def scale_of(folder):
    return 2 if folder == 'real-window' else 1


def scan(path, folder):
    img = read(path)
    s = scale_of(folder)
    th = theme(img)
    r = dict(theme=th, w=img.shape[1], h=img.shape[0])
    # A dialog's backdrop: the dimmed window or panel background covers at least 2 % of the picture.
    k = DIM[th]
    under = DARK_BG[:2] if th == 'dark' else LIGHT_BG[:2]
    r['backdrop'] = sum(int(near(img, c * k, 1).sum()) for c in under) >= 0.02 * img.shape[0] * img.shape[1]
    layers = (('plain', 1.0), ('dimmed', k)) if r['backdrop'] else (('plain', 1.0),)
    for name in ('placeholder', 'danger', 'accent_row'):
        r[name + '_dimmed'] = [] if name != 'danger' else dict(glyph=[], button=[], graphlike=0, tall=0, line=[], dots=[])
    for layer, k in layers:
        line = []
        r['placeholder_' + layer] = placeholder(img, th, s, k, line)
        r['placeholder_line_' + layer] = line
        if th == 'dark':
            r['danger_' + layer] = danger(img, s, k)
            line, dots = [], []
            r['accent_row_' + layer] = accent_on_row(img, s, k, line, dots)
            r['accent_line_' + layer] = line
            r['accent_dots_' + layer] = dots
    return r


def double_placeholder(img, th, s, line=None):
    """The placeholder under two backdrops: None when the picture shows no second backdrop. Every
    small ink component in a box goes into `line` when given."""
    k = DIM[th] ** 2
    under = DARK_BG[:2] if th == 'dark' else LIGHT_BG[:2]
    if sum(int(near(img, c * k, 1).sum()) for c in under) < 0.02 * img.shape[0] * img.shape[1]:
        return None
    box = BOX[th] * k
    bg = near(img, box, 0)
    if bg.sum() < 50:
        return []
    inside = boxsum(bg, 4 * s) >= 0.3 * (8 * s + 1) ** 2
    ink = inside & ~near(img, box, 1)
    lab, n = label(ink)
    if n == 0:
        return []
    area, y0, y1, x0, x1, mx, mn = stats(lab, n, img)
    p = np.rint(PH[th] * k)
    hits = []
    for i in range(1, n + 1):
        h, w = y1[i] - y0[i] + 1, x1[i] - x0[i] + 1
        if h <= 16 * s and w <= 120 * s and line is not None:
            line.append((int(x0[i]), int(y0[i]), int(w), int(h)))
        if not (3 * s <= h <= 16 * s and w <= 120 * s and area[i] >= 4 * s * s):
            continue
        v = mx[i] if th == 'dark' else mn[i]
        if np.all(np.abs(v - p) <= 2):
            hits.append((int(x0[i]), int(y0[i]), int(w), int(h)))
    return hits


def recheck_dimmed(img, s):
    """The danger found only under a backdrop, checked again: a dimmed glyph must stay as dark as
    the dimmed danger colour (its brightest red at most 0.4 x 229 + 12) and lie on a dimmed
    background (the pixels round it mostly within 3 of a dark background x 0.4)."""
    k = 0.4
    bgs = [c * k for c in DARK_BG] + [H('#2F3542') * k, H('#2A3150') * k, H('#1C1F27') * k]
    mask, lab, comps = coloured(img, DANGER, True, k)
    n = int(lab.max())
    mx = stats(lab, n, img)[5] if n else None
    kept = []
    for c in comps:
        if not (5 * s <= c['h'] <= 15 * s and 3 * s <= c['w'] <= 250 * s and c['area'] >= 10 * s * s):
            continue
        if continues(mask, lab, c, s) or mx[c['i'], 0] > 0.4 * 229 + 12:
            continue
        a, b = max(0, c['y'] - 3 * s), c['y'] + c['h'] + 3 * s
        left, right = max(0, c['x'] - 3 * s), c['x'] + c['w'] + 3 * s
        sub, keep = img[a:b, left:right], ~mask[a:b, left:right]
        onbg = np.zeros(keep.shape, bool)
        for v in bgs:
            onbg |= near(sub, v, 3)
        if (onbg & keep).sum() >= 0.5 * max(1, keep.sum()):
            kept.append((c['x'], c['y'], c['w'], c['h']))
    return kept


def key(r):
    return f"{r['phase']}/{r['folder']}/{r['name']}"


def scan_one(item):
    """Scans one picture: its row of scan.json, with the second passes' results."""
    ph, folder, n, path = item
    r = dict(phase=ph, folder=folder, name=n)
    r.update(scan(path, folder))
    s = scale_of(folder)
    img = None
    if r['backdrop'] or not (r['placeholder_plain'] or r['placeholder_dimmed']):
        img = read(path)
        line = []
        hits = double_placeholder(img, r['theme'], s, line)
        if not (r['placeholder_plain'] or r['placeholder_dimmed']):
            # The second pass of the lists: only where the first found nothing.
            r['placeholder_double'] = dict(double_backdrop=hits is not None, hits=hits or [])
        else:
            # For expected.json alone: the window's placeholder under two backdrops (a dialog
            # over the settings screen) beside the dialog's own boxes.
            r['placeholder_double_boxes'] = hits or []
        r['placeholder_line_double'] = line
    if r['theme'] == 'dark' and not (r['danger_plain']['glyph'] or r['danger_plain']['button']) \
            and (r['danger_dimmed']['glyph'] or r['danger_dimmed']['button']):
        img = read(path) if img is None else img
        r['danger_dimmed_recheck'] = recheck_dimmed(img, s)
    # As scan.json stores it: tuples as lists.
    return json.loads(json.dumps(r))


# ---- What the detections say ------------------------------------------------------------------

def is_placeholder(r):
    return bool(r['placeholder_plain'] or r['placeholder_dimmed']) or is_double(r) or key(r) in TOOLTIP_OVER_FILTER


def is_double(r):
    return bool(r.get('placeholder_double', {}).get('hits'))


def is_danger_button(r):
    return r['theme'] == 'dark' and bool(r['danger_plain']['button'] or r['danger_dimmed']['button'])


def danger_glyphs(r):
    """The danger glyphs that count: lane 3's cut pieces left out, a dimmed-only danger rechecked."""
    if r['theme'] != 'dark' or key(r) in LANE3_CUT:
        return []
    plain = [g for g in r['danger_plain']['glyph'] if tuple(g[:4]) not in LANE3_PILL]
    if plain:
        return plain + [g for g in r['danger_dimmed']['glyph']]
    if 'danger_dimmed_recheck' in r:
        return r['danger_dimmed_recheck']
    return r['danger_dimmed']['glyph']


def is_danger(r):
    return is_danger_button(r) or bool(danger_glyphs(r))


def is_danger_on_row(r):
    return r['theme'] == 'dark' and any(g[5] in ('hover', 'selection') for g in r['danger_plain']['glyph'] + r['danger_dimmed']['glyph'])


def accent_hits(r):
    # The graph's working-changes ring (a 10-px circle) on the selected row, with no dash under it
    # (a repository without commits), is the one shape that passes the glyph rules: drop round ones.
    s = scale_of(r['folder'])
    return [h for h in r.get('accent_row_plain', []) + r.get('accent_row_dimmed', [])
            if not (abs(h[2] - h[3]) <= 1 and h[2] >= 9 * s)]


def is_accent(r):
    return r['theme'] == 'dark' and key(r) not in ACCENT_FRINGE and bool(accent_hits(r))


def changes(r):
    return is_placeholder(r) or is_danger(r) or is_accent(r)


DETECTORS = [('placeholder', is_placeholder), ('placeholder-double-backdrop', is_double), ('danger', is_danger),
             ('danger-button', is_danger_button), ('danger-on-row', is_danger_on_row), ('accent-on-row', is_accent)]


def lists_of(rows):
    lists = {name: [key(r) for r in rows if f(r)] for name, f in DETECTORS}
    lists['any'] = [key(r) for r in rows if changes(r)]
    lists['none'] = [key(r) for r in rows if not changes(r)]
    return lists


def report_of(rows, lists):
    out = []
    table = defaultdict(lambda: defaultdict(int))
    for r in rows:
        t = table[(r['phase'], r['folder'])]
        t['pictures'] += 1
        t['dark'] += r['theme'] == 'dark'
        for name, f in DETECTORS:
            t[name] += f(r)
        t['any'] += changes(r)
    cols = ['pictures', 'dark', 'placeholder', 'placeholder-double-backdrop', 'danger', 'danger-button', 'danger-on-row', 'accent-on-row', 'any']
    out.append('| Phase | Folder | ' + ' | '.join(cols) + ' |')
    out.append('|' + '---|' * (len(cols) + 2))
    tot = defaultdict(lambda: defaultdict(int))
    for (p, f), t in sorted(table.items()):
        out.append(f'| {p} | {f} | ' + ' | '.join(str(t[c]) for c in cols) + ' |')
        for c in cols:
            tot[f][c] += t[c]
            tot['all'][c] += t[c]
    for f in ('scripted', 'real-window', 'all'):
        out.append(f'| **all** | {f} | ' + ' | '.join(str(tot[f][c]) for c in cols) + ' |')

    found = set(lists['danger-button'])
    planned = set()
    for (folder, phase), ids in PLAN_DANGER_LABELS.items():
        for i in ids.split():
            m = [key(r) for r in rows if r['phase'] == phase and r['folder'] == folder and r['name'].startswith(i + '-')]
            planned.update(m)
            if not m:
                out.append(f'plan id without a picture: {phase} {folder} {i}')
    out.append('')
    out.append(f'plan list: {len(planned)} found by the button detector: {len(planned & found)}')
    out.append(f'in the plan, not found: {sorted(planned - found)}')
    out.append(f'found, not in the plan: {sorted(found - planned)}')
    return '\n'.join(out) + '\n'


def grown(hit, g, w, h):
    x, y, bw, bh = hit[:4]
    return [max(0, x - g), max(0, y - g), min(w, x + bw + g), min(h, y + bh + g)]


def same_line(a, b):
    """Whether two boxes share a text line: their rows overlap."""
    return a[1] < b[3] and b[1] < a[3]


def gap_across(a, b):
    """The columns between two boxes (0 or less when they overlap across)."""
    return max(a[0], b[0]) - min(a[2], b[2])


def union(a, b):
    return [min(a[0], b[0]), min(a[1], b[1]), max(a[2], b[2]), max(a[3], b[3])]


def along_line(box, line, gap, dots=(), dot_gap=0):
    """The box widened along its text line, again and again: every component of `line` (grown
    boxes) whose rows overlap the box's within `gap` across of it, and every one of `dots` within
    `dot_gap`, joins it. A dot's gap is short, so that the fringes of the grey text beside a letter
    stay out while an ellipsis's dots, a pixel or two apart, come in."""
    box = list(box)
    left = [(c, gap) for c in line] + [(c, dot_gap) for c in dots]
    grew = True
    while grew:
        grew = False
        for item in left:
            c, limit = item
            if same_line(box, c) and gap_across(box, c) <= limit:
                box = union(box, c)
                left.remove(item)
                grew = True
                break
    return box


def joined(boxes, gap):
    """Joins boxes of one text line (their rows overlap) whose gap across is at most `gap`."""
    boxes = [list(b) for b in boxes]
    merged = True
    while merged:
        merged = False
        for i in range(len(boxes)):
            for j in range(i + 1, len(boxes)):
                if same_line(boxes[i], boxes[j]) and gap_across(boxes[i], boxes[j]) <= gap:
                    boxes[i] = union(boxes[i], boxes[j])
                    del boxes[j]
                    merged = True
                    break
            if merged:
                break
    return sorted(boxes, key=lambda b: (b[1], b[0]))


def expected_of(rows):
    """Per picture the round changes, the boxes it may change in (the module's docstring): each
    hit grown by GROW px, widened along its text line by the ink of its kind there (a letter or an
    ellipsis the detector's rules leave out), joined with the hits of its line within JOIN px; a
    danger button whole; the tooltip pictures' boxes measured by hand."""
    expected = {}
    for r in rows:
        if not changes(r):
            continue
        s = scale_of(r['folder'])
        g, w, h, gap = GROW * s, r['w'], r['h'], JOIN * s
        texts, buttons = [], []

        def add(hits, line, dots=()):
            ink = [grown(c, g, w, h) for c in line]
            small = [grown(c, g, w, h) for c in dots]
            texts.extend(along_line(grown(hit, g, w, h), ink, gap, small, DOT_GAP * s) for hit in hits)

        if is_placeholder(r):
            add(r['placeholder_plain'] + r['placeholder_dimmed'] + r.get('placeholder_double', {}).get('hits', [])
                + r.get('placeholder_double_boxes', []),
                r.get('placeholder_line_plain', []) + r.get('placeholder_line_dimmed', []) + r.get('placeholder_line_double', []))
        if is_danger(r):
            add(danger_glyphs(r), r['danger_plain'].get('line', []) + r['danger_dimmed'].get('line', []),
                r['danger_plain'].get('dots', []) + r['danger_dimmed'].get('dots', []))
            buttons += r['danger_plain']['button'] + r['danger_dimmed']['button']
        if is_accent(r):
            add(accent_hits(r), r.get('accent_line_plain', []) + r.get('accent_line_dimmed', []),
                r.get('accent_dots_plain', []) + r.get('accent_dots_dimmed', []))
        boxes = joined(texts, gap)
        boxes += [grown(b, g, w, h) for b in buttons]
        boxes += TOOLTIP_OVER_FILTER.get(key(r), [])
        expected[key(r)] = boxes
    return expected


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('pictures', help='the folder holding phase-N/scripted and phase-N/real-window')
    parser.add_argument('output', help='the folder the scan writes into')
    parser.add_argument('--jobs', type=int, default=min(4, os.cpu_count() or 1), help='pictures scanned at once')
    args = parser.parse_args()
    os.makedirs(args.output, exist_ok=True)

    items = list(pictures(args.pictures))
    if not items:
        sys.exit(f'No pictures under {args.pictures}.')
    with ProcessPoolExecutor(max_workers=max(1, args.jobs)) as pool:
        rows = []
        for done, r in enumerate(pool.map(scan_one, items, chunksize=4), start=1):
            rows.append(r)
            if done % 100 == 0 or done == len(items):
                print(f'{done} of {len(items)} pictures scanned', flush=True)
    rows.sort(key=lambda r: (r['phase'], r['folder'], r['name']))

    with open(os.path.join(args.output, 'scan.json'), 'w', encoding='utf-8') as fh:
        json.dump(rows, fh)
    lists = lists_of(rows)
    for name in LISTS:
        with open(os.path.join(args.output, f'colours-{name}.txt'), 'w', encoding='utf-8', newline='\n') as fh:
            fh.write('\n'.join(lists[name]) + ('\n' if lists[name] else ''))
    report = report_of(rows, lists)
    with open(os.path.join(args.output, 'report.txt'), 'w', encoding='utf-8', newline='\n') as fh:
        fh.write(report)
    with open(os.path.join(args.output, 'expected.json'), 'w', encoding='utf-8', newline='\n') as fh:
        # One picture per line, its boxes after it.
        expected = expected_of(rows)
        fh.write('{\n' + ',\n'.join(f'{json.dumps(name)}: {json.dumps(expected[name])}' for name in sorted(expected)) + '\n}\n')
    print(report, end='')
    print(f'{len(lists["any"])} of {len(rows)} pictures change; lists, report.txt and expected.json in {args.output}')


if __name__ == '__main__':
    main()
