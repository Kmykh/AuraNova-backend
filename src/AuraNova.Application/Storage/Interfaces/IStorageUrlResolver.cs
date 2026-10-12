namespace AuraNova.Application.Storage.Interfaces
{
    /// <summary>
    /// Traduce entre rutas relativas del bucket (lo que se guarda en BD) y URLs públicas (lo que consume el cliente).
    /// Guardar rutas relativas permite cambiar de proveedor/CDN cambiando solo la configuración.
    /// </summary>
    public interface IStorageUrlResolver
    {
        /// <summary>Ruta relativa → URL pública. Las URLs absolutas y data: se devuelven sin cambios.</summary>
        string? ToPublicUrl(string? pathOrUrl);

        /// <summary>URL pública propia → ruta relativa. Cualquier otro valor se devuelve sin cambios.</summary>
        string? ToStoragePath(string? pathOrUrl);
    }
}
