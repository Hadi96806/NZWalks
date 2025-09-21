using System.ComponentModel.DataAnnotations;

namespace NZWalks.API.Models.DTO
{
    public class AddRegionDto
    {
        [Required]
        [MinLength(3, ErrorMessage = "Code has to be a minimum of 3 characters")]
        [MaxLength(5, ErrorMessage = "Code has to be a maximunm of 5 characters")]
        public string Code { get; set; }
        [Required]
        [MaxLength(25, ErrorMessage = "Name must not exceeds 25 characters")]
        public string Name { get; set; }
        public string? RegionImageUrl { get; set; }
    }
}
