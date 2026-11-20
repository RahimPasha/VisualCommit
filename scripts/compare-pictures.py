#!/usr/bin/env python3
"""Compares the walk-through's pictures with reference pictures, for a gate's inspection.

For every picture under the new folder that has a twin under the reference folder (the same
path below `phase-N/`), it writes a difference picture (the new picture dimmed, every changed
pixel in red) and lists the boxes of changed pixels with the regions of the window they lie in,
so that an inspector compares rather than re-reads (docs/phase-5-design.md 15.7). Pictures that
are the same file are only counted.

    python scripts/compare-pictures.py <reference folder> <new folder> <output folder>
        [--expected <file>]...

Both folders hold `phase-N/scripted/*.png` (and `phase-N/real-window/*.png`), as
`artifacts/visual` does. The output folder gets `diff/<phase>/<kind>/<name>.png` and
`summary.md`. Needs Pillow and NumPy.

With `--expected <file>` (docs/phase-8a-design.md 0.7, 2.6; D419), the changes a merge is meant to
make are told apart from the rest. The file is JSON: it maps `phase-N/<folder>/<name>.png`, or an
`fnmatch` pattern of such paths, to boxes `[left, top, right, bottom]` (right and bottom
exclusive) in the picture's own pixels, never scaled here, in which the picture may change, as
`scripts/scan-contrast.py` writes its `expected.json`. The option may be given more than once; a
picture's boxes are those of every key that matches it, exact or a pattern, from every file,
united. The summary then has four headings, always in this order: "Changed outside the expected
regions" (per picture, the changed pixels outside its boxes), "Changed, not expected" (a changed
picture no key matches), "Expected, not changed" (a picture with boxes whose pixels did not change)
and "Changed size" (a picture whose width or height is not its reference's, unexpected whatever its
boxes). Its difference picture draws a change inside the boxes in amber, one outside them in red,
and each box's edge in blue where nothing changed. Without the option the output is as before.
"""
import argparse
import fnmatch
import hashlib
import json
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

# The difference picture's colours with --expected: a change inside the expected boxes, one
# outside them, and a box's edge where nothing changed.
INSIDE = (255, 176, 0)
OUTSIDE = (255, 0, 0)
EDGE = (0, 120, 255)


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


def mask_of(expected, height, width):
    """The pixels inside the expected boxes (each clipped to the picture), and the boxes' edges."""
    inside = np.zeros((height, width), dtype=bool)
    edges = np.zeros((height, width), dtype=bool)
    for left, top, right, bottom in expected:
        left, top = max(0, int(left)), max(0, int(top))
        right, bottom = min(width, int(right)), min(height, int(bottom))
        if left >= right or top >= bottom:
            continue
        inside[top:bottom, left:right] = True
        edges[top, left:right] = True
        edges[bottom - 1, left:right] = True
        edges[top:bottom, left] = True
        edges[top:bottom, right - 1] = True
    return inside, edges


def compare(reference, new, out, expected=None):
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
    if expected is None:
        shown[changed] = (255, 0, 0)
    else:
        inside, edges = mask_of(expected, height, width)
        shown[edges & ~changed] = EDGE
        shown[changed & inside] = INSIDE
        shown[changed & ~inside] = OUTSIDE
    out.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(shown).save(out)
    regions = sorted({name for box in boxes for name in regions_of(box[:4], width, height)})
    result = {
        'pixels': int(changed.sum()),
        'largest': int(delta.max()),
        'boxes': boxes,
        'regions': regions,
    }
    if expected is not None:
        outside = changed & ~inside
        outside_boxes = boxes_of(outside)
        result['outside'] = {
            'pixels': int(outside.sum()),
            'largest': int(delta[outside].max()) if outside.any() else 0,
            'boxes': outside_boxes,
            'regions': sorted({name for box in outside_boxes for name in regions_of(box[:4], width, height)}),
        }
    return result


class Expected:
    """The boxes of every --expected file, by exact path and by pattern."""

    def __init__(self, files):
        self.files = files
        self.exact = {}
        self.patterns = []
        for file in files:
            with open(file, encoding='utf-8') as handle:
                entries = json.load(handle)
            if not isinstance(entries, dict):
                sys.exit(f'{file}: not a JSON object of picture paths and their boxes.')
            for key, boxes in entries.items():
                if not isinstance(boxes, list) or any(not (isinstance(box, list) and len(box) == 4) for box in boxes):
                    sys.exit(f'{file}: the boxes of {key} are not a list of [left, top, right, bottom].')
                if any(character in key for character in '*?['):
                    self.patterns.append((key, boxes))
                else:
                    self.exact.setdefault(key, []).extend(boxes)

    def boxes_for(self, relative):
        """The boxes of every key that matches the picture, exact or a pattern, from every file; None when none matches."""
        path = relative.as_posix()
        matched = path in self.exact
        boxes = list(self.exact.get(path, []))
        for pattern, pattern_boxes in self.patterns:
            if fnmatch.fnmatchcase(path, pattern):
                matched = True
                boxes.extend(pattern_boxes)
        return boxes if matched else None


def box_list(boxes):
    """The first six boxes of a list, and how many more there are."""
    text = '; '.join(f'({b[0]},{b[1]})-({b[2]},{b[3]}) {b[4]} px' for b in boxes[:6])
    more = f' and {len(boxes) - 6} more' if len(boxes) > 6 else ''
    return text + more


def expected_summary(reference, new, expected, same, changed, only_new, only_old):
    """The summary with --expected: the counts, the four headings, then the pictures without a twin."""
    outside, not_expected, not_changed, size = [], [], [], []
    inside_only = 0
    for relative in same:
        if expected.boxes_for(relative) is not None:
            not_changed.append(relative)
    for relative, result, boxes in changed:
        if 'size' in result:
            size.append((relative, result))
        elif result['pixels'] == 0:
            # Another file with the same pixels.
            if boxes is not None:
                not_changed.append(relative)
        elif boxes is None:
            not_expected.append((relative, result))
        elif result['outside']['pixels'] > 0:
            outside.append((relative, result))
        else:
            inside_only += 1
    not_changed.sort()
    lines = [
        '# Pictures compared with their references',
        '',
        f'Reference: `{reference}`. New: `{new}`.',
        '',
        'Expected regions: ' + ', '.join(f'`{file}`' for file in expected.files) + '.',
        '',
        f'{len(same)} the same file, {len(changed)} changed, {len(only_new)} new (no reference), '
        f'{len(only_old)} in the reference only.',
        '',
        f'{inside_only} changed only inside their expected regions; {len(outside)} changed outside them, '
        f'{len(not_expected)} changed and not expected, {len(not_changed)} expected and not changed, '
        f'{len(size)} changed size.',
        '',
        f'## Changed outside the expected regions ({len(outside)})',
        '',
    ]
    for relative, result in outside:
        part = result['outside']
        regions = ', '.join(part['regions']) or 'no region'
        lines.append(f'- `{relative.as_posix()}`: {part["pixels"]} px outside of {result["pixels"]} changed, '
                     f'largest change outside {part["largest"]}; {box_list(part["boxes"])} ({regions})')
    lines.append('')
    lines.append(f'## Changed, not expected ({len(not_expected)})')
    lines.append('')
    for relative, result in not_expected:
        regions = ', '.join(result['regions']) or 'no region'
        lines.append(f'- `{relative.as_posix()}`: {result["pixels"]} px, largest change {result["largest"]}; '
                     f'{box_list(result["boxes"])} ({regions})')
    lines.append('')
    lines.append(f'## Expected, not changed ({len(not_changed)})')
    lines.append('')
    lines.extend(f'- `{relative.as_posix()}`' for relative in not_changed)
    lines.append('')
    lines.append(f'## Changed size ({len(size)})')
    lines.append('')
    lines.extend(f'- `{relative.as_posix()}`: size {result["size"]}' for relative, result in size)
    lines.append('')
    for title, items in (('New, no reference', only_new), ('In the reference only', only_old)):
        lines.append(f'## {title} ({len(items)})')
        lines.append('')
        lines.extend(f'- `{relative.as_posix()}`' for relative in items)
        lines.append('')
    counts = (f'{inside_only} inside, {len(outside)} outside, {len(not_expected)} not expected, '
              f'{len(not_changed)} not changed, {len(size)} changed size')
    return lines, counts


class Usage(argparse.ArgumentParser):
    """The command's arguments; a mistake prints the module's text, as before the option."""

    def error(self, message):
        sys.exit(f'{message}\n{__doc__}')


def main():
    parser = Usage(add_help=False)
    parser.add_argument('reference')
    parser.add_argument('new')
    parser.add_argument('out')
    parser.add_argument('--expected', action='append', default=[])
    arguments = parser.parse_args()
    reference, new, out = Path(arguments.reference), Path(arguments.new), Path(arguments.out)
    expected = Expected(arguments.expected) if arguments.expected else None
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
        if expected is None:
            changed.append((relative, compare(old, new / relative, out / 'diff' / relative)))
        else:
            boxes = expected.boxes_for(relative)
            changed.append((relative, compare(old, new / relative, out / 'diff' / relative, boxes or []), boxes))
    only_old = sorted(old_pictures - set(pictures))

    if expected is not None:
        lines, counts = expected_summary(reference, new, expected, same, changed, only_new, only_old)
        out.mkdir(parents=True, exist_ok=True)
        (out / 'summary.md').write_text('\n'.join(lines), encoding='utf-8')
        print(f'{len(same)} same, {len(changed)} changed, {len(only_new)} new, {len(only_old)} reference only; '
              f'{counts}; summary in {out / "summary.md"}')
        return

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
