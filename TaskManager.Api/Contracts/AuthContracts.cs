using System.ComponentModel.DataAnnotations;

namespace TaskManager.Api.Contracts;

public class RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = "";

    [Required, StringLength(128, MinimumLength = 8)]
    public string Password { get; init; } = "";
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = "";

    [Required]
    public string Password { get; init; } = "";
}

public record AuthResponse(string Token, string Email);
