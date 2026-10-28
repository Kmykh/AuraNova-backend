using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using AuraNova.Domain.Entities;

namespace AuraNova.Infrastructure.Persistence.Configurations
{
    public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.ToTable("Payments");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Amount).HasPrecision(18, 2).IsRequired();
            builder.Property(p => p.AmountPaid).HasPrecision(18, 2).IsRequired().HasDefaultValue(0m);
            builder.Property(p => p.Method).IsRequired();
            builder.Property(p => p.Status).IsRequired();

            builder.Property(p => p.EvidenceUrl).HasMaxLength(2000);
            builder.Property(p => p.Notes).HasMaxLength(2000);

            builder.Property(p => p.CreatedAt).IsRequired();
            builder.Property(p => p.UpdatedAt);
            builder.Property(p => p.VerifiedAt);

            // One-to-One relationship with Order
            builder.HasOne(p => p.Order)
                .WithOne(o => o.Payment)
                .HasForeignKey<Payment>(p => p.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(p => p.Transactions)
                .WithOne(t => t.Payment)
                .HasForeignKey(t => t.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
    {
        public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
        {
            builder.ToTable("PaymentTransactions");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Provider).IsRequired().HasMaxLength(30);
            builder.Property(t => t.ExternalOrderId).HasMaxLength(100);
            builder.Property(t => t.ExternalPaymentId).HasMaxLength(100);
            builder.Property(t => t.MethodId).IsRequired().HasMaxLength(40);
            builder.Property(t => t.MethodType).HasMaxLength(40);
            builder.Property(t => t.Amount).HasPrecision(18, 2).IsRequired();
            builder.Property(t => t.Status).IsRequired().HasMaxLength(40);
            builder.Property(t => t.StatusDetail).HasMaxLength(100);
            builder.Property(t => t.IdempotencyKey).HasMaxLength(64);
            builder.Property(t => t.RefundedAmount).HasPrecision(18, 2).HasDefaultValue(0m);
            builder.Property(t => t.CreatedAt).IsRequired();

            // Una order de Mercado Pago corresponde a un solo cobro nuestro (el webhook la busca por aquí)
            builder.HasIndex(t => t.ExternalOrderId).IsUnique().HasFilter("\"ExternalOrderId\" IS NOT NULL");
            builder.HasIndex(t => t.OrderId);
            // La conciliación busca cobros sin confirmar
            builder.HasIndex(t => new { t.Status, t.CreatedAt });
        }
    }

    public class PaymentWebhookEventConfiguration : IEntityTypeConfiguration<PaymentWebhookEvent>
    {
        public void Configure(EntityTypeBuilder<PaymentWebhookEvent> builder)
        {
            builder.ToTable("PaymentWebhookEvents");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Topic).HasMaxLength(40);
            builder.Property(e => e.ResourceId).HasMaxLength(100);
            builder.Property(e => e.Result).IsRequired().HasMaxLength(30);
            builder.Property(e => e.Detail).HasMaxLength(250);
            builder.HasIndex(e => e.ReceivedAt);
        }
    }
}
