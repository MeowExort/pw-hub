namespace Pw.Modules.Api.Application.Auth.Dtos;

public sealed class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class UpdateUsernameRequest
{
    public string Username { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class AuthResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool Developer { get; set; }
    public string Token { get; set; } = string.Empty;
}

public sealed class UserDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool Developer { get; set; }

    // Telegram profile link info
    public long? TelegramId { get; set; }
    public string? TelegramUsername { get; set; }
    public DateTimeOffset? TelegramLinkedAt { get; set; }
}
