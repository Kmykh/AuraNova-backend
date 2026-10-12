namespace AuraNova.API.Configuration
{
    /// <summary>
    /// Carga el archivo .env de la raíz del repo como variables de entorno, solo para desarrollo local
    /// (en Render el archivo no existe: está en .gitignore y .dockerignore). Las variables de entorno reales
    /// tienen prioridad y los valores vacíos se ignoran, para que no pisen user-secrets.
    /// </summary>
    public static class DotEnv
    {
        public static string? Load(string fileName = ".env", int maxLevelsUp = 6)
        {
            var path = Find(fileName, maxLevelsUp);
            if (path == null)
                return null;

            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                if (line.StartsWith("export "))
                    line = line["export ".Length..].TrimStart();

                var separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;

                var key = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim();
                if (value.Length >= 2 && (value[0] == '"' && value[^1] == '"' || value[0] == '\'' && value[^1] == '\''))
                    value = value[1..^1];

                if (value.Length == 0 || Environment.GetEnvironmentVariable(key) != null)
                    continue;

                Environment.SetEnvironmentVariable(key, value);
            }

            return path;
        }

        private static string? Find(string fileName, int maxLevelsUp)
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (var level = 0; directory != null && level <= maxLevelsUp; level++, directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, fileName);
                if (File.Exists(candidate))
                    return candidate;

                // No subir más allá de la raíz del repo
                if (File.Exists(Path.Combine(directory.FullName, "AuraNova.sln")))
                    return null;
            }

            return null;
        }
    }
}
