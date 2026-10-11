using FluentValidation;
using NZWalks.API.Models.DTO;

namespace NZWalks.API.Validators
{
    public class RegisterRequestDtoValidator : AbstractValidator<RegisterRequestDto>
    {
        public RegisterRequestDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress();

            //Complexity (length, digit, symbol) is owned by IdentityOptions.Password; only cap the size here
            RuleFor(x => x.Password)
                .NotEmpty()
                .MaximumLength(AuthRules.MaxPasswordLength);
        }
    }
}
