using System.Threading.Tasks;
using AuraNova.Application.BusinessSettings.DTOs;
using AuraNova.Application.BusinessSettings.Interfaces;
using AuraNova.Infrastructure.WhatsApp;
using Moq;
using Xunit;

namespace AuraNova.UnitTests
{
    public class WhatsAppMessageServiceTests
    {
        [Theory]
        [InlineData("999999999", "51999999999")] // Peruvian mobile without code
        [InlineData("51999999999", "51999999999")] // Already has code
        [InlineData("+51 999 999 999", "51999999999")] // Has symbols and spaces
        [InlineData("999-999-999", "51999999999")] // Has dashes
        [InlineData("", "")]
        [InlineData(null, "")]
        public void NormalizePhone_ShouldFormatCorrectly(string? input, string expected)
        {
            var mockSettings = new Mock<IBusinessSettingsService>();
            var service = new WhatsAppMessageService(mockSettings.Object);

            var result = service.NormalizePhone(input!);

            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task GenerateUrlAsync_ShouldCreateValidWaMeUrl()
        {
            var mockSettings = new Mock<IBusinessSettingsService>();
            mockSettings.Setup(s => s.GetPublicAsync()).ReturnsAsync(new BusinessSettingsResponse { WhatsAppNumber = "51999999999" });
            var service = new WhatsAppMessageService(mockSettings.Object);
            var message = "Hola, esto es una prueba.";

            var result = await service.GenerateUrlAsync(message);

            Assert.StartsWith("https://wa.me/51999999999", result);
            Assert.Contains("?text=", result);
        }

        [Fact]
        public async Task GenerateUrlAsync_ShouldEncodeMessageInUrl()
        {
            var mockSettings = new Mock<IBusinessSettingsService>();
            mockSettings.Setup(s => s.GetPublicAsync()).ReturnsAsync(new BusinessSettingsResponse { WhatsAppNumber = "51999999999" });
            var service = new WhatsAppMessageService(mockSettings.Object);
            var message = "Mensaje con espacios y símbolos: %&";

            var result = await service.GenerateUrlAsync(message);

            var textPart = result.Split("?text=")[1];
            Assert.DoesNotContain(" ", textPart);
            Assert.Contains("%20", textPart); // Space encoded
        }
    }
}
