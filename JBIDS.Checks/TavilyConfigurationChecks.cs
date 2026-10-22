// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Checks;

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using JBIDS.Server.Data;
using JBIDS.Server.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

/// <summary>Checks account isolation, credential protection, validation, and removal.</summary>
internal static class TavilyConfigurationChecks
{
    /// <summary>Runs checks against the existing isolated test database.</summary>
    /// <param name="factory">The isolated context factory.</param>
    /// <returns>The asynchronous check run.</returns>
    public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory)
    {
        var userA = new ApplicationUser { UserName = "key-check-a" };
        var userB = new ApplicationUser { UserName = "key-check-b" };
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Users.AddRange(userA, userB);
            await db.SaveChangesAsync();
        }

        var authentication = new TestAuthentication();
        var protection = new EphemeralDataProtectionProvider();
        var settings = new TavilyConfigurationService(factory, authentication, protection);
        await MustFail<UnauthorizedAccessException>(() => settings.IsConfiguredAsync());
        await MustFail<UnauthorizedAccessException>(() => settings.GetApiKeyAsync());
        await MustFail<UnauthorizedAccessException>(() => settings.SaveAsync("synthetic-test-key"));
        await MustFail<UnauthorizedAccessException>(() => settings.RemoveAsync());
        authentication.SignIn(userA.Id);
        Assert(!await settings.IsConfiguredAsync(), "Account starts without a key.");
        await MustFail<ValidationException>(() => settings.SaveAsync(" "));
        await MustFail<ValidationException>(() => settings.SaveAsync("contains space"));
        await MustFail<ValidationException>(() => settings.SaveAsync(new string('a', 1025)));
        await settings.SaveAsync("synthetic-test-key-a");
        Assert(await settings.IsConfiguredAsync(), "Saved status is available.");
        var fresh = new TavilyConfigurationService(factory, authentication, protection);
        Assert(await fresh.GetApiKeyAsync() == "synthetic-test-key-a", "Key persists across service contexts.");
        await using (var db = await factory.CreateDbContextAsync())
        {
            var saved = await db.UserTokens.SingleAsync(t => t.UserId == userA.Id);
            Assert(saved.Value is not null && !saved.Value.Contains("synthetic-test-key"), "Stored key is encrypted.");
            await MustFail<CryptographicException>(() => Task.FromResult(protection.CreateProtector("JBIDS.Tavily.ApiKey.v1", userB.Id).Unprotect(saved.Value!)));
        }

        authentication.SignIn(userB.Id);
        Assert(!await settings.IsConfiguredAsync() && await settings.GetApiKeyAsync() is null, "Another account cannot access the first key.");
        await settings.RemoveAsync();
        await settings.SaveAsync("synthetic-test-key-b");
        authentication.SignIn(userA.Id);
        Assert(await settings.GetApiKeyAsync() == "synthetic-test-key-a", "Another account cannot replace or remove the first key.");
        await settings.SaveAsync("replacement-test-key-a");
        Assert(await settings.GetApiKeyAsync() == "replacement-test-key-a", "Replacement is saved.");
        await using (var db = await factory.CreateDbContextAsync())
        {
            Assert(await db.UserTokens.CountAsync(t => t.UserId == userA.Id) == 1, "Replacing does not duplicate the credential.");
        }

        await settings.RemoveAsync();
        Assert(!await settings.IsConfiguredAsync() && await settings.GetApiKeyAsync() is null, "Removal clears the key.");
        authentication.SignIn(userB.Id);
        Assert(await settings.GetApiKeyAsync() == "synthetic-test-key-b", "Removal preserves other accounts.");
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Users.Remove(await db.Users.SingleAsync(u => u.Id == userB.Id));
            await db.SaveChangesAsync();
            Assert(!await db.UserTokens.AnyAsync(t => t.UserId == userB.Id), "Account deletion removes its credential.");
        }

        Console.WriteLine("PASS: Tavily credential encryption, account isolation, replacement, removal, validation, and anonymous access rejection.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static async Task MustFail<TException>(Func<Task> operation)
        where TException : Exception
    {
        try
        {
            await operation();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private sealed class TestAuthentication : AuthenticationStateProvider
    {
        private ClaimsPrincipal principal = new ClaimsPrincipal(new ClaimsIdentity());

        public void SignIn(string id) => this.principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "Test"));

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(this.principal));
    }
}
