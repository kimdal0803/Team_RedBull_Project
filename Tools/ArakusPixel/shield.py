import numpy as np, json, os
from PIL import Image, ImageDraw
from scipy import ndimage as ndi
from pixel import premul_resize, map_to_palette
import frames as FR
cx,cy,a,b=1705,1018,292,441
im=Image.open('work/cut/ShieldDash.png').convert('RGBA')
arr=np.asarray(im).astype(np.float32)/255
m=Image.new('L',im.size,0); ImageDraw.Draw(m).ellipse((cx-a,cy-b,cx+a,cy+b),fill=255)
mask=np.asarray(m.filter(__import__('PIL.ImageFilter',fromlist=['x']).GaussianBlur(2))).astype(np.float32)/255
arr[...,3]*=mask
crop=arr[cy-b:cy+b, cx-a:cx+a]
D=40
v=premul_resize(crop,(D,D))
rgb=v[...,:3]; rgb=(rgb-0.5)*FR.CONTRAST+0.5; mm=rgb.mean(-1,keepdims=True); rgb=mm+(rgb-mm)*FR.SATURATION; v[...,:3]=np.clip(rgb,0,1)
pal=np.load('work/palette.npy')
solid=v[...,3]>0.5
idx=map_to_palette(v[...,:3],pal)
er=ndi.binary_erosion(solid,structure=np.array([[0,1,0],[1,1,1],[0,1,0]]))
dark=int(np.argmin(((pal*255-np.array(FR.OUTLINE_RGB))**2).sum(1)))
idx[solid&~er]=dark
out=np.zeros((D,D,4),np.uint8); out[...,:3]=(pal[idx]*255).round(); out[...,3]=np.where(solid,255,0)
Image.fromarray(out,'RGBA').save('out/sheets/Arakus_Shield_Projectile.png')
big=Image.new('RGBA',(D,D),(112,112,120,255)); big.alpha_composite(Image.fromarray(out,'RGBA'))
big.convert('RGB').resize((D*8,D*8),Image.NEAREST).save('work/shield_zoom.png')
print('ok')
