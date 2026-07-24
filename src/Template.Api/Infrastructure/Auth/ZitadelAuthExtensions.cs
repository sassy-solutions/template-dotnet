// -----------------------------------------------------------------------
// <copyright file="ZitadelAuthExtensions.cs" company="Nexus">
//     Copyright (c) 2025 Sassy Solutions. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Template.Api.Infrastructure.Auth;

/// <summary>
/// Zitadel JWT-bearer authentication for a deployed Nexus app. Validates the access token's
/// issuer + audience + project-role claims. The <c>Audience</c> is the per-(project, environment)
/// Zitadel project id injected at deploy time via <c>Zitadel__Audience</c>, so a token minted for
/// the <b>dev</b> environment is rejected in <b>prod</b> (different audience).
/// <para>
/// Gated: only active when <c>Zitadel:Authority</c> is configured and the app is not running in
/// Development. Locally (no Zitadel config) the API runs open — identical to the prior behaviour —
/// so existing apps keep working until the platform injects the Zitadel config at deploy.
/// </para>
/// <para>
/// Auth is <b>opt-in per endpoint</b> (no global fallback policy) so health probes and public
/// endpoints stay anonymous; controllers add <c>[Authorize]</c> / <c>[NexusAuthorize]</c> where needed.
/// </para>
/// </summary>
public static class ZitadelAuthExtensions
{
    /// <summary>Zitadel asserts project roles under this claim (a JSON object keyed by role name).</summary>
    public const string ZitadelRolesClaim = "urn:zitadel:iam:org:project:roles";

    public static IServiceCollection AddZitadelAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var authority = configuration["Zitadel:Authority"];
        var audience = configuration["Zitadel:Audience"];

        // No Zitadel configured (local/dev) → leave the API open, same behaviour as before.
        if (string.IsNullOrWhiteSpace(authority) || environment.IsDevelopment())
        {
            return services;
        }

        // Keep raw claim types ('sub', the Zitadel roles URI) instead of remapping to WS-* URIs.
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.MapInboundClaims = false;

                if (!string.IsNullOrWhiteSpace(audience))
                {
                    options.Audience = audience;
                }

                // In-cluster JWKS / hairpin-NAT: allow overriding the OIDC metadata endpoint.
                var metadataAddress = configuration["Zitadel:MetadataAddress"];
                if (!string.IsNullOrWhiteSpace(metadataAddress))
                {
                    options.MetadataAddress = metadataAddress;
                }

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authority,
                    // Reject a token whose audience is not this environment's Zitadel project id
                    // (dev token in prod). Only enforced when an audience is configured.
                    ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "sub",
                    RoleClaimType = ZitadelRolesClaim,
                };
            });

        // Registers the authorization services so [Authorize] works; no global fallback policy
        // (endpoints opt in), keeping health probes and public routes anonymous by default.
        services.AddAuthorization();

        return services;
    }
}
