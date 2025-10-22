using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Data;
using Pw.Modules.Api.Domain;

namespace Pw.Modules.Api.Features.Auth;

public static class AuthUtils
{
    public static async Task<User?> GetUserByTokenAsync(ModulesDbContext db, string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var now = DateTimeOffset.UtcNow;
        var session = await db.Sessions.Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Token == token && s.ExpiresAt > now);
        return session?.User;
    }

    public static (string hash, string salt) HashPassword(string password)
    {
        var salt = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var hash = HashWithSalt(password, salt);
        return (hash, salt);
    }

    public static string HashWithSalt(string password, string salt)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(password + ":" + salt);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public static string NewToken() => Guid.NewGuid().ToString("N");
}