using System.Threading.Tasks;
using AuraNova.Application.Orders.DTOs;

namespace AuraNova.Application.Orders.Interfaces
{
    public interface IOrderTrackingService
    {
        Task<PublicTrackingResponse?> GetTrackingAsync(string orderCode, string trackingToken);
    }
}
