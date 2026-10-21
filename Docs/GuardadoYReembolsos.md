# Guardado en la nube y reembolsos

Rama: `PlorpGuardadoNube` (sale de `PlorpConPagos`). Todo el código está listo, pero **apagado** hasta que se agreguen los símbolos de compilación. Sin ellos, el juego funciona igual que antes y guarda solo en el teléfono.

| Símbolo | Qué enciende | Requisito |
|---|---|---|
| `PLORP_PGS` | Guardado en Google Play Juegos | Plugin de Play Games importado y configurado |
| `PLORP_REFUNDS` | Registro de compras y descuento de gemas reembolsadas | `PLORP_PGS` y el servidor desplegado |

Se agregan en **Edit ▸ Project Settings ▸ Player ▸ Android ▸ Other Settings ▸ Scripting Define Symbols**, junto a `PLORP_IAP`.

## Qué hace el código

- **`SaveStore`** (`Assets/Scripts/Core/SaveStore.cs`) reemplaza a `PlayerPrefs` en todo el juego. Usa las mismas claves `GlorpFarm.*` y guarda en el mismo lugar, así que las partidas actuales siguen funcionando. Además anota qué claves usa el juego, para copiar la partida completa a la nube.
- **`CloudSave`** (`Assets/Scripts/Core/CloudSave.cs`):
  - Al abrir el juego inicia sesión en Play Games sin botón y lee la copia de la nube.
  - Si la nube tiene **más tiempo jugado** que el teléfono, la trae y recarga la granja. Pepito avisa: "Plorp encontró tu granja en Google Play Juegos".
  - Si no, sube la del teléfono: al cambiar algo (máximo una vez por minuto) y al pasar el juego a segundo plano.
  - Nunca pisa una copia de la nube con más tiempo jugado.
  - No trae la nube si hay una compra a medio entregar en el teléfono.
- **`SaveNotice`** (`Assets/Scripts/Daily/SaveNotice.cs`):
  - Ventana "TU PARTIDA" la primera vez que se abre el juego, antes de la guía de Pepito.
  - En **AJUSTES ▸ PARTIDA GUARDADA** muestra el estado y los botones CONECTAR y VER AVISO.
- **`RefundSync`** (`Assets/Scripts/Shop/RefundSync.cs`):
  - Cada compra entregada se registra en el servidor con el id de jugador de Play Games.
  - Cada 10 minutos pide las compras reembolsadas y descuenta sus gemas. El saldo puede quedar negativo, y Pepito explica por qué.
- **Servidor** (`Server/`, Firebase):
  - `registerpurchase` verifica la compra con Google Play y guarda de quién es.
  - `checkvoided` lee cada 6 horas las compras anuladas de Google Play.
  - `refundsync` entrega al juego las gemas a descontar.

## Paso 1: Play Games y guardado en la nube

**En Play Console:**

1. **Play Games Services ▸ Configuración**: crear el proyecto de juego para `com.glorpfarm.app`.
2. **Credenciales de Android**, con el SHA-1 de:
    - la **firma de apps de Google Play** (Configuración ▸ Integridad de la app);
    - la **clave de subida**;
    - la clave de depuración, si se prueba con builds de desarrollo.
3. **Credencial de servidor de juegos** (cliente web de OAuth): la usa el servidor de reembolsos. Guardar el *Client ID* y el *Client secret*.
4. Pantalla de consentimiento de OAuth completa (nombre de la app, correo y política de privacidad).
5. **Propiedades ▸ Partidas guardadas: ACTIVADO.**
6. Agregar las cuentas de prueba como **testers** de Play Games y publicar los cambios de la configuración.

**En Unity:**

1. Importar `GooglePlayGamesPlugin-2.3.0.unitypackage` (de [github.com/playgameservices/play-games-plugin-for-unity](https://github.com/playgameservices/play-games-plugin-for-unity), carpeta `current-build`).
2. **Assets ▸ External Dependency Manager ▸ Android Resolver ▸ Force Resolve.**
3. **Window ▸ Google Play Games ▸ Setup ▸ Android Setup**:
    - pegar los recursos XML que entrega Play Console (Logros o Configuración ▸ "Obtener recursos");
    - pegar el *Client ID* web.
4. **Minimum API Level: 24 (Android 7.0)**, que es lo que exige el plugin 2.3.0.
5. Agregar `PLORP_PGS` a los símbolos de Android.
6. Usar una vez **Plorp ▸ Hornear UI en el canvas**, para que los botones nuevos de AJUSTES queden en el canvas.

## Paso 2: servidor de reembolsos (Firebase)

1. Crear un proyecto de Firebase. Puede ser el mismo proyecto de Google Cloud de Play Games.
2. Activar el **plan Blaze** (pago por uso). Lo piden las tareas programadas y los secretos; con pocos jugadores el uso cabe en la cuota gratis.
3. Crear **Firestore** en `southamerica-east1`.
4. En Google Cloud, habilitar **Google Play Android Developer API** y **Google Play Game Services API**.
5. En `Server/.firebaserc`, reemplazar `PONER-AQUI-EL-ID-DEL-PROYECTO` por el id del proyecto.
6. Desde la carpeta `Server/`:
    ```
    npm install --prefix functions
    npm test --prefix functions
    firebase functions:secrets:set PGS_WEB_CLIENT_SECRET
    firebase deploy --only functions,firestore:rules
    ```
    `firebase deploy` pregunta `PGS_WEB_CLIENT_ID` (el *Client ID* web del paso 1). `PLAY_PACKAGE_NAME` ya viene como `com.glorpfarm.app`.
7. En **Play Console ▸ Usuarios y permisos**, invitar la cuenta de servicio de las funciones (`NUMERO-DEL-PROYECTO-compute@developer.gserviceaccount.com`) con el permiso **Ver datos financieros, pedidos y respuestas de la encuesta de cancelación**. Google puede tardar hasta 24 horas en activar ese permiso.
8. En `Assets/Scripts/Shop/RefundSync.cs`, poner el id del proyecto en `ServerUrl`.
9. Agregar `PLORP_REFUNDS` a los símbolos de Android.

## Comprobación

**Sin símbolos nuevos (lo primero, antes de configurar nada):**

- [ ] El proyecto compila sin errores en la consola.
- [ ] Las partidas existentes siguen igual: monedas, gemas, mejoras, jardín y cocina.
- [ ] Sale la ventana TU PARTIDA una sola vez y se vuelve a ver desde AJUSTES ▸ VER AVISO.
- [ ] AJUSTES ▸ PARTIDA GUARDADA dice "Tu partida se guarda solo en este teléfono".
- [ ] Una compra de prueba sigue entregando las gemas, y la suite `Tests/LocalPurchases` pasa.
- [ ] El viaje estelar sigue funcionando (usa la misma recarga de la granja).

**Con `PLORP_PGS`, en el celular con una cuenta tester:**

- [ ] Al abrir el juego aparece el inicio de sesión de Play Games y AJUSTES dice "...y en tu cuenta de Google Play Juegos".
- [ ] Jugar 5 minutos, pasar el juego a segundo plano, desinstalar, reinstalar y abrir: vuelve la granja y Pepito avisa.
- [ ] Teléfono B con la misma cuenta: trae la partida del teléfono A.
- [ ] Sin sesión (cancelar el inicio): el juego funciona y guarda solo en el teléfono. CONECTAR permite iniciar sesión después.
- [ ] Sin internet: el juego funciona y sube la copia cuando vuelve la conexión.
- [ ] Una compra hecha en el teléfono aparece en el otro teléfono después de sincronizar.

**Con `PLORP_REFUNDS`:**

- [ ] Al comprar, la colección `purchases` de Firestore recibe la compra con el id del jugador.
- [ ] Reembolsar ese pedido en Play Console (Gestión de pedidos ▸ Reembolsar). En las 6 horas siguientes aparece en `deductions`. Para no esperar, ejecutar `checkvoided` a mano desde Google Cloud ▸ Cloud Scheduler ▸ "Forzar ejecución".
- [ ] En el juego, dentro de los 10 minutos siguientes, se descuentan las gemas y Pepito lo explica. El documento de `deductions` queda con `applied: true`.
- [ ] Si las gemas ya se habían gastado, el saldo queda negativo y no se puede gastar gemas hasta recuperarlas.

## Límites conocidos

- Las partidas guardadas **antes** de esta versión empiezan con 0 de tiempo jugado. Si en otro teléfono ya existe una copia en la nube con más tiempo, esa copia gana. Solo afecta a los testers actuales.
- Si dos teléfonos juegan a la vez con la misma cuenta, gana el que lleva más tiempo jugado. El otro pierde lo jugado desde la última copia.
- Sin sesión de Play Games no hay copia en la nube ni registro de compras. Esas gemas no se pueden descontar si se reembolsan.
- Una app modificada puede saltarse las comprobaciones del teléfono. El servidor solo controla los reembolsos, no el saldo de gemas.
