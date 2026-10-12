namespace AuraNova.Application.Storage.Interfaces
{
    public interface IReferenceImageService
    {
        /// <summary>
        /// Convierte la imagen de referencia de un pedido personalizado (data URL base64 enviada por el cliente)
        /// en un archivo optimizado del bucket y devuelve su ruta relativa. Así la BD guarda ~60 bytes en lugar de MBs.
        /// </summary>
        /// <exception cref="InvalidImageException">El valor no es una imagen válida o excede el tamaño permitido.</exception>
        Task<string?> PersistAsync(string? reference, CancellationToken cancellationToken = default);

        /// <summary>Sube un archivo de imagen enviado por el formulario (multipart) y devuelve su ruta.</summary>
        Task<string> PersistFileAsync(Stream content, string contentType, CancellationToken cancellationToken = default);
    }
}
