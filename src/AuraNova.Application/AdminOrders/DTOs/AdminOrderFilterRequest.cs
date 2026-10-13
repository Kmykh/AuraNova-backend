using System;
using AuraNova.Domain.Enums;

namespace AuraNova.Application.AdminOrders.DTOs
{
    public class AdminOrderFilterRequest
    {
        public OrderStatus? Status { get; set; }
        public DeliveryType? DeliveryType { get; set; }
        public DateTimeOffset? DateFrom { get; set; }
        public DateTimeOffset? DateTo { get; set; }
        public string? Search { get; set; }
        /// <summary>
        /// Grupo de estados: "action" (te toca a ti), "customer" (espera al cliente), "workshop" (en taller),
        /// "delivery" (por entregar), "done" (cerrados). Se ignora si viene <see cref="Status"/>.
        /// </summary>
        public string? View { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
