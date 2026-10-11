
namespace NZWalks.API.Models.DTO
{
    public class ImageUploadDto
    {
        public IFormFile File { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
    }
}
