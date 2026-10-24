# Granja Idle de Plorp

Juego idle de granja en Unity (6000.5, uGUI, vertical 570×1018). Plorp es un extraterrestre verde que vive en su nave y tiene hambre. Pepito, un gato callejero gris, es su traductor: aparece con su nombre abajo para dar consejos, avisar cosas y pedir comida de parte de Plorp.

**La meta:** producir comida (cultivos, huevos, queso, leche, pescados, miel, manzanas y platos de cocina) hasta cumplir todos los pedidos de Plorp y dejarlo **conforme**. Después se puede hacer un **viaje estelar** (prestigio) y volver a empezar más fuerte.

---

## Índice

1. [Cómo abrir y probar el juego](#cómo-abrir-y-probar-el-juego)
2. [Personajes](#personajes)
3. [Pantallas y controles](#pantallas-y-controles)
4. [Cómo está armado el proyecto](#cómo-está-armado-el-proyecto)
5. [La granja (núcleo idle)](#la-granja-núcleo-idle)
6. [Plorp, sus pedidos y Pepito](#plorp-sus-pedidos-y-pepito)
7. [Jardinería](#jardinería)
8. [Tienda y gemas](#tienda-y-gemas)
9. [Logros](#logros)
10. [Guía de bienvenida de Pepito](#guía-de-bienvenida-de-pepito)
11. [Módulo 1: Mientras no estabas](#módulo-1-mientras-no-estabas)
12. [Módulo 2: Misiones diarias y racha](#módulo-2-misiones-diarias-y-racha)
13. [Módulo 3: Eventos en la granja](#módulo-3-eventos-en-la-granja)
14. [Casa de Plorp](#casa-de-plorp) y [Módulo 4: Cocina de Plorp](#módulo-4-cocina-de-plorp)
15. [Módulo 5: Día y noche, y estaciones](#módulo-5-día-y-noche-y-estaciones)
16. [Módulo 6: Zonas en las nubes y pesca](#módulo-6-zonas-en-las-nubes-y-pesca)
17. [Módulos 7 y 8: Sala de juegos](#módulos-7-y-8-sala-de-juegos)
18. [Módulo 9: Colmenas y huerto](#módulo-9-colmenas-y-huerto)
19. [Módulo 10: Viaje estelar](#módulo-10-viaje-estelar)
20. [Sonido y música](#sonido-y-música)
21. [Cómo se juntan todos los multiplicadores](#cómo-se-juntan-todos-los-multiplicadores)
22. [Guardado en la nube y reembolsos](#guardado-en-la-nube-y-reembolsos)
23. [Guardado y pruebas](#guardado-y-pruebas)
24. [De dónde salen las gemas](#de-dónde-salen-las-gemas)
25. [Ajustes](#ajustes)
26. [Mapa de archivos](#mapa-de-archivos)
27. [Estado y pendientes](#estado-y-pendientes)

---

## Cómo abrir y probar el juego

- **Requisitos:** Unity **6000.5.1f1** (URP), con el módulo **Android Build Support** para el celular. Paquetes principales: uGUI, Input System y Unity Purchasing (IAP).
- **Abrir:** abre la carpeta del repositorio en Unity Hub y luego la escena `Assets/Scenes/Granja.unity`. Usa la vista **Game** en formato vertical (570×1018 o un celular en vertical).
- **Jugar:** dale Play. Sale la portada, la pantalla de carga y luego la granja. La primera vez, Pepito explica la granja con su guía.
- **Primera vez después de bajar el proyecto:** usa una vez el menú **Plorp ▸ Hornear UI en el canvas** (ver [Editar el layout en Unity](#editar-el-layout-en-unity)) para que toda la interfaz quede en el canvas y se pueda mover a mano.
- **Celular:** File ▸ Build Profiles ▸ Android, con orientación vertical. Las compras reales solo funcionan con el define `PLORP_IAP` y los productos creados en Google Play (ver [Tienda y gemas](#tienda-y-gemas)).

## Personajes

| Personaje | Quién es | Qué hace en el juego |
|---|---|---|
| **Plorp** | Extraterrestre verde que vive en su nave (un platillo). Solo dice "plorp". | Hace los pedidos de comida (tarjeta "PLORP QUIERE"), pasa en su nave por la ventana de Jardinería soltando gemas, tiene su casa en el granero amarillo y su computador con minijuegos. |
| **Pepito** | Gato callejero gris, el traductor de Plorp. | Da la guía de bienvenida de cada sección, aparece en la granja con consejos, avisa de eventos, platos listos, la noche y la estación nueva, y pide antojos para Plorp. |
| **Gatos de la casa** | 4 gatos: gris, naranjo, negro y blanco. | Caminan dentro de la casa de Plorp; al tocarlos ronronean. |
| **Visitas de la granja** | Cuervo, zorro y camión de feria. | Son los eventos del Módulo 3. |

## Pantallas y controles

**HUD (arriba, siempre visible en la granja):**
- **Nivel de granja** con su barra de progreso (izquierda).
- **Monedas** y **gemas** con su botón **+**, que abre la tienda en la pestaña correspondiente (derecha).
- **Pedido de Plorp** ("PLORP QUIERE") debajo del nivel, con los 3 ingredientes y el botón ENTREGAR.
- **Insignia de estación** (estación, cultivo de temporada, sol o luna) y, si hiciste un viaje estelar, las semillas estelares.

**Abajo:** las tres filas de mejoras (Semillas, Animales, Trabajadores) y las pestañas **GRANJA, TIENDA, LOGROS, JARDINERÍA y AJUSTES**. La **Casa de Plorp** y la **Cocina** se abren desde el granero amarillo de la granja.

**Controles (todo con un dedo):**
- **Tocar** las palabras doradas (¡COSECHA!, ¡HUEVO!…) para recoger, los carteles de las zonas para abrirlas, y los botones.
- **Arrastrar** sobre la granja para recorrer el mapa. El mapa puede bajar más allá del borde de arriba, para que lo que está al norte (las colmenas) quede visible debajo del HUD.
- **Arrastrar** las herramientas de Jardinería (regadera, lámpara, tijeras, abono) sobre la planta.
- **Mantener apretado** en Abducción; **arrastrar** la canasta en Atrapa huevos; **tocar a tiempo** en la pesca.

## Cómo está armado el proyecto

- **Una sola escena:** `Assets/Scenes/Granja.unity`, con el prefab `Assets/Prefabs/PixelFarmCanvas.prefab`.
- **El canvas manda.** Al dar Play, `FarmUIBootstrap` arma cada pantalla con `PixelUIFactory`. Esa fábrica **primero busca en el canvas** un objeto con ese nombre y, si existe, lo usa **tal como está**: respeta su posición, tamaño, color, texto e imagen. Solo crea lo que falta. Lo que el código decide siempre son las acciones de los botones y los valores que cambian mientras juegas (monedas, barras, tiempos).
- **Medidas:** el retrato es de 570×1018 píxeles (0,0 arriba a la izquierda). La granja usa píxeles del diseño `farm_design.png` (1024×765).
- **Guardado:** `PlayerPrefs`, con claves `GlorpFarm.*`.

### Editar el layout en Unity

1. Abre la escena `Granja` y usa el menú **Plorp ▸ Hornear UI en el canvas**.
   - Este comando arma toda la UI del juego dentro de `PixelFarmCanvas.prefab` y la guarda: HUD, pedido de Plorp, Pepito, páginas, casa, cocina, zonas de las nubes, insignia de estación, guía y portada.
   - Los scripts no quedan guardados en el prefab; el juego los agrega solo al dar Play.
   - También borra piezas viejas que ya no se usan (páginas antiguas y terreno modular).
2. Mueve, cambia el tamaño, el color, el texto o la imagen de lo que quieras en la jerarquía, y guarda el prefab o la escena.
3. Al dar Play, el juego usa lo que dejaste.
4. Si después se agregan pantallas nuevas por código, vuelve a hornear. Lo que ya editaste **no se pisa**: solo se agregan las piezas que faltan.

**Granja:** la primera vez se ubica todo desde `FarmArtLayout`, y se guarda el objeto vacío `Layout from canvas` dentro de `Modular farm world`. Desde entonces, campos, cultivos, animales, trabajadores, zoom y vista inicial salen de lo que haya en el canvas. Si borras ese objeto, el código los vuelve a ubicar.

**Cosas que el código sigue moviendo:** solo lo que está animado, como Pepito entrando de lado, las cortinas, la nave, los gatos o lo que rebota. Esas piezas animan **desde la posición que les pongas**.

### Arte: cambiar imágenes

Todas las imágenes están en `Assets/Resources/Art/`, separadas por sección:

| Carpeta | Qué hay |
|---|---|
| `Farm/Map` | `farm_design` (la granja pintada), `clouds_frame` (nubes del borde) |
| `Farm/Crops`, `Farm/Animals`, `Farm/Workers`, `Farm/Buildings` | Cultivos por etapa (`carrot_0..2`…), animales, trabajadores y edificios |
| `Farm/Events`, `Farm/Zones` | Cuervo, zorro y camión; estanque, colmenas, huerto y plataforma |
| `Characters` | Pepito (`pepito_talk`, tira de 34 cuadros), Plorp y su nave |
| `Items` | Huevo, queso, leche, pescados, miel y manzana |
| `Kitchen` | Platos, ollas y pared de la cocina |
| `House` | Interior de la casa, estufa, computador y gatos (`cat_<color>_0/1`) |
| `Minigames` | Fondos de pesca, Abducción y Atrapa huevos, monitor de PLORP-OS, canasta, huevos, piedra y corcho |
| `Garden` | Jardinería: fondo, hojas por estado, flores, herramientas, bichos, cortina y macetero |
| `Icons`, `Shop`, `Screens`, `UI` | Íconos (moneda, gema, sol, luna…), packs de gemas, portada y carga, flecha de la guía y piezas del canvas |
| `Effects` | Partículas: lluvia, pétalos, hojas, nieve, polen y luciérnagas |

- **Para cambiar una imagen,** reemplaza el PNG **manteniendo su nombre**. El código las pide solo por nombre (`Art.Tex("carrot_2")`), así que también puedes moverlas entre carpetas de `Art`; lo único que no puede haber son dos con el mismo nombre.
- **En el canvas,** también puedes poner otra imagen directo en el `RawImage` de un objeto. Esa elección gana sobre la del código.
- **Ya no hay dibujos hechos por código:** fondos, pared de la cocina, monitor, cortinas, flecha y partículas ahora son PNG editables.

## La granja (núcleo idle)

- **Inicio:** 1 campo de zanahorias, 1 gallina y 1 trabajador.
- **Cosechar:** cuando un campo o un animal tiene algo listo, aparece encima una **palabra dorada curva** (¡COSECHA!, ¡HUEVO!, ¡QUESO!, ¡LECHE!). Al tocarla se recoge todo lo de ese campo o animal. Las monedas vuelan al contador y la comida vuela a Plorp. Hay un 4% de probabilidad de **cosecha dorada**, que vale x10 (x2 de probabilidad de noche).
- **Recorrer:** se arrastra el dedo para moverse por la granja. Alrededor hay nubes con candados: son las zonas del Módulo 6.
- **Cultivos con etapas:** brote → planta → fruto. Siguen creciendo con el juego cerrado.
- **Las tres mejoras** (filas de abajo). Cada compra sube un nivel, y cada 10 compras es un **escalón**:

| Mejora | Qué hace cada compra | Cada 10 compras |
|---|---|---|
| Semillas | +8% al valor de los cultivos | Se abre un campo nuevo con un cultivo mejor: zanahoria → repollo → tomate → calabaza. Con todos abiertos, los cultivos valen x2. |
| Animales | +1 animal y +8% al valor de sus productos | Llega una especie nueva: gallina (huevo) → oveja (queso) → vaca (leche). Después, los animales valen x2. |
| Trabajadores | +5% a **todo**, acumulable | Además cosechan solos cada vez más rápido (desde 15 s, hasta 1,5 s). |

- **Precios:** semillas `150 × 1,21^nivel`, animales `250 × 1,24^nivel`, trabajadores `1.200 × 1,32^nivel`. Si un precio sale en rojo te faltan monedas, y el botón te dice cuántas.
- **Ritmo esperado:** una compra cada 30 a 60 segundos al principio, y cada 2 a 3 minutos pasada la primera hora.
- **Nivel máximo de cada mejora:** Semillas 100, Animales 80 y Trabajadores 50. Al llegar, el precio dice MAX.
- **Nivel de granja:** sube cada 3 compras y hace que zonas y premios valgan más.

## Plorp, sus pedidos y Pepito

- **Pedidos:** la tarjeta "PLORP QUIERE" (arriba a la izquierda) pide comida que ya puedes producir. Prefiere lo que Plorp todavía no ha probado, así que cada cultivo o animal nuevo aparece pronto en un pedido.
  - Desde el 3er pedido también pide **un plato de la cocina**, y dos desde el 7º.
  - Al juntar todo se toca **ENTREGAR** y Plorp da monedas y gemas.
  - Son **8 pedidos principales** (Primer bocado … Gran cosecha). Al cumplirlos, Plorp queda **conforme**: se abre el despegue del viaje estelar y siguen "antojos extra".
- **Pepito:**
  - Aparece en la granja cada 30 a 60 segundos con consejos.
  - A veces pide un antojo para Plorp (por ejemplo, una lata de atún) con precio al azar en monedas o gemas. Pagarlo da logros.
  - Avisa de eventos, platos listos, la noche y la estación nueva.
  - Fuera de la pestaña Granja se queda callado y no tapa las otras pantallas.
- **Tips curvos:** de vez en cuando aparecen frases en arco ("¡Toca los tickets dorados!", "¡Hay misiones por cobrar!", etc.).

## Jardinería

Pestaña JARDINERÍA: cuidar la planta de Plorp.

- **Barras:** agua, luz y cuidados. La zona verde de cada barra es lo que le gusta a la planta. Con los cuidados al 100% sube de nivel (1 a 5), y en el **nivel 5 da flor**.
- **La flor:** junta **monedas de flor**, 1 cada 90 s y hasta 10. Además da un bonus permanente a la granja, que depende de la especie.
- **Clima:** normal, soleado o nublado, y cambia cada 2 a 4 min.
  - Si entra **mucho sol** por la ventana, el **botón rojo de la mesa** cierra las cortinas.
  - Si está nublado hay humedad y no hace falta regar.
- **Herramientas que se arrastran con el dedo:**
  - **Regadera:** agua.
  - **Lámpara:** luz.
  - **Tijeras:** cortan hojas secas.
  - **Abono:** revive hasta 2 hojas marchitas y sube los cuidados. Cuesta 2 monedas de flor. Si echas demasiado, las hojas se ensucian.
- **Hojas sucias:** se limpian tocándolas.
- **Bichitos:** aparecen cada 50 a 100 s y se aplastan tocándolos (dan monedas de flor). Si se dejan mucho rato, marchitan hojas.
- **Plorp:** cada 2,5 a 5 min pasa en su nave por la ventana y **suelta gemas** que hay que tocar.
- **Tienda de semillas:** es el macetero con el brote que parpadea. Las especies se compran con monedas de flor:

| Especie | Costo (monedas de flor) | Bonus al florecer |
|---|---|---|
| Planta especial | gratis | +10% a todo |
| Girasol | 8 | +15% cultivos |
| Cactus | 16 | +15% animales |
| Helecho | 25 | trabajadores 15% más rápidos |
| Rosal | 40 | +15% a todo |
| Orquídea | 70 | +25% a todo |

- **Avisos:** los da Pepito con una pausa de 30 s entre uno y otro, sin repetir el mismo mensaje.

## Tienda y gemas

- **BOOSTS (con gemas):**
  - Ganancias x2 por 15 min (20 gemas) o 1 h (60 gemas).
  - Cosecha turbo (todo al doble de rápido) por 10 min (25 gemas).
  - Jardín mágico (cuidados x3) por 10 min (30 gemas).
  - Saltos de tiempo de 1 h (40 gemas) y 4 h (140 gemas).
  - 5 monedas de flor (25 gemas).
- **GEMAS con dinero real** (Unity IAP 5, define `PLORP_IAP`). En el Editor se simulan; en un teléfono nunca. Los precios reales los da Google Play; estos son los de referencia en pesos chilenos:

| Pack | Producto | Precio (CLP) | Gemas | Extra |
|---|---|---|---|---|
| Puñado | `plorp.gems.20` | 200 | 20 | — |
| Bolsita | `plorp.gems.45` | 400 | 45 | — |
| Pack de inicio | `plorp.starter.150` | 700, una vez | 150 | 10 monedas de flor y 1 h de ganancias x2 |
| Bolsa | `plorp.gems.120` | 900 | 120 | — |
| Cofre | `plorp.gems.260` | 1.800 | 260 | — |
| Nave | `plorp.gems.560` | 3.500 | 560 | — |

- **Cómo se protege una compra:** se valida la firma de Google Play en el teléfono, cada compra se entrega una sola vez y una entrega cortada se recupera al volver a abrir. Detalles en [`Docs/ComprasAndroid.md`](Docs/ComprasAndroid.md).
- **Reembolsos:** con el servidor de `Server/` y el define `PLORP_REFUNDS`, las gemas de una compra reembolsada se descuentan. Ver [Guardado en la nube y reembolsos](#guardado-en-la-nube-y-reembolsos).

## Logros

Pestaña LOGROS ▸ sub-pestaña LOGROS. Son metas permanentes que dan gemas: cosechas, monedas ganadas, mejoras, pedidos de Plorp, antojos, cosechas doradas, flores y bichitos. El número rojo de la pestaña suma los logros, las misiones y la racha por cobrar.

## Guía de bienvenida de Pepito

- La primera vez que entras a cada sección (Granja, Tienda, Logros, Jardinería, Ajustes, Casa de Plorp y Cocina), Pepito la explica paso a paso.
- Oscurece la pantalla y marca cada cosa con un marco dorado y una flecha que salta. Tiene botones **SIGUIENTE** y **SALTAR**, y al tocar el globo se completa el texto.
- Cada sección se marca como vista **apenas se abre su guía**: si cierras el juego a mitad de camino, no vuelve a empezar.
- En Ajustes, **VER GUÍA DE PEPITO** la repite entera.

---

## Módulo 1: Mientras no estabas

- Al volver al juego (o desde segundo plano en el celular), si estuviste fuera **1 minuto o más**, sale un panel con Pepito y lo que produjo la granja.
- **Cuánto se gana:** los trabajadores rinden al **50%** sin ti.
- **Tope de tiempo:** 2 h, más 10 min por nivel de trabajadores, hasta **8 h**.
- **Botones:**
  - **RECOGER:** monedas y comida.
  - **x2:** duplica las monedas (la comida no) por 2 gemas cada media hora contada, entre 2 y 20.
- La última vez que estuviste se guarda cada 10 s, al pausar y al salir.

## Módulo 2: Misiones diarias y racha

- **3 misiones por día** (fácil, media y difícil), que se renuevan a medianoche.
  - **Tipos:** cosechar cultivos, juntar productos de animales, comprar mejoras, entregar pedidos, aplastar bichitos, ganar monedas, conseguir cosechas doradas o dar el antojo de Pepito.
  - Las metas crecen con tu granja.
- **Premios:** 2, 3 o 5 gemas, más monedas que crecen con lo que gana tu granja. Con las 3 cobradas se abre el **Cofre del día** (+5 gemas).
- **Racha diaria:** entrar días seguidos da 2, 3, 4, 5, 6, 8 y **15 gemas** el día 7, más monedas, y luego vuelve a empezar. Si faltas un día, vuelve al día 1. La primera entrada de cada día muestra el popup RACHA DIARIA.
- Lo ganado en "Mientras no estabas" **no cuenta** para las misiones.

## Módulo 3: Eventos en la granja

Cada 2 a 3 minutos, solo mientras miras la granja, pasa algo. Pepito lo anuncia y la vista se desliza sola hasta el lugar. Nunca sale el mismo evento dos veces seguidas.

| Evento | Qué pasa | Si reaccionas |
|---|---|---|
| Cuervo | Se posa en el campo con más cosecha lista y se come hasta 4 cultivos. | Tócalo: se va y te da monedas. |
| Lluvia | Durante 1 minuto, todo crece y produce **x2**. | Nada. |
| Zorro | Va al gallinero y roba hasta 3 huevos del granero. | Tócalo: huye y da **1 gema**. |
| Camión de feria | Se estaciona 45 s. | Tócalo: compra a **precio doble** la comida que te sobra (no toca lo del pedido de Plorp). |

## Casa de Plorp

El **granero amarillo** de la granja es la casa de Plorp. Al tocarlo (o su placa "CASA DE PLORP") se abre el interior, con piso de madera, ventana, póster de Plorp, repisas y alfombra.

- **Estufa:** abre la **cocina** (Módulo 4). La olla de encima brilla y dice **¡LISTO!** cuando hay un plato terminado. En la granja, sobre el techo del granero, aparece el mismo aviso.
- **Computador verde de Plorp:** abre **PLORP-OS** con los minijuegos (pesca, Abducción y Atrapa huevos).
  - Al tocarlo, la pantallita del escritorio **se agranda** hasta llenar la ventana.
  - El botón rojo **APAGAR** la **encoge de vuelta** al computador.
  - Al terminar un juego vuelves al computador.
- **Gatos de la casa:** 4 gatos (gris, naranjo, negro y blanco) caminan por el piso. Al tocarlos se detienen y maúllan.
- **VOLVER** regresa a la granja. La primera vez, Pepito explica la casa con su guía.

## Módulo 4: Cocina de Plorp

- **Acceso:** la estufa dentro de la casa de Plorp. Para salir, VOLVER (regresa a la casa).
- **Ollas:** se parte con 2 y la 3ª cuesta 50 gemas. Cocinan con tiempo real, también con el juego cerrado. Al terminar brillan, y al tocarlas el plato pasa al granero.
- **Recetas:** se habilitan cuando la granja produce todos los ingredientes.

| Plato | Ingredientes | Tiempo |
|---|---|---|
| Pastel de zanahoria | 5 zanahorias + 2 huevos | 40 s |
| Ensalada | 4 repollos + 3 zanahorias | 1 min |
| Tortilla | 3 huevos + 2 tomates | 45 s |
| Pizza de queso | 3 tomates + 2 quesos | 2 min |
| Sopa de calabaza | 2 calabazas + 1 leche | 1:30 |
| Tarta de manzana | 4 manzanas + 1 miel + 2 huevos | 1:15 |

- **Valor de un plato:** 1,6 veces lo que valen sus ingredientes.
- **Usos de los platos:**
  - Plorp los pide en sus pedidos, con premios mayores.
  - Se pueden **VENDER** de a uno.
  - El camión de feria también los compra.

## Módulo 5: Día y noche, y estaciones

- **Día y noche:** un día dura **16 minutos reales**: día, atardecer naranjo, **noche** azul con luciérnagas y amanecer. Sigue el reloj real. De noche, las **cosechas doradas salen el doble**.
- **Estaciones:** cambia una por día real, en orden. Cada una tiene su efecto y su **cultivo de temporada**, que vale **x1,5**:

| Estación | Efecto | Cultivo x1,5 |
|---|---|---|
| Primavera | pétalos | zanahoria |
| Verano | polen | tomate |
| Otoño | hojas | calabaza |
| Invierno | nieve | repollo |

- **Insignia en el HUD:** muestra la estación, el cultivo de temporada y un sol o una luna. Al tocarla, Pepito explica la estación y cuánto falta para la noche.
- Pepito anuncia la estación nueva una vez al día y avisa cuando cae la noche.

## Módulo 6: Zonas en las nubes y pesca

Los 4 candados de las nubes ahora son zonas. Se abren tocando su cartel, y quedan abiertas para siempre, incluso después de un viaje estelar.

| Lado | Zona | Cómo se abre |
|---|---|---|
| Oeste | ESTANQUE | 2.000 monedas |
| Norte | COLMENAS | 8.000 monedas (Módulo 9) |
| Este | HUERTO | 20.000 monedas (Módulo 9) |
| Sur | DESPEGUE | se abre solo cuando Plorp queda conforme (Módulo 10) |

**Pesca** (cartel del estanque, o el computador de Plorp):
1. **LANZAR** usa 1 cebo y tira el corcho al agua.
2. Espera **sin tocar**. Cuando el corcho se hunde y sale **¡PICA!**, toca rápido. Si tocas antes, el pez se asusta; si tardas, se va.
3. **Tira:** una barra amarilla va y viene, y hay que tocar cuando está sobre la zona verde.

Peces:

| Pez | Probabilidad | Pescados | Zona verde | Extra |
|---|---|---|---|---|
| Pez | 60% | 1 | ancha | — |
| Trucha | 30% | 2 | media | — |
| Pez dorado | 10% | 3 | angosta y rápida | **+2 gemas** |

- Los pescados van al granero (Plorp los pide) y dan monedas.
- **Cebos:** máximo 5, y vuelve 1 cada 8 min.

## Módulos 7 y 8: Sala de juegos

Los minijuegos están en el **computador verde de Plorp**, dentro de su casa: Pesca, Abducción y Atrapa huevos.

- Cada minijuego arcade dura **30 s**, con cuenta regresiva 3-2-1.
- Usan **boletos**: 3 por juego, y vuelve 1 cada 20 min.
- Se guarda el **récord** de cada juego.

**Abducción (Módulo 7).** Mantén el dedo apretado: la nave de Plorp te sigue y prende el rayo. Deja el rayo sobre un animal un momento para subirlo: **vaca +2**, **oveja +1** y **Pepito −3** (no lo abduzcas). Premio: monedas por punto y gemas desde 10, 16 y 24 puntos (1, 2 y 3).

**Atrapa huevos (Módulo 8).** Arrastra la canasta. **Huevo +1**, **huevo dorado +5** (cuenta como 3 huevos) y **piedra −3**. Caen cada vez más rápido. Los huevos atrapados **van al granero** y dan monedas, y hay gemas desde 18, 28 y 40 puntos.

## Módulo 9: Colmenas y huerto

- **Colmenas (norte):** cada colmena hace 1 **miel** cada 2 min y guarda hasta 4.
- **Huerto (este):** cada manzano hace 1 **manzana** cada 75 s y guarda hasta 5.
- Producen solas, también con el juego cerrado.
- **Ventana de cada zona (al tocar su cartel):**
  - **RECOGER:** pasa la producción al granero y da monedas.
  - **+1 colmena / +1 manzano:** hasta 5; cada uno cuesta 1,8 veces más que el anterior.
- La miel y las manzanas aparecen en los pedidos de Plorp y se usan en la **tarta de manzana**.
- Su valor sube con el nivel de granja y con los trabajadores.

## Módulo 10: Viaje estelar

- **Cuándo:** cuando Plorp queda conforme (8 pedidos), se abre el **DESPEGUE** en las nubes del sur, con la nave de Plorp.
- **Semillas estelares que ganas:** `√(monedas cosechadas en esta granja ÷ 20.000)`, redondeado hacia abajo.
- **Qué da cada semilla:** **+10% a todo lo que gana la granja, para siempre**. También más monedas al empezar: 250 + 500 por semilla.
- **Qué se reinicia:** monedas, mejoras, nivel de granja, granero, cultivos y pedidos de Plorp.
- **Qué se mantiene:** gemas, logros, jardinería, ollas de la cocina, zonas de las nubes, misiones, racha, récords y guía.
- **Cómo se viaja:** **¡DESPEGAR!** pide confirmación (se toca dos veces). Después se recarga la escena con la pantalla de título, y Pepito cuenta cuántas semillas trajo Plorp.
- El HUD muestra "Semillas estelares N (+X%)".

---

## Sonido y música

Todo el audio está en `Assets/Resources/Audio/` y se pide por nombre (`GameAudio`). Para cambiar un sonido, reemplaza el archivo **con el mismo nombre**. Los interruptores Música y Sonidos de Ajustes apagan cada parte.

| Lugar | Música | Ambiente |
|---|---|---|
| Portada y carga | `music_menu_loading` | — |
| Granja de día / de noche | `music_farm_day` / `music_farm_night` | viento + pájaros o grillos (+ lluvia en el evento) |
| Jardinería | `music_garden` | — |
| Casa de Plorp y cocina | `music_house` | olla hirviendo mientras cocina |
| Computador de Plorp (minijuegos) | `music_arcade` | — |
| Estanque (pesca) | `music_pond` | — |
| Viaje estelar | `music_star_trip_jingle` (una vez) | — |

**Efectos (`Audio/SFX`):**
- **Recompensas:** `sfx_gem_drop`, `sfx_gem_pickup`, `sfx_coin`, `sfx_coin_shower`.
- **Cosecha:** `sfx_harvest_ready`, `sfx_harvest_pop`.
- **Planta:** `sfx_water`, `sfx_light`, `sfx_cut`, `sfx_fertilizer`, `sfx_leaf_clean`, `sfx_bug_squash`, `sfx_curtain_close`, `sfx_curtain_open`.
- **Granja y eventos:** `sfx_crow`, `sfx_fox`, `sfx_truck_honk`, `sfx_apple_drop`, `sfx_ufo_pass`.
- **Casa:** `sfx_cat_purr`, `sfx_dish_ready`.
- **Pesca:** `sfx_fish_cast`, `sfx_fish_bite`.
- **Minijuegos:** `sfx_beam`, `sfx_egg_catch`, `sfx_stone_hit`, `sfx_countdown`, `sfx_countdown_go`, `sfx_minigame_end`.
- **Progreso:** `sfx_upgrade`, `sfx_tier_up`, `sfx_order_delivered`, `sfx_achievement`, `sfx_streak_reward`.
- **Personajes e interfaz:** `sfx_pepito_meow`, `sfx_plorp_voice`, `sfx_ui_click`, `sfx_ui_error`.

**Formatos:** música en `.ogg` estéreo (Streaming), ambientes en `.ogg` y efectos en `.wav` mono, normalizados a −1 dBFS. Los scripts que los generan están en `AudioSource/`.

## Cómo se juntan todos los multiplicadores

```
valor de un cultivo = base del cultivo × (1 + 8% × nivel de semillas) × x2 por escalón extra
                      × trabajadores (1 + 5% c/u) × flor del jardín × boost de ganancias
                      × semillas estelares (1 + 10% c/u) × cultivo de temporada (x1,5)

velocidad (cultivos y animales) = boost turbo × lluvia (x2)
probabilidad de cosecha dorada = 4% × noche (x2)
plato = 1,6 × suma de sus ingredientes
```

## Guardado en la nube y reembolsos

- **`SaveStore`** es la única puerta al guardado. Se usa igual que `PlayerPrefs`, con las mismas claves `GlorpFarm.*`, y además permite copiar la partida completa.
- **Aviso TU PARTIDA:** sale la primera vez que se abre el juego y explica dónde se guarda la partida. Se vuelve a ver en AJUSTES ▸ PARTIDA GUARDADA, donde también está el estado de la nube y el botón CONECTAR.
- **Google Play Juegos** (define `PLORP_PGS`): la partida se copia en la cuenta del jugador y vuelve al reinstalar o en otro teléfono. Gana la copia con más tiempo jugado.
- **Reembolsos** (define `PLORP_REFUNDS`): cada compra se registra en el servidor (`Server/`, Firebase). Si Google la anula, el juego descuenta sus gemas y Pepito lo explica.
- Configuración paso a paso y pruebas: [`Docs/GuardadoYReembolsos.md`](Docs/GuardadoYReembolsos.md).

## Guardado y pruebas

- **Todo se guarda solo** con `SaveStore` (que escribe en `PlayerPrefs`). Si cambia el balance base, `ProgressionVersion` en `FarmGameState` reinicia la partida (hoy está en la versión 7).
- **El progreso ya no se borra al compilar ni al dar Play.** Se quitó la herramienta que lo hacía, porque chocaba con "Mientras no estabas", las misiones y la racha.
- **Probar estaciones o la racha:** cambia la fecha del computador. La noche llega sola cada 16 min.
- **Probar "Mientras no estabas":** cierra el juego más de 1 minuto y vuelve a abrir.
- **Reiniciar todo:** Edit ▸ Clear All PlayerPrefs.

## De dónde salen las gemas

| Fuente | Cuántas |
|---|---|
| Pedidos de Plorp | según el pedido; más en los pedidos con platos |
| Logros | cada logro cobrado |
| Misiones diarias | 2, 3 o 5 por misión, y +5 del Cofre del día |
| Racha diaria | de 2 a 15 (día 7) |
| Plorp en la ventana de Jardinería | las que suelta su nave cada 2,5 a 5 min |
| Zorro (evento) | 1 al espantarlo |
| Pez dorado | +2 |
| Abducción y Atrapa huevos | 1, 2 o 3 según los puntos |
| Tienda (dinero real) | packs de 20 a 560 |

**En qué se gastan:** boosts y saltos de tiempo de la tienda, monedas de flor, la 3ª olla de la cocina (50), el x2 de "Mientras no estabas" y algunos antojos de Pepito.

## Ajustes

- **MÚSICA:** prende o apaga la música (ACTIVADO / APAGADO) y ajusta su nivel de volumen con la barra interactiva o los botones `-` y `+`.
- **SONIDOS:** prende o apaga los efectos y ambientes (ACTIVADO / APAGADO) y ajusta su nivel de volumen con la barra interactiva o los botones `-` y `+`.
- **PARTIDA GUARDADA:** dónde se guarda la partida, el botón CONECTAR (Google Play Juegos) y VER AVISO.
- **VER GUÍA DE PEPITO:** vuelve a mostrar la guía de todas las secciones, empezando por la granja.

Las preferencias se guardan (incluso en la nube con Google Play Juegos) y se mantienen aunque hagas un viaje estelar.

## Mapa de archivos

| Carpeta | Qué hay |
|---|---|
| `Scripts/Core/` | `FarmUIBootstrap` (arranque, pestañas, anuncios, pausa), `FarmGameState` (monedas, gemas, niveles, granero, pedidos), `FarmScreen` (HUD y mejoras), `FarmMenuScreen` (páginas), `FarmItems` (toda la comida), `Recharge` (cebos y boletos que se recargan) |
| `Scripts/Farm/` | Economía (`FarmEconomy`), granja pintada y campos (`FarmArtLayout`, `FarmWorldController`, `CropCell`, `CropPlot`, `WorldActor`, `HarvestTicket`), eventos (`FarmEvents`), día/noche y estaciones (`FarmClock`, `FarmAmbience`), zonas (`FarmZones`), viaje estelar (`StarTrip`) |
| `Scripts/Characters/` | Pepito (visitas, consejos, guía de bienvenida), Plorp (cara, pedidos y su panel) |
| `Scripts/Daily/` | Mientras no estabas, misiones diarias y racha |
| `Scripts/Garden/` | Jardinería (planta y pantalla) |
| `Scripts/House/` | Casa de Plorp (interior, gatos) y entrada en el granero amarillo |
| `Scripts/Kitchen/` | Recetas, ollas y página de la cocina |
| `Scripts/Minigames/` | Computador PLORP-OS, pesca, Abducción, Atrapa huevos, boletos |
| `Scripts/Core/SaveStore.cs`, `CloudSave.cs` | Guardado y copia en Google Play Juegos |
| `Scripts/Daily/SaveNotice.cs` | Aviso TU PARTIDA y tarjeta PARTIDA GUARDADA de Ajustes |
| `Scripts/Shop/`, `Scripts/Achievements/` | Tienda, compras reales (validación, registro de entregas, `RefundSync`) y logros |
| `Scripts/UI/` | `PixelUIFactory` (usa el canvas primero), `Art` (imágenes por nombre), `UiUtil` (ayudas comunes), `Popup` (ventanas), textos curvos, recompensas, portada |
| `Scripts/Core/GameAudio.cs` | Música, ambientes y efectos |
| `Editor/PlorpCanvasBaker.cs` | Menú **Plorp ▸ Hornear UI en el canvas** |
| `AudioSource/` (fuera de Assets) | Scripts en Python que generan la música y los efectos |
| `Server/` (fuera de Assets) | Funciones de Firebase para los reembolsos |
| `Docs/` | Compras, guardado y reembolsos, términos, privacidad y ficha de Google Play |
| `Tests/LocalPurchases/` | Pruebas de las compras fuera de Unity (.NET 10) |

## Estado y pendientes

- **Probar en Unity:** el proyecto compila sin errores. Falta probar en el celular el volumen nuevo, el layout y el audio.
- **Hornear el canvas** una vez con **Plorp ▸ Hornear UI en el canvas** para poder editar todo a mano, incluidas las barras de volumen de Ajustes.
- **Sonidos provisionales:** la música y los efectos de `Assets/Resources/Audio/` son sintetizados. Para la versión final se pueden reemplazar por otros (por ejemplo, hechos con Suno, Udio o ElevenLabs) **manteniendo el nombre de cada archivo**.
- **Antes de publicar:**
  - Configurar un keystore local: `ProjectSettings` todavía apunta a `C:/Users/cmont/OneDrive/Desktop/plorp-upload.keystore`, una ruta de otra máquina.
  - Crear en Google Play Console los 6 productos de la tabla de la tienda.
  - Configurar Play Games y el servidor, y probar en el celular (ver [`Docs/GuardadoYReembolsos.md`](Docs/GuardadoYReembolsos.md)).
  - Revisar y publicar los [términos de uso](Docs/TerminosDeUso.md) y la [política de privacidad](Docs/PoliticaDePrivacidad.md).
  - Completar la ficha de Google Play ([borrador](Docs/FichaGooglePlay.md)).
