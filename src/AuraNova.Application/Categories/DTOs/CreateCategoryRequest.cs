namespace AuraNova.Application.Categories.DTOs
{
    public class CreateCategoryRequest
    {
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
