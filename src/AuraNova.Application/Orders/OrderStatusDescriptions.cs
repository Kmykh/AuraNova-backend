using System;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;

namespace AuraNova.Application.Orders
{
    public static class OrderStatusDescriptions
    {
        public static string? GetDescription(OrderStatus status, Order order)
        {
            return status switch
            {
                OrderStatus.WaitingQuote => order.IsCustomOrder 
                    ? "Estamos analizando los detalles de tu pedido personalizado para darte el mejor precio."
                    : "Estamos revisando tu pedido para confirmar stock y costos de envío.",
                
                OrderStatus.QuoteReady => "¡Cotización lista! Ya puedes revisar el monto total y proceder con el pago para empezar.",
                
                OrderStatus.WaitingPayment => "Tu pedido está pendiente de pago. Por favor, sube tu comprobante para continuar.",
                
                OrderStatus.PaymentReported => "Recibimos tu comprobante de pago y se encuentra en proceso de revisión por nuestro equipo.",
                
                OrderStatus.PaymentConfirmed => "¡Tu pago ha sido confirmado exitosamente! En breve empezaremos con la preparación.",
                
                OrderStatus.Preparing => order.EstimatedReadyAt.HasValue
                    ? $"Estamos preparando tu detalle con mucho amor. Estimamos tenerlo listo el {order.EstimatedReadyAt.Value:dd} de {GetMonthName(order.EstimatedReadyAt.Value.Month)}."
                    : (order.IsCustomOrder ? "Estamos armando tu diseño personalizado tomando en cuenta tus indicaciones." : "Estamos preparando y empaquetando tu pedido con mucho amor."),
                
                OrderStatus.Ready => order.DeliveryType == DeliveryType.MeetingPoint
                    ? "¡Tu pedido está listo! Ya puedes pasar a recogerlo en el punto de encuentro acordado."
                    : "¡Tu pedido está listo y empacado! Pronto será despachado.",
                
                OrderStatus.Shipped => "Tu pedido está en camino hacia la dirección que nos indicaste. ¡Atento a tu teléfono!",
                
                OrderStatus.DeliveredToAgency => GenerateDeliveredToAgencyDescription(order),
                
                OrderStatus.Delivered => "¡Pedido entregado! Esperamos que lo disfrutes mucho. Gracias por confiar en AuraNova.",
                
                OrderStatus.Cancelled => "Este pedido ha sido cancelado. Si crees que es un error, por favor contáctanos.",
                
                _ => null
            };
        }

        private static string GenerateDeliveredToAgencyDescription(Order order)
        {
            if (string.IsNullOrWhiteSpace(order.ShippingProvider))
                return "Tu paquete ha sido dejado en la agencia de envíos. En breve actualizaremos tu código de rastreo.";

            var provider = order.ShippingProvider;
            
            if (string.IsNullOrWhiteSpace(order.ShippingTrackingCode))
                return $"Tu paquete ha sido dejado en la agencia {provider}. En breve te brindaremos tu código de rastreo.";

            return $"Tu paquete ha sido entregado a la agencia {provider}. Tu código de rastreo es {order.ShippingTrackingCode}. Puedes hacer el seguimiento en su página oficial.";
        }

        private static string GetMonthName(int month)
        {
            string[] months = { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
            return month >= 1 && month <= 12 ? months[month] : "";
        }
    }
}
