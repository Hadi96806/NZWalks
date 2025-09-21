using System.ComponentModel.DataAnnotations;

namespace NZWalks.API.Models.DTO
{
    public class LogInRequestDto
    {
        [Required]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}
