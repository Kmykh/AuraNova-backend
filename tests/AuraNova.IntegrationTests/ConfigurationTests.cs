using System;
using System.Collections.Generic;
using System.IO;
using AuraNova.API.Configuration;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace AuraNova.IntegrationTests
{
    public class ConfigurationTests
    {
        private static IConfiguration Config(Dictionary<string, string?> values) =>
            new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        private static readonly Dictionary<string, string?> DatabaseSection = new()
        {
            ["Database:Host"] = "aws-0-us-west-2.pooler.supabase.com",
            ["Database:Port"] = "5432",
            ["Database:Name"] = "postgres",
            ["Database:Username"] = "postgres.project"
        };

        [Fact]
        public void DatabaseConnection_BuildsFromSectionAndPassword()
        {
            var values = new Dictionary<string, string?>(DatabaseSection) { ["Database:Password"] = "p@ss;word" };

            var builder = new NpgsqlConnectionStringBuilder(DatabaseConnection.Resolve(Config(values)));

            Assert.Equal("aws-0-us-west-2.pooler.supabase.com", builder.Host);
            Assert.Equal(5432, builder.Port);
            Assert.Equal("postgres", builder.Database);
            Assert.Equal("postgres.project", builder.Username);
            Assert.Equal("p@ss;word", builder.Password); // special characters are escaped correctly
            Assert.Equal(SslMode.Require, builder.SslMode);
        }

        [Fact]
        public void DatabaseConnection_FullConnectionStringTakesPriority()
        {
            var values = new Dictionary<string, string?>(DatabaseSection)
            {
                ["Database:Password"] = "ignored",
                ["ConnectionStrings:DefaultConnection"] = "Host=other;Database=x;Username=y;Password=z"
            };

            Assert.Equal("Host=other;Database=x;Username=y;Password=z", DatabaseConnection.Resolve(Config(values)));
        }

        [Fact]
        public void DatabaseConnection_MissingPassword_ExplainsWhatToConfigure()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => DatabaseConnection.Resolve(Config(new(DatabaseSection))));

            Assert.Contains("Database__Password", ex.Message);
        }

        [Fact]
        public void DotEnv_LoadsValuesWithoutOverridingRealVariables()
        {
            var directory = Directory.CreateTempSubdirectory("auranova-dotenv-");
            var previous = Directory.GetCurrentDirectory();
            var suffix = Guid.NewGuid().ToString("N");
            var loadedKey = $"AURANOVA_TEST_LOADED_{suffix}";
            var existingKey = $"AURANOVA_TEST_EXISTING_{suffix}";
            var emptyKey = $"AURANOVA_TEST_EMPTY_{suffix}";

            try
            {
                File.WriteAllText(Path.Combine(directory.FullName, ".env"),
                    $"# comentario\n{loadedKey}=\"valor con espacios\"\n{existingKey}=desde-env\n{emptyKey}=\n");
                Environment.SetEnvironmentVariable(existingKey, "real");
                Directory.SetCurrentDirectory(directory.FullName);

                Assert.NotNull(DotEnv.Load());

                Assert.Equal("valor con espacios", Environment.GetEnvironmentVariable(loadedKey));
                Assert.Equal("real", Environment.GetEnvironmentVariable(existingKey));
                Assert.Null(Environment.GetEnvironmentVariable(emptyKey));
            }
            finally
            {
                Directory.SetCurrentDirectory(previous);
                Environment.SetEnvironmentVariable(loadedKey, null);
                Environment.SetEnvironmentVariable(existingKey, null);
                directory.Delete(recursive: true);
            }
        }
    }
}
