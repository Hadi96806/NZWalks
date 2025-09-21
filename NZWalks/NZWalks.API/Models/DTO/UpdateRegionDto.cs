using System.ComponentModel.DataAnnotations;

namespace NZWalks.API.Models.DTO
{
    public class UpdateRegionDto
    {
        [Required]
        [MaxLength(5, ErrorMessage = "Code has to be a maximunm of 5 characters")]
        [MinLength(3, ErrorMessage = "Code has to be a minimum of 3 characters")]
        public string Code { get; set; }
        [Required]
        [MaxLength(25, ErrorMessage = "Name must not exceeds 25 characters")]
        [MinLength(3, ErrorMessage = "Name must atleast be 3 characters")]
        public string Name { get; set; }
        public string? RegionImageUrl { get; set; }
    }
}
