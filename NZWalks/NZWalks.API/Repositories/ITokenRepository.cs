using Microsoft.AspNetCore.Identity;

namespace NZWalks.API.Repositories
{
    public interface ITokenRepository
    {
        string CreateToken(IdentityUser user, List<string> roles);
    }
}
