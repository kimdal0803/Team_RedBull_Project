"""Animated previews that play each sheet on the real pattern timings (speed x1)."""
import os, json, math
import numpy as np
from PIL import Image, ImageDraw
from timeline import PATTERNS, ARCS, TREMBLE_BEFORE_ATTACK, expand

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, 'out')
meta = json.load(open(os.path.join(OUT, 'sheets.json')))
FW, FH = meta['frameWidth'], meta['frameHeight']
PX, PYB = meta['pivotPx']
PY = FH - PYB                      # pivot row from top
PPU = meta['pixelsPerUnit']
FPS = 25
SCALE = 2
BG = (46, 44, 54)
MOVE_CAP = 90                      # px of travel shown at most (real dashes are longer)


def load_frames(name):
    sh = Image.open(os.path.join(OUT, 'sheets', name + '.png')).convert('RGBA')
    n = sh.width // FW
    return [sh.crop((i * FW, 0, (i + 1) * FW, FH)) for i in range(n)]


IDLE = load_frames('Arakus_Idle')


def place(canvas, fr, x, y, sx=1.0, sy=1.0):
    """draw frame with its pivot at canvas (x, y) (game px), scaled about the pivot (nearest)"""
    if abs(sx - 1) > 1e-3 or abs(sy - 1) > 1e-3:
        w, h = max(1, round(FW * sx)), max(1, round(FH * sy))
        fr = fr.resize((w, h), Image.NEAREST)
        px, py = PX * sx, PY * sy
    else:
        px, py = PX, PY
    canvas.alpha_composite(fr, (int(round(x - px)), int(round(y - py))))


def render(sheet):
    frames = load_frames(sheet)
    p = PATTERNS[sheet]
    keys, ev = expand(sheet)
    dash = p.get('dash')
    travel = 0.0
    if dash:
        travel = dash[2] * PPU
        if abs(travel) > MOVE_CAP:
            travel = math.copysign(MOVE_CAP, travel)
    pre, post = 0.6, 0.5
    total = pre + ev['end'] + post
    arc = ARCS.get(sheet)
    head = int(arc[2] * PPU) if arc else 0
    W = FW + int(abs(travel)) + 20
    H = FH + 14 + head
    ground = FH + 2 + head
    x0 = 10 + PX + (abs(travel) if travel < 0 else 0)
    out = []
    n = int(total * FPS)
    for k in range(n):
        t = k / FPS - pre
        cv = Image.new('RGBA', (W, H), BG + (255,))
        d = ImageDraw.Draw(cv)
        d.line([(0, ground), (W, ground)], fill=(90, 120, 90, 255))
        x, yoff, sx, sy = x0, 0.0, 1.0, 1.0
        if t < 0 or t >= ev['end']:
            tt = (t + pre) if t < 0 else (t - ev['end'])
            fr = IDLE[[0, 1, 2, 1][int(tt * 4) % 4]]
            if t >= ev['end']:
                x = x0 + travel
        else:
            cur = keys[0]
            for key in keys:
                if key[0] <= t:
                    cur = key
            fr = frames[cur[1]]
            since = t - cur[0]
            if 'punch' in cur[2] and since < 0.14:
                a = 1 - since / 0.14
                sx, sy = 1 + 0.07 * a, 1 - 0.06 * a
            if cur[0] == 0 and since < 0.12:            # squash into the telegraph pose
                a = 1 - since / 0.12
                sy = 1 - 0.06 * a
            if ev['attack'] >= 0.45 and ev['attack'] - TREMBLE_BEFORE_ATTACK <= t < ev['attack']:
                x += 1 if (k % 2) else -1
            if dash:
                ds, de = ev['dashStart'], ev['dashEnd']
                u = 0 if t < ds else (1 if t >= de else (t - ds) / (de - ds))
                x = x0 + travel * (1 - (1 - u) ** 2)
            if arc:
                a0, a1 = ev[arc[0]], ev[arc[1]]
                if a0 <= t < a1:
                    u = (t - a0) / (a1 - a0)
                    yoff = -4 * arc[2] * PPU * u * (1 - u)
        place(cv, fr, x, ground + yoff, sx, sy)
        im = cv.convert('RGB').resize((W * SCALE, H * SCALE), Image.NEAREST)
        out.append(im)
    path = os.path.join(OUT, 'preview', sheet + '.gif')
    out[0].save(path, save_all=True, append_images=out[1:], duration=int(1000 / FPS), loop=0, optimize=True)
    return path, len(out)


if __name__ == '__main__':
    os.makedirs(os.path.join(OUT, 'preview'), exist_ok=True)
    for s in PATTERNS:
        print(render(s))
