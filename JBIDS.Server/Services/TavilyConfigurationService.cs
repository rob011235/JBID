// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Server.Services;

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using JBIDS.Server.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

/// <summary>Stores protected Tavily credentials for the authenticated account.</summary>
/// <param name="factory">Creates database contexts for individual operations.</param>
/// <param name="authentication">Supplies the server-authenticated account.</param>
/// <param name="protection">Protects credentials using the server key ring.</param>
public sealed class TavilyConfigurationService(
    IDbContextFactory<ApplicationDbContext> factory,
    AuthenticationStateProvider authentication,
    IDataProtectionProvider protection)
{
    private const string Provider = "JBIDS.Tavily";
    private const string TokenName = "ApiKey";

    /// <summary>Checks whether the current account has saved a key without retrieving it.</summary>
    /// <returns>Whether a credential is configured.</returns>
    public async Task<bool> IsConfiguredAsync()
    {
        var userId = await this.CurrentUserIdAsync();
        await using var db = await factory.CreateDbContextAsync();
        return await db.UserTokens.AnyAsync(t => t.UserId == userId && t.LoginProvider == Provider && t.Name == TokenName);
    }

    /// <summary>Saves or replaces the current account's protected credential.</summary>
    /// <param name="apiKey">The user-entered key; never logged or returned to the page.</param>
    /// <returns>The asynchronous save operation.</returns>
    public async Task SaveAsync(string apiKey)
    {
        var userId = await this.CurrentUserIdAsync();
        var key = apiKey?.Trim();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 1024 || key.Any(char.IsWhiteSpace) || key.Any(char.IsControl))
        {
            throw new ValidationException("Enter a nonempty API key, up to 1,024 characters, without spaces or control characters.");
        }

        var encrypted = protection.CreateProtector("JBIDS.Tavily.ApiKey.v1", userId).Protect(key);
        await using var db = await factory.CreateDbContextAsync();
        if (!await db.Users.AnyAsync(u => u.Id == userId))
        {
            throw new UnauthorizedAccessException("Sign in to configure Tavily.");
        }

        var saved = await db.UserTokens.SingleOrDefaultAsync(t => t.UserId == userId && t.LoginProvider == Provider && t.Name == TokenName);
        if (saved is null)
        {
            db.UserTokens.Add(new IdentityUserToken<string> { UserId = userId, LoginProvider = Provider, Name = TokenName, Value = encrypted });
        }
        else
        {
            saved.Value = encrypted;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Removes only the authenticated account's Tavily credential.</summary>
    /// <returns>The asynchronous removal operation.</returns>
    public async Task RemoveAsync()
    {
        var userId = await this.CurrentUserIdAsync();
        await using var db = await factory.CreateDbContextAsync();
        var saved = await db.UserTokens.SingleOrDefaultAsync(t => t.UserId == userId && t.LoginProvider == Provider && t.Name == TokenName);
        if (saved is not null)
        {
            db.UserTokens.Remove(saved);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Retrieves the current account's key for server-side provider calls only.</summary>
    /// <returns>The decrypted key, or null if none is configured.</returns>
    public async Task<string?> GetApiKeyAsync()
    {
        var userId = await this.CurrentUserIdAsync();
        await using var db = await factory.CreateDbContextAsync();
        var saved = await db.UserTokens.AsNoTracking().SingleOrDefaultAsync(t => t.UserId == userId && t.LoginProvider == Provider && t.Name == TokenName);
        return saved?.Value is null ? null : protection.CreateProtector("JBIDS.Tavily.ApiKey.v1", userId).Unprotect(saved.Value);
    }

    private async Task<string> CurrentUserIdAsync()
    {
        var state = await authentication.GetAuthenticationStateAsync();
        var userId = state.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (state.User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("Sign in to configure Tavily.");
        }

        return userId;
    }
}
