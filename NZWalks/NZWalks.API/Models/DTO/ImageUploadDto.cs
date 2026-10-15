
namespace NZWalks.API.Models.DTO
{
    public class ImageUploadDto
    {
        //Bound from the form; ImageUploadDtoValidator rejects a missing file before the action runs
        public IFormFile File { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
