# Compras locales de Android

La integración usa Unity IAP 5.4 y la validación criptográfica local de Google Play. No requiere servicios externos.

## Antes de generar el AAB

1. Copiar la **clave pública RSA de licencia** de esta app desde Play Console, en la configuración de monetización/licencias.
2. En Unity, abrir **Plorp → Configurar validación de compras**, pegar la cadena Base64 completa y guardarla.
3. Esperar la recompilación. La clave pública se guarda ofuscada en `Assets/Scripts/Shop/GooglePlayLicenseKey.cs`. No introducir contraseñas, claves de servicio ni la clave privada de firma.
4. Mantener `PLORP_IAP` en los símbolos de compilación de Android. El control de compilación impide generar un Android sin IAP o sin la clave pública.
5. Para el Galaxy A02s de prueba, incluir **ARMv7 y ARM64**, aumentar el código de versión y generar el nuevo AAB firmado. Este cambio de compatibilidad debe revisarse en Player Settings.

## Qué verifica una compra

- La firma RSA del recibo usando el validador de Unity IAP.
- El identificador exacto de la aplicación, producto y token recibido de Google Play.
- Que el estado firmado sea comprado y la cantidad sea una unidad. Los pagos diferidos no reciben recompensas.
- Que el token no se haya entregado antes en este guardado. El registro almacena el hash SHA-256 del token; también reconoce registros de la integración anterior.
- El pack de inicio se entrega una vez mientras se conserva el guardado.

Los precios reales proceden de Google Play. Si no están disponibles, se deshabilitan los botones; los precios CLP de referencia solo se usan en la simulación del Editor. En una compilación para teléfono nunca se simula una compra por faltar IAP.

## Guardado, confirmación y recuperación

Antes de modificar el saldo se escribe y vacía a disco un registro de recuperación (`iap-delivery-v1.journal`). Contiene los saldos finales de gemas y monedas de flor, el final del boost, el estado del pack de inicio y el hash de la compra. Después se guardan juntos en PlayerPrefs y se confirma la compra con Google Play. La confirmación consume los packs consumibles para que puedan volver a comprarse.

Al iniciar el juego se recupera una entrega interrumpida antes de cargar los saldos. Si el guardado ya contiene el marcador de entrega, el registro no sobrescribe gemas que el jugador haya gastado después. El checksum detecta corrupción accidental; no autentica archivos frente a una persona que pueda modificarlos.

Una entrega inválida o que no pudo guardarse no se confirma. El botón **Revisar compras** consulta compras pendientes y el pack de inicio. Los fallos de conexión o confirmación se reintentan mientras la app está abierta; una compra ya entregada se confirma sin volver a conceder gemas. Un registro de recuperación dañado bloquea compras adicionales y se conserva para diagnóstico.

## Comprobaciones antes de publicar

Instalar desde la prueba interna con una cuenta incluida en las pruebas de licencia. Confirmar que el diálogo de Google indique una compra de prueba.

| Producto | Recompensa | Referencia CLP |
|---|---|---|
| `plorp.gems.20` | 20 gemas | 200 |
| `plorp.gems.45` | 45 gemas | 400 |
| `plorp.starter.150` | 150 gemas, 10 monedas de flor, x2 durante 60 min | 700 |
| `plorp.gems.120` | 120 gemas | 900 |
| `plorp.gems.260` | 260 gemas | 1800 |
| `plorp.gems.560` | 560 gemas | 3500 |

Comprobar una compra de cada pack, recompra de consumibles, doble toque, cancelación, rechazo, pago diferido, pérdida de conexión y reinicio tras comprar. Verificar que las recompensas persisten al cerrar y que revisar compras varias veces no las duplica.

La suite ejecutable `dotnet run --project Tests/LocalPurchases/LocalPurchases.csproj` requiere .NET SDK 10 y que Unity haya resuelto el paquete IAP en `Library/PackageCache`. Usa el código criptográfico real del paquete con recibos RSA generados para las pruebas. Simula PlayerPrefs, la tienda y los cierres para probar las decisiones de entrega; no sustituye las compras de prueba de Google en el teléfono.

## Alcance de la protección local

Una aplicación modificada puede saltarse estas comprobaciones o alterar PlayerPrefs. Sin un servicio externo no hay un registro global entre dispositivos ni comprobación fiable de reembolsos posteriores: un recibo originalmente válido sigue teniendo una firma válida. Borrar los datos elimina el saldo y el historial local. Google Play no devuelve compras consumibles ya consumidas, por lo que sus gemas no se restauran tras borrar datos o reinstalar. Restaurar el pack de inicio en un guardado nuevo puede volver a conceder sus beneficios; impedirlo globalmente requiere cuentas y un registro externo.

Referencias: [validación local de Unity](https://docs.unity.com/en-us/iap/upgrade-to-iap-v5), [procesamiento y confirmación en Google Play](https://developer.android.com/google/play/billing/integrate), [seguridad de Google Play Billing](https://developer.android.com/google/play/billing/security).
