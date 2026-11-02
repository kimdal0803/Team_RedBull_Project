"""Step 2: high-res cutout -> pixel art.

downscale(rgba, scale, ...)  premultiplied box downscale -> small RGBA float
quantize with a shared palette, alpha threshold, outline pass.
"""
import os, json
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

ROOT = os.path.dirname(os.path.abspath(__file__))
CUT = os.path.join(ROOT, 'work', 'cut')

# helmet height (dome top -> lowest cheek guard) measured on the 2048px sources
HELMET = {
    'Aggressive_Attack': 374, 'Charge': 366, 'NormalAttack_F': 335, 'NormalAttack_L': 300,
    'NormalAttack_R': 356, 'Of_Wrath': 365, 'QuickStep': 300, 'ShieldDash': 400,
    'ShieldThrowing_DashAttack': 374, 'Shield_Throwing_Fly_Attack': 340, 'Sword_Swinging': 360,
}
HELMET_PX = 21.0  # target helmet height in game pixels


def load_cut(name, flip=False):
    im = Image.open(os.path.join(CUT, name + '.png')).convert('RGBA')
    if flip:
        im = im.transpose(Image.FLIP_LEFT_RIGHT)
    return np.asarray(im).astype(np.float32) / 255.0


def premul_resize(a, size):
    """area-average downscale with premultiplied alpha. a: HxWx4 float 0..1"""
    rgb = a[..., :3] * a[..., 3:4]
    out = []
    for c in range(3):
        out.append(np.asarray(Image.fromarray(rgb[..., c]).resize(size, Image.BOX)))
    al = np.asarray(Image.fromarray(a[..., 3]).resize(size, Image.BOX))
    rgbo = np.stack(out, -1)
    with np.errstate(invalid='ignore', divide='ignore'):
        col = np.where(al[..., None] > 1e-4, rgbo / np.maximum(al[..., None], 1e-4), 0)
    return np.dstack([np.clip(col, 0, 1), al])


# ---------------------------------------------------------------- colour helpers
def srgb_to_lab(rgb):
    rgb = np.asarray(rgb, np.float64)
    lin = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
    M = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = lin @ M.T
    xyz /= np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    L = 116 * f[..., 1] - 16
    A = 500 * (f[..., 0] - f[..., 1])
    B = 200 * (f[..., 1] - f[..., 2])
    return np.stack([L, A, B], -1)


def kmeans_palette(pixels_rgb, k, seed=0, iters=40):
    from sklearn.cluster import KMeans
    lab = srgb_to_lab(pixels_rgb)
    km = KMeans(n_clusters=k, n_init=6, random_state=seed, max_iter=iters).fit(lab)
    # palette colour = mean sRGB of members (keeps it a real colour from the art)
    pal = np.array([pixels_rgb[km.labels_ == i].mean(0) for i in range(k)])
    return pal


def map_to_palette(rgb, pal):
    lab = srgb_to_lab(rgb.reshape(-1, 3))
    plab = srgb_to_lab(pal)
    d = ((lab[:, None, :] - plab[None, :, :]) ** 2).sum(-1)
    idx = d.argmin(1)
    return idx.reshape(rgb.shape[:2])
