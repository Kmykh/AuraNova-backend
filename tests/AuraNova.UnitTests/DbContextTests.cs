using System;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AuraNova.Infrastructure.Persistence;

namespace AuraNova.UnitTests
{
    public class DbContextTests
    {
        [Fact]
        public void CanConstructDbContext_WithInMemoryProvider()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb")
                .Options;

            using var ctx = new AppDbContext(options);
            Assert.NotNull(ctx);
        }
    }
}
