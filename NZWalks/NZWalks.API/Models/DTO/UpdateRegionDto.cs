
namespace NZWalks.API.Models.DTO
{
    public class UpdateRegionDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? RegionImageUrl { get; set; }
    }
}
