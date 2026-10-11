using FluentValidation;
using NZWalks.API.Models.DTO;

namespace NZWalks.API.Validators
{
    public class WalkQueryDtoValidator : AbstractValidator<WalkQueryDto>
    {
        public const int MaxPageSize = 100;
        private static readonly string[] FilterFields = { "Name" };
        private static readonly string[] SortFields = { "Name", "Length" };

        public WalkQueryDtoValidator()
        {
            RuleFor(x => x.PageNumber)
                .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be 1 or greater.");

            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, MaxPageSize).WithMessage($"PageSize must be between 1 and {MaxPageSize}.");

            RuleFor(x => x.FilterOn)
                .Must(BeOneOf(FilterFields))
                .WithMessage($"FilterOn must be one of: {string.Join(", ", FilterFields)}.");

            RuleFor(x => x.FilterQuery)
                .MaximumLength(100);

            RuleFor(x => x.SortBy)
                .Must(BeOneOf(SortFields))
                .WithMessage($"SortBy must be one of: {string.Join(", ", SortFields)}.");
        }

        //Optional field: empty passes, otherwise a case-insensitive match against the allowed names
        private static Func<string?, bool> BeOneOf(string[] allowed) =>
            value => string.IsNullOrEmpty(value) || allowed.Contains(value, StringComparer.OrdinalIgnoreCase);
    }
}
