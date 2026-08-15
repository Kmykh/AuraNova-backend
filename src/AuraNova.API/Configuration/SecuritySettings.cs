namespace AuraNova.API.Configuration
{
    public class SecuritySettings
    {
        public string[] AllowedOrigins { get; set; } = [];
        public int UploadMaxSizeMb { get; set; } = 5;
    }
}
