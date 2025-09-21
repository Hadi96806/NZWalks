using System.ComponentModel.DataAnnotations;

namespace NZWalks.API.Models.DTO
{
    public class AddWalkDto
    {
        [Required]
        [MinLength(3, ErrorMessage = "Code has to be a minimum of 3 characters")]
        [MaxLength(100, ErrorMessage = "Code has to be a maximunm of 100 characters")]
        public string Name { get; set; }
        [Required]
        [MinLength(20, ErrorMessage = "Code has to be a minimum of 20 characters")]
        [MaxLength(1000, ErrorMessage = "Code has to be a maximunm of 1000 characters")]
        public string Description { get; set; }
        [Required]
        [Range(1, 100)]
        public double LengthInKm { get; set; }
        public string? WalkImageUrl { get; set; }
        [Required]
        public Guid DifficultyId { get; set; }
        [Required]
        public Guid RegionId { get; set; }
    }
}
