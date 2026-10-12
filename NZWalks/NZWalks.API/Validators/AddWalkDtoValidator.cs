using FluentValidation;
using NZWalks.API.Models.DTO;

namespace NZWalks.API.Validators
{
    public class AddWalkDtoValidator : AbstractValidator<AddWalkDto>
    {
        public AddWalkDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .Length(3, 100).WithMessage("Name must be between 3 and 100 characters.");

            RuleFor(x => x.Description)
                .NotEmpty()
                .Length(20, 1000).WithMessage("Description must be between 20 and 1000 characters.");

            RuleFor(x => x.LengthInKm)
                .InclusiveBetween(1, 100).WithMessage("LengthInKm must be between 1 and 100.");

            //An omitted Guid binds to Guid.Empty; NotEmpty rejects it. Whether the id exists is checked in the repository.
            RuleFor(x => x.DifficultyId).NotEmpty();
            RuleFor(x => x.RegionId).NotEmpty();

            RuleFor(x => x.WalkImageUrl).MustBeOptionalHttpUrl();
        }
    }
}
