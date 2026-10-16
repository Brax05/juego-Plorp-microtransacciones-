# PlorpFarm

## Canvas como fuente de verdad

La escena `Assets/Scenes/SampleScene.unity` usa `Assets/Prefabs/PixelFarmCanvas.prefab`.
En `Portrait screen` se editan paneles, iconos, textos, botones y piezas de
`Modular chrome`. El codigo enlaza las acciones y actualiza valores del juego;
no coloca de nuevo estos objetos ni les cambia fuentes, colores o alineacion.

En `Farm Viewport/Modular farm world` se editan terreno, edificios, corrales,
parcelas, celdas de cultivo, animales y trabajadores. Los animales y trabajadores
se muestran en el editor como referencia; al jugar, aparecen segun las mejoras
compradas, manteniendo la posicion y el aspecto definidos en el prefab.

Haz los cambios antes de Play y guarda la escena o el prefab con Ctrl+S. Unity
descarta los cambios realizados durante Play al salir de ese modo.

`Tools > Plorp > Modularize Farm Canvas` solo agrega piezas faltantes y conserva
las que ya hayas editado. `Pixel UI frame` queda desactivado como atlas de origen.
