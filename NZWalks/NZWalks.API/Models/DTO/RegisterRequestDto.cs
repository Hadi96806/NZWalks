using System.Text.Json.Serialization;

namespace NZWalks.API.Models.DTO
{
    //Disallow: a client still sending the removed "roles" field gets a 400 instead of a silent Reader account
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public class RegisterRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
