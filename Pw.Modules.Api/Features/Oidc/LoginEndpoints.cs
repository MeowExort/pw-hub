using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Data;
using Pw.Modules.Api.Features.Auth;
using Pw.Modules.Api.Domain;
using OpenIddict.Abstractions;

namespace Pw.Modules.Api.Features.Oidc;

public static class LoginEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/login", LoginGet).WithName("LoginUi");
        app.MapPost("/login", LoginPost).WithName("LoginUiPost").DisableAntiforgery();
        app.MapPost("/login/register", RegisterPost).WithName("LoginUiRegister").DisableAntiforgery();
    }

    private static async Task<IResult> LoginGet(HttpContext context, IWebHostEnvironment env, IOpenIddictApplicationManager manager)
    {
        var path = Path.Combine(env.WebRootPath, "login.html");
        if (!File.Exists(path)) return Results.Content("Login page not found", "text/plain");
        var html = await File.ReadAllTextAsync(path);

        string appName = "PW Hub";
        var returnUrl = context.Request.Query["ReturnUrl"].ToString();
        if (string.IsNullOrEmpty(returnUrl)) returnUrl = context.Request.Query["returnUrl"].ToString();

        if (!string.IsNullOrEmpty(returnUrl) && Uri.TryCreate("http://dummy" + returnUrl, UriKind.Absolute, out var uri))
        {
             var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
             if (query.TryGetValue("client_id", out var clientIdVal))
             {
                 var clientId = clientIdVal.FirstOrDefault();
                 if (!string.IsNullOrEmpty(clientId))
                 {
                     var app = await manager.FindByClientIdAsync(clientId);
                     if (app != null)
                     {
                         var name = await manager.GetDisplayNameAsync(app);
                         if (!string.IsNullOrEmpty(name)) appName = name;
                     }
                 }
             }
        }
        
        html = html.Replace("{{APP_NAME}}", System.Net.WebUtility.HtmlEncode(appName));
        return Results.Content(html, "text/html");
    }

    record LoginUiRequest(string Username, string Password, string? ReturnUrl);

    private static async Task<IResult> LoginPost(HttpContext context, ModulesDbContext db, [FromBody] LoginUiRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == req.Username.ToLower());
        bool valid = false;
        if (user != null)
        {
             var hash = AuthUtils.HashWithSalt(req.Password, user.PasswordSalt);
             if (string.Equals(hash, user.PasswordHash, StringComparison.Ordinal))
             {
                 valid = true;
             }
        }

        if (!valid)
        {
             return Results.BadRequest(new { message = "Неверное имя пользователя или пароль" });
        }

        await SignInAsync(context, user!);

        var redirectUrl = !string.IsNullOrEmpty(req.ReturnUrl) && UrlUtils.IsLocalUrl(req.ReturnUrl) ? req.ReturnUrl : "/";
        return Results.Ok(new { redirectUrl });
    }

    private static async Task<IResult> RegisterPost(HttpContext context, ModulesDbContext db, [FromBody] LoginUiRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return Results.BadRequest(new { message = "Заполните все поля" });

        if (req.Username.Length < 3 || req.Password.Length < 3)
             return Results.BadRequest(new { message = "Минимум 3 символа" });

        if (await db.Users.AnyAsync(u => u.Username.ToLower() == req.Username.ToLower()))
        {
            return Results.Conflict(new { message = "Пользователь уже существует" });
        }

        var (hash, salt) = AuthUtils.HashPassword(req.Password);
        var user = new User
        {
            Id = Guid.NewGuid().ToString("n"),
            Username = req.Username,
            PasswordHash = hash,
            PasswordSalt = salt,
            Developer = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await SignInAsync(context, user);

        var redirectUrl = !string.IsNullOrEmpty(req.ReturnUrl) && UrlUtils.IsLocalUrl(req.ReturnUrl) ? req.ReturnUrl : "/";
        return Results.Ok(new { redirectUrl });
    }

    private static async Task SignInAsync(HttpContext context, User user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("sub", user.Id.ToString())
        };
        
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }
}

public static class UrlUtils
{
    public static bool IsLocalUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        return url.StartsWith("/") && !url.StartsWith("//") && !url.StartsWith("/\\");
    }
}
