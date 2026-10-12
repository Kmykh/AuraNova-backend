using AuraNova.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace AuraNova.UnitTests
{
    internal static class TestUrlResolver
    {
        public const string PublicBase = "https://test.supabase.co/storage/v1/object/public/payment-evidence";

        public static readonly StorageUrlResolver Instance = new(Options.Create(new SupabaseSettings
        {
            Url = "https://test.supabase.co",
            ServiceRoleKey = "fake-key"
        }));
    }
}
