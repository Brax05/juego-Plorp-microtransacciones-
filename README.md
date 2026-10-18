# Granja Idle de Plorp

Juego idle de granja en Unity (6000.5, uGUI, vertical 570×1018). Plorp es un extraterrestre verde que vive en su nave y tiene hambre. Pepito, un gato callejero gris, es su traductor: aparece con su nombre abajo para dar consejos, avisar cosas y pedir comida de parte de Plorp.

**La meta:** producir comida (cultivos, huevos, queso, leche, pescados, miel, manzanas y platos de cocina) hasta cumplir todos los pedidos de Plorp y dejarlo **conforme**. Después se puede hacer un **viaje estelar** (prestigio) y volver a empezar más fuerte.

---

## Índice

1. [Cómo está armado el proyecto](#cómo-está-armado-el-proyecto)
2. [La granja (núcleo idle)](#la-granja-núcleo-idle)
3. [Plorp, sus pedidos y Pepito](#plorp-sus-pedidos-y-pepito)
4. [Jardinería](#jardinería)
5. [Tienda y gemas](#tienda-y-gemas)
6. [Logros](#logros)
7. [Guía de bienvenida de Pepito](#guía-de-bienvenida-de-pepito)
8. [Módulo 1: Mientras no estabas](#módulo-1-mientras-no-estabas)
9. [Módulo 2: Misiones diarias y racha](#módulo-2-misiones-diarias-y-racha)
10. [Módulo 3: Eventos en la granja](#módulo-3-eventos-en-la-granja)
11. [Casa de Plorp](#casa-de-plorp) y [Módulo 4: Cocina de Plorp](#módulo-4-cocina-de-plorp)
12. [Módulo 5: Día y noche, y estaciones](#módulo-5-día-y-noche-y-estaciones)
13. [Módulo 6: Zonas en las nubes y pesca](#módulo-6-zonas-en-las-nubes-y-pesca)
14. [Módulos 7 y 8: Sala de juegos](#módulos-7-y-8-sala-de-juegos)
15. [Módulo 9: Colmenas y huerto](#módulo-9-colmenas-y-huerto)
16. [Módulo 10: Viaje estelar](#módulo-10-viaje-estelar)
17. [Cómo se juntan todos los multiplicadores](#cómo-se-juntan-todos-los-multiplicadores)
18. [Guardado y pruebas](#guardado-y-pruebas)
19. [Mapa de archivos](#mapa-de-archivos)

---

## Cómo está armado el proyecto

- **Una sola escena:** `Assets/Scenes/Granja.unity`, con el prefab `Assets/Prefabs/PixelFarmCanvas.prefab`.
- **El código arma la UI al dar Play.** `FarmUIBootstrap` se crea solo (`RuntimeInitializeOnLoadMethod`) y compone todo sobre el `Portrait screen` del canvas: granja pintada, nubes, Plorp, Pepito, páginas de menú, ventanas y minijuegos. Las coordenadas son píxeles del retrato de 570×1018 medidos desde la esquina superior izquierda. La granja usa píxeles del diseño `World/farm_design.png` (1024×765).
- **Vista previa en el editor:** `Tools ▸ Plorp ▸ Vista previa de la escena` (Ctrl/Cmd+Shift+P) construye en modo edición una copia que no se guarda, para ver la UI como en Play. Cada pestaña tiene su botón.
- **Arte:** todo está en `Assets/Resources` y se carga por ruta (`Resources.Load`). Los sprites nuevos (cuervo, zorro, camión, platos, ollas, peces, colmenas, huerto, etc.) son pixel art con borde oscuro, igual que los animales.
- **Guardado:** `PlayerPrefs`, con claves `GlorpFarm.*`. El prefijo viejo se mantiene para no perder partidas.

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
- **GEMAS con dinero real** (Unity IAP, define `PLORP_IAP`; sin ese define funciona en modo prueba):

| Pack | Precio | Gemas | Extra |
|---|---|---|---|
| Puñado | $50 | 20 | — |
| Bolsita | $100 | 45 | — |
| Bolsa | $250 | 120 | — |
| Cofre | $500 | 260 | — |
| Nave | $1.000 | 560 | — |
| Pack de inicio | $300, una vez | 150 | 10 monedas de flor y 1 h de boost |

- **Antes de publicar:** crear los productos `plorp.gems.*` y `plorp.starter` en Google Play Console y validar los recibos en un servidor.

## Logros

Pestaña LOGROS ▸ sub-pestaña LOGROS. Son metas permanentes que dan gemas: cosechas, monedas ganadas, mejoras, pedidos de Plorp, antojos, cosechas doradas, flores y bichitos. El número rojo de la pestaña suma los logros, las misiones y la racha por cobrar.

## Guía de bienvenida de Pepito

- La primera vez que entras a cada sección (Granja, Tienda, Logros, Jardinería, Ajustes, Casa de Plorp y Cocina), Pepito la explica paso a paso.
- Oscurece la pantalla y marca cada cosa con un marco dorado y una flecha que salta. Tiene botones **SIGUIENTE** y **SALTAR**, y al tocar el globo se completa el texto.
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

## Cómo se juntan todos los multiplicadores

```
valor de un cultivo = base del cultivo × (1 + 8% × nivel de semillas) × x2 por escalón extra
                      × trabajadores (1 + 5% c/u) × flor del jardín × boost de ganancias
                      × semillas estelares (1 + 10% c/u) × cultivo de temporada (x1,5)

velocidad (cultivos y animales) = boost turbo × lluvia (x2)
probabilidad de cosecha dorada = 4% × noche (x2)
plato = 1,6 × suma de sus ingredientes
```

## Guardado y pruebas

- **Todo se guarda solo** en `PlayerPrefs`. Si cambia el balance base, `ProgressionVersion` en `FarmGameState` reinicia la partida (hoy está en la versión 7).
- **Probar estaciones o la racha:** cambia la fecha del computador. La noche llega sola cada 16 min.
- **Probar "Mientras no estabas":** cierra el juego más de 1 minuto y vuelve a abrir.
- **Reiniciar todo:** borra los PlayerPrefs (Edit ▸ Clear All PlayerPrefs).

## Mapa de archivos

| Carpeta / archivo | Qué hay |
|---|---|
| `Scripts/FarmUIBootstrap.cs` | Arma la UI, navegación de pestañas, anuncios de Pepito, guardado al pausar |
| `Scripts/FarmGameState.cs` | Monedas, gemas, niveles, granero, pedidos, guardado |
| `Scripts/World/FarmEconomy.cs` | Todos los números del idle (valores, tiempos, precios, offline) |
| `Scripts/World/FarmWorldController.cs`, `FarmArtLayout.cs`, `CropCell.cs`, `CropPlot.cs`, `HarvestTicket.cs` | Granja pintada, campos, animales, trabajadores, palabras doradas |
| `Scripts/World/FarmEvents.cs` | Módulo 3 (cuervo, lluvia, zorro, camión) |
| `Scripts/World/FarmClock.cs`, `FarmAmbience.cs` | Módulo 5 (día/noche, estaciones, insignia) |
| `Scripts/World/FarmZones.cs` | Módulos 6, 9 y 10 (zonas en las nubes, colmenas, huerto) |
| `Scripts/World/StarTrip.cs` | Módulo 10 (viaje estelar) |
| `Scripts/House/` | Casa de Plorp (interior, gatos) y la entrada en el granero amarillo |
| `Scripts/Minigames/` | Computador de Plorp (PLORP-OS), pesca, Abducción, Atrapa huevos, boletos |
| `Scripts/Kitchen/` | Módulo 4 (recetas, ollas y página de la cocina) |
| `Scripts/Daily/` | Módulo 2 (misiones y racha) |
| `Scripts/Plorp/` | Plorp, pedidos, Pepito, guía, Mientras no estabas, comida (`FarmItems`) |
| `Scripts/Garden/` | Jardinería |
| `Scripts/Shop/`, `Scripts/Achievements/` | Tienda, compras reales y logros |
| `Scripts/UI/` | Fábrica de UI pixel, ventanas (`Popup`), textos curvos, pantallas de inicio |
| `Editor/PlorpScenePreview.cs` | Vista previa de la escena en el editor |
