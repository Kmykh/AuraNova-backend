using System.Threading.Tasks;
using AuraNova.Application.Dashboard.DTOs;

namespace AuraNova.Application.Dashboard.Interfaces
{
    public interface IDashboardService
    {
        Task<DashboardSummaryResponse> GetSummaryAsync();
    }
}
