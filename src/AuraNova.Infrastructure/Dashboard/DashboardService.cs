using System;
using System.Linq;
using System.Threading.Tasks;
using AuraNova.Application.Dashboard.DTOs;
using AuraNova.Application.Dashboard.Interfaces;
using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuraNova.Infrastructure.Dashboard
{
    public class DashboardService : IDashboardService
    {
        private readonly AppDbContext _db;

        public DashboardService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<DashboardSummaryResponse> GetSummaryAsync()
        {
            var response = new DashboardSummaryResponse();

            // Order Status counts
            var orderCounts = await _db.Set<Order>()
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            response.Orders.WaitingQuote = orderCounts.GetValueOrDefault(OrderStatus.WaitingQuote, 0);
            response.Orders.QuoteReady = orderCounts.GetValueOrDefault(OrderStatus.QuoteReady, 0);
            response.Orders.WaitingPayment = orderCounts.GetValueOrDefault(OrderStatus.WaitingPayment, 0);
            response.Orders.PaymentReported = orderCounts.GetValueOrDefault(OrderStatus.PaymentReported, 0);
            response.Orders.PaymentConfirmed = orderCounts.GetValueOrDefault(OrderStatus.PaymentConfirmed, 0);
            response.Orders.Preparing = orderCounts.GetValueOrDefault(OrderStatus.Preparing, 0);
            response.Orders.Ready = orderCounts.GetValueOrDefault(OrderStatus.Ready, 0);
            response.Orders.Shipped = orderCounts.GetValueOrDefault(OrderStatus.Shipped, 0);
            response.Orders.Delivered = orderCounts.GetValueOrDefault(OrderStatus.Delivered, 0);
            response.Orders.Cancelled = orderCounts.GetValueOrDefault(OrderStatus.Cancelled, 0);

            // Quote Status counts
            var quoteCounts = await _db.Set<Quote>()
                .GroupBy(q => q.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            response.Quotes.Pending = quoteCounts.GetValueOrDefault(QuoteStatus.Pending, 0);
            response.Quotes.Ready = quoteCounts.GetValueOrDefault(QuoteStatus.Ready, 0);

            // Payment Status counts
            var paymentCounts = await _db.Set<Payment>()
                .GroupBy(p => p.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            response.Payments.PendingVerification = paymentCounts.GetValueOrDefault(PaymentStatus.Pending, 0);
            response.Payments.Confirmed = paymentCounts.GetValueOrDefault(PaymentStatus.Confirmed, 0);
            response.Payments.Rejected = paymentCounts.GetValueOrDefault(PaymentStatus.Rejected, 0);

            // Today metrics using UTC
            var todayStart = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
            
            response.Today.Orders = await _db.Set<Order>()
                .Where(o => o.CreatedAt >= todayStart)
                .CountAsync();

            response.Today.ConfirmedPayments = await _db.Set<Payment>()
                .Where(p => p.Status == PaymentStatus.Confirmed && p.VerifiedAt >= todayStart)
                .CountAsync();

            response.Today.Sales = await _db.Set<Payment>()
                .Where(p => p.Status == PaymentStatus.Confirmed && p.VerifiedAt >= todayStart)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            return response;
        }
    }
}
