using System.Text.Json.Serialization;

namespace NZWalks.API.Models.DTO
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public class UserRoleRequestDto
    {
        public string Email { get; set; } = string.Empty;

        //Checked against RoleNames.All by UserRoleRequestDtoValidator, which lists the valid values
        public string Role { get; set; } = string.Empty;
    }
}
