namespace AuraNova.Application.WhatsApp.Interfaces
{
    public interface IWhatsAppMessageService
    {
        string NormalizePhone(string phone);
        Task<string> GenerateUrlAsync(string phone, string message);
    }
}
