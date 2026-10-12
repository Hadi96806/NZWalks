using FluentValidation;
using NZWalks.API.Models.DTO;

namespace NZWalks.API.Validators
{
    public class UpdateRegionDtoValidator : AbstractValidator<UpdateRegionDto>
    {
        public UpdateRegionDtoValidator()
        {
            RuleFor(x => x.Code)
                .NotEmpty()
                .Length(3, 5).WithMessage("Code must be between 3 and 5 characters.")
                .Matches("^[A-Z]+$").WithMessage("Code must contain only uppercase letters (e.g. AKL).");

            RuleFor(x => x.Name)
                .NotEmpty()
                .Length(3, 25).WithMessage("Name must be between 3 and 25 characters.");

            RuleFor(x => x.RegionImageUrl).MustBeOptionalHttpUrl();
        }
    }
}
