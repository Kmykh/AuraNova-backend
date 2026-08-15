using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Orders;
using Xunit;

namespace AuraNova.UnitTests
{
    public class OrderStatusTransitionServiceTests
    {
        private readonly OrderStatusTransitionService _sut = new();

        // ===================== VALID TRANSITIONS =====================

        [Theory]
        [InlineData(OrderStatus.WaitingPayment, OrderStatus.PaymentReported, DeliveryType.Delivery)]
        [InlineData(OrderStatus.WaitingPayment, OrderStatus.PaymentReported, DeliveryType.MeetingPoint)]
        [InlineData(OrderStatus.PaymentReported, OrderStatus.PaymentConfirmed, DeliveryType.Delivery)]
        [InlineData(OrderStatus.PaymentReported, OrderStatus.WaitingPayment, DeliveryType.Delivery)] // Rejection
        [InlineData(OrderStatus.PaymentConfirmed, OrderStatus.Preparing, DeliveryType.Delivery)]
        [InlineData(OrderStatus.Preparing, OrderStatus.Ready, DeliveryType.Delivery)]
        [InlineData(OrderStatus.Ready, OrderStatus.Delivered, DeliveryType.Delivery)]
        [InlineData(OrderStatus.Ready, OrderStatus.Delivered, DeliveryType.MeetingPoint)]
        public void IsTransitionAllowed_ValidDeliveryTransitions_ShouldReturnTrue(
            OrderStatus current, OrderStatus target, DeliveryType deliveryType)
        {
            Assert.True(_sut.IsTransitionAllowed(current, target, deliveryType));
        }

        [Theory]
        [InlineData(OrderStatus.WaitingQuote, OrderStatus.QuoteReady)]
        [InlineData(OrderStatus.QuoteReady, OrderStatus.WaitingPayment)]
        [InlineData(OrderStatus.WaitingPayment, OrderStatus.PaymentReported)]
        [InlineData(OrderStatus.PaymentConfirmed, OrderStatus.Preparing)]
        [InlineData(OrderStatus.Preparing, OrderStatus.Ready)]
        [InlineData(OrderStatus.Ready, OrderStatus.Shipped)]
        [InlineData(OrderStatus.Shipped, OrderStatus.Delivered)]
        public void IsTransitionAllowed_ValidNationalShippingTransitions_ShouldReturnTrue(
            OrderStatus current, OrderStatus target)
        {
            Assert.True(_sut.IsTransitionAllowed(current, target, DeliveryType.NationalShipping));
        }

        // ===================== CANCELLATION =====================

        [Theory]
        [InlineData(OrderStatus.WaitingQuote)]
        [InlineData(OrderStatus.QuoteReady)]
        [InlineData(OrderStatus.WaitingPayment)]
        [InlineData(OrderStatus.PaymentReported)]
        [InlineData(OrderStatus.PaymentConfirmed)]
        [InlineData(OrderStatus.Preparing)]
        public void IsTransitionAllowed_CancellationFromValidStates_ShouldReturnTrue(OrderStatus current)
        {
            Assert.True(_sut.IsTransitionAllowed(current, OrderStatus.Cancelled, DeliveryType.Delivery));
        }

        [Fact]
        public void IsTransitionAllowed_CancellationFromDelivered_ShouldReturnFalse()
        {
            Assert.False(_sut.IsTransitionAllowed(OrderStatus.Delivered, OrderStatus.Cancelled, DeliveryType.Delivery));
        }

        [Fact]
        public void IsTransitionAllowed_CancellationFromCancelled_ShouldReturnFalse()
        {
            Assert.False(_sut.IsTransitionAllowed(OrderStatus.Cancelled, OrderStatus.Cancelled, DeliveryType.Delivery));
        }

        // ===================== INVALID TRANSITIONS =====================

        [Theory]
        [InlineData(OrderStatus.WaitingPayment, OrderStatus.Delivered, DeliveryType.Delivery)]
        [InlineData(OrderStatus.WaitingPayment, OrderStatus.Preparing, DeliveryType.Delivery)]
        [InlineData(OrderStatus.PaymentConfirmed, OrderStatus.Delivered, DeliveryType.Delivery)]
        [InlineData(OrderStatus.Preparing, OrderStatus.Delivered, DeliveryType.Delivery)]
        [InlineData(OrderStatus.Preparing, OrderStatus.Shipped, DeliveryType.Delivery)] // Delivery no usa Shipped
        [InlineData(OrderStatus.Delivered, OrderStatus.Preparing, DeliveryType.Delivery)]
        [InlineData(OrderStatus.QuoteReady, OrderStatus.Preparing, DeliveryType.NationalShipping)]
        [InlineData(OrderStatus.WaitingQuote, OrderStatus.PaymentReported, DeliveryType.NationalShipping)]
        public void IsTransitionAllowed_InvalidTransitions_ShouldReturnFalse(
            OrderStatus current, OrderStatus target, DeliveryType deliveryType)
        {
            Assert.False(_sut.IsTransitionAllowed(current, target, deliveryType));
        }

        // ===================== TERMINAL STATES =====================

        [Fact]
        public void IsTransitionAllowed_FromCancelledToAnyState_ShouldReturnFalse()
        {
            foreach (var status in Enum.GetValues<OrderStatus>())
            {
                Assert.False(_sut.IsTransitionAllowed(OrderStatus.Cancelled, status, DeliveryType.Delivery));
            }
        }

        [Fact]
        public void IsTransitionAllowed_FromDeliveredToAnyState_ShouldReturnFalse()
        {
            foreach (var status in Enum.GetValues<OrderStatus>())
            {
                Assert.False(_sut.IsTransitionAllowed(OrderStatus.Delivered, status, DeliveryType.Delivery));
            }
        }

        // ===================== DELIVERY TYPE SPECIFIC =====================

        [Fact]
        public void IsTransitionAllowed_ReadyToShipped_OnlyForNationalShipping()
        {
            Assert.True(_sut.IsTransitionAllowed(OrderStatus.Ready, OrderStatus.Shipped, DeliveryType.NationalShipping));
            Assert.False(_sut.IsTransitionAllowed(OrderStatus.Ready, OrderStatus.Shipped, DeliveryType.Delivery));
            Assert.False(_sut.IsTransitionAllowed(OrderStatus.Ready, OrderStatus.Shipped, DeliveryType.MeetingPoint));
        }
    }
}
