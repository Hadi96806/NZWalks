using FluentValidation;
using NZWalks.API.Models.DTO;

namespace NZWalks.API.Validators
{
    public class ImageUploadDtoValidator : AbstractValidator<ImageUploadDto>
    {
        public const long MaxFileSizeInBytes = 10 * 1024 * 1024;
        private static readonly string[] AllowedExtensions = { ".jpg", ".png", ".gif", ".svg", ".webp" };

        public ImageUploadDtoValidator()
        {
            RuleFor(x => x.File)
                .NotNull().WithMessage("File is required.")
                .DependentRules(() =>
                {
                    RuleFor(x => x.File.FileName)
                        .Must(name => AllowedExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
                        .OverridePropertyName("File")
                        .WithMessage($"Unsupported file extension. Allowed: {string.Join(", ", AllowedExtensions)}.");

                    RuleFor(x => x.File.Length)
                        .LessThanOrEqualTo(MaxFileSizeInBytes)
                        .OverridePropertyName("File")
                        .WithMessage("File size exceeds 10MB.");
                });

            RuleFor(x => x.Name)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.Description)
                .MaximumLength(500);
        }
    }
}
