using System.Collections.Generic;

namespace AuraNova.Application.Dashboard.DTOs
{
    public class DashboardSummaryResponse
    {
        public OrderSummary Orders { get; set; } = new();
        public QuoteSummary Quotes { get; set; } = new();
        public PaymentSummary Payments { get; set; } = new();
        public TodaySummary Today { get; set; } = new();
    }

    public class OrderSummary
    {
        public int WaitingQuote { get; set; }
        public int QuoteReady { get; set; }
        public int WaitingPayment { get; set; }
        public int PaymentReported { get; set; }
        public int PaymentConfirmed { get; set; }
        public int Preparing { get; set; }
        public int Ready { get; set; }
        public int Shipped { get; set; }
        public int Delivered { get; set; }
        public int Cancelled { get; set; }
    }

    public class QuoteSummary
    {
        public int Pending { get; set; }
        public int Ready { get; set; }
    }

    public class PaymentSummary
    {
        public int PendingVerification { get; set; }
        public int Confirmed { get; set; }
        public int Rejected { get; set; }
    }

    public class TodaySummary
    {
        public int Orders { get; set; }
        public int ConfirmedPayments { get; set; }
        public decimal Sales { get; set; }
    }
}
