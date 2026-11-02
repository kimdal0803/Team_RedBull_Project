"""Step 1: remove the flat gray background from each pose (high-res).

Output: work/cut/<name>.png (RGBA, full res) and work/check/<name>.jpg (on a colored bg for viewing).
"""
import sys, os, glob, json
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

ROOT = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(ROOT, 'src')
OUT = os.path.join(ROOT, 'work', 'cut')
CHK = os.path.join(ROOT, 'work', 'check')
os.makedirs(OUT, exist_ok=True); os.makedirs(CHK, exist_ok=True)


def bg_model(a):
    """Smooth estimate of the background color at every pixel."""
    h, w, _ = a.shape
    border = np.concatenate([a[:16].reshape(-1, 3), a[-16:].reshape(-1, 3),
                             a[:, :16].reshape(-1, 3), a[:, -16:].reshape(-1, 3)])
    med = np.median(border, axis=0)
    sat = a.max(2) - a.min(2)
    lum = a.mean(2)
    cand = (sat < 14) & (np.abs(lum - med.mean()) < 22)
    # normalized convolution on a downscaled grid (fast)
    s = 8
    small = a[::s, ::s]
    cm = cand[::s, ::s].astype(float)
    sig = 12
    num = np.stack([ndi.gaussian_filter(small[..., c] * cm, sig) for c in range(3)], -1)
    den = ndi.gaussian_filter(cm, sig)[..., None]
    est = np.where(den > 1e-3, num / np.maximum(den, 1e-6), med)
    est = np.stack([ndi.zoom(est[..., c], s, order=1)[:h, :w] for c in range(3)], -1)
    return est, med


def vertical_runs(mask):
    """length of the vertical run of True each pixel belongs to"""
    h, w = mask.shape
    m = mask.astype(np.int32)
    down = np.zeros_like(m)
    up = np.zeros_like(m)
    for y in range(h):
        if y == 0:
            down[y] = m[y]
        else:
            down[y] = (down[y - 1] + 1) * m[y]
    for y in range(h - 1, -1, -1):
        if y == h - 1:
            up[y] = m[y]
        else:
            up[y] = (up[y + 1] + 1) * m[y]
    return np.where(mask, down + up - 1, 0)


def remove_ground_shadow(a, fg, opts, diff):
    """Ground shadow = thin horizontal black strokes (+ bg-colored gaps) at the feet.
    1) dark/fringe pixels in the bottom band whose vertical run is short are removed
       (boots, legs and blades belong to long runs and survive);
    2) thick leftovers are listed per image as 'kill_dark' rects (only dark/fringe pixels inside);
    3) 'protect' rects are never touched."""
    ys, xs = np.nonzero(fg)
    ybot = ys.max()
    band = opts.get('band', 150)
    vmin = opts.get('vmin', 60)
    runs = vertical_runs(fg)
    lum = a.mean(2)
    sat = a.max(2) - a.min(2)
    yy = np.arange(fg.shape[0])[:, None]
    inband = (yy >= ybot - band)
    dark = lum < 110
    fringe = (sat < 24) & (lum > 120)
    shadowish = dark | fringe
    kill = fg & inband & (runs < vmin) & shadowish
    for (x0, y0, x1, y1) in opts.get('kill_dark', []):
        kill[y0:y1, x0:x1] |= (fg & shadowish)[y0:y1, x0:x1]
    for (x0, y0, x1, y1) in opts.get('protect', []):
        kill[y0:y1, x0:x1] = False
    fg = fg & ~kill
    lab, n = ndi.label(fg)
    if n > 1:
        areas = ndi.sum(np.ones(fg.shape), lab, index=np.arange(1, n + 1))
        keep = np.zeros(n + 1, bool)
        keep[1:] = areas >= areas.max() * 0.004
        fg = keep[lab]
    return fg


def cut(name, opts):
    a = np.asarray(Image.open(os.path.join(SRC, name + '.png')).convert('RGB')).astype(np.float32)
    h, w, _ = a.shape
    est, med = bg_model(a)
    diff = np.sqrt(((a - est) ** 2).sum(2))
    T = opts.get('T', 20.0)
    bgl = diff <= T
    lab, n = ndi.label(bgl)
    border_labels = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    bg = np.isin(lab, list(border_labels))
    # enclosed background pockets (between legs, under arms): big + flat
    areas = ndi.sum(np.ones_like(diff), lab, index=np.arange(1, n + 1))
    dmean = ndi.mean(diff, lab, index=np.arange(1, n + 1))
    # local texture: std of luminance inside the region
    lum = a.mean(2)
    lmean = ndi.mean(lum, lab, index=np.arange(1, n + 1))
    lsq = ndi.mean(lum * lum, lab, index=np.arange(1, n + 1))
    lstd = np.sqrt(np.maximum(lsq - lmean ** 2, 0))
    min_area = opts.get('pocket_min_area', 900)
    pockets = []
    for i in range(n):
        L = i + 1
        if L in border_labels:
            continue
        if areas[i] >= min_area and dmean[i] < opts.get('pocket_dmean', 9.0) and lstd[i] < opts.get('pocket_std', 6.0):
            pockets.append(L)
    if pockets:
        bg |= np.isin(lab, pockets)
    fg = ~bg
    # close tiny pinholes inside the figure
    fg = ndi.binary_fill_holes(fg) & ~np.isin(lab, pockets) if pockets else ndi.binary_fill_holes(fg)
    # remove specks; keep components per options
    flab, fn = ndi.label(fg)
    fareas = ndi.sum(np.ones_like(diff), flab, index=np.arange(1, fn + 1))
    keep = np.zeros(fn + 1, bool)
    big = fareas.max()
    seeds = opts.get('keep_points')  # (x, y) points that must be inside kept components
    if seeds:
        for (x, y) in seeds:
            keep[flab[y, x]] = True
    else:
        for i in range(fn):
            if fareas[i] >= big * opts.get('keep_ratio', 0.02):
                keep[i + 1] = True
    keep[0] = False
    fg = keep[flab]
    # ground shadow: thin horizontal strokes in the bottom band
    if opts.get('shadow', True):
        fg = remove_ground_shadow(a, fg, opts, diff)
    # user rectangles to erase (x0,y0,x1,y1)
    for r in opts.get('erase', []):
        x0, y0, x1, y1 = r
        fg[y0:y1, x0:x1] = False
    # soft alpha at the rim: blend factor from diff
    soft = np.clip((diff - T * 0.5) / (T * 1.5), 0, 1)
    rim = fg & ~ndi.binary_erosion(fg, iterations=2)
    alpha = np.where(fg, 1.0, 0.0)
    alpha = np.where(rim, np.maximum(soft, 0.0), alpha)
    rgba = np.dstack([a, alpha * 255]).clip(0, 255).astype(np.uint8)
    Image.fromarray(rgba, 'RGBA').save(os.path.join(OUT, name + '.png'))
    # check image
    chk = np.zeros_like(a)
    chk[:] = (40, 160, 90)
    comp = a * alpha[..., None] + chk * (1 - alpha[..., None])
    im = Image.fromarray(comp.astype(np.uint8)).resize((768, 768), Image.LANCZOS)
    im.save(os.path.join(CHK, name + '.jpg'), quality=88)
    ys, xs = np.nonzero(fg)
    return dict(name=name, bbox=[int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())],
                pockets=len(pockets), comps=int(keep.sum()), bgmed=med.round(1).tolist())


if __name__ == '__main__':
    opts_all = json.load(open(os.path.join(ROOT, 'cut_opts.json'))) if os.path.exists(os.path.join(ROOT, 'cut_opts.json')) else {}
    names = sys.argv[1:] or [os.path.splitext(os.path.basename(p))[0] for p in sorted(glob.glob(os.path.join(SRC, '*.png')))]
    for nm in names:
        o = dict(opts_all.get('_default', {})); o.update(opts_all.get(nm, {}))
        print(cut(nm, o))
