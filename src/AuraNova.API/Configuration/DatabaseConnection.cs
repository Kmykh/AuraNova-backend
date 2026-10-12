using Npgsql;

namespace AuraNova.API.Configuration
{
    /// <summary>
    /// La cadena de conexión se arma con la sección "Database" de appsettings.json (host, puerto, base y usuario,
    /// que no son secretos) más Database:Password, que es lo único que se configura como variable de entorno.
    /// Si existe ConnectionStrings:DefaultConnection, tiene prioridad (útil para apuntar a otra BD completa).
    /// </summary>
    public static class DatabaseConnection
    {
        public static string Resolve(IConfiguration configuration)
        {
            var fullConnectionString = configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(fullConnectionString))
                return fullConnectionString;

            var section = configuration.GetSection("Database");
            var password = section["Password"];
            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException(
                    "Falta la contraseña de la base de datos. Configura Database__Password como variable de entorno " +
                    "(Render) o en el archivo .env de la raíz del repo (local).");

            var host = section["Host"];
            if (string.IsNullOrWhiteSpace(host))
                throw new InvalidOperationException("Falta Database:Host en appsettings.json.");

            return new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = section.GetValue("Port", 5432),
                Database = section["Name"] ?? "postgres",
                Username = section["Username"],
                Password = password,
                // Npgsql 8: Require encrypts without validating the certificate (TrustServerCertificate is obsolete)
                SslMode = SslMode.Require
            }.ConnectionString;
        }
    }
}
