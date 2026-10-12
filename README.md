# GlorpFarm UI

Open `Assets/Scenes/SampleScene.unity` in Unity 6 and press **Play**. The portrait interface scales to fit the Game view.

The UI is split into `FarmUIBootstrap` (composition and navigation), `FarmScreen` (farm view), `FarmMenuScreen` (secondary views), `PixelUIFactory` (shared visual controls), and `FarmGameState` (saved game values). The bottom tabs open the store, achievements, upgrades, and settings. Harvesting adds coins; upgrades spend coins; the store exchanges gems for coins; achievements award gems. Values are saved with `PlayerPrefs`. The music and sound switches save preferences ready for a future audio system.

To rebuild the interface art after editing it, run `python Tools/make_ui_art.py` with Pillow installed. The farm illustration source is kept in `Tools/SourceArt/farm_source.png`.

The farm art was generated with the built-in image generation tool using this prompt:

> Use case: stylized-concept. Asset type: background artwork for the upper half of a portrait mobile farming game UI in Unity. Use the attached image as a visual style and composition reference only. Create a polished, crisp 16-bit pixel-art farm scene viewed from a slightly elevated isometric angle: vivid green grass, winding tan paths, four rectangular crop beds with tomatoes, carrots and cabbage, small fenced chicken pen with a few white chickens at upper center, sheep pasture at right, rustic wooden barn at upper right, red shed at lower right, a few little farmers in straw hats, trees and tiny flowers. Warm cheerful palette, dark brown pixel outlines, readable large forms. Fill the entire image with artwork, no frame, no UI panels, no text, no numbers, no currency, no buttons, no watermark. Portrait 1:1.1-ish composition; important structures and crops should stay clear of the top 15% because UI will overlay there. Pixel edges sharp, no blurry filtering.
