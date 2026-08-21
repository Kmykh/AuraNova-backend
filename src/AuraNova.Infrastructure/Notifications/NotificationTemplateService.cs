using System.Threading.Tasks;
using AuraNova.Application.BusinessSettings.Interfaces;
using AuraNova.Application.Notifications.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;

namespace AuraNova.Infrastructure.Notifications
{
    public class NotificationTemplateService : INotificationTemplateService
    {
        private readonly ITrackingUrlService _trackingUrlService;
        private readonly IBusinessSettingsService _settingsService;

        public NotificationTemplateService(ITrackingUrlService trackingUrlService, IBusinessSettingsService settingsService)
        {
            _trackingUrlService = trackingUrlService;
            _settingsService = settingsService;
        }

        private string GetName(Order order) => order.Customer?.Name ?? "Cliente";
        private async Task<string> GetTrackingAsync(Order order) => await _trackingUrlService.GenerateTrackingUrlAsync(order);
        private async Task<string> GetBusinessNameAsync() 
        {
            var settings = await _settingsService.GetPublicAsync();
            return settings.BusinessName;
        }

        public async Task<string> BuildOrderCreatedMessageAsync(Order order)
        {
            var isQuote = order.DeliveryType == DeliveryType.NationalShipping;
            var businessName = await GetBusinessNameAsync();
            var tracking = await GetTrackingAsync(order);

            if (isQuote)
            {
                return $"""
                    Hola {GetName(order)}
                    
                    Hemos recibido tu pedido {order.OrderCode} con envío a nivel nacional.
                    
                    En breve calcularemos el costo de envío exacto y te enviaremos la cotización.
                    
                    Tu código de pedido es: {order.OrderCode}
                    Tu token de seguridad es: {order.TrackingToken}
                    
                    Gracias por elegir {businessName}
                    """.Replace("                    ", "");
            }

            return $"""
                Hola {GetName(order)}
                
                ¡Tu pedido {order.OrderCode} ha sido registrado!
                
                Total a pagar: S/ {order.Total:F2}
                
                Por favor, realiza el pago mediante Yape y sube la evidencia en nuestra plataforma para que podamos confirmarlo.
                
                Tu código de pedido es: {order.OrderCode}
                Tu token de seguridad es: {order.TrackingToken}
                
                Gracias por elegir {businessName}
                """.Replace("                ", "");
        }

        public async Task<string> BuildQuoteReadyMessageAsync(Order order)
        {
            var shippingCost = order.Quote?.ShippingCost ?? 0m;
            var subtotal = order.Subtotal;
            var total = subtotal + shippingCost;
            var businessName = await GetBusinessNameAsync();
            var tracking = await GetTrackingAsync(order);

            return $"""
                Hola {GetName(order)}
                
                Tu cotización de {businessName} para el pedido {order.OrderCode} ya está lista.
                
                Productos: S/ {subtotal:F2}
                Envío: S/ {shippingCost:F2}
                Total: S/ {total:F2}
                
                Tu código de pedido es: {order.OrderCode}
                Tu token de seguridad es: {order.TrackingToken}
                
                Gracias por elegir {businessName}
                """.Replace("                ", "");
        }

        public async Task<string> BuildPaymentReportedMessageAsync(Order order)
        {
            var tracking = await GetTrackingAsync(order);
            return $"""
                Hola {GetName(order)}
                
                Recibimos la evidencia de pago de tu pedido {order.OrderCode}.
                
                Nuestro equipo verificará el pago y te avisaremos cuando haya sido confirmado.
                
                Tu código de pedido es: {order.OrderCode}
                Tu token de seguridad es: {order.TrackingToken}
                """.Replace("                ", "");
        }

        public async Task<string> BuildPaymentConfirmedMessageAsync(Order order)
        {
            var businessName = await GetBusinessNameAsync();
            var tracking = await GetTrackingAsync(order);
            return $"""
                Hola {GetName(order)}
                
                Tu pago para el pedido {order.OrderCode} ha sido confirmado
                
                Ahora comenzaremos a preparar tu pedido.
                
                Tu código de pedido es: {order.OrderCode}
                Tu token de seguridad es: {order.TrackingToken}
                
                Gracias por elegir {businessName}
                """.Replace("                ", "");
        }

        public async Task<string> BuildPaymentRejectedMessageAsync(Order order, string? reason)
        {
            var tracking = await GetTrackingAsync(order);
            return $"""
                Hola {GetName(order)}
                
                Necesitamos revisar nuevamente el pago de tu pedido {order.OrderCode}.
                
                Motivo:
                {reason ?? "Inconvenientes con la evidencia enviada."}
                
                Puedes volver a realizar el proceso de pago y enviar una nueva evidencia.
                
                Tu código de pedido es: {order.OrderCode}
                Tu token de seguridad es: {order.TrackingToken}
                """.Replace("                ", "");
        }

        public async Task<string> BuildPreparingMessageAsync(Order order)
        {
            var tracking = await GetTrackingAsync(order);
            return $"""
                Hola {GetName(order)}
                
                Tu pedido {order.OrderCode} ya está siendo preparado.
                
                Te avisaremos cuando esté listo.
                
                Tu código de pedido es: {order.OrderCode}
                Tu token de seguridad es: {order.TrackingToken}
                """.Replace("                ", "");
        }

        public async Task<string> BuildReadyMessageAsync(Order order)
        {
            var tracking = await GetTrackingAsync(order);
            if (order.DeliveryType == DeliveryType.Delivery)
            {
                return $"""
                    Hola {GetName(order)}
                    
                    Tu pedido {order.OrderCode} ya está listo para ser entregado.
                    
                    Tipo de entrega: Delivery
                    
                    Pronto coordinaremos la entrega contigo.
                    
                    Tu código de pedido es: {order.OrderCode}
                    Tu token de seguridad es: {order.TrackingToken}
                    """.Replace("                    ", "");
            }

            return $"""
                Hola {GetName(order)}
                
                Tu pedido {order.OrderCode} ya está listo para recoger en el punto de encuentro.
                
                Tu código de pedido es: {order.OrderCode}
                Tu token de seguridad es: {order.TrackingToken}
                """.Replace("                ", "");
        }

        public async Task<string> BuildShippedMessageAsync(Order order)
        {
            var tracking = await GetTrackingAsync(order);
            return $"""
                Hola {GetName(order)}
                
                Tu pedido {order.OrderCode} ya fue enviado.
                
                Tu código de pedido es: {order.OrderCode}
                Tu token de seguridad es: {order.TrackingToken}
                """.Replace("                ", "");
        }

        public async Task<string> BuildDeliveredMessageAsync(Order order)
        {
            var businessName = await GetBusinessNameAsync();
            var tracking = await GetTrackingAsync(order);
            return $"""
                Hola {GetName(order)}
                
                Tu pedido {order.OrderCode} ha sido marcado como entregado
                
                Gracias por confiar en {businessName}
                
                Tu código de pedido es: {order.OrderCode}
                Tu token de seguridad es: {order.TrackingToken}
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
