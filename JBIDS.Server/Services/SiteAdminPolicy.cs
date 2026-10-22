// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Server.Services;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

/// <summary>Defines site administrator access using an installation-controlled account email.</summary>
public static class SiteAdminPolicy
{
    /// <summary>Adds the administrator policy without granting privileges to new registrants.</summary>
    /// <param name="options">The application's authorization options.</param>
    /// <param name="configuration">Server-controlled administrator configuration.</param>
    public static void Configure(AuthorizationOptions options, IConfiguration configuration)
    {
        options.AddPolicy(JobSearchWebsiteService.AdminPolicy, policy =>
            policy.RequireAuthenticatedUser().RequireAssertion(context =>
            {
                var adminEmail = configuration["SiteAdmin:Email"];
                var email = context.User.FindFirst(ClaimTypes.Email)?.Value;
                return !string.IsNullOrWhiteSpace(adminEmail)
                    && string.Equals(email, adminEmail.Trim(), StringComparison.OrdinalIgnoreCase);
            }));
    }
}
