using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace NZWalks.API.Respositries
{
    public class TokenRepository : ITokenRepository
    {
        private readonly IConfiguration config;

        public TokenRepository (IConfiguration config)
        {
            this.config = config;
        }

        public string CreateToken(IdentityUser user, List<string> roles)
        {
            //One snapshot of the clock, shared by iat and exp so they cannot disagree
            var now = DateTime.UtcNow;

            //Identity marks Email nullable; fail with a clear message instead of a NullReferenceException
            var email = user.Email
                ?? throw new InvalidOperationException($"User '{user.Id}' has no email, so a token cannot be issued.");

            //Create claims
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                new Claim(ClaimTypes.Email, email)
            };

            foreach (var role in roles)
            {
                claims.Add( new Claim(ClaimTypes.Role, role));
            }

            var jwtKey = config["Jwt:Key"]
                ?? throw new InvalidOperationException("Missing configuration value 'Jwt:Key'.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            //Program.cs rejects a missing or non-positive value at startup, so it is safe to read here
            var expiryMinutes = config.GetValue<int>("Jwt:ExpiryMinutes");

            var token = new JwtSecurityToken(
                config["Jwt:Issuer"],
                config["Jwt:Audience"],
                claims,
                expires: now.AddMinutes(expiryMinutes),
                signingCredentials: credentials
                );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
