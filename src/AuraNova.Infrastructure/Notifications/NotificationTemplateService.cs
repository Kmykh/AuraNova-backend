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
                    Hola {GetName(order)} \U0001F338
                    
                    Hemos recibido tu pedido {order.OrderCode} con envío a nivel nacional.
                    
                    En breve calcularemos el costo de envío exacto y te enviaremos la cotización.
                    
                    Puedes revisar tu pedido aquí:
                    
                    {tracking}
                    
                    Gracias por elegir {businessName} \U0001F495
                    """.Replace("                    ", "");
            }

            return $"""
                Hola {GetName(order)} \U0001F338
                
                ¡Tu pedido {order.OrderCode} ha sido registrado!
                
                Total a pagar: S/ {order.Total:F2}
                
                Por favor, realiza el pago mediante Yape y sube la evidencia en el siguiente enlace para que podamos confirmarlo:
                
                {tracking}
                
                Gracias por elegir {businessName} \U0001F495
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
                Hola {GetName(order)} \U0001F338
                
                Tu cotización de {businessName} para el pedido {order.OrderCode} ya está lista.
                
                Productos: S/ {subtotal:F2}
                Envío: S/ {shippingCost:F2}
                Total: S/ {total:F2}
                
                Puedes revisar el detalle y aceptar la cotización aquí:
                
                {tracking}
                
                Gracias por elegir {businessName} \U0001F495
                """.Replace("                ", "");
        }

        public async Task<string> BuildPaymentReportedMessageAsync(Order order)
        {
            var tracking = await GetTrackingAsync(order);
            return $"""
                Hola {GetName(order)} \U0001F338
                
                Recibimos la evidencia de pago de tu pedido {order.OrderCode}.
                
                Nuestro equipo verificará el pago y te avisaremos cuando haya sido confirmado.
                
                Puedes consultar el estado aquí:
                
                {tracking}
                """.Replace("                ", "");
        }

        public async Task<string> BuildPaymentConfirmedMessageAsync(Order order)
        {
            var businessName = await GetBusinessNameAsync();
            var tracking = await GetTrackingAsync(order);
            return $"""
                Hola {GetName(order)} \U0001F338
                
                Tu pago para el pedido {order.OrderCode} ha sido confirmado \u2705
                
                Ahora comenzaremos a preparar tu pedido.
                
                Puedes consultar su estado aquí:
                
                {tracking}
                
                Gracias por elegir {businessName} \U0001F495
                """.Replace("                ", "");
        }

        public async Task<string> BuildPaymentRejectedMessageAsync(Order order, string? reason)
        {
            var tracking = await GetTrackingAsync(order);
            return $"""
                Hola {GetName(order)} \U0001F338
                
                Necesitamos revisar nuevamente el pago de tu pedido {order.OrderCode}.
                
                Motivo:
                {reason ?? "Inconvenientes con la evidencia enviada."}
                
                Puedes volver a realizar el proceso de pago y enviar una nueva evidencia.
                
                Consulta tu pedido aquí:
                
                {tracking}
                """.Replace("                ", "");
        }

        public async Task<string> BuildPreparingMessageAsync(Order order)
        {
            var tracking = await GetTrackingAsync(order);
            return $"""
                Hola {GetName(order)} \U0001F338
                
                Tu pedido {order.OrderCode} ya está siendo preparado.
                
                Te avisaremos cuando esté listo.
                
                Puedes revisar su seguimiento aquí:
                
                {tracking}
                """.Replace("                ", "");
        }

        public async Task<string> BuildReadyMessageAsync(Order order)
        {
            var tracking = await GetTrackingAsync(order);
            if (order.DeliveryType == DeliveryType.Delivery)
            {
                return $"""
                    Hola {GetName(order)} \U0001F338
                    
                    Tu pedido {order.OrderCode} ya está listo para ser entregado.
                    
                    Tipo de entrega: Delivery
                    
                    Pronto coordinaremos la entrega contigo.
                    
                    Seguimiento:
                    
                    {tracking}
                    """.Replace("                    ", "");
            }

            return $"""
                Hola {GetName(order)} \U0001F338
                
                Tu pedido {order.OrderCode} ya está listo para recoger en el punto de encuentro.
                
                Seguimiento:
                
                {tracking}
                """.Replace("                ", "");
        }

        public async Task<string> BuildShippedMessageAsync(Order order)
        {
            var tracking = await GetTrackingAsync(order);
            return $"""
                Hola {GetName(order)} \U0001F338
                
                Tu pedido {order.OrderCode} ya fue enviado.
                
                Puedes consultar su seguimiento aquí:
                
                {tracking}
                """.Replace("                ", "");
        }

        public async Task<string> BuildDeliveredMessageAsync(Order order)
        {
            var businessName = await GetBusinessNameAsync();
            var tracking = await GetTrackingAsync(order);
            return $"""
                Hola {GetName(order)} 🌸
                
                Tu pedido {order.OrderCode} ha sido marcado como entregado ✅
                
                Gracias por confiar en {businessName} 💕
                
                Puedes consultar el historial del pedido aquí:
                
                {tracking}
                """.Replace("                ", "");
        }

        public async Task<string> BuildCancelledMessageAsync(Order order, string? reason)
        {
            var businessName = await GetBusinessNameAsync();
            return $"""
                Hola {GetName(order)} 🌸
                
                Tu pedido {order.OrderCode} ha sido cancelado.
                
                Motivo:
                {reason ?? "Cancelado por el administrador."}
                
                Para cualquier consulta, puedes comunicarte con {businessName}.
                """.Replace("                ", "");
        }
    }
}
