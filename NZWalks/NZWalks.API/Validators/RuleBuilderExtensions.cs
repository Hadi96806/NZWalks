using FluentValidation;

namespace NZWalks.API.Validators
{
    public static class RuleBuilderExtensions
    {
        public const int MaxUrlLength = 2048;

        //Optional image links: null/empty passes, anything else must be an absolute http(s) URL
        public static IRuleBuilderOptions<T, string?> MustBeOptionalHttpUrl<T>(this IRuleBuilder<T, string?> rule) =>
            rule.MaximumLength(MaxUrlLength)
                .Must(url => string.IsNullOrEmpty(url) ||
                    (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                     (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
                .WithMessage("{PropertyName} must be an absolute http or https URL.");
    }
}
