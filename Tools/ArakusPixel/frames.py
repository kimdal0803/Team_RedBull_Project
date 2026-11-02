"""Step 3: build every animation frame from the 11 cutouts, quantize with one shared palette,
and write sprite sheets (one horizontal strip per animation, all frames the same size,
same pivot point).

Run:  python3 frames.py            -> out/sheets/*.png, out/preview/*.gif, work/contact.png, out/sheets.json
"""
import os, json, math
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi
from pixel import premul_resize, kmeans_palette, map_to_palette, HELMET, HELMET_PX

ROOT = os.path.dirname(os.path.abspath(__file__))
CUT = os.path.join(ROOT, 'work', 'cut')
OUT = os.path.join(ROOT, 'out')
SUP = 4               # supersampling factor for transforms (work at 4x the game resolution)
ALPHA_CUT = 0.42      # coverage needed for a game pixel to be solid
PALETTE_SIZE = 32
CONTRAST = 1.07
SATURATION = 1.12
OUTLINE_RGB = (44, 30, 32)

# ------------------------------------------------------------------ source poses
# hip = belt centre (x) and belt height (y) on the 2048px source, after flipping if flip=True.
# ground = sole line; None -> lowest opaque pixel.
POSES = {
    'R':  dict(file='NormalAttack_R', hip=(990, 1048)),
    'L':  dict(file='NormalAttack_L', hip=(1000, 1152)),
    'F':  dict(file='NormalAttack_F', hip=(900, 892)),
    'SS': dict(file='Sword_Swinging', hip=(912, 1144)),
    'QS': dict(file='QuickStep', hip=(1200, 992)),
    'TD': dict(file='ShieldThrowing_DashAttack', hip=(1112, 1080)),
    'SD': dict(file='ShieldDash', hip=(1172, 1092)),
    'FA': dict(file='Shield_Throwing_Fly_Attack', hip=(980, 940)),
    'AG': dict(file='Aggressive_Attack', hip=(2048 - 1220, 1120), flip=True),
    'AGn': dict(file='Aggressive_Attack_noglow', hip=(2048 - 1220, 1120), flip=True, helmet_of='Aggressive_Attack'),
    'W':  dict(file='Of_Wrath', hip=(1160, 1200)),
    'C':  dict(file='Charge', hip=(1000, 1048)),
}


def F(pose, lean=0.0, sx=1.0, sy=1.0, dx=0.0, dy=0.0, breathe=0.0, note=''):
    """one frame recipe. lean: degrees, + = upper body forward (feet stay planted).
    sx/sy: squash about the feet. dx/dy: game pixels. breathe: upper body sinks by N game px."""
    return dict(pose=pose, lean=lean, sx=sx, sy=sy, dx=dx, dy=dy, breathe=breathe, note=note)


# ------------------------------------------------------------------ sheets
# name -> frames. 'roles' tells the game which frame is which (see README / ArakusSpriteSet).
SHEETS = {
    # 대기 (엑셀 표에는 없지만 반드시 필요) — 방패를 앞세운 경계 자세에서 숨쉬기
    'Arakus_Idle': [F('SD', breathe=0), F('SD', breathe=1), F('SD', breathe=2), F('SD', breathe=1)],

    # 일반공격 1 — 오른팔로 거검을 들어 횡베기
    'Arakus_NormalAttack_R_PT': [
        F('SD', lean=-5, sx=1.02, sy=0.95, dx=-1, note='준비: 칼을 뒤로 당기며 낮춤'),
        F('R', lean=3, sx=1.04, sy=0.99, dx=2, note='공격: 횡베기'),
        F('R', lean=-1, note='복귀'),
    ],
    # 일반공격 2 — 방패를 머리까지 올렸다가 땅으로 내려찍기
    'Arakus_NormalAttack_L_PT': [
        F('L', lean=-3, sx=0.98, sy=1.03, note='준비: 방패를 머리 위로'),
        F('C', lean=2, sx=1.03, sy=0.93, dx=1, note='공격: 방패로 내려찍음'),
        F('C', lean=-2, sy=0.97, note='복귀'),
    ],
    # 일반공격 3 — 오른발로 걷어차기
    'Arakus_NormalAttack_F_PT': [
        F('F', lean=-7, dx=-1, note='준비: 다리를 들고 뒤로 젖힘'),
        F('F', lean=2, dx=2, note='공격: 걷어참'),
        F('F', lean=-2, note='복귀'),
    ],
    # 거검 휘두르기 — 방패를 등에 메고 양손으로 검을 잡아 자세를 낮춘 준비 동작
    'Arakus_Sword_Swinging_PT': [
        F('SS', lean=-2, sx=1.02, sy=0.97, note='준비: 어깨 위로 검을 멤'),
        F('AGn', lean=4, dx=2, note='공격: 내려벰 (휩쓸기 자세를 빛 없이)'),
        F('AGn', sy=0.98, note='복귀'),
    ],
    # 퀵스텝 — 순간 자세를 낮추고 뒤로 빠짐
    'Arakus_QuickStep_PT': [
        F('SD', lean=1, sx=1.04, sy=0.90, note='준비: 순간 낮춤'),
        F('QS', note='공격: 뒤로 뜀'),
        F('SD', sx=1.03, sy=0.93, note='복귀: 착지'),
    ],
    # 방패 투척 후 대쉬 공격 — 방패를 던지고 달려와 검을 휘두름
    'Arakus_ShieldThrowing_DashAttack_PT': [
        F('L', lean=-8, dx=-2, note='준비: 방패를 뒤로 젖혀 던질 준비'),
        F('TD', note='공격: 던진 손을 뻗은 채 달려듦 (방패 없음)'),
        F('TD', lean=-3, sy=0.98, note='복귀'),
    ],
    # 방패돌진 — 방패를 들어 달릴 준비
    'Arakus_ShieldDash_PT': [
        F('SD', lean=3, sx=1.03, sy=0.94, note='준비: 낮춰 달릴 준비'),
        F('C', lean=2, sx=1.03, sy=0.98, note='공격: 방패를 앞세워 돌진'),
        F('SD', lean=-3, sy=0.97, note='복귀: 제동'),
    ],
    # 방패 던지고 내려찍기 — 던진 방패를 돌려받은 뒤 크게 점프해 방패로 내려찍음
    'Arakus_Shield_Throwing_Fly_Attack_PT': [
        F('L', lean=-8, dx=-2, note='준비: 던질 준비'),
        F('TD', note='투척: 방패를 놓음'),
        F('TD', lean=-6, note='도약 (높이는 게임에서 곡선으로)'),
        F('FA', note='낙하: 방패를 받아 내려옴'),
        F('C', lean=3, sx=1.04, sy=0.92, dx=1, note='내려찍기 + 복귀'),
    ],
    # 일대 휩쓸기 — 검에 기를 모으고 3번 휩쓸기
    'Arakus_Aggressive_Attack_PT': [
        F('AG', note='준비: 검에 기를 모음'),
        F('AG', lean=5, sx=1.04, sy=0.98, dx=2, note='공격: 휩쓸기'),
        F('SS', lean=-1, note='다음 휩쓸기 준비 (사이)'),
        F('AGn', lean=-2, note='복귀'),
    ],
    # 아라쿠스의 분노 — 한쪽 무릎을 꿇고 마음을 다잡음, 버프 타이밍에 파동
    'Arakus_Of_Wrath_PT': [
        F('W', note='준비: 무릎 꿇고 기를 모음'),
        F('W', sx=0.98, sy=1.04, note='버프: 몸을 부풀림 (파동)'),
        F('W', lean=2, sy=0.98, note='복귀'),
    ],
    # 돌진 — 자세를 낮춰 준비한 다음 돌진
    'Arakus_Charge_PT': [
        F('C', lean=1, sx=1.03, sy=0.92, note='준비: 낮춤'),
        F('C', lean=4, sx=1.06, sy=0.97, dx=2, note='공격: 돌진'),
        F('C', lean=-3, sy=0.97, note='복귀'),
    ],
    # 그로기 (엑셀 표에는 없음) — 무릎 꿇고 축 처짐
    'Arakus_Groggy': [F('W', lean=7, sy=0.97, dy=2), F('W', lean=9, sy=0.96, dy=2)],
}

# which frame plays in which part of a pattern (consumed by the Unity side)
ROLES = {
    'Arakus_Idle': {'loop': [0, 1, 2, 1], 'fps': 4},
    'Arakus_Groggy': {'loop': [0, 1], 'fps': 3},
}


# ------------------------------------------------------------------ rendering
_cache = {}


def pose_source(key):
    """returns (rgba float at SUP x game scale, hip(x,y), ground_y, scale) in that space"""
    if key in _cache:
        return _cache[key]
    p = POSES[key]
    im = Image.open(os.path.join(CUT, p['file'] + '.png')).convert('RGBA')
    if p.get('flip'):
        im = im.transpose(Image.FLIP_LEFT_RIGHT)
    a = np.asarray(im).astype(np.float32) / 255.0
    helmet = HELMET[p.get('helmet_of', p['file'])]
    s = HELMET_PX / helmet * SUP
    size = (int(round(a.shape[1] * s)), int(round(a.shape[0] * s)))
    small = premul_resize(a, size)
    ys = np.nonzero(a[..., 3] > 0.5)[0]
    ground = p.get('ground', ys.max())
    hip = (p['hip'][0] * s, p['hip'][1] * s)
    res = (small, hip, ground * s, s)
    _cache[key] = res
    return res


def warp_frame(fr, canvas, anchor):
    """render one frame recipe into a SUP-scale canvas (premultiplied float RGBA)"""
    src, hip, ground, s = pose_source(fr['pose'])
    W, H = canvas
    ax, ay = anchor
    # forward: q = A + M (p - G) + d     with G = (hip_x, ground)
    # M = shear(lean) @ scale(sx, sy)
    t = math.tan(math.radians(fr['lean']))
    M = np.array([[fr['sx'], -t * fr['sy']], [0.0, fr['sy']]])
    Minv = np.linalg.inv(M)
    d = np.array([fr['dx'] * SUP, fr['dy'] * SUP])
    G = np.array([hip[0], ground])
    A = np.array([ax, ay])
    # inverse: p = G + Minv (q - A - d)
    off = G - Minv @ (A + d)
    coeffs = (Minv[0, 0], Minv[0, 1], off[0], Minv[1, 0], Minv[1, 1], off[1])
    pre = src.copy()
    pre[..., :3] *= pre[..., 3:4]
    chans = []
    for c in range(4):
        imc = Image.fromarray(pre[..., c].astype(np.float32), 'F')
        out = imc.transform((W, H), Image.AFFINE, coeffs, resample=Image.BILINEAR)
        chans.append(np.asarray(out))
    res = np.stack(chans, -1)
    if fr['breathe']:
        # upper body sinks by N game px; legs compress smoothly between feet and hip
        hh = max(1.0, ground - hip[1]) * fr['sy']
        yy = np.arange(H, dtype=np.float32)
        above = (ay - yy) / hh                      # 0 at the feet, 1 at the hip
        disp = fr['breathe'] * SUP * np.clip(above, 0, 1)   # pixels to shift down
        coords_y = (yy - disp)[:, None].repeat(W, 1)
        coords_x = np.arange(W, dtype=np.float32)[None, :].repeat(H, 0)
        res = np.stack([ndi.map_coordinates(res[..., c], [coords_y, coords_x], order=1, mode='constant')
                        for c in range(4)], -1)
    return res


def downsample(pre, k=SUP):
    H, W = pre.shape[:2]
    pre = pre[:H - H % k, :W - W % k]
    r = pre.reshape(H // k, k, W // k, k, 4).mean((1, 3))
    al = r[..., 3]
    with np.errstate(invalid='ignore', divide='ignore'):
        rgb = np.where(al[..., None] > 1e-4, r[..., :3] / np.maximum(al[..., None], 1e-4), 0)
    return np.dstack([np.clip(rgb, 0, 1), al])


def bbox_of(fr):
    """extent of a frame around the anchor, in game px (left, top, right, bottom)"""
    big = 4000
    pre = warp_frame(fr, (big, big), (big // 2, big // 2))
    ys, xs = np.nonzero(pre[..., 3] > 0.25)
    c = big // 2
    return ((c - xs.min()) / SUP, (c - ys.min()) / SUP, (xs.max() - c) / SUP, (ys.max() - c) / SUP)


def despeckle(idx, solid):
    """single stray pixels whose 4 neighbours mostly agree take the neighbours' colour"""
    out = idx.copy()
    H, W = idx.shape
    for y in range(1, H - 1):
        for x in range(1, W - 1):
            if not solid[y, x]:
                continue
            nb = [(y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)]
            if not all(solid[p] for p in nb):
                continue
            vals = [idx[p] for p in nb]
            if idx[y, x] in vals:
                continue
            best = max(set(vals), key=vals.count)
            if vals.count(best) >= 3:
                out[y, x] = best
    return out


def main():
    os.makedirs(os.path.join(OUT, 'sheets'), exist_ok=True)
    os.makedirs(os.path.join(OUT, 'preview'), exist_ok=True)
    frames = [(name, i, fr) for name, lst in SHEETS.items() for i, fr in enumerate(lst)]
    # 1) common canvas
    ext = np.array([bbox_of(fr) for _, _, fr in frames])
    left, top, right, bottom = [int(math.ceil(v)) for v in ext.max(0)]
    margin = 2
    Wg = left + right + 2 * margin
    Hg = top + bottom + 2 * margin
    Wg += Wg % 2
    Hg += Hg % 2
    anchor_g = (margin + left, margin + top)          # pivot in game px (from top-left)
    print('frame size', Wg, Hg, 'pivot', anchor_g, 'extents L T R B', left, top, right, bottom)
    canvas = (Wg * SUP, Hg * SUP)
    anchor = (anchor_g[0] * SUP + SUP / 2, anchor_g[1] * SUP)
    # 2) render all at game res (float)
    rendered = {}
    for name, i, fr in frames:
        v = downsample(warp_frame(fr, canvas, anchor))
        rgb = v[..., :3]
        rgb = (rgb - 0.5) * CONTRAST + 0.5
        m = rgb.mean(-1, keepdims=True)
        rgb = m + (rgb - m) * SATURATION
        v[..., :3] = np.clip(rgb, 0, 1)
        rendered[(name, i)] = v
    # 3) shared palette
    pix = np.concatenate([v[..., :3][v[..., 3] > ALPHA_CUT] for v in rendered.values()])
    rng = np.random.default_rng(0)
    if len(pix) > 60000:
        pix = pix[rng.choice(len(pix), 60000, replace=False)]
    pal = kmeans_palette(pix, PALETTE_SIZE)
    order = np.argsort(pal.sum(1))
    pal = pal[order]
    # outline colour: a very dark warm brown (reads on dark backgrounds better than pure black)
    dark = int(np.argmin(((pal * 255 - np.array(OUTLINE_RGB)) ** 2).sum(1)))
    np.save(os.path.join(ROOT, 'work', 'palette.npy'), pal)
    # 4) quantize + clean + outline
    final = {}
    for key, v in rendered.items():
        solid = v[..., 3] > ALPHA_CUT
        idx = map_to_palette(v[..., :3], pal)
        idx = despeckle(idx, solid)
        er = ndi.binary_erosion(solid, structure=np.array([[0, 1, 0], [1, 1, 1], [0, 1, 0]]))
        edge = solid & ~er
        idx[edge] = dark
        rgba = np.zeros(v.shape[:2] + (4,), np.uint8)
        rgba[..., :3] = (pal[idx] * 255).round().astype(np.uint8)
        rgba[..., 3] = np.where(solid, 255, 0)
        final[key] = rgba
    # 5) write sheets
    meta = {'frameWidth': Wg, 'frameHeight': Hg, 'pivotPx': [anchor_g[0], Hg - anchor_g[1]],
            'pivot': [round((anchor_g[0] + 0.5) / Wg, 5), round((Hg - anchor_g[1]) / Hg, 5)],
            'pixelsPerUnit': 45, 'palette': [[int(c * 255 + 0.5) for c in p] for p in pal], 'sheets': {}}
    for name, lst in SHEETS.items():
        sheet = np.zeros((Hg, Wg * len(lst), 4), np.uint8)
        for i in range(len(lst)):
            sheet[:, i * Wg:(i + 1) * Wg] = final[(name, i)]
        Image.fromarray(sheet, 'RGBA').save(os.path.join(OUT, 'sheets', name + '.png'), optimize=True)
        meta['sheets'][name] = {'frames': len(lst), 'notes': [f['note'] for f in lst], **ROLES.get(name, {})}
    json.dump(meta, open(os.path.join(OUT, 'sheets.json'), 'w'), ensure_ascii=False, indent=1)
    np.save(os.path.join(ROOT, 'work', 'final_frames.npy'), np.array([0]))
    print('palette', len(pal), 'sheets', len(SHEETS))
    return meta, final


if __name__ == '__main__':
    main()
