using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AuraNova.Application.Payments.DTOs;

namespace AuraNova.Application.Payments.Interfaces
{
    public interface IPaymentService
    {
        PaymentInfoResponse GetPaymentInfo();
        Task<PaymentResponse> ReportEvidenceAsync(Guid orderId, Stream fileStream, string fileName, string contentType);
        Task<IReadOnlyList<AdminPaymentResponse>> GetAdminPaymentsAsync();
        Task<AdminPaymentResponse?> GetAdminPaymentByIdAsync(Guid paymentId);
        Task<bool> ConfirmAsync(Guid paymentId);
        Task<bool> RejectAsync(Guid paymentId, RejectPaymentRequest request);
    }
}
