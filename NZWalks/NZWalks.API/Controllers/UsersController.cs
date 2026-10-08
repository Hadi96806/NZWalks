using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NZWalks.API.Constants;
using NZWalks.API.Models.DTO;
using System.Security.Claims;

namespace NZWalks.API.Controllers
{
    //Class-level guard: every action here is Admin-only, so a new action can't be added unprotected
    [Authorize(Roles = RoleNames.Admin)]
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<IdentityUser> userManager;
        private readonly ILogger<UsersController> logger;

        public UsersController(UserManager<IdentityUser> userManager, ILogger<UsersController> logger)
        {
            this.userManager = userManager;
            this.logger = logger;
        }

        //Post: /api/Users/GrantRole
        [HttpPost]
        [Route("GrantRole")]
        public async Task<IActionResult> GrantRole([FromBody] UserRoleRequestDto request)
        {
            var role = ResolveRole(request.Role);
            if (role is null)
            {
                return InvalidRole(request.Role);
            }

            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is null)
            {
                return UserNotFound(request.Email);
            }

            //Granting a role the user already holds is a no-op, so repeating the call is safe
            if (await userManager.IsInRoleAsync(user, role))
            {
                return NoContent();
            }

            var result = await userManager.AddToRoleAsync(user, role);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not grant role '{role}' to user '{user.Id}': " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            //Audit trail: who raised whose privileges
            logger.LogWarning("Role {Role} granted to user {UserId} by {Admin}",
                role, user.Id, User.FindFirstValue(ClaimTypes.Email));

            return NoContent();
        }

        //Post: /api/Users/RevokeRole
        [HttpPost]
        [Route("RevokeRole")]
        public async Task<IActionResult> RevokeRole([FromBody] UserRoleRequestDto request)
        {
            var role = ResolveRole(request.Role);
            if (role is null)
            {
                return InvalidRole(request.Role);
            }

            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is null)
            {
                return UserNotFound(request.Email);
            }

            //Revoking a role the user does not hold is a no-op, so repeating the call is safe
            if (!await userManager.IsInRoleAsync(user, role))
            {
                return NoContent();
            }

            //Login rejects a user with no roles, so removing the last one would lock the account out
            var currentRoles = await userManager.GetRolesAsync(user);
            if (currentRoles.Count == 1)
            {
                return Problem(statusCode: StatusCodes.Status409Conflict,
                    title: "Cannot remove the last role",
                    detail: $"'{request.Email}' only has the '{role}' role. A user with no roles cannot log in.");
            }

            //Never leave the system without an Admin: nobody could grant roles again except through SQL
            if (role == RoleNames.Admin)
            {
                var admins = await userManager.GetUsersInRoleAsync(RoleNames.Admin);
                if (admins.Count <= 1)
                {
                    return Problem(statusCode: StatusCodes.Status409Conflict,
                        title: "Cannot remove the last Admin",
                        detail: "At least one Admin must remain. Grant Admin to another user first.");
                }
            }

            var result = await userManager.RemoveFromRoleAsync(user, role);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not revoke role '{role}' from user '{user.Id}': " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            logger.LogWarning("Role {Role} revoked from user {UserId} by {Admin}",
                role, user.Id, User.FindFirstValue(ClaimTypes.Email));

            return NoContent();
        }

        //Match case-insensitively but return the canonical spelling ("writer" -> "Writer"); null if unknown
        private static string? ResolveRole(string requested) =>
            RoleNames.All.FirstOrDefault(r => string.Equals(r, requested, StringComparison.OrdinalIgnoreCase));

        private ActionResult InvalidRole(string requested)
        {
            ModelState.AddModelError(nameof(UserRoleRequestDto.Role),
                $"'{requested}' is not a valid role. Valid roles: {string.Join(", ", RoleNames.All)}.");
            return ValidationProblem(ModelState);
        }

        private ActionResult UserNotFound(string email) =>
            Problem(statusCode: StatusCodes.Status404NotFound,
                title: "User not found", detail: $"No user is registered with email '{email}'.");
    }
}
