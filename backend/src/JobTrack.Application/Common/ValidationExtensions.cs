using FluentValidation;

namespace JobTrack.Application.Common;

public static class ValidationExtensions
{
    public static IRuleBuilderOptions<T, string?> HttpUrl<T>(this IRuleBuilder<T, string?> rule, int maxLength) =>
        rule
            .MaximumLength(maxLength)
            .Must(value => string.IsNullOrWhiteSpace(value) || IsHttpUrl(value.Trim()))
            .WithMessage("Debe ser una URL válida que comience por http:// o https://.");

    public static IRuleBuilderOptions<T, string?> IanaTimeZone<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .NotEmpty()
            .MaximumLength(64)
            .Must(value => value is not null && TimeZoneInfo.TryFindSystemTimeZoneById(value, out _))
            .WithMessage("Zona horaria desconocida. Usa un identificador IANA, por ejemplo \"Europe/Madrid\".");

    public static void ApplyPaging<T>(this AbstractValidator<T> validator)
        where T : PagedQuery
    {
        validator.RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        validator.RuleFor(q => q.PageSize).InclusiveBetween(1, PagedQuery.MaxPageSize);
    }

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
