using AuraNova.Application.Quotes.DTOs;

namespace AuraNova.Application.Quotes.Interfaces
{
    public interface IQuoteService
    {
        Task<IReadOnlyList<QuoteResponse>> GetAllAsync();
        Task<QuoteResponse?> GetByIdAsync(Guid id);
        Task<QuoteResponse> UpdateAsync(Guid id, UpdateQuoteRequest request);
    }
}
