namespace Pw.Modules.Api.Features.Auth;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");
        Register.RegisterEndpoint.Map(group);
        Login.LoginEndpoint.Map(group);
        Me.MeEndpoint.Map(group);
        return group;
    }
}