using FluentValidation;
using JobTrack.Application.Common;

namespace JobTrack.Application.Auth;

public sealed record UserDto(Guid Id, string Email, string FullName, string TimeZone, DateTimeOffset CreatedAt);

public sealed record RegisterRequest(string Email, string Password, string FullName, string? TimeZone);

public sealed record LoginRequest(string Email, string Password);

/// <summary>Returned by login/register/refresh. The refresh token is never in the body: it travels in an HttpOnly cookie.</summary>
public sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, UserDto User);

public sealed record UpdateProfileRequest(string FullName, string TimeZone);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty()
            .MinimumLength(MinLength)
            .MaximumLength(MaxLength)
            .Matches("[a-z]").WithMessage("La contraseña debe incluir al menos una minúscula.")
            .Matches("[A-Z]").WithMessage("La contraseña debe incluir al menos una mayúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe incluir al menos un número.");
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(r => r.Password).StrongPassword();
        RuleFor(r => r.FullName).NotEmpty().MaximumLength(150);
        RuleFor(r => r.TimeZone).IanaTimeZone().When(r => r.TimeZone is not null);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().MaximumLength(254);
        RuleFor(r => r.Password).NotEmpty().MaximumLength(PasswordPolicy.MaxLength);
    }
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(r => r.FullName).NotEmpty().MaximumLength(150);
        RuleFor(r => r.TimeZone).IanaTimeZone();
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(r => r.CurrentPassword).NotEmpty().MaximumLength(PasswordPolicy.MaxLength);
        RuleFor(r => r.NewPassword)
            .StrongPassword()
            .NotEqual(r => r.CurrentPassword)
            .WithMessage("La nueva contraseña debe ser distinta de la actual.");
    }
}
