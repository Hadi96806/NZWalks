using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NZWalks.API.Models.DTO
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public class UserRoleRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        //Checked against RoleNames.All in the controller so the error can list the valid values
        [Required]
        public string Role { get; set; }
    }
}
