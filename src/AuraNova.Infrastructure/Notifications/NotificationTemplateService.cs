using System.Threading.Tasks;
using AuraNova.Application.BusinessSettings.Interfaces;
using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using Microsoft.Extensions.Options;

namespace AuraNova.Infrastructure.Notifications
{
    public class NotificationTemplateService : INotificationTemplateService
    {
        private readonly IBusinessSettingsService _settingsService;
        private readonly string _siteUrl;

        public NotificationTemplateService(IBusinessSettingsService settingsService, IOptions<PublicSiteSettings>? siteOptions = null)
        {
            _settingsService = settingsService;
            _siteUrl = (siteOptions?.Value.BaseUrl ?? new PublicSiteSettings().BaseUrl).TrimEnd('/');
        }

        /// <summary>Enlace directo al seguimiento: el cliente no necesita escribir ningún código.</summary>
        private string TrackingUrl(Order order) => $"{_siteUrl}/seguimiento/{order.TrackingToken}";

        /// <summary>Página donde paga con tarjeta o Yape (también acepta la cotización).</summary>
        private string PaymentUrl(Order order) => $"{_siteUrl}/pago/{order.Id}";

        /// <summary>Solo el nombre: "Hola María," se siente más cercano que el nombre completo.</summary>
        private static string FirstName(Order order)
        {
            var name = order.Customer?.Name?.Trim();
            return string.IsNullOrEmpty(name) ? "" : name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        }

        private static string Greeting(Order order) => FirstName(order) is { Length: > 0 } first ? $"Hola {first}," : "Hola,";

        private static string Money(decimal value) => $"S/ {value:F2}";

        private static decimal BalanceDue(Order order) =>
            order.Payment == null ? 0m : Math.Max(0m, order.Payment.Amount - order.Payment.AmountPaid);

        private async Task<string> GetBusinessNameAsync()
        {
            var settings = await _settingsService.GetPublicAsync();
            return string.IsNullOrWhiteSpace(settings.BusinessName) ? "Aura Nova" : settings.BusinessName;
        }

        /// <summary>
        /// Une los bloques del mensaje con una línea en blanco (los vacíos se omiten).
        /// En el correo, los bloques de líneas "Dato: valor" se muestran como tabla.
        /// </summary>
        private static string Compose(params string?[] blocks) =>
            string.Join("\n\n", blocks.Where(b => !string.IsNullOrWhiteSpace(b)).Select(b => b!.Trim()));

        private static string Lines(params string?[] lines) =>
            string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));

        private string TrackingLines(Order order, params string?[] extra) => Lines([
            $"Pedido: {order.OrderCode}",
            .. extra,
            $"Código de seguimiento: {order.TrackingToken}",
            $"Seguimiento: {TrackingUrl(order)}"
        ]);

        public async Task<string> BuildOrderCreatedMessageAsync(Order order)
        {
            var businessName = await GetBusinessNameAsync();

            if (order.IsCustomOrder)
            {
                return Compose(
                    Greeting(order),
                    "Recibimos la idea para tu pedido personalizado. Vamos a revisar tus referencias y te enviaremos la cotización por WhatsApp y por correo.",
                    "Cuando la aceptes, podrás pagar el 50 % de adelanto o el total con tarjeta o Yape.",
                    TrackingLines(order),
                    $"Gracias por elegir {businessName}");
            }

            var total = order.Payment?.Amount ?? order.Total ?? 0m;
            var agencyNote = order.DeliveryType == DeliveryType.NationalShipping
                ? "El envío por agencia no está incluido: lo pagas al recoger tu pedido en destino."
                : null;

            return Compose(
                Greeting(order),
                "Tu pedido está reservado. Para empezar a prepararlo, paga el 50 % de adelanto o el total con tarjeta o Yape.",
                agencyNote,
                Lines(
                    $"Pedido: {order.OrderCode}",
                    $"Total: {Money(total)}",
                    total > 0 ? $"Adelanto (50 %): {Money(Payments.OnlinePaymentService.DepositFor(total))}" : null,
                    $"Pagar: {PaymentUrl(order)}",
                    $"Código de seguimiento: {order.TrackingToken}",
                    $"Seguimiento: {TrackingUrl(order)}"),
                $"Gracias por elegir {businessName}");
        }

        public async Task<string> BuildQuoteReadyMessageAsync(Order order)
        {
            var shippingCost = order.DeliveryCost ?? order.Quote?.ShippingCost ?? 0m;
            var customCost = order.Quote?.CustomizationCost ?? order.CustomizationCost;
            // El total guardado en el pedido es el que se cobra
            var total = order.Total ?? order.Subtotal + shippingCost + customCost;
            var businessName = await GetBusinessNameAsync();
            var shippingLine = order.DeliveryType == DeliveryType.NationalShipping && shippingCost == 0
                ? "Envío: se paga al recoger en la agencia"
                : $"Envío: {Money(shippingCost)}";
            var note = string.IsNullOrWhiteSpace(order.Quote?.Notes) ? null : $"Nota: {order.Quote!.Notes!.Trim().ReplaceLineEndings(" ")}";

            return Compose(
                Greeting(order),
                "Tu cotización está lista. Si estás de acuerdo, acéptala y paga el 50 % de adelanto o el total con tarjeta o Yape.",
                Lines(
                    $"Pedido: {order.OrderCode}",
                    order.IsCustomOrder ? $"Costo del detalle: {Money(customCost)}" : $"Productos: {Money(order.Subtotal)}",
                    shippingLine,
                    $"Total: {Money(total)}",
                    note,
                    $"Ver y aceptar: {PaymentUrl(order)}",
                    $"Código de seguimiento: {order.TrackingToken}"),
                $"Gracias por elegir {businessName}");
        }

        public async Task<string> BuildPaymentReportedMessageAsync(Order order)
        {
            var businessName = await GetBusinessNameAsync();
            return Compose(
                Greeting(order),
                "Recibimos tu comprobante de pago. Lo revisamos y te avisamos apenas esté confirmado.",
                TrackingLines(order),
                $"Gracias por elegir {businessName}");
        }

        public async Task<string> BuildPaymentConfirmedMessageAsync(Order order)
        {
            var businessName = await GetBusinessNameAsync();
            var due = BalanceDue(order);
            var paid = order.Payment?.AmountPaid ?? 0m;

            if (due > 0)
            {
                return Compose(
                    Greeting(order),
                    "Recibimos tu adelanto y ya empezamos a preparar tu pedido.",
                    "El saldo lo pagas cuando tu pedido esté listo; te avisaremos por aquí.",
                    TrackingLines(order, $"Pagaste: {Money(paid)}", $"Falta pagar: {Money(due)}"),
                    $"Gracias por elegir {businessName}");
            }

            return Compose(
                Greeting(order),
                "Tu pago fue aprobado y ya empezamos a preparar tu pedido. Te avisaremos en cada paso hasta la entrega.",
                TrackingLines(order, $"Total pagado: {Money(paid)}"),
                $"Gracias por elegir {businessName}");
        }

        public async Task<string> BuildBalancePaidMessageAsync(Order order)
        {
            var businessName = await GetBusinessNameAsync();
            var next = order.Status == OrderStatus.Ready
                ? "Tu pedido ya está listo: te escribimos para coordinar la entrega."
                : "Seguimos preparando tu pedido y te avisamos cuando esté listo.";
            return Compose(
                Greeting(order),
                $"Recibimos el pago de tu saldo: tu pedido está pagado por completo. {next}",
                TrackingLines(order, $"Total pagado: {Money(order.Payment?.AmountPaid ?? 0m)}"),
                $"Gracias por elegir {businessName}");
        }

        public async Task<string> BuildPaymentRejectedMessageAsync(Order order, string? reason)
        {
            var businessName = await GetBusinessNameAsync();
            var motive = string.IsNullOrWhiteSpace(reason) ? "No pudimos validar el comprobante." : reason.Trim().ReplaceLineEndings(" ");
            return Compose(
                Greeting(order),
                "No pudimos validar el pago de tu pedido. Puedes intentarlo de nuevo con tarjeta o Yape.",
                Lines(
                    $"Pedido: {order.OrderCode}",
                    $"Motivo: {motive}",
                    $"Pagar de nuevo: {PaymentUrl(order)}",
                    $"Código de seguimiento: {order.TrackingToken}"),
                $"Gracias por elegir {businessName}");
        }

        public async Task<string> BuildPreparingMessageAsync(Order order)
        {
            var businessName = await GetBusinessNameAsync();
            return Compose(
                Greeting(order),
                "Tu pedido ya está en el taller: lo estamos preparando con mucho cariño. Te avisaremos cuando esté listo.",
                TrackingLines(order),
                $"Gracias por elegir {businessName}");
        }

        public async Task<string> BuildReadyMessageAsync(Order order)
        {
            var businessName = await GetBusinessNameAsync();
            var where = order.DeliveryType switch
            {
                DeliveryType.Delivery => "Te lo llevamos por delivery",
                DeliveryType.NationalShipping => "Lo enviamos por agencia",
                _ => "Lo entregamos en el punto de encuentro"
            };

            // Solo pagó el adelanto: el pedido sale cuando pague el saldo
            var due = BalanceDue(order);
            if (due > 0)
            {
                return Compose(
                    Greeting(order),
                    $"¡Tu pedido está listo! {where} apenas pagues el saldo de {Money(due)}. Puedes pagarlo con tarjeta o Yape desde el enlace.",
                    Lines(
                        $"Pedido: {order.OrderCode}",
                        $"Falta pagar: {Money(due)}",
                        $"Pagar: {PaymentUrl(order)}",
                        $"Código de seguimiento: {order.TrackingToken}",
                        $"Seguimiento: {TrackingUrl(order)}"),
                    $"Gracias por elegir {businessName}");
            }

            return Compose(
                Greeting(order),
                $"¡Tu pedido está listo! {where}; te escribimos para coordinar la entrega.",
                TrackingLines(order),
                $"Gracias por elegir {businessName}");
        }

        public async Task<string> BuildShippedMessageAsync(Order order)
        {
            var businessName = await GetBusinessNameAsync();
            var byAgency = !string.IsNullOrWhiteSpace(order.ShippingProvider);
            var destination = order.District ?? order.Province ?? "tu ciudad";

            return Compose(
                Greeting(order),
                byAgency
                    ? $"Tu pedido va en camino. Lo dejamos en {order.ShippingProvider} con destino a {destination}."
                    : "Tu pedido va en camino. ¡Pronto estará en tus manos!",
                byAgency ? "Recógelo con tu DNI en la agencia; el envío se paga al recoger." : null,
                TrackingLines(order,
                    byAgency ? $"Agencia: {order.ShippingProvider}" : null,
                    byAgency && !string.IsNullOrWhiteSpace(order.ShippingTrackingCode) ? $"Código de {order.ShippingProvider}: {order.ShippingTrackingCode}" : null),
                $"Gracias por elegir {businessName}");
        }

        public async Task<string> BuildDeliveredMessageAsync(Order order)
        {
            var businessName = await GetBusinessNameAsync();
            return Compose(
                Greeting(order),
                "Tu pedido fue entregado. ¡Esperamos que te encante!",
                "Si quieres contarnos qué te pareció, responde este mensaje: nos encanta leerte.",
                Lines($"Pedido: {order.OrderCode}", $"Seguimiento: {TrackingUrl(order)}"),
                $"Gracias por confiar en {businessName}");
        }

        public async Task<string> BuildCancelledMessageAsync(Order order, string? reason)
        {
            var businessName = await GetBusinessNameAsync();
            var motive = string.IsNullOrWhiteSpace(reason) ? "Lo cancelamos desde la tienda." : reason.Trim().ReplaceLineEndings(" ");
            return Compose(
                Greeting(order),
                "Tu pedido fue cancelado.",
                Lines($"Pedido: {order.OrderCode}", $"Motivo: {motive}"),
                "Si tienes alguna duda, responde este mensaje y te ayudamos.",
                $"Gracias por elegir {businessName}");
        }
    }
}
