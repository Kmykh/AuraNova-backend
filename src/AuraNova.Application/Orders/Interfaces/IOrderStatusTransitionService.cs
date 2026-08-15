using AuraNova.Domain.Enums;

namespace AuraNova.Application.Orders.Interfaces
{
    public interface IOrderStatusTransitionService
    {
        bool IsTransitionAllowed(OrderStatus current, OrderStatus target, DeliveryType deliveryType);
    }
}
