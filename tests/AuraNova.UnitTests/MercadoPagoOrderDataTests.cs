using AuraNova.Domain.Entities;
using AuraNova.Domain.Enums;
using AuraNova.Infrastructure.Payments;

namespace AuraNova.UnitTests
{
    /// <summary>Datos del pedido para Mercado Pago: las líneas deben sumar exactamente el monto cobrado.</summary>
    public class MercadoPagoOrderDataTests
    {
        private static Order OrderWith(decimal deliveryCost, params (string Name, int Quantity, decimal Price)[] lines)
        {
            var items = lines.Select(l => new OrderItem
            {
                ProductId = Guid.NewGuid(),
                Product = new Product { Name = l.Name, ImageUrl = "products/foto.webp" },
                Quantity = l.Quantity,
                UnitPrice = l.Price
            }).ToList();
            var subtotal = items.Sum(i => i.Quantity * i.UnitPrice);
            return new Order
            {
                OrderCode = "AN-1001",
                DeliveryType = DeliveryType.Delivery,
                Items = items,
                Subtotal = subtotal,
                DeliveryCost = deliveryCost,
                Total = subtotal + deliveryCost
            };
        }

        private static string? Picture(string? path) => path == null ? null : $"https://media.example.com/{path}";

        [Fact]
        public void FullPayment_SendsEachProductAndDelivery()
        {
            var order = OrderWith(8m, ("Ramo de rosas", 2, 45.50m), ("Caja sorpresa", 1, 39.90m));

            var items = MercadoPagoOrderData.Items(order, 138.90m, Picture);

            Assert.Equal(3, items.Count);
            Assert.Equal(138.90m, items.Sum(i => i.Quantity * i.UnitPrice));
            Assert.Equal("Ramo de rosas", items[0].Title);
            Assert.Equal(2, items[0].Quantity);
            Assert.Equal("https://media.example.com/products/foto.webp", items[0].PictureUrl);
            Assert.Equal(MercadoPagoOrderData.ServiceCategory, items[2].CategoryId);
            Assert.All(items, i => Assert.True(i.ExternalCode.Length <= 30));
        }

        [Fact]
        public void Deposit_ProratesLines_ToTheExactAmount()
        {
            var order = OrderWith(8m, ("Ramo de rosas", 2, 45.50m), ("Caja sorpresa", 1, 39.90m));

            var items = MercadoPagoOrderData.Items(order, 69.45m, Picture);

            Assert.Equal(3, items.Count);
            Assert.Equal(69.45m, items.Sum(i => i.Quantity * i.UnitPrice));
            Assert.Equal(2, items[0].Quantity);
        }

        [Fact]
        public void WhenCentsCannotMatch_SendsOneSummaryLine()
        {
            // 3 x 33.37 = 100.11: el 50% (50.06) no se puede repartir en 3 unidades iguales
            var order = OrderWith(0m, ("Rosa eterna", 3, 33.37m));

            var items = MercadoPagoOrderData.Items(order, 50.06m, Picture);

            var line = Assert.Single(items);
            Assert.Equal(50.06m, line.UnitPrice);
            Assert.Equal(1, line.Quantity);
            Assert.Equal("AN-1001", line.ExternalCode);
            Assert.Contains("3x Rosa eterna", line.Description);
        }

        [Fact]
        public void OrderWithoutProducts_SendsSummaryLine()
        {
            var order = new Order { OrderCode = "AN-1002", IsCustomOrder = true, Subtotal = 0, Total = 120m };

            var line = Assert.Single(MercadoPagoOrderData.Items(order, 60m, Picture));

            Assert.Equal(60m, line.UnitPrice);
            Assert.Equal("Pedido AN-1002 - Aura Nova", line.Title);
        }

        [Fact]
        public void CustomOrderQuote_IncludesPersonalizationLine()
        {
            var order = new Order
            {
                OrderCode = "AN-1003", IsCustomOrder = true, CustomizationCost = 110m, CustomizationNotes = "Ramo con girasoles",
                DeliveryType = DeliveryType.Delivery, DeliveryCost = 10m, Subtotal = 0, Total = 120m
            };

            var items = MercadoPagoOrderData.Items(order, 120m, Picture);

            Assert.Equal(["Detalle personalizado a medida", "Delivery"], items.Select(i => i.Title));
            Assert.Equal(120m, items.Sum(i => i.Quantity * i.UnitPrice));
        }

        [Theory]
        [InlineData("María José López Pérez", "María", "José López Pérez")]
        [InlineData("Ana", "Ana", null)]
        [InlineData("  ", null, null)]
        public void SplitName_UsesFirstWordAsFirstName(string full, string? first, string? last)
        {
            Assert.Equal((first, last), MercadoPagoOrderData.SplitName(full));
        }

        [Theory]
        [InlineData("987654321", "987654321")]
        [InlineData("+51 987 654 321", "987654321")]
        [InlineData("12345", null)]
        public void Phone_NormalizesPeruvianNumbers(string input, string? expected)
        {
            Assert.Equal(expected, MercadoPagoOrderData.Phone(input));
        }

        [Fact]
        public void Addresses_OnlyForTheBuyerWhenItIsNotAGift()
        {
            var order = new Order
            {
                DeliveryType = DeliveryType.Delivery, DeliveryAddress = "Jr. Puno 450", District = "El Tambo", Department = "Junín",
                DeliveryReference = "Frente al parque"
            };

            Assert.Equal(new GatewayAddress("Jr. Puno 450", "El Tambo", "Junín", "Frente al parque"), MercadoPagoOrderData.PayerAddress(order));

            order.RecipientName = "Ana Pérez";
            Assert.Null(MercadoPagoOrderData.PayerAddress(order));
            Assert.NotNull(MercadoPagoOrderData.ShipmentAddress(order));

            order.DeliveryType = DeliveryType.MeetingPoint;
            Assert.Null(MercadoPagoOrderData.ShipmentAddress(order));
        }
    }
}
