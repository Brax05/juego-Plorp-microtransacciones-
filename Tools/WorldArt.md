# World art source

`make_world_art.py` uses Pillow to crop `SourceArt/farm_background_source.png` into the static world background and draw 20 independent transparent pixel sprites. Run `python Tools/make_world_art.py` from the project root to rebuild the assets. The result can be previewed in `world_preview.png` and `world_sprites_preview.png`.

The static landscape source was edited from `SourceArt/farm_source.png` with the built-in image generation tool using this prompt:

> Use case: precise-object-edit. Asset type: STATIC BACKGROUND LAYER for a modular portrait pixel-art farming game. Edit the supplied farm image. Keep the camera angle, overall layout, winding tan paths, green grass, trees, flowers, four rectangular soil beds, fences, chicken shelter, yellow-roof barn, red shed, and sheep pasture fences in place. Remove EVERY changeable gameplay object from the image: all chickens, all sheep, all farmers/workers, all vegetables and fruit/cabbages/carrots/tomatoes, all growing seedlings, and any harvest or tool items. The four crop beds should be clean brown tilled soil with subtle furrows, ready for separate crop sprites to overlay. The animal pens should contain only grass. Keep buildings and environmental props static. Maintain crisp 16-bit pixel-art pixels, dark brown outlines, warm palette. Do not add labels, UI, text, people, animals, crops, or new buildings. Preserve the original image aspect ratio and full composition as closely as possible.

World behavior lives in `Assets/Scripts/World`. `FarmWorldController` places every object, `CropPlot` groups cells, `CropCell` saves growth stages, `WorldActor` saves animal and worker production timers, and `WorldSprite` handles visual art and clicks.
