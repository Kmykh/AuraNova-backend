using System;
using System.Threading.Tasks;
using AuraNova.Application.Orders.DTOs;

namespace AuraNova.Application.Orders.Interfaces
{
    public interface IOrderTrackingService
    {
        Task<PublicTrackingResponse?> GetTrackingAsync(string orderCode, string trackingToken);

        /// <summary>Seguimiento con un solo código ("K7M3-Q9XA"); acepta minúsculas, espacios o el enlace completo.</summary>
        Task<PublicTrackingResponse?> GetTrackingByCodeAsync(string trackingCode);

        /// <summary>Montos, estado y productos para la página de pago (/pago/{id}).</summary>
        Task<OrderPaymentSummaryResponse?> GetPaymentSummaryAsync(Guid orderId);
    }
}
