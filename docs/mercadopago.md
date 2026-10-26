# Pagos con Mercado Pago — manual de implementación

Aura Nova cobra en su propia página (sin redirigir) con **Checkout API vía Orders**: tarjeta (Card Payment Brick) y Yape.
El cliente elige pagar el **50 % de adelanto** o el **total**; el saldo se paga después en la misma página
(`/pago/{id}`) y el pedido no se entrega hasta pagarlo.

## 1. Credenciales y variables

| Dónde | Variable | Valor |
|---|---|---|
| Backend (Render) | `MercadoPago__AccessToken` | Access Token de la aplicación (prueba o producción). **Nunca** en el frontend ni en git. |
| Backend (Render) | `MercadoPago__WebhookSecret` | Clave secreta de *Webhooks → Configurar notificaciones*. |
| Backend (Render) | `MercadoPago__StatementDescriptor` | Opcional. Nombre en el estado de cuenta de la tarjeta (por defecto `AURANOVA`). |
| Backend (Render) | `MercadoPago__PublicKey` | Public Key de la misma aplicación (no es secreta). La web la pide al backend: así las dos claves viven juntas. |
| Frontend (Vercel) | `NEXT_PUBLIC_MERCADOPAGO_PUBLIC_KEY` | Opcional. Si existe, tiene prioridad; requiere volver a desplegar en Vercel. |

Las credenciales de prueba y las de producción empiezan con `APP_USR-`. Se usan las de **prueba** hasta terminar las pruebas,
y se cambian **las dos** (Public Key y Access Token) a las de producción al salir en vivo. Si la Public Key no está
en ningún lado, la página de pago muestra "Los pagos en línea se están activando" y solo ofrece WhatsApp.

## 2. Webhooks (notificaciones)

En *Tus integraciones → Aura Nova Web → Webhooks*, configura **por separado** el modo **Prueba** y el modo **Producción**:

- URL: `https://auranova-backend.onrender.com/api/webhooks/mercadopago`
- Evento: **Order (Mercado Pago)**
- Copia la **clave secreta** en `MercadoPago__WebhookSecret` (Render) y vuelve a desplegar.

El backend valida la firma `x-signature` con el SDK oficial, responde `200` y vuelve a consultar la order a Mercado Pago
con el Access Token (nunca confía en el contenido del aviso). En el panel, **Pagos → Avisos de Mercado Pago** muestra
cada aviso recibido:

| Resultado | Qué significa |
|---|---|
| `processed` | Aviso válido y procesado. |
| `ignored` | Tópico que no usamos (se confirma igual para que no se reintente). |
| `invalid_signature` | La clave secreta de Render no coincide con la del panel de Mercado Pago. |
| `error` | Mercado Pago no respondió al consultar la order: devolvemos 500 y lo reintenta. |

**Respaldo:** cada 5 minutos el servidor consulta los cobros que siguen sin resultado (en revisión, esperando al cliente
o sin respuesta al crearlos, buscándolos por `external_reference`). Así un pago se confirma aunque un aviso se pierda.

## 3. Qué se envía en cada order

- `external_reference`: id interno del intento de cobro (un GUID, **sin datos personales**), único por order.
- `X-Idempotency-Key`: derivada del token de un solo uso; un reenvío del navegador no cobra dos veces.
- `X-meli-session-id`: identificador del dispositivo que genera `security.js` en la página de pago.
- `items`: productos con código, nombre, detalle, categoría (`home`), cantidad, precio y foto; delivery y personalización
  como líneas propias. **Deben sumar el monto cobrado**: en el adelanto y el saldo se prorratean
  (y si los céntimos no cuadran, se envía una línea con el resumen del pedido).
- `payer`: correo, nombre, apellido, documento, celular y dirección (si el delivery es para el propio comprador).
- `shipment.address`: dirección de entrega o de la agencia.
- `additional_info`: fecha de registro del cliente, si es su primera compra, su última compra y si recoge en un punto.
- `statement_descriptor`: nombre en el estado de cuenta (solo tarjetas).

El monto **siempre lo calcula el servidor** (adelanto, total o saldo); el navegador solo envía el token.

## 4. Devoluciones y anulaciones

- **Devolver** (total o parcial): en el detalle del pedido → *Pagos con Mercado Pago* → **Devolver**. Se descuenta de lo
  pagado, queda en el historial y el saldo pendiente se recalcula. Haz las devoluciones desde el panel de Aura Nova;
  si se devuelve todo desde la cuenta de Mercado Pago, el aviso también lo descuenta.
- **Cancelar un pedido** anula en Mercado Pago los cobros que aún esperan una acción del cliente.
  Si un pago en revisión se aprueba con el pedido ya cancelado, se devuelve automáticamente.
- Los pedidos impagos que vencen solos no se cancelan mientras tengan un pago en revisión.

## 5. Pruebas (credenciales de prueba)

| Medio | Datos | Resultado |
|---|---|---|
| Visa `4009 1753 3280 6176`, CVV `123`, vencimiento futuro, DNI `12345678` | Titular `APRO` | Aprobado |
| La misma tarjeta | Titular `OTHE` | Rechazado |
| La misma tarjeta | Titular `CONT` | En revisión (pendiente) |
| Yape, celular `111111111`, código `123456` | — | Aprobado |
| Yape, celular `111111113` | — | Rechazado por monto insuficiente |

Para medir la **calidad de la integración**, haz una compra de prueba **desde la página** (así cuentan MercadoPago.js y
el identificador del dispositivo) y usa el Order ID `ORDTST…` que aparece en el panel, en *Pagos con Mercado Pago*.

## 6. Salir a producción

1. Webhook de **Producción** configurado (URL, evento Order y clave secreta en Render).
2. Cambiar `MercadoPago__AccessToken` (Render) y `NEXT_PUBLIC_MERCADOPAGO_PUBLIC_KEY` (Vercel) a producción y redesplegar.
3. Hacer una compra real de bajo monto y devolverla desde el panel.
4. Revisar en **Pagos** que el aviso llegó como `processed`.

## 7. Conciliación financiera

- Panel → **Pagos**: cobros del periodo con totales (cobrado, devuelto, neto), estado de cada intento y exportación a CSV.
- Cuenta de Mercado Pago → *Reportes*: **Liquidaciones** (comisiones y lo que se deposita) y **Todas las transacciones**.
  Se cruzan con el CSV del panel por el Order ID (`ORD…`).
