# Servidor de reembolsos de Plorp

Funciones de Firebase (Node 22, TypeScript) que registran las compras y descuentan las gemas de las compras reembolsadas.

| Función | Tipo | Qué hace |
|---|---|---|
| `registerpurchase` | POST | Verifica la compra con Google Play y guarda de qué jugador es. |
| `refundsync` | POST | Entrega al juego las gemas a descontar y marca las ya aplicadas. |
| `checkvoided` | Cada 6 horas | Lee las compras anuladas de Google Play y crea los descuentos. |

- Las reglas del descuento están en `functions/src/logic.ts`, con sus pruebas en `functions/src/test/`.
- La configuración paso a paso y las pruebas en el celular están en [`Docs/GuardadoYReembolsos.md`](../Docs/GuardadoYReembolsos.md).
