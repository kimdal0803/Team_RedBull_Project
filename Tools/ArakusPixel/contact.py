import json, os, sys
from PIL import Image, ImageDraw
meta=json.load(open('out/sheets.json'))
W,H=meta['frameWidth'],meta['frameHeight']; px,py=meta['pivotPx']; py=H-py
names=sys.argv[2:] or list(meta['sheets'].keys())
scale=int(sys.argv[1]) if len(sys.argv)>1 else 2
rows=[]
for n in names:
    sh=Image.open(f'out/sheets/{n}.png')
    k=meta['sheets'][n]['frames']
    row=Image.new('RGB',(k*(W*scale+6)+6, H*scale+22),(30,30,34))
    d=ImageDraw.Draw(row); d.text((6,2),n,fill=(255,255,255))
    for i in range(k):
        fr=sh.crop((i*W,0,(i+1)*W,H))
        bg=Image.new('RGBA',(W,H),(112,112,120,255)); 
        bd=ImageDraw.Draw(bg); bd.line([(0,py),(W,py)],fill=(80,160,80,255)); bd.line([(px,py-4),(px,py+3)],fill=(255,80,80,255))
        bg.alpha_composite(fr)
        row.paste(bg.convert('RGB').resize((W*scale,H*scale),Image.NEAREST),(6+i*(W*scale+6),18))
    rows.append(row)
Wt=max(r.width for r in rows); Ht=sum(r.height for r in rows)
sheet=Image.new('RGB',(Wt,Ht),(30,30,34)); y=0
for r in rows: sheet.paste(r,(0,y)); y+=r.height
sheet.save('work/contact.png'); print(sheet.size)
