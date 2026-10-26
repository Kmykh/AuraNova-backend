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

        private string GetName(Order order) => order.Customer?.Name ?? "Cliente";
        private async Task<string> GetBusinessNameAsync() 
        {
            var settings = await _settingsService.GetPublicAsync();
            return settings.BusinessName;
        }

        public async Task<string> BuildOrderCreatedMessageAsync(Order order)
        {
            var isQuote = order.DeliveryType == DeliveryType.NationalShipping;
            var isCustom = order.IsCustomOrder;
            var businessName = await GetBusinessNameAsync();

            if (isCustom)
            {
                return $"""
                    Hola {GetName(order)}
                    
                    Hemos recibido tu solicitud de pedido personalizado ({order.OrderCode}).
                    
                    En breve revisaremos tus indicaciones y la imagen de referencia para enviarte la cotización.
                    
                    Pedido: {order.OrderCode}
                    Código de seguimiento: {order.TrackingToken}
                    Síguelo aquí: {TrackingUrl(order)}
                    
                    Gracias por elegir {businessName}
                    """.Replace("                    ", "");
            }

            if (isQuote)
            {
                return $"""
                    Hola {GetName(order)}
                    
                    Hemos recibido tu pedido {order.OrderCode} con envío a nivel nacional.
                    
                    En breve calcularemos el costo de envío exacto y te enviaremos la cotización.
                    
                    Pedido: {order.OrderCode}
                    Código de seguimiento: {order.TrackingToken}
                    Síguelo aquí: {TrackingUrl(order)}
                    
                    Gracias por elegir {businessName}
                    """.Replace("                    ", "");
            }

            return $"""
                Hola {GetName(order)}
                
                ¡Tu pedido {order.OrderCode} ha sido registrado!
                
                Total a pagar: S/ {order.Total:F2}
                
                Por favor, realiza el pago mediante Yape y sube la evidencia en nuestra plataforma para que podamos confirmarlo.
                
                Pedido: {order.OrderCode}
                Código de seguimiento: {order.TrackingToken}
                Síguelo aquí: {TrackingUrl(order)}
                
                Gracias por elegir {businessName}
                """.Replace("                ", "");
        }

        public async Task<string> BuildQuoteReadyMessageAsync(Order order)
        {
            var shippingCost = order.DeliveryCost ?? order.Quote?.ShippingCost ?? 0m;
            var customCost = order.Quote?.CustomizationCost ?? order.CustomizationCost;
            var subtotal = order.Subtotal;
            // El total guardado en el pedido es el que se cobra
            var total = order.Total ?? subtotal + shippingCost + customCost;
            var businessName = await GetBusinessNameAsync();
            var shippingLine = order.DeliveryType == DeliveryType.NationalShipping && shippingCost == 0
                ? "Envío: se paga en destino"
                : $"Envío: S/ {shippingCost:F2}";
            var notes = string.IsNullOrWhiteSpace(order.Quote?.Notes) ? "" : $"\nNota: {order.Quote!.Notes!.Trim()}\n";

            if (order.IsCustomOrder)
            {
                return $"""
                    Hola {GetName(order)}
                    
                    Tu cotización de {businessName} para tu pedido personalizado ({order.OrderCode}) ya está lista.
                    
                    Costo del detalle: S/ {customCost:F2}
                    {shippingLine}
                    Total a pagar: S/ {total:F2}
                    {notes}
                    Acéptala y paga desde aquí: {TrackingUrl(order)}
                    Código de seguimiento: {order.TrackingToken}
                    
                    Gracias por elegir {businessName}
                    """.Replace("                    ", "");
            }

            return $"""
                Hola {GetName(order)}
                
                Tu cotización de {businessName} para el pedido {order.OrderCode} ya está lista.
                
                Productos: S/ {subtotal:F2}
                {shippingLine}
                Total: S/ {total:F2}
                {notes}
                Acéptala y paga desde aquí: {TrackingUrl(order)}
                Código de seguimiento: {order.TrackingToken}
                
                Gracias por elegir {businessName}
                """.Replace("                ", "");
        }

        public async Task<string> BuildPaymentReportedMessageAsync(Order order)
        {
            await Task.CompletedTask;
            return $"""
                Hola {GetName(order)}
                
                Recibimos la evidencia de pago de tu pedido {order.OrderCode}.
                
                Nuestro equipo verificará el pago y te avisaremos cuando haya sido confirmado.
                
                Pedido: {order.OrderCode}
                Código de seguimiento: {order.TrackingToken}
                Síguelo aquí: {TrackingUrl(order)}
                """.Replace("                ", "");
        }

        public async Task<string> BuildPaymentConfirmedMessageAsync(Order order)
        {
            var businessName = await GetBusinessNameAsync();
            return $"""
                Hola {GetName(order)}
                
                Tu pago para el pedido {order.OrderCode} ha sido confirmado
                
                Ahora comenzaremos a preparar tu pedido.
                
                Pedido: {order.OrderCode}
                Código de seguimiento: {order.TrackingToken}
                Síguelo aquí: {TrackingUrl(order)}
                
                Gracias por elegir {businessName}
                """.Replace("                ", "");
        }

        public async Task<string> BuildPaymentRejectedMessageAsync(Order order, string? reason)
        {
            await Task.CompletedTask;
            return $"""
                Hola {GetName(order)}
                
                Necesitamos revisar nuevamente el pago de tu pedido {order.OrderCode}.
                
                Motivo:
                {reason ?? "Inconvenientes con la evidencia enviada."}
                
                Puedes volver a realizar el proceso de pago y enviar una nueva evidencia.
                
                Pedido: {order.OrderCode}
                Código de seguimiento: {order.TrackingToken}
                Síguelo aquí: {TrackingUrl(order)}
                """.Replace("                ", "");
        }

        public async Task<string> BuildPreparingMessageAsync(Order order)
        {
            await Task.CompletedTask;
            return $"""
                Hola {GetName(order)}
                
                Tu pedido {order.OrderCode} ya está siendo preparado.
                
                Te avisaremos cuando esté listo.
                
                Pedido: {order.OrderCode}
                Código de seguimiento: {order.TrackingToken}
                Síguelo aquí: {TrackingUrl(order)}
                """.Replace("                ", "");
        }

        public async Task<string> BuildReadyMessageAsync(Order order)
        {
            await Task.CompletedTask;
            var where = order.DeliveryType switch
            {
                DeliveryType.Delivery => "ya está listo para el delivery",
                DeliveryType.NationalShipping => "ya está listo para enviarlo por agencia",
                _ => "ya está listo para recogerlo en el punto de encuentro"
            };

            // Solo pagó el adelanto: el pedido sale cuando pague el saldo
            var due = order.Payment == null ? 0m : Math.Max(0m, order.Payment.Amount - order.Payment.AmountPaid);
            var balance = due > 0
                ? $"\n\nPara entregarlo falta pagar el saldo de S/ {due:F2}.\nPaga con tarjeta o Yape aquí: {_siteUrl}/pago/{order.Id}"
                : "";

            return $"""
                Hola {GetName(order)}
                
                Tu pedido {order.OrderCode} {where}.{balance}
                
                Pedido: {order.OrderCode}
                Código de seguimiento: {order.TrackingToken}
                Síguelo aquí: {TrackingUrl(order)}
                """.Replace("                ", "");
        }

        public async Task<string> BuildShippedMessageAsync(Order order)
        {
            await Task.CompletedTask;
            return $"""
                Hola {GetName(order)}
                
                Tu pedido {order.OrderCode} ya fue enviado.
                
                Pedido: {order.OrderCode}
                Código de seguimiento: {order.TrackingToken}
                Síguelo aquí: {TrackingUrl(order)}
                """.Replace("                ", "");
        }

        public async Task<string> BuildDeliveredMessageAsync(Order order)
        {
            var businessName = await GetBusinessNameAsync();
            return $"""
                Hola {GetName(order)}
                
                Tu pedido {order.OrderCode} ha sido marcado como entregado
                
                Gracias por confiar en {businessName}
                
                Pedido: {order.OrderCode}
                Código de seguimiento: {order.TrackingToken}
                Síguelo aquí: {TrackingUrl(order)}
                """.Replace("                ", "");
        }

        public async Task<string> BuildCancelledMessageAsync(Order order, string? reason)
        {
            var businessName = await GetBusinessNameAsync();
            return $"""
                Hola {GetName(order)}
                
                Tu pedido {order.OrderCode} ha sido cancelado.
                
                Motivo:
                {reason ?? "Cancelado por el administrador."}
                
                Para cualquier consulta, puedes comunicarte con {businessName}.
                """.Replace("                ", "");
        }
    }
}
