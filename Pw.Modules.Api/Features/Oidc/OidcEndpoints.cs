using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Pw.Modules.Api.Features.Oidc;

public static class OidcEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/connect/authorize", AuthorizeAsync);
        app.MapPost("/connect/authorize", AuthorizeAsync);
        app.MapGet("/connect/logout", LogoutAsync);
        app.MapPost("/connect/logout", LogoutAsync);
        app.MapPost("/connect/token", ExchangeAsync);
    }

    private static async Task<IResult> LogoutAsync(HttpRequest request)
    {
        await request.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return Results.SignOut(
            authenticationSchemes: new[] { OpenIddictServerAspNetCoreDefaults.AuthenticationScheme },
            properties: new AuthenticationProperties
            {
                RedirectUri = "/"
            });
    }

    private static async Task<IResult> AuthorizeAsync(HttpRequest request)
    {
        var context = request.HttpContext;
        var oidcRequest = context.GetOpenIddictServerRequest();
        if (oidcRequest == null) return Results.BadRequest();

        // Retrieve the user principal stored in the cookie.
        var result = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        
        if (!result.Succeeded)
        {
            return Results.Challenge(authenticationSchemes: new[] { CookieAuthenticationDefaults.AuthenticationScheme });
        }

        // Create a new ClaimsPrincipal containing the claims that
        // will be used to create an id_token, a token or a code.
        var identity = new ClaimsIdentity(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: OpenIddictConstants.Claims.Name,
            roleType: OpenIddictConstants.Claims.Role);

        // Add the claims that will be persisted in the tokens.
        // Note: result.Principal.Claims contains the Cookie claims.
        // We must copy them to the new identity.
        foreach (var claim in result.Principal.Claims)
        {
            var newClaim = new Claim(claim.Type, claim.Value, claim.ValueType, claim.Issuer);
            newClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);
            identity.AddClaim(newClaim);
        }

        var principal = new ClaimsPrincipal(identity);

        // Scopes
        principal.SetScopes(oidcRequest.GetScopes());

        // Sign in with OpenIddict
        return Results.SignIn(principal, properties: null, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> ExchangeAsync(HttpRequest request)
    {
        var context = request.HttpContext;
        var oidcRequest = context.GetOpenIddictServerRequest();
        if (oidcRequest == null) return Results.BadRequest();

        if (oidcRequest.IsClientCredentialsGrantType())
        {
            // Client credentials flow: create a principal for the application itself.
            var identity = new ClaimsIdentity(
                authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                nameType: OpenIddictConstants.Claims.Name,
                roleType: OpenIddictConstants.Claims.Role);

            // Add the client_id as the subject claim.
            var clientIdClaim = new Claim(OpenIddictConstants.Claims.Subject, oidcRequest.ClientId ?? string.Empty);
            clientIdClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
            identity.AddClaim(clientIdClaim);

            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(oidcRequest.GetScopes());

            return Results.SignIn(principal, properties: null, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (oidcRequest.IsAuthorizationCodeGrantType() || oidcRequest.IsRefreshTokenGrantType())
        {
             // Retrieve the claims principal stored in the authorization code/refresh token.
             var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
             var user = result.Principal;
             if (user == null) return Results.Forbid(authenticationSchemes: new[] { OpenIddictServerAspNetCoreDefaults.AuthenticationScheme });

             // Create a new ClaimsPrincipal for the access token.
             // Reuse the existing one but reset the authentication type.
             var identity = new ClaimsIdentity(
                 user.Claims,
                 authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                 nameType: OpenIddictConstants.Claims.Name,
                 roleType: OpenIddictConstants.Claims.Role);
             
             foreach (var claim in identity.Claims)
             {
                  claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);
             }

             var principal = new ClaimsPrincipal(identity);
             
             // Ensure scopes are preserved or updated
             principal.SetScopes(oidcRequest.GetScopes());

             return Results.SignIn(principal, properties: null, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return Results.BadRequest(new { error = "unsupported_grant_type" });
    }
}
