using System.Threading.Tasks;
using AuraNova.Domain.Entities;

namespace AuraNova.Application.Notifications.Interfaces
{
    public interface ITrackingUrlService
    {
        Task<string> GenerateTrackingUrlAsync(Order order);
    }
}
