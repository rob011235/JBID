// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Server.Services;

using System.Data;
using System.Globalization;
using System.Security.Claims;
using JBIDS.Server.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

/// <summary>Reserves a conservative monthly allowance before making provider requests.</summary>
/// <param name="factory">Creates database contexts.</param>
/// <param name="authentication">Supplies the current account.</param>
/// <param name="configuration">Supplies the installation's credit allowance.</param>
public sealed class TavilyUsageService(
    IDbContextFactory<ApplicationDbContext> factory,
    AuthenticationStateProvider authentication,
    IConfiguration configuration)
{
    private const string Provider = "JBIDS.Tavily.Usage";

    /// <summary>Gets the maximum monthly reserved credits per account.</summary>
    public int Limit => Math.Clamp(configuration.GetValue("Tavily:MonthlyCreditLimit", 900), 0, 1000);

    /// <summary>Reads the account's current monthly reservation total.</summary>
    /// <returns>The reserved credits, including requests whose billing is uncertain.</returns>
    public async Task<int> ReservedAsync()
    {
        var user = await this.UserIdAsync();
        await using var db = await factory.CreateDbContextAsync();
        var month = Month();
        var row = await db.UserTokens.AsNoTracking().SingleOrDefaultAsync(t => t.UserId == user && t.LoginProvider == Provider && t.Name == month);
        return Parse(row?.Value);
    }

    /// <summary>Reserves credits durably before a request; failures are not automatically refunded.</summary>
    /// <param name="credits">The conservative maximum credit estimate.</param>
    /// <returns>The asynchronous reservation.</returns>
    public async Task ReserveAsync(int credits)
    {
        if (credits < 1 || credits > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(credits));
        }

        var user = await this.UserIdAsync();
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var month = Month();
        var row = await db.UserTokens.SingleOrDefaultAsync(t => t.UserId == user && t.LoginProvider == Provider && t.Name == month);
        var used = Parse(row?.Value);
        if (used + credits > this.Limit)
        {
            throw new InvalidOperationException("Your JBIDS monthly Tavily allowance is exhausted. No additional provider request was made.");
        }

        if (row is null)
        {
            row = new IdentityUserToken<string> { UserId = user, LoginProvider = Provider, Name = month };
            db.UserTokens.Add(row);
        }

        row.Value = (used + credits).ToString(CultureInfo.InvariantCulture);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static string Month() => DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static int Parse(string? value)
    {
        if (value is null)
        {
            return 0;
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count >= 0
            ? count : throw new InvalidOperationException("The saved usage counter is invalid. Contact your administrator.");
    }

    private async Task<string> UserIdAsync()
    {
        var principal = (await authentication.GetAuthenticationStateAsync()).User;
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(id))
        {
            throw new UnauthorizedAccessException("Sign in to search for announcements.");
        }

        return id;
    }
}
