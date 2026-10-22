// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Server.Services;

using System.ComponentModel.DataAnnotations;
using JBIDS.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

/// <summary>Manages the site-wide list of permitted job search domains.</summary>
/// <param name="factory">Creates database contexts.</param>
/// <param name="authentication">Supplies the current account.</param>
/// <param name="authorization">Enforces administrator access on management operations.</param>
public sealed class JobSearchWebsiteService(
    IDbContextFactory<ApplicationDbContext> factory,
    AuthenticationStateProvider authentication,
    IAuthorizationService authorization)
{
    /// <summary>Gets the authorization policy used for site administration.</summary>
    public const string AdminPolicy = "SiteAdmin";

    /// <summary>Lists the enabled domains for a search.</summary>
    /// <returns>The enabled website records.</returns>
    public async Task<List<JobSearchWebsite>> EnabledAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.JobSearchWebsites.AsNoTracking().Where(w => w.IsEnabled).OrderBy(w => w.Name).ToListAsync();
    }

    /// <summary>Lists all websites for an authorized administrator.</summary>
    /// <returns>The website records.</returns>
    public async Task<List<JobSearchWebsite>> ListAsync()
    {
        await this.RequireAdminAsync();
        await using var db = await factory.CreateDbContextAsync();
        return await db.JobSearchWebsites.AsNoTracking().OrderBy(w => w.Name).ToListAsync();
    }

    /// <summary>Adds or updates a website after normalizing and validating its domain.</summary>
    /// <param name="input">The administrator's website settings.</param>
    /// <returns>The asynchronous save.</returns>
    public async Task SaveAsync(JobSearchWebsite input)
    {
        await this.RequireAdminAsync();
        Validator.ValidateObject(input, new ValidationContext(input), validateAllProperties: true);
        var domain = NormalizeDomain(input.Domain);
        await using var db = await factory.CreateDbContextAsync();
        if (await db.JobSearchWebsites.AnyAsync(w => w.Domain == domain && w.Id != input.Id))
        {
            throw new ValidationException("That domain is already in the website list.");
        }

        if (input.Id == 0)
        {
            db.JobSearchWebsites.Add(new JobSearchWebsite { Name = input.Name.Trim(), Domain = domain, IsEnabled = input.IsEnabled });
        }
        else
        {
            var saved = await db.JobSearchWebsites.SingleOrDefaultAsync(w => w.Id == input.Id)
                ?? throw new ValidationException("Website no longer exists. Reload the list.");
            saved.Name = input.Name.Trim();
            saved.Domain = domain;
            saved.IsEnabled = input.IsEnabled;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Deletes a website without changing previously collected announcements.</summary>
    /// <param name="id">The website identifier.</param>
    /// <returns>The asynchronous deletion.</returns>
    public async Task DeleteAsync(int id)
    {
        await this.RequireAdminAsync();
        await using var db = await factory.CreateDbContextAsync();
        var saved = await db.JobSearchWebsites.SingleOrDefaultAsync(w => w.Id == id);
        if (saved is not null)
        {
            db.JobSearchWebsites.Remove(saved);
            await db.SaveChangesAsync();
        }
    }

    private static string NormalizeDomain(string domain)
    {
        var value = domain.Trim();
        if (!value.Contains("://", StringComparison.Ordinal))
        {
            value = "https://" + value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https")
            || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0
            || uri.HostNameType != UriHostNameType.Dns || !uri.Host.Contains('.') || uri.IsLoopback || uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Enter a public domain such as indeed.com, without a page path, query, or port.");
        }

        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        if (host.Length > 253)
        {
            throw new ValidationException("The normalized domain is too long.");
        }

        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }

    private async Task RequireAdminAsync()
    {
        var principal = (await authentication.GetAuthenticationStateAsync()).User;
        if (!(await authorization.AuthorizeAsync(principal, AdminPolicy)).Succeeded)
        {
            throw new UnauthorizedAccessException("Site administrator access is required.");
        }
    }
}
