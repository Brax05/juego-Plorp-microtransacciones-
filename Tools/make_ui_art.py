"""Build the crisp, editable first-pass pixel UI chrome for GlorpFarm."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets" / "Resources" / "UI"
OUT.mkdir(parents=True, exist_ok=True)
S = 2
W, H = 285, 509
im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
d = ImageDraw.Draw(im)

dark = "#301b1e"
edge = "#593029"
wood = "#9c5333"
wood_light = "#c57441"
cream = "#efc188"
gold = "#ffc948"
green = "#45b74b"

font_path = "C:/Windows/Fonts/arialbd.ttf"
def font(size): return ImageFont.truetype(font_path, size)
def label(x, y, value, size=12, fill="white", anchor=None):
    d.text((x, y), value, font=font(size), fill=fill, stroke_width=2, stroke_fill=dark, anchor=anchor)

def box(x0, y0, x1, y1, fill=wood, inner=wood_light):
    d.rectangle((x0+2, y0+3, x1+2, y1+3), fill=dark)
    d.rectangle((x0, y0, x1, y1), fill=dark)
    d.rectangle((x0+2, y0+2, x1-2, y1-2), fill=edge)
    d.rectangle((x0+4, y0+4, x1-4, y1-4), fill=fill)
    d.line((x0+5, y0+5, x1-5, y0+5), fill=inner, width=2)
    d.line((x0+5, y0+5, x0+5, y1-5), fill=inner, width=2)

def coin(cx, cy):
    d.ellipse((cx-6, cy-6, cx+6, cy+6), fill=dark)
    d.ellipse((cx-5, cy-5, cx+5, cy+5), fill="#e88d19")
    d.ellipse((cx-3, cy-4, cx+3, cy+3), fill=gold)
    d.line((cx-1,cy-3,cx-1,cy+2),fill="#ffe786",width=2)

def gem(cx,cy):
    d.polygon([(cx-6,cy-2),(cx-3,cy-6),(cx+4,cy-6),(cx+7,cy-2),(cx,cy+6)],fill=dark)
    d.polygon([(cx-4,cy-2),(cx-2,cy-4),(cx+3,cy-4),(cx+5,cy-2),(cx,cy+4)],fill="#31d2a3")
    d.polygon([(cx-2,cy-4),(cx+2,cy-4),(cx,cy+3)],fill="#9cf9d3")

def chicken(x,y):
    d.ellipse((x+4,y+10,x+22,y+26),fill=dark)
    d.ellipse((x+5,y+9,x+21,y+24),fill="#fff8db")
    d.ellipse((x+16,y+5,x+24,y+14),fill="#fff8db",outline=dark,width=1)
    d.polygon([(x+22,y+10),(x+27,y+12),(x+22,y+14)],fill="#ecac32")
    d.rectangle((x+18,y+7,x+19,y+8),fill=dark)
    d.rectangle((x+18,y+3,x+21,y+6),fill="#d34b37")
    d.line((x+9,y+24,x+8,y+28),fill="#d78338",width=2)
    d.line((x+17,y+24,x+18,y+28),fill="#d78338",width=2)

def farmer(x,y):
    d.rectangle((x+10,y+5,x+22,y+10),fill="#d88a40",outline=dark)
    d.rectangle((x+6,y+9,x+26,y+12),fill="#b96833",outline=dark)
    d.rectangle((x+11,y+12,x+21,y+21),fill="#e8a16a",outline=dark)
    d.rectangle((x+10,y+21,x+22,y+31),fill="#3976aa",outline=dark)
    d.rectangle((x+11,y+31,x+14,y+35),fill=dark)
    d.rectangle((x+19,y+31,x+22,y+35),fill=dark)
    d.rectangle((x+7,y+21,x+10,y+28),fill="#b95b46")
    d.rectangle((x+22,y+21,x+25,y+28),fill="#b95b46")

def seeds(x,y):
    d.ellipse((x+6,y+7,x+24,y+27),fill=dark)
    d.polygon([(x+8,y+11),(x+22,y+11),(x+21,y+26),(x+9,y+26)],fill="#dcb97d")
    d.ellipse((x+9,y+5,x+21,y+10),fill="#b5804d",outline=dark)
    d.ellipse((x+13,y+16,x+17,y+20),fill="#8b5f32")

def tractor(x,y):
    d.rectangle((x+3,y+15,x+22,y+27),fill="#a42f34",outline=dark)
    d.rectangle((x+11,y+7,x+21,y+16),fill="#d1473e",outline=dark)
    d.rectangle((x+13,y+9,x+19,y+15),fill="#8fc9ca")
    d.rectangle((x+21,y+18,x+29,y+25),fill="#d1473e",outline=dark)
    d.ellipse((x+6,y+23,x+15,y+33),fill=dark)
    d.ellipse((x+8,y+25,x+13,y+30),fill="#697280")
    d.ellipse((x+22,y+23,x+28,y+30),fill=dark)

# HUD panels float above the artwork.
box(4,5,128,49,fill="#dba875",inner="#f5d39a")
farmer(10,9)
label(39,8,"Nivel",12)
d.rectangle((39,30,119,42),fill=dark)
d.rectangle((41,32,117,40),fill="#425577")
d.rectangle((41,32,81,40),fill=gold)
d.line((42,33,80,33),fill="#ffe689",width=2)

box(190,5,280,50,fill="#996445",inner="#d89a6b")
d.rectangle((194,11,210,25),fill="#c78852",outline=dark,width=2)
d.rectangle((194,29,210,43),fill="#c78852",outline=dark,width=2)
label(202,11,"+",15,fill=gold,anchor="mt")
label(202,29,"+",15,fill=gold,anchor="mt")
coin(269,18)
gem(269,36)

# The bottom interface is deliberately bold so it reads on a small phone.
d.rectangle((0,263,284,508),fill=dark)
d.rectangle((2,266,282,508),fill="#6d392d")
d.rectangle((4,268,280,507),fill="#ae6138")
d.line((2,265,282,265),fill="#e1a268",width=3)
box(9,276,275,308,fill="#b9623b",inner="#e2a06b")
label(142,284,"TOCAR PARA COSECHAR",12,anchor="mt")

rows=[("SEMILLAS","150K",seeds),("ANIMALES","1.2M",chicken),("TRABAJADORES","5.0M",farmer),("MAQUINARIA","10.0M",tractor)]
for i,(title,cost,icon) in enumerate(rows):
    y=314+i*40
    box(9,y,275,y+36,fill="#a85d3c",inner="#ca8455")
    d.rectangle((14,y+4,54,y+32),fill=dark)
    d.rectangle((16,y+6,52,y+30),fill="#704638")
    icon(19,y+3)
    label(61,y+3,title,12)
    coin(107 if i<2 else (104 if i==2 else 109),y+27)
    box(200,y+4,270,y+31,fill="#299f45",inner="#82d86a")
    label(235,y+6,"CONTRATAR" if i==2 else "¡MEJORAR!",9,anchor="mt")

tabs=[("GRANJA","farm"),("TIENDA","shop"),("LOGROS","cup"),("MEJORAS","up"),("AJUSTES","gear")]
for i,(title,icon) in enumerate(tabs):
    x=i*57
    box(x+1,475,x+56,509,fill="#b46c43" if i==0 else "#a05739",inner="#dca16b")
    cx=x+28
    if icon=="farm":
        d.rectangle((cx-10,484,cx+10,496),fill="#85512b",outline=dark,width=2)
        d.polygon([(cx-13,485),(cx,478),(cx+13,485)],fill="#d68635")
        d.rectangle((cx-4,479,cx+4,484),fill="#4cac50")
    elif icon=="shop":
        d.rectangle((cx-9,483,cx+9,496),fill="#e9c59a",outline=dark,width=2)
        d.polygon([(cx-12,484),(cx-9,478),(cx+9,478),(cx+12,484)],fill="#df5e4f",outline=dark)
        d.rectangle((cx-3,489,cx+3,496),fill="#5090b5")
    elif icon=="cup":
        d.polygon([(cx-8,479),(cx+8,479),(cx+5,488),(cx,491),(cx-5,488)],fill=gold,outline=dark)
        d.rectangle((cx-2,490,cx+2,495),fill=gold)
        d.rectangle((cx-6,495,cx+6,497),fill=dark)
    elif icon=="up":
        d.polygon([(cx,477),(cx-10,487),(cx-5,487),(cx-5,496),(cx+5,496),(cx+5,487),(cx+10,487)],fill="#43c450",outline=dark)
    else:
        d.ellipse((cx-10,478,cx+10,498),fill="#93aac2",outline=dark,width=2)
        d.ellipse((cx-4,484,cx+4,492),fill="#414c65",outline=dark,width=2)
    label(cx,497,title,8,anchor="mt")

im.resize((W*S,H*S),Image.Resampling.NEAREST).save(OUT / "chrome.png")

source = ROOT / "Tools" / "SourceArt" / "farm_source.png"
farm = Image.open(source).convert("RGB")
# Match the mobile viewport, then reduce and enlarge with nearest-neighbour.
target_ratio = W / 265
fw, fh = farm.size
crop_h = int(fw / target_ratio)
if crop_h <= fh:
    top = max(0,(fh-crop_h)//2)
    farm = farm.crop((0,top,fw,top+crop_h))
else:
    crop_w = int(fh*target_ratio)
    left = (fw-crop_w)//2
    farm = farm.crop((left,0,left+crop_w,fh))
farm.resize((W,265),Image.Resampling.NEAREST).resize((W*S,530),Image.Resampling.NEAREST).save(OUT / "farm.png")
print(OUT)
