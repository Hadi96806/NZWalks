using FluentValidation;
using NZWalks.API.Models.DTO;

namespace NZWalks.API.Validators
{
    public class LogInRequestDtoValidator : AbstractValidator<LogInRequestDto>
    {
        public LogInRequestDtoValidator()
        {
            //No format or complexity rules: login errors stay generic so they don't reveal the password policy
            RuleFor(x => x.Email).NotEmpty();

            RuleFor(x => x.Password)
                .NotEmpty()
                .MaximumLength(AuthRules.MaxPasswordLength);
        }
    }
}
