namespace AuraNova.Application.Storage.Interfaces
{
    public interface IFileStorageService
    {
        Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, string folder);
        Task DeleteAsync(string path);
    }
}
