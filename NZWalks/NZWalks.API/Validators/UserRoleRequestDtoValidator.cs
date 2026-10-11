using FluentValidation;
using NZWalks.API.Constants;
using NZWalks.API.Models.DTO;

namespace NZWalks.API.Validators
{
    public class UserRoleRequestDtoValidator : AbstractValidator<UserRoleRequestDto>
    {
        public UserRoleRequestDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress();

            RuleFor(x => x.Role)
                .NotEmpty()
                .Must(role => RoleNames.All.Contains(role, StringComparer.OrdinalIgnoreCase))
                .WithMessage(x => $"'{x.Role}' is not a valid role. Valid roles: {string.Join(", ", RoleNames.All)}.");
        }
    }
}
