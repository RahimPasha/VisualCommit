#!/usr/bin/env python3
"""Compares the walk-through's pictures with reference pictures, for a gate's inspection.

For every picture under the new folder that has a twin under the reference folder (the same
path below `phase-N/`), it writes a difference picture (the new picture dimmed, every changed
pixel in red) and lists the boxes of changed pixels with the regions of the window they lie in,
so that an inspector compares rather than re-reads (docs/phase-5-design.md 15.7). Pictures that
are the same file are only counted.

    python scripts/compare-pictures.py <reference folder> <new folder> <output folder>

Both folders hold `phase-N/scripted/*.png` (and `phase-N/real-window/*.png`), as
`artifacts/visual` does. The output folder gets `diff/<phase>/<kind>/<name>.png` and
`summary.md`. Needs Pillow and NumPy.
"""
import hashlib
import sys
from pathlib import Path

import numpy as np
from PIL import Image

# WindowLayout.cs, in logical pixels; the headless pictures are drawn at scale 1.
TAB_STRIP = 36
TOOLBAR = 52
STATUS_BAR = 26
LEFT_PANEL = 260
RIGHT_PANEL = 400
COLUMN_HEADERS = 28

# A cell of the grid that boxes are grown from; changed cells that touch form one box.
CELL = 8


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def regions_of(box, width, height):
    """The window's regions a box (left, top, right, bottom; exclusive) overlaps."""
    left, top, right, bottom = box
    names = []

    def overlaps(x0, y0, x1, y1):
        return left < x1 and right > x0 and top < y1 and bottom > y0

    body_top = TAB_STRIP + TOOLBAR
    body_bottom = height - STATUS_BAR
    if overlaps(0, 0, width, TAB_STRIP):
        names.append('tab strip')
    if overlaps(0, TAB_STRIP, width, body_top):
        names.append('toolbar')
    if overlaps(0, body_bottom, width, height):
        names.append('status bar')
    if overlaps(0, body_top, LEFT_PANEL, body_bottom):
        names.append('left panel')
    if overlaps(width - RIGHT_PANEL, body_top, width, body_bottom):
        names.append('right panel')
    if overlaps(LEFT_PANEL, body_top, width - RIGHT_PANEL, body_top + COLUMN_HEADERS):
        names.append('column headers')
    if overlaps(LEFT_PANEL, body_top + COLUMN_HEADERS, width - RIGHT_PANEL, body_bottom):
        names.append('graph area')
    return names


def boxes_of(changed):
    """Boxes around groups of touching changed cells."""
    height, width = changed.shape
    rows, cols = (height + CELL - 1) // CELL, (width + CELL - 1) // CELL
    padded = np.zeros((rows * CELL, cols * CELL), dtype=bool)
    padded[:height, :width] = changed
    cells = padded.reshape(rows, CELL, cols, CELL).any(axis=(1, 3))
    seen = np.zeros_like(cells)
    boxes = []
    for row, col in zip(*np.nonzero(cells)):
        if seen[row, col]:
            continue
        stack = [(row, col)]
        seen[row, col] = True
        found = []
        while stack:
            r, c = stack.pop()
            found.append((r, c))
            for dr in (-1, 0, 1):
                for dc in (-1, 0, 1):
                    nr, nc = r + dr, c + dc
                    if 0 <= nr < rows and 0 <= nc < cols and cells[nr, nc] and not seen[nr, nc]:
                        seen[nr, nc] = True
                        stack.append((nr, nc))
        rs = [r for r, _ in found]
        cs = [c for _, c in found]
        top, left = min(rs) * CELL, min(cs) * CELL
        bottom, right = min((max(rs) + 1) * CELL, height), min((max(cs) + 1) * CELL, width)
        part = changed[top:bottom, left:right]
        ys, xs = np.nonzero(part)
        boxes.append((left + xs.min(), top + ys.min(), left + xs.max() + 1, top + ys.max() + 1, int(part.sum())))
    return sorted(boxes, key=lambda b: (b[1], b[0]))


def compare(reference, new, out):
    old = np.asarray(Image.open(reference).convert('RGBA')).astype(np.int16)
    now_image = Image.open(new).convert('RGBA')
    now = np.asarray(now_image).astype(np.int16)
    if old.shape != now.shape:
        return {'size': f'{old.shape[1]}x{old.shape[0]} -> {now.shape[1]}x{now.shape[0]}'}
    delta = np.abs(old - now).max(axis=2)
    changed = delta > 0
    height, width = changed.shape
    boxes = boxes_of(changed)
    shown = (np.asarray(now_image.convert('RGB')).astype(np.float32) * 0.35).astype(np.uint8)
    shown[changed] = (255, 0, 0)
    out.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(shown).save(out)
    regions = sorted({name for box in boxes for name in regions_of(box[:4], width, height)})
    return {
        'pixels': int(changed.sum()),
        'largest': int(delta.max()),
        'boxes': boxes,
        'regions': regions,
    }


def main():
    if len(sys.argv) != 4:
        sys.exit(__doc__)
    reference, new, out = (Path(argument) for argument in sys.argv[1:])
    same, changed, only_new, only_old = [], [], [], []
    pictures = sorted(path.relative_to(new) for path in new.rglob('*.png'))
    old_pictures = {path.relative_to(reference) for path in reference.rglob('*.png')}
    for relative in pictures:
        old = reference / relative
        if relative not in old_pictures:
            only_new.append(relative)
            continue
        if digest(old) == digest(new / relative):
            same.append(relative)
            continue
        changed.append((relative, compare(old, new / relative, out / 'diff' / relative)))
    only_old = sorted(old_pictures - set(pictures))

    lines = [
        '# Pictures compared with their references',
        '',
        f'Reference: `{reference}`. New: `{new}`.',
        '',
        f'{len(same)} the same file, {len(changed)} changed, {len(only_new)} new (no reference), '
        f'{len(only_old)} in the reference only.',
        '',
    ]
    groups = {}
    for relative, result in changed:
        key = ', '.join(result.get('regions', [])) or result.get('size', 'unknown')
        groups.setdefault(key, []).append((relative, result))
    lines.append('## Changed, grouped by the regions their changes lie in')
    lines.append('')
    for key in sorted(groups, key=lambda k: (-len(groups[k]), k)):
        lines.append(f'### {key} ({len(groups[key])})')
        lines.append('')
        for relative, result in groups[key]:
            if 'size' in result:
                lines.append(f'- `{relative.as_posix()}`: size {result["size"]}')
                continue
            boxes = '; '.join(f'({b[0]},{b[1]})-({b[2]},{b[3]}) {b[4]} px' for b in result['boxes'][:6])
            more = f' and {len(result["boxes"]) - 6} more' if len(result['boxes']) > 6 else ''
            lines.append(f'- `{relative.as_posix()}`: {result["pixels"]} px, largest change {result["largest"]}; {boxes}{more}')
        lines.append('')
    for title, items in (('New, no reference', only_new), ('In the reference only', only_old)):
        lines.append(f'## {title} ({len(items)})')
        lines.append('')
        lines.extend(f'- `{relative.as_posix()}`' for relative in items)
        lines.append('')
    out.mkdir(parents=True, exist_ok=True)
    (out / 'summary.md').write_text('\n'.join(lines), encoding='utf-8')
    print(f'{len(same)} same, {len(changed)} changed, {len(only_new)} new, {len(only_old)} reference only; '
          f'summary in {out / "summary.md"}')


if __name__ == '__main__':
    main()
