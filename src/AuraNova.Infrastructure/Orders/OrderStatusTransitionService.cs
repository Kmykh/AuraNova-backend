using AuraNova.Application.Orders.Interfaces;
using AuraNova.Domain.Enums;
using System.Collections.Generic;

namespace AuraNova.Infrastructure.Orders
{
    public class OrderStatusTransitionService : IOrderStatusTransitionService
    {
        // States from which cancellation is allowed
        private static readonly HashSet<OrderStatus> CancellableStates =
        [
            OrderStatus.WaitingQuote,
            OrderStatus.QuoteReady,
            OrderStatus.WaitingPayment,
            OrderStatus.PaymentReported,
            OrderStatus.PaymentConfirmed,
            OrderStatus.Preparing
        ];

        // Forward transitions for MeetingPoint
        private static readonly Dictionary<OrderStatus, OrderStatus[]> MeetingPointTransitions = new()
        {
            [OrderStatus.WaitingQuote]     = [OrderStatus.QuoteReady], // For Custom Orders
            [OrderStatus.QuoteReady]       = [OrderStatus.WaitingPayment], // For Custom Orders
            [OrderStatus.WaitingPayment]   = [OrderStatus.PaymentReported],
            [OrderStatus.PaymentReported]  = [OrderStatus.PaymentConfirmed, OrderStatus.WaitingPayment],
            [OrderStatus.PaymentConfirmed] = [OrderStatus.Preparing],
            [OrderStatus.Preparing]        = [OrderStatus.Ready],
            [OrderStatus.Ready]            = [OrderStatus.Delivered],
        };

        // Forward transitions for Delivery
        private static readonly Dictionary<OrderStatus, OrderStatus[]> DeliveryTransitions = new()
        {
            [OrderStatus.WaitingQuote]     = [OrderStatus.QuoteReady], // For Custom Orders
            [OrderStatus.QuoteReady]       = [OrderStatus.WaitingPayment], // For Custom Orders
            [OrderStatus.WaitingPayment]   = [OrderStatus.PaymentReported],
            [OrderStatus.PaymentReported]  = [OrderStatus.PaymentConfirmed, OrderStatus.WaitingPayment],
            [OrderStatus.PaymentConfirmed] = [OrderStatus.Preparing],
            [OrderStatus.Preparing]        = [OrderStatus.Ready],
            [OrderStatus.Ready]            = [OrderStatus.Shipped],
            [OrderStatus.Shipped]          = [OrderStatus.Delivered],
        };

        // Forward transitions for NationalShipping
        private static readonly Dictionary<OrderStatus, OrderStatus[]> NationalShippingTransitions = new()
        {
            [OrderStatus.WaitingQuote]     = [OrderStatus.QuoteReady], // For Custom Orders
            [OrderStatus.QuoteReady]       = [OrderStatus.WaitingPayment], // For Custom Orders
            [OrderStatus.WaitingPayment]   = [OrderStatus.PaymentReported],
            [OrderStatus.PaymentReported]  = [OrderStatus.PaymentConfirmed, OrderStatus.WaitingPayment],
            [OrderStatus.PaymentConfirmed] = [OrderStatus.Preparing],
            [OrderStatus.Preparing]        = [OrderStatus.Ready],
            [OrderStatus.Ready]            = [OrderStatus.DeliveredToAgency],
            [OrderStatus.DeliveredToAgency] = [OrderStatus.Delivered],
        };

        public bool IsTransitionAllowed(OrderStatus current, OrderStatus target, DeliveryType deliveryType)
        {
            // Cancelled is terminal — no exit
            if (current == OrderStatus.Cancelled)
                return false;

            // Delivered is terminal — no exit
            if (current == OrderStatus.Delivered)
                return false;

            // Cancellation from allowed states
            if (target == OrderStatus.Cancelled)
                return CancellableStates.Contains(current);

            // Select transition map
            var transitions = deliveryType switch
            {
                DeliveryType.NationalShipping => NationalShippingTransitions,
                DeliveryType.Delivery => DeliveryTransitions,
                _ => MeetingPointTransitions
            };

            if (transitions.TryGetValue(current, out var allowedTargets))
                return allowedTargets.Contains(target);

            return false;
        }
    }
}
