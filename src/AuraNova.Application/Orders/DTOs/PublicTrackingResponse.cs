using System;
using System.Collections.Generic;

namespace AuraNova.Application.Orders.DTOs
{
    public class PublicTrackingResponse
    {
        /// <summary>
        /// Id interno: lo necesita el cliente para subir el comprobante o aceptar la cotización desde el seguimiento.
        /// Solo lo recibe quien tiene el código de seguimiento.
        /// </summary>
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = null!;
        public string? CustomerFirstName { get; set; }
        public string Status { get; set; } = null!;
        public string StatusLabel { get; set; } = null!;
        public string DeliveryType { get; set; } = null!;
        
        public bool IsCustomOrder { get; set; }
        public string? ReferenceImageUrl { get; set; }
        public string? CustomizationNotes { get; set; }
        public string? Notes { get; set; }

        // Custom order brief (signed URLs for the images)
        public List<string> ReferenceImages { get; set; } = new();
        public List<string> ReferenceLinks { get; set; } = new();
        public DateOnly? DesiredDate { get; set; }
        public string? Occasion { get; set; }
        public string? BudgetRange { get; set; }

        public PublicTrackingCosts Costs { get; set; } = new();
        /// <summary>Lo cobrado y lo devuelto, con medio y fecha: lo que el cliente necesita saber de su dinero.</summary>
        public PublicTrackingPayment Payment { get; set; } = new();
        public PublicTrackingEstimates Estimates { get; set; } = new();
        public PublicTrackingNationalShipping? NationalShippingDetails { get; set; }

        public List<TrackingTimelineItem> Timeline { get; set; } = [];
        public PublicTrackingDeliveryResponse? Delivery { get; set; }
        public List<PublicTrackingItemResponse> Items { get; set; } = [];
    }

    public class PublicTrackingCosts
    {
        public decimal Subtotal { get; set; }
        public decimal? DeliveryCost { get; set; }
        public decimal CustomizationCost { get; set; }
        public decimal? Total { get; set; }
        /// <summary>Lo pagado con Mercado Pago y lo que falta (con el adelanto del 50%).</summary>
        public decimal AmountPaid { get; set; }
        public decimal BalanceDue { get; set; }
    }

    public class PublicTrackingPayment
    {
        /// <summary>
        /// unpaid (falta pagar), processing (Mercado Pago lo está confirmando), deposit (adelanto pagado),
        /// paid (pagado completo), none (cancelado sin cobro), refund_pending (cancelado con dinero por devolver),
        /// refunded (cancelado y devuelto).
        /// </summary>
        public string State { get; set; } = "unpaid";
        public decimal Total { get; set; }
        /// <summary>Todo lo cobrado (aprobado), antes de devoluciones.</summary>
        public decimal Charged { get; set; }
        public decimal Refunded { get; set; }
        /// <summary>Lo que el negocio tiene del cliente: cobrado menos devuelto.</summary>
        public decimal NetPaid { get; set; }
        public decimal BalanceDue { get; set; }
        public List<PublicTrackingMoneyMovement> Movements { get; set; } = new();
    }

    /// <summary>Un cobro o una devolución: "S/ 25.00 con Yape el 27 oct".</summary>
    public class PublicTrackingMoneyMovement
    {
        /// <summary>payment o refund.</summary>
        public string Kind { get; set; } = "payment";
        public decimal Amount { get; set; }
        /// <summary>Yape, Visa, Mastercard…</summary>
        public string Method { get; set; } = null!;
        public bool IsDeposit { get; set; }
        public DateTimeOffset? Date { get; set; }
    }

    public class PublicTrackingEstimates
    {
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? EstimatedReadyAt { get; set; }
    }

    public class PublicTrackingNationalShipping
    {
        public string? Provider { get; set; }
        public string? TrackingCode { get; set; }
        public string? ProofUrl { get; set; }
    }

    public class PublicTrackingDeliveryResponse
    {
        public string? DeliveryZoneName { get; set; }
        public string? MeetingPointName { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? Department { get; set; }
        public string? Province { get; set; }
        public string? District { get; set; }
    }

    public class PublicTrackingItemResponse
    {
        public string ProductName { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string? ImageUrl { get; set; }
        public List<string> SelectedColors { get; set; } = new List<string>();
        public List<string> SelectedFlowerTypes { get; set; } = new List<string>();
        public bool HasLights { get; set; }
        public bool HasButterfly { get; set; }
        public List<OrderItemAccessoryDto> Accessories { get; set; } = new();
        public bool HasPhraseCard { get; set; }
        public string? PhraseText { get; set; }
    }

    public class TrackingTimelineItem
    {
        public string Status { get; set; } = null!;
        /// <summary>status, payment, refund, rejection o cancelled: cómo mostrarlo.</summary>
        public string Kind { get; set; } = "status";
        public string Label { get; set; } = null!;
        public string? Description { get; set; }
        public bool Completed { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }
    }
}
