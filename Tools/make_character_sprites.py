"""Extract transparent, consistently aligned actors from the source sprite sheets."""
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Tools" / "SourceArt"
OUT = ROOT / "Assets" / "Resources" / "World" / "Sprites"
OUT.mkdir(parents=True, exist_ok=True)


def finish(name, image, size):
    """Fit a cutout to a fixed canvas, with the feet at a common baseline."""
    alpha = image.getchannel("A")
    bounds = alpha.point(lambda value: 255 if value > 96 else 0).getbbox()
    if bounds is None:
        raise ValueError(f"No visible pixels in {name}")
    image = image.crop(bounds)
    target_w, target_h = size
    scale = min((target_w - 4) / image.width, (target_h - 3) / image.height)
    image = image.resize((max(1, round(image.width * scale)), max(1, round(image.height * scale))), Image.Resampling.BOX)
    # A hard alpha edge preserves the pixel outline over any background.
    alpha = image.getchannel("A").point(lambda value: 255 if value >= 128 else 0)
    image.putalpha(alpha)
    canvas = Image.new("RGBA", size)
    canvas.alpha_composite(image, ((target_w - image.width) // 2, target_h - image.height - 1))
    canvas.save(OUT / f"{name}.png")


def animal(name, bounds, size=(52, 52)):
    sheet = Image.open(SOURCE / "animals_sheet.png").convert("RGBA")
    image = sheet.crop(bounds)
    rgb = cv2.cvtColor(np.array(image.convert("RGB")), cv2.COLOR_RGB2BGR)
    height, width = rgb.shape[:2]
    # The model added a vignette despite the transparent-sheet prompt. GrabCut
    # separates the crisp animal silhouettes from that soft local background.
    mask = np.zeros((height, width), np.uint8)
    bg_model = np.zeros((1, 65), np.float64)
    fg_model = np.zeros((1, 65), np.float64)
    cv2.grabCut(rgb, mask, (15, 15, width - 30, height - 30), bg_model, fg_model, 8, cv2.GC_INIT_WITH_RECT)
    foreground = np.where((mask == cv2.GC_FGD) | (mask == cv2.GC_PR_FGD), 255, 0).astype(np.uint8)
    # Keep the main body; detached vignette patches are not part of the sprite.
    count, labels, stats, _ = cv2.connectedComponentsWithStats(foreground)
    if count < 2:
        raise ValueError(f"Unable to extract {name}")
    main = 1 + np.argmax(stats[1:, cv2.CC_STAT_AREA])
    foreground = np.where(labels == main, 255, 0).astype(np.uint8)
    rgba = np.array(image)
    rgba[:, :, 3] = foreground
    finish(name, Image.fromarray(rgba), size)


def farmer(name, bounds):
    sheet = Image.open(SOURCE / "farmers_sheet.png").convert("RGBA")
    image = sheet.crop(bounds)
    # The farmer sheet has a clean alpha channel. Remove faint fringe pixels.
    alpha = image.getchannel("A").point(lambda value: 255 if value >= 80 else 0)
    image.putalpha(alpha)
    finish(name, image, (48, 58))


def build():
    animal("chicken_white", (90, 95, 475, 500))
    animal("chicken_brown", (570, 95, 970, 500))
    animal("sheep_white", (1020, 110, 1485, 505), (58, 52))
    animal("sheep_gray", (70, 550, 525, 950), (58, 52))
    animal("cow", (535, 535, 1020, 950), (60, 55))
    farmer("worker_blue", (205, 65, 610, 535))
    farmer("worker_red", (865, 65, 1220, 535))
    farmer("worker_blue_walk", (205, 595, 610, 1090))
    farmer("worker_red_walk", (865, 595, 1220, 1090))


if __name__ == "__main__":
    build()
