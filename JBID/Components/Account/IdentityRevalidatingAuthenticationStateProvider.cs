// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBID.Server.Components.Account
{
    using System.Security.Claims;
    using JBID.Server.Data;
    using Microsoft.AspNetCore.Components.Authorization;
    using Microsoft.AspNetCore.Components.Server;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.Extensions.Options;

    // This is a server-side AuthenticationStateProvider that revalidates the security stamp for the connected user
    // every 30 minutes an interactive circuit is connected.

    /// <summary>
    /// Revalidates the connected user security stamp every thirty minutes.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create authentication loggers.</param>
    /// <param name="scopeFactory">The factory for fresh identity service scopes.</param>
    /// <param name="options">The configured identity claim options.</param>
    internal sealed class IdentityRevalidatingAuthenticationStateProvider(
            ILoggerFactory loggerFactory,
            IServiceScopeFactory scopeFactory,
            IOptions<IdentityOptions> options)
        : RevalidatingServerAuthenticationStateProvider(loggerFactory)
    {
        /// <inheritdoc/>
        protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

        /// <inheritdoc/>
        protected override async Task<bool> ValidateAuthenticationStateAsync(
            AuthenticationState authenticationState, CancellationToken cancellationToken)
        {
            // Get the user manager from a new scope to ensure it fetches fresh data
            await using var scope = scopeFactory.CreateAsyncScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            return await this.ValidateSecurityStampAsync(userManager, authenticationState.User);
        }

        /// <summary>
        /// Compares the principal security stamp with the current database value.
        /// </summary>
        /// <param name="userManager">The manager used to load the user and security stamp.</param>
        /// <param name="principal">The principal associated with the connected circuit.</param>
        /// <returns>A task indicating whether the user still has a valid security stamp.</returns>
        private async Task<bool> ValidateSecurityStampAsync(UserManager<ApplicationUser> userManager, ClaimsPrincipal principal)
        {
            var user = await userManager.GetUserAsync(principal);
            if (user is null)
            {
                return false;
            }
            else if (!userManager.SupportsUserSecurityStamp)
            {
                return true;
            }
            else
            {
                var principalStamp = principal.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
                var userStamp = await userManager.GetSecurityStampAsync(user);
                return principalStamp == userStamp;
            }
        }
    }
}
