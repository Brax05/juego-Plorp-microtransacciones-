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

# HUD panels float above the artwork.
# Keep the level card roomy and give each piece of information its own area.
box(4,5,153,53,fill="#b96f45",inner="#f0b875")
d.rectangle((9,9,42,49),fill=dark)
d.rectangle((11,11,40,47),fill="#7b4938")
d.line((12,12,39,12),fill="#d99a62",width=2)
d.line((12,12,12,46),fill="#d99a62",width=2)

label(46,13,"NIVEL DE GRANJA",8)

# Dedicated plate for the live level number.
d.rectangle((122,9,148,27),fill=dark)
d.rectangle((124,11,146,25),fill="#694031")
d.line((125,12,145,12),fill="#d99a62",width=1)

# Framed XP track. The live gold fill is drawn by FarmScreen.
d.rectangle((46,30,148,47),fill=dark)
d.rectangle((49,33,145,44),fill="#25324e")
d.line((50,34,144,34),fill="#52698e",width=1)
# The live fill is drawn by FarmScreen so it accurately reflects progression.

box(180,5,280,53,fill="#8d5038",inner="#d58b58")

# Two balanced resource rows: action, resource icon, then its live amount.
d.rectangle((184,9,276,28),fill="#71402f")
d.rectangle((184,30,276,49),fill="#71402f")
d.line((185,10,275,10),fill="#a96746",width=1)
d.line((185,31,275,31),fill="#a96746",width=1)

for cy in (18,40):
    d.rectangle((186,cy-8,205,cy+8),fill=dark)
    d.rectangle((188,cy-6,203,cy+6),fill="#2f9847")
    d.line((189,cy-5,202,cy-5),fill="#78d06e",width=1)
    label(196,cy-1,"+",13,fill=gold,anchor="mm")

d.line((210,12,210,46),fill="#4a2927",width=2)
coin(228,18)
gem(228,40)

# The bottom interface is deliberately bold so it reads on a small phone.
d.rectangle((0,263,284,508),fill=dark)
d.rectangle((2,266,282,508),fill="#6d392d")
d.rectangle((4,268,280,507),fill="#ae6138")
d.line((2,265,282,265),fill="#e1a268",width=3)
box(9,276,275,308,fill="#b9623b",inner="#e2a06b")
label(142,286,"TOCAR PARA COSECHAR",12,anchor="mt")

rows=[("SEMILLAS","#6f5332"),("ANIMALES","#5c6940"),("TRABAJADORES","#425b71")]
for i,(title,tile_color) in enumerate(rows):
    y=318+i*52
    box(9,y,275,y+44,fill="#a85d3c",inner="#ca8455")
    d.rectangle((14,y+4,54,y+40),fill=dark)
    d.rectangle((16,y+6,52,y+38),fill=tile_color)
    d.line((17,y+7,51,y+7),fill="#b98962",width=1)
    label(61,y+6,title,10)
    coin(66,y+29)
    box(200,y+6,270,y+39,fill="#299f45",inner="#82d86a")
    label(235,y+13,"CONTRATAR" if i==2 else "MEJORAR",8,anchor="mt")

for i in range(5):
    x=i*57
    box(x+1,475,x+56,509,fill="#a05739",inner="#dca16b")

chrome = im.resize((W*S,H*S),Image.Resampling.NEAREST)

# Use the user-provided UI artwork directly, normalized into equal icon boxes.
sprite_root = ROOT / "Assets" / "Resources" / "UI" / "Icons"

# Prepare independent, equally-sized navigation textures for the runtime UI.
for filename in ("granja.png", "tienda.png", "trofeos.png", "mejora.png", "ajustes.png"):
    source_icon = Image.open(sprite_root / filename).convert("RGBA")
    source_box = source_icon.getchannel("A").getbbox()
    if source_box:
        source_icon = source_icon.crop(source_box)
    nav_scale = min(112 / source_icon.width, 112 / source_icon.height)
    nav_size = (max(1, round(source_icon.width * nav_scale)), max(1, round(source_icon.height * nav_scale)))
    source_icon = source_icon.resize(nav_size, Image.Resampling.NEAREST)
    nav_icon = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    nav_icon.alpha_composite(source_icon, ((128 - nav_size[0]) // 2, (128 - nav_size[1]) // 2))
    nav_icon.save(sprite_root / filename)

upgrade_icons = ["semillas.png", "animales.png", "trabajadores.png"]
icon_box = (32, 648, 72, 64)
for index, filename in enumerate(upgrade_icons):
    sprite = Image.open(sprite_root / filename).convert("RGBA")
    alpha_box = sprite.getchannel("A").getbbox()
    if alpha_box:
        sprite = sprite.crop(alpha_box)
    # Normalize the visible silhouette, preserving its original proportions.
    scale = min(60 / sprite.width, 58 / sprite.height)
    size = (max(1, round(sprite.width * scale)), max(1, round(sprite.height * scale)))
    sprite = sprite.resize(size, Image.Resampling.NEAREST)
    box_x, box_y, box_w, box_h = icon_box
    box_y += index * 104
    position = (box_x + (box_w - size[0]) // 2, box_y + (box_h - size[1]) // 2)
    chrome.alpha_composite(sprite, position)

chrome.save(OUT / "chrome.png")

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
preview = Image.new("RGBA", chrome.size, (35, 73, 52, 255))
preview.alpha_composite(Image.open(OUT / "farm.png").convert("RGBA"), (0, 0))
preview.alpha_composite(chrome)
# The level emblem is an independent RawImage at runtime, not part of chrome.png.
level_icon = Image.open(sprite_root / "nivel.png").convert("RGBA").resize((72, 72), Image.Resampling.NEAREST)
preview.alpha_composite(level_icon, (15, 22))
# Show the runtime-composed navigation in the static design preview.
preview_draw = ImageDraw.Draw(preview)
nav_titles = ("GRANJA", "TIENDA", "LOGROS", "MEJORAS", "AJUSTES")
nav_files = ("granja.png", "tienda.png", "trofeos.png", "mejora.png", "ajustes.png")
for index, (title, filename) in enumerate(zip(nav_titles, nav_files)):
    nav_texture = Image.open(sprite_root / filename).convert("RGBA").resize((54, 54), Image.Resampling.NEAREST)
    preview.alpha_composite(nav_texture, (30 + index * 114, 956))
    preview_draw.text((57 + index * 114, 1001), title, font=font(15), fill="white",
                      stroke_width=2, stroke_fill=dark, anchor="mm")
preview.save(ROOT / "Tools" / "ui_preview.png")
print(OUT)
