using Microsoft.AspNetCore.Identity;

namespace NZWalks.API.Respositries
{
    public interface ITokenRepository
    {
        string CreateToken(IdentityUser user, List<string> roles);
    }
}
