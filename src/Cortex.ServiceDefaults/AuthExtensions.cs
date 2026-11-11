using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cortex.ServiceDefaults;

public static class Roles
{
    public const string User = "user";
    public const string Admin = "admin";
}

public static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);
}

public static class AuthExtensions
{
    public static IHostApplicationBuilder AddCortexAuth(
        this IHostApplicationBuilder builder,
        string keycloakServiceName = "keycloak",
        string realm = "cortex",
        string audience = "cortex-api")
    {
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddKeycloakJwtBearer(keycloakServiceName, realm, o =>
            {
                o.Audience = audience;
                o.MapInboundClaims = false;
                o.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

                o.TokenValidationParameters.NameClaimType = "preferred_username";
                o.TokenValidationParameters.RoleClaimType = "roles";
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin));

        return builder;
    }
}