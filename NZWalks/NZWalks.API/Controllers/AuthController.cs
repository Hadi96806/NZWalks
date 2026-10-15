using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NZWalks.API.Constants;
using NZWalks.API.Models.DTO;
using NZWalks.API.Respositries;

namespace NZWalks.API.Controllers
{
    //Every action here takes credentials, so the strict limit sits on the class: a new action can't be added without it
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<IdentityUser> userManager;
        private readonly SignInManager<IdentityUser> signInManager;
        private readonly ITokenRepository tokenRepository;
        private readonly ILogger<AuthController> logger;

        public AuthController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager,
            ITokenRepository tokenRepository, ILogger<AuthController> logger)
        {
            this.logger = logger;
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.tokenRepository = tokenRepository;
        }


        //Post: /api/Auth/Register
        [HttpPost]
        [Route("Register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto registerRequestDto)
        {
            var identityUser = new IdentityUser
            {
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

            if (user != null)
            {
                //One call: refuses a locked-out account, counts a wrong password towards lockout,
                //and resets the count on success. Issues no cookie - the JWT below is the only credential.
                var signInResult = await signInManager.CheckPasswordSignInAsync(user, logInRequestDto.Password, lockoutOnFailure: true);
                //Security event: the caller still gets the generic 401 below, but the log records the lockout
                if (signInResult.IsLockedOut)
                {
                    logger.LogWarning("Login refused for locked-out user {UserId} from {ClientIp}",
                        user.Id, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                }

                if (signInResult.Succeeded)
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

            //One generic 401 for unknown email, wrong password, locked-out account and no roles,
            //so callers can't probe which one failed (or that guessing is working)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Username or password incorrect.");
        }
    }
}
