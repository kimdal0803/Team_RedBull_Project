"""Aggressive_Attack(휩쓸기) 그림에서 칼날의 빛만 걷어낸 버전을 만든다 (거검 휘두르기 타격 프레임용).
칼날 다각형 안: 주황 테두리 -> 어두운 외곽선 색, 따뜻한 빛 -> 무채색 금속, 불투명하게
칼날 다각형 밖(손 아래 영역): 크림색/흰 빛 -> 투명"""
import os
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

ROOT = os.path.dirname(os.path.abspath(__file__))
CUT = os.path.join(ROOT, 'work', 'cut')

im = Image.open(os.path.join(CUT, 'Aggressive_Attack.png')).convert('RGBA')
a = np.asarray(im).astype(np.float32)
h, w = a.shape[:2]
# blade outline measured on the 2048px source (before flipping)
poly = [(629, 825), (553, 1008), (493, 1135), (453, 1228), (373, 1402), (350, 1513), (339, 1670), (352, 1668),
        (432, 1556), (532, 1356), (612, 1196), (672, 1050), (726, 915), (775, 785), (720, 772)]
pm = Image.new('L', (w, h), 0)
ImageDraw.Draw(pm).polygon(poly, fill=255)
inside = ndi.binary_dilation(np.asarray(pm) > 0, iterations=5)
R, G, B, A = [a[..., i] for i in range(4)]
lum = (R + G + B) / 3
blood = (R > 90) & (G < 85) & ((R - G) > 40)
orange = (R > 150) & (G > 80) & ((R - B) > 60) & ((R - G) > 30)
warm = ((R - B) > 18) & ~blood
out = a.copy()
gray = np.stack([lum * 0.96, lum * 0.96, lum * 1.0], -1)
m_rim = inside & orange
m_warm = inside & warm & ~orange
out[m_warm, 0:3] = gray[m_warm]
out[m_rim, 0:3] = np.array([48, 42, 42])
out[inside, 3] = np.maximum(out[inside, 3], np.where(lum[inside] > 0, 255, 0))
box = np.zeros((h, w), bool)
box[735:900, 280:760] = True      # below the hands only
box[900:1700, 280:880] = True
halo = box & ~inside & (A > 0) & (((lum > 150) & ((R - B) > 8)) | (lum > 215))
out[halo, 3] = 0
al = out[..., 3] > 0
lab, n = ndi.label(al)
areas = ndi.sum(np.ones(al.shape), lab, index=np.arange(1, n + 1))
keep = np.zeros(n + 1, bool)
keep[1:] = areas >= areas.max() * 0.002
out[..., 3] = np.where(keep[lab], out[..., 3], 0)
Image.fromarray(out.clip(0, 255).astype(np.uint8), 'RGBA').save(os.path.join(CUT, 'Aggressive_Attack_noglow.png'))
print('ok')
