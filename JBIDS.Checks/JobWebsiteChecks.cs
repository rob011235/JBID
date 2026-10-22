// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Checks;

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using JBIDS.Server.Data;
using JBIDS.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Checks seeded job websites and administrator-only management.</summary>
internal static class JobWebsiteChecks
{
    /// <summary>Exercises website persistence, validation, and the production administrator policy.</summary>
    /// <param name="factory">The isolated database factory.</param>
    /// <returns>The asynchronous checks.</returns>
    public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory)
    {
        var auth = new TestAuthentication();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["SiteAdmin:Email"] = "admin@example.com" }).Build();
        using var provider = new ServiceCollection().AddLogging().AddAuthorization(options => SiteAdminPolicy.Configure(options, config)).BuildServiceProvider();
        var service = new JobSearchWebsiteService(factory, auth, provider.GetRequiredService<IAuthorizationService>());
        var seeds = await service.EnabledAsync();
        Assert(seeds.Select(w => w.Domain).Order().SequenceEqual(new[] { "indeed.com", "linkedin.com", "glassdoor.com", "ziprecruiter.com", "dice.com", "usajobs.gov" }.Order()), "Six default websites are seeded and enabled.");
        await MustFail<UnauthorizedAccessException>(() => service.ListAsync());
        await MustFail<UnauthorizedAccessException>(() => service.SaveAsync(new JobSearchWebsite { Name = "Bad", Domain = "bad.example" }));
        await MustFail<UnauthorizedAccessException>(() => service.DeleteAsync(1));
        auth.Set("reader@example.com");
        await MustFail<UnauthorizedAccessException>(() => service.ListAsync());
        await MustFail<UnauthorizedAccessException>(() => service.SaveAsync(new JobSearchWebsite { Id = 1, Name = "Changed", Domain = "indeed.com", IsEnabled = false }));
        await MustFail<UnauthorizedAccessException>(() => service.DeleteAsync(1));
        auth.Set("admin@example.com", authenticated: false);
        await MustFail<UnauthorizedAccessException>(() => service.ListAsync());
        auth.Set("ADMIN@example.com");
        Assert((await service.ListAsync()).Count == 6, "The explicitly configured administrator can list websites.");
        await service.SaveAsync(new JobSearchWebsite { Name = "Example jobs", Domain = "https://www.Jobs.Example.com/" });
        var custom = (await service.ListAsync()).Single(w => w.Domain == "jobs.example.com");
        await MustFail<ValidationException>(() => service.SaveAsync(new JobSearchWebsite { Name = "Duplicate", Domain = "JOBS.EXAMPLE.COM" }));
        foreach (var invalid in new[] { "https://example.com/jobs", "javascript:alert(1)", "localhost", "127.0.0.1", "https://example.com:8443", "https://example.com?q=test" })
        {
            await MustFail<ValidationException>(() => service.SaveAsync(new JobSearchWebsite { Name = "Invalid", Domain = invalid }));
        }

        custom.Name = "Updated jobs";
        custom.IsEnabled = false;
        await service.SaveAsync(custom);
        Assert(!(await service.EnabledAsync()).Any(w => w.Id == custom.Id), "Disabled domains are excluded from search.");
        Assert((await service.ListAsync()).Single(w => w.Id == custom.Id).Name == "Updated jobs", "Edits persist.");
        await service.DeleteAsync(custom.Id);
        var fresh = new JobSearchWebsiteService(factory, auth, provider.GetRequiredService<IAuthorizationService>());
        Assert((await fresh.ListAsync()).Count == 6, "Deletion persists across contexts without recreating removed websites.");
        config["SiteAdmin:Email"] = string.Empty;
        await MustFail<UnauthorizedAccessException>(() => service.ListAsync());
        Console.WriteLine("PASS: six seeded job websites, administrator-only CRUD, normalized unique domains, enable/disable behavior, and no implicit administrator assignment.");
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

        public void Set(string email, bool authenticated = true) => this.principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Email, email) }, authenticated ? "Test" : null));

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(this.principal));
    }
}
