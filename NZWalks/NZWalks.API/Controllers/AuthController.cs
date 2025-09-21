using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
            var identityResult = await userManager.CreateAsync(identityUser, registerRequestDto.Password);

            if(identityResult.Succeeded)
            {
                //Add roles to this user
                if(registerRequestDto.Roles != null && registerRequestDto.Roles.Any()) 
                {
                    await userManager.AddToRoleAsync(identityUser, registerRequestDto.Roles[0]);

                    if(identityResult.Succeeded)
                    {
                        return Ok("User was registered Succecfully.");
                    }
                }
            }
            return BadRequest("Something went wrong!");
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
