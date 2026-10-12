"""Build the fixed landscape and independent pixel sprites used by the world actors."""
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets" / "Resources" / "World"
SPRITES = OUT / "Sprites"
SPRITES.mkdir(parents=True, exist_ok=True)

# Keep the same viewport crop as the original UI illustration.
source = Image.open(ROOT / "Tools" / "SourceArt" / "farm_background_source.png").convert("RGB")
width, height = source.size
crop_height = int(width / (570 / 530))
if crop_height <= height:
    top = (height - crop_height) // 2
    source = source.crop((0, top, width, top + crop_height))
else:
    crop_width = int(height * 570 / 530)
    left = (width - crop_width) // 2
    source = source.crop((left, 0, left + crop_width, height))
source.resize((570, 530), Image.Resampling.NEAREST).save(OUT / "farm_background.png")

INK = "#321f25"
SHADOW = "#765035"
LEAF = "#278446"
LIGHT_LEAF = "#5ec553"

def canvas(name, draw_fn, size=(32, 32)):
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(image))
    image.save(SPRITES / f"{name}.png")

def seed(d):
    d.ellipse((11, 23, 23, 28), fill=SHADOW)
    d.polygon([(16, 22), (13, 20), (13, 17), (16, 18), (18, 15), (20, 17), (18, 22)], fill=LEAF, outline=INK)
    d.rectangle((17, 17, 18, 23), fill=LIGHT_LEAF)

def sprout(d, crop):
    seed(d)
    d.line((17, 23, 17, 11), fill=INK, width=4)
    d.line((17, 21, 17, 11), fill=LIGHT_LEAF, width=2)
    d.polygon([(16, 15), (9, 11), (7, 6), (13, 7), (18, 12)], fill=LEAF, outline=INK)
    d.polygon([(18, 15), (23, 8), (29, 8), (26, 14), (19, 18)], fill=LIGHT_LEAF, outline=INK)
    if crop == "carrot":
        d.polygon([(15, 21), (21, 21), (18, 28)], fill="#db7935", outline=INK)
    if crop == "tomato":
        d.ellipse((15, 20, 22, 27), fill="#b84035", outline=INK)

def mature(d, crop):
    d.ellipse((5, 26, 29, 30), fill=SHADOW)
    if crop == "carrot":
        d.polygon([(12, 14), (24, 14), (21, 25), (17, 29), (13, 23)], fill="#e58b2d", outline=INK)
        d.polygon([(14, 16), (17, 5), (19, 15), (23, 6), (23, 16), (28, 11), (25, 19)], fill=LEAF, outline=INK)
        d.line((14, 18, 21, 18), fill="#ffd05b", width=2)
    elif crop == "tomato":
        d.line((17, 19, 17, 8), fill=INK, width=3)
        d.polygon([(17, 11), (10, 5), (8, 10), (14, 15)], fill=LEAF, outline=INK)
        d.polygon([(18, 10), (25, 4), (27, 11), (21, 16)], fill=LIGHT_LEAF, outline=INK)
        for x, y, r in [(11, 20, 6), (22, 20, 6), (17, 24, 6)]:
            d.ellipse((x-r, y-r, x+r, y+r), fill="#d83e31", outline=INK, width=2)
            d.rectangle((x-2, y-r+2, x, y-r+4), fill="#ff8a51")
        d.polygon([(14, 16), (17, 13), (20, 16), (17, 18)], fill=LEAF)
    elif crop == "cabbage":
        d.ellipse((5, 9, 29, 29), fill="#277d3d", outline=INK, width=2)
        d.ellipse((8, 8, 27, 26), fill="#5bbd52", outline=INK)
        d.arc((10, 10, 25, 24), 190, 350, fill="#a3df6c", width=3)
        d.arc((12, 11, 26, 24), 30, 190, fill="#318e42", width=3)
        d.ellipse((14, 13, 20, 18), fill="#8bd665")
    else:  # Pumpkin
        d.ellipse((5, 13, 28, 28), fill="#d77e25", outline=INK, width=2)
        d.arc((10, 13, 24, 28), 80, 270, fill="#f8ac3e", width=2)
        d.arc((8, 14, 20, 27), 270, 85, fill="#aa5424", width=2)
        d.polygon([(15, 15), (15, 7), (20, 6), (22, 10), (18, 9), (18, 15)], fill=LEAF, outline=INK)

for crop in ("carrot", "tomato", "cabbage", "pumpkin"):
    canvas(f"{crop}_0", seed)
    canvas(f"{crop}_1", lambda d, c=crop: sprout(d, c))
    canvas(f"{crop}_2", lambda d, c=crop: mature(d, c))

def tractor(d):
    d.ellipse((2, 27, 31, 31), fill="#567b42")
    d.rectangle((7, 14, 22, 25), fill="#c44837", outline=INK, width=2)
    d.rectangle((14, 5, 24, 15), fill="#c44837", outline=INK, width=2)
    d.rectangle((16, 7, 22, 13), fill="#9bd5d2", outline=INK)
    d.rectangle((21, 17, 30, 24), fill="#dc6544", outline=INK)
    d.ellipse((6, 22, 17, 31), fill=INK)
    d.ellipse((9, 25, 14, 29), fill="#80949d")
    d.ellipse((23, 24, 31, 31), fill=INK)
    d.ellipse((26, 26, 29, 29), fill="#80949d")

canvas("tractor", tractor)

from make_character_sprites import build as build_character_sprites
build_character_sprites()
print(f"Generated background and {len(list(SPRITES.glob('*.png')))} individual sprites in {OUT}")
