using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NZWalks.API.Constants;
using NZWalks.API.Models.DTO;
using NZWalks.API.Respositries;

namespace NZWalks.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<IdentityUser> userManager;
        private readonly ITokenRepository tokenRepository;

        public AuthController(UserManager<IdentityUser> userManager, ITokenRepository tokenRepository)
        {
            this.userManager = userManager;
            this.tokenRepository = tokenRepository;
        }


        //Post: /api/Auth/Register
        [HttpPost]
        [Route("Register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto registerRequestDto)
        {
            var identityUser = new IdentityUser {
                UserName = registerRequestDto.Email,
                Email = registerRequestDto.Email
            };
            var createResult = await userManager.CreateAsync(identityUser, registerRequestDto.Password);

            if (!createResult.Succeeded)
            {
                //Identity already wrote a readable message per broken rule (weak password, duplicate email...)
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(error.Code, error.Description);
                }
                return ValidationProblem(ModelState);
            }

            //Self-registration always gets the lowest role; higher roles are granted by an Admin (UsersController)
            IdentityResult roleResult;
            try
            {
                roleResult = await userManager.AddToRoleAsync(identityUser, RoleNames.Reader);
            }
            catch
            {
                //Identity throws when the role row does not exist; do not leave a user nobody can log in as
                await userManager.DeleteAsync(identityUser);
                throw;
            }

            if (!roleResult.Succeeded)
            {
                await userManager.DeleteAsync(identityUser);
                throw new InvalidOperationException(
                    $"Could not assign role '{RoleNames.Reader}': " +
                    string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            }

            return Ok("User was registered successfully.");
        }

        //Post; /api/Auth/Login
        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> Login([FromBody] LogInRequestDto logInRequestDto)
        {
            var user = await userManager.FindByEmailAsync(logInRequestDto.Email);

            if(user != null)
            {
                var checkPasswordResult = await userManager.CheckPasswordAsync(user, logInRequestDto.Password);
                if (checkPasswordResult)
                {
                    var roles = await userManager.GetRolesAsync(user);
                    if (roles != null && roles.Any())
                    {
                        //CreateToken
                        var jwtToken = tokenRepository.CreateToken(user, roles.ToList());
                        var response = new LoginResponseDto 
                        { 
                            JwtToken = jwtToken
                        };

                        return Ok(response);
                    }
                }


            }

            return BadRequest("Username or password incorrect!");
        }
    }
}
