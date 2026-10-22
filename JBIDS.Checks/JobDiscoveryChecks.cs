// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Checks;

using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using JBIDS.Server.Data;
using JBIDS.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Exercises discovery against deterministic provider responses without using live credits.</summary>
internal static class JobDiscoveryChecks
{
    /// <summary>Checks search contracts, import persistence, duplicate handling, and credit reservations.</summary>
    /// <param name="factory">Creates isolated test contexts.</param>
    /// <returns>The asynchronous checks.</returns>
    public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory)
    {
        var user = new ApplicationUser { UserName = "discovery-check" };
        var secondUser = new ApplicationUser { UserName = "discovery-check-other" };
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Users.AddRange(user, secondUser);
            db.JobSearchWebsites.Add(new JobSearchWebsite { Name = "Test jobs", Domain = "jobs.example.com", IsEnabled = true });
            await db.SaveChangesAsync();
        }

        var authentication = new TestAuthentication();
        authentication.SignIn(user.Id);
        var protection = new EphemeralDataProtectionProvider();
        var keys = new TavilyConfigurationService(factory, authentication, protection);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Tavily:MonthlyCreditLimit"] = "4" }).Build();
        var usage = new TavilyUsageService(factory, authentication, configuration);
        var curriculum = new CurriculumService(factory);
        var project = new CurriculumProject { Name = "Discovery checks", Description = "Firmware roles" };
        await curriculum.SaveProjectAsync(project);
        await curriculum.SaveAnnouncementAsync(new JobAnnouncement { ProjectId = project.Id, Title = "Existing", Employer = "Original", Url = "https://jobs.example.com/existing", Text = "Existing evidence" });
        using var handler = new ProviderHandler();
        using var http = new HttpClient(handler);
        using var services = new ServiceCollection().AddLogging().AddAuthorization(options => SiteAdminPolicy.Configure(options, configuration)).BuildServiceProvider();
        var websites = new JobSearchWebsiteService(factory, authentication, services.GetRequiredService<IAuthorizationService>());
        var discovery = new JobDiscoveryService(http, keys, usage, curriculum, websites, authentication);
        await MustFail<InvalidOperationException>(() => discovery.SearchAsync(project.Id, "Firmware", new[] { "firmware jobs" }));
        Assert(handler.Calls == 0, "Missing credentials do not contact provider.");
        await keys.SaveAsync("synthetic-discovery-key");
        await MustFail<ValidationException>(() => discovery.SearchAsync(project.Id, "Firmware", Enumerable.Repeat("query", 6).ToArray()));
        Assert(handler.Calls == 0, "Invalid request counts do not contact provider.");
        await discovery.SearchAsync(project.Id, "Entry-level firmware", new[] { "firmware jobs", "embedded careers" });
        Assert(handler.Calls == 2 && discovery.Candidates.Count == 4, "Search deduplicates tracking URLs, rejects unsafe URLs, and preserves unique results.");
        Assert(discovery.Candidates.Single(c => c.Url.EndsWith("/existing", StringComparison.Ordinal)).Imported, "Existing sources are marked.");
        var a = discovery.Candidates.Single(c => c.Url.EndsWith("/a", StringComparison.Ordinal));
        var b = discovery.Candidates.Single(c => c.Url.EndsWith("/b", StringComparison.Ordinal));
        var c = discovery.Candidates.Single(c => c.Url.EndsWith("/c", StringComparison.Ordinal));
        await discovery.ExtractAsync(new[] { a.Id, b.Id, c.Id });
        Assert(handler.ExtractedUrls.SequenceEqual(new[] { a.Url, b.Url, c.Url }), "Only selected URLs are sent for extraction.");
        Assert(a.Text == "Implement firmware and test sensors." && b.Text.Length == 0, "Full content is preserved and failed extractions do not use snippets.");
        Assert(!b.Message!.Contains("sensitive-provider-body", StringComparison.Ordinal), "Provider error bodies are not exposed.");
        await MustFail<ValidationException>(() => discovery.ImportAsync(a.Id));
        a.Employer = "Reviewed employer";
        Assert(await discovery.ImportAsync(a.Id), "Reviewed source imports.");
        var saved = (await curriculum.AnnouncementsAsync(project.Id)).Single(x => x.Url == a.Url);
        Assert(saved.DiscoveryQuery == "firmware jobs" && saved.DiscoveryDescription == "Entry-level firmware", "Query and occupation persist with the source.");
        await curriculum.SaveAnnouncementAsync(saved);
        Assert((await curriculum.AnnouncementsAsync(project.Id)).Single(x => x.Id == saved.Id).DiscoveryQuery == saved.DiscoveryQuery, "Editing preserves provenance.");
        b.Employer = "Manual employer";
        b.Text = "Manually preserved job announcement.";
        Assert(await discovery.ImportAsync(b.Id), "Manual fallback imports without extraction.");
        c.Employer = "Reposted employer";
        c.Text = a.Text;
        Assert(!await discovery.ImportAsync(c.Id), "Identical source text is skipped without overwriting original evidence.");
        Assert(handler.Calls == 3, "Import and fallback never call the provider.");
        Assert(await new TavilyUsageService(factory, authentication, configuration).ReservedAsync() == 3, "Reservations persist across service instances.");
        handler.Mode = "unauthorized";
        await discovery.SearchAsync(project.Id, "Firmware", new[] { "test key" });
        Assert(handler.Calls == 4 && discovery.Messages.Single().Contains("API key", StringComparison.Ordinal), "Invalid keys have a safe actionable error and no retries.");
        await discovery.SearchAsync(project.Id, "Firmware", new[] { "over budget" });
        Assert(handler.Calls == 4 && discovery.Messages.Single().Contains("allowance", StringComparison.Ordinal), "Exhausted allowance prevents provider calls.");
        authentication.SignIn(secondUser.Id);
        await MustFail<UnauthorizedAccessException>(() => discovery.ImportAsync(a.Id));
        Assert(await usage.ReservedAsync() == 0, "Monthly counters are isolated by account.");
        await keys.SaveAsync("synthetic-discovery-key");
        configuration["Tavily:MonthlyCreditLimit"] = "100";
        handler.Mode = "malformed";
        await discovery.SearchAsync(project.Id, "Firmware", new[] { "bad response" });
        Assert(discovery.Messages.Count == 1 && !discovery.Messages[0].Contains("sensitive-provider-body", StringComparison.Ordinal), "Malformed responses are sanitized.");
        handler.Mode = "many";
        await discovery.SearchAsync(project.Id, "Firmware", new[] { "one", "two", "three", "four", "five" });
        Assert(discovery.Candidates.Count == 50, "At most fifty candidates are available in a run.");
        await discovery.ExtractAsync(discovery.Candidates.Select(x => x.Id).ToArray());
        Assert(discovery.ExtractionAttempts == 50, "Extraction attempts are bounded per run.");
        var calls = handler.Calls;
        await MustFail<ValidationException>(() => discovery.ExtractAsync(new[] { discovery.Candidates[0].Id }));
        Assert(handler.Calls == calls, "Retrying beyond fifty attempts does not contact provider.");
        Assert(await usage.ReservedAsync() == 16, "Malformed request, five searches, and ten extraction batches are reserved conservatively.");
        Assert(JobDiscoveryService.NormalizeUrl("http://127.0.0.1/secrets") is null && JobDiscoveryService.NormalizeUrl("javascript:alert(1)") is null, "Nonpublic and executable URLs are rejected.");
        handler.Mode = "normal";
        var automaticProject = new CurriculumProject { Name = "Automatic collection", Description = "Save first, prune later" };
        await curriculum.SaveProjectAsync(automaticProject);
        Assert(await discovery.FindAndSaveAsync(automaticProject.Id, "firmware developer") == 4, "One action saves every unique result without employer entry or selection.");
        var automatic = await curriculum.AnnouncementsAsync(automaticProject.Id);
        Assert(automatic.Count == 4 && automatic.All(x => x.Employer == "Not identified"), "Missing employers do not block collection or become invented names.");
        var full = automatic.Single(x => x.Url.EndsWith("/a", StringComparison.Ordinal));
        var preview = automatic.Single(x => x.Url.EndsWith("/b", StringComparison.Ordinal));
        Assert(full.Text == "Implement firmware and test sensors." && preview.Text.StartsWith("[Search preview", StringComparison.Ordinal), "Full text enriches saved records and failed pages retain marked previews.");
        Assert(await discovery.FindAndSaveAsync(automaticProject.Id, "firmware developer") == 0, "Repeating a query skips existing source URLs.");
        var objective = new TrainingObjective { ProjectId = automaticProject.Id, Text = "Implement firmware.", Level = BloomLevel.Apply };
        await curriculum.SaveObjectiveAsync(objective);
        await curriculum.SaveEvidenceAsync(automaticProject.Id, new ObjectiveEvidence { ObjectiveId = objective.Id, AnnouncementId = full.Id, Excerpt = "Implement firmware" });
        await curriculum.SaveEvidenceAsync(automaticProject.Id, new ObjectiveEvidence { ObjectiveId = objective.Id, AnnouncementId = preview.Id, Excerpt = "Snippet" });
        await curriculum.ReviewAsync(automaticProject.Id, objective.Id, ReviewStatus.Approved);
        await MustFail<ValidationException>(() => curriculum.DeleteAnnouncementsAsync(project.Id, new[] { full.Id }));
        Assert((await curriculum.AnnouncementsAsync(automaticProject.Id)).Count == 4, "Cross-project deletion is rejected without changing sources.");
        Assert(await curriculum.DeleteAnnouncementsAsync(automaticProject.Id, new[] { full.Id }) == 1, "Individual pruning deletes the source.");
        Assert((await curriculum.ObjectivesAsync(automaticProject.Id)).Single().Status == ReviewStatus.Draft, "Deleting supporting evidence resets approval while retaining the objective.");
        Assert((await curriculum.EvidenceAsync(automaticProject.Id)).Single().AnnouncementId == preview.Id, "Only deleted-source evidence links are removed.");
        var remainder = (await curriculum.AnnouncementsAsync(automaticProject.Id)).Select(x => x.Id).ToArray();
        Assert(await curriculum.DeleteAnnouncementsAsync(automaticProject.Id, remainder) == 3, "Bulk pruning deletes selected announcements and linked evidence.");
        Assert((await curriculum.AnnouncementsAsync(automaticProject.Id)).Count == 0 && (await curriculum.EvidenceAsync(automaticProject.Id)).Count == 0, "Bulk deletion persists without orphan evidence.");
        await using (var db = await factory.CreateDbContextAsync())
        {
            var allSites = await db.JobSearchWebsites.ToListAsync();
            allSites.ForEach(w => w.IsEnabled = false);
            await db.SaveChangesAsync();
        }

        var callsBeforeEmptyList = handler.Calls;
        var creditsBeforeEmptyList = await usage.ReservedAsync();
        await MustFail<InvalidOperationException>(() => discovery.SearchAsync(project.Id, "Firmware", new[] { "firmware jobs" }));
        Assert(handler.Calls == callsBeforeEmptyList && await usage.ReservedAsync() == creditsBeforeEmptyList, "Empty enabled-domain list prevents broad search and credit spending.");
        Console.WriteLine("PASS: automatic collection, preview fallback, source pruning, approval invalidation, provenance, duplicates, budgets, request caps, and sanitized failures.");
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

    private sealed class ProviderHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public string Mode { get; set; } = "normal";

        public List<string> ExtractedUrls { get; } = new List<string>();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Calls++;
            Assert(request.Method == HttpMethod.Post && request.RequestUri!.Host == "api.tavily.com", "Requests use only the fixed Tavily host.");
            Assert(request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == "synthetic-discovery-key", "Authentication is server-side and per-request.");
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var body = json.RootElement;
            if (this.Mode == "unauthorized")
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("sensitive-provider-body") };
            }

            if (this.Mode == "malformed")
            {
                return Reply("sensitive-provider-body: not JSON");
            }

            if (request.RequestUri!.AbsolutePath == "/search")
            {
                Assert(body.GetProperty("include_domains").EnumerateArray().Any(d => d.GetString() == "jobs.example.com") && body.GetProperty("include_domains_mode").GetString() == "restrict", "Search requests restrict results to enabled domains.");
                Assert(body.GetProperty("search_depth").GetString() == "basic" && !body.GetProperty("auto_parameters").GetBoolean() && body.GetProperty("max_results").GetInt32() == 20, "Basic search stays bounded.");
                if (this.Mode == "many")
                {
                    return Reply(JsonSerializer.Serialize(new { results = Enumerable.Range(0, 10).Select(i => new { url = $"https://jobs.example.com/{this.Calls}/{i}", title = "Developer", content = "Search snippet" }) }));
                }

                return Reply("""
                    {"results":[
                    {"url":"https://jobs.example.com/existing","title":"Existing","content":"Existing snippet"},
                    {"url":"https://jobs.example.com/a?utm_source=test","title":"Firmware developer","content":"Search snippet only"},
                    {"url":"https://jobs.example.com/a#top","title":"Duplicate","content":"Snippet"},
                    {"url":"https://jobs.example.com/b","title":"Embedded developer","content":"Snippet"},
                    {"url":"https://jobs.example.com/c","title":"Firmware repost","content":"Snippet"},
                    {"url":"javascript:alert(1)","title":"Unsafe","content":"Snippet"},
                    {"url":"https://medium.com/article","title":"Unrelated article","content":"Snippet"},
                    {"url":"https://jobs.example.com.evil.example/ad","title":"Lookalike domain","content":"Snippet"}]}
                    """);
            }

            Assert(request.RequestUri!.AbsolutePath == "/extract" && body.GetProperty("extract_depth").GetString() == "basic" && body.GetProperty("format").GetString() == "text", "Extraction requests full plain text at basic depth.");
            var urls = body.GetProperty("urls").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
            Assert(urls.Length <= 5, "Extraction batches are bounded at five URLs.");
            this.ExtractedUrls.AddRange(urls);
            return this.Mode == "many" ? Reply("{\"results\":[],\"failed_results\":[]}") : Reply("""
                {"results":[{"url":"https://jobs.example.com/a","raw_content":"Implement firmware and test sensors."}],
                "failed_results":[{"url":"https://jobs.example.com/b","error":"sensitive-provider-body"}]}
                """);
        }

        private static HttpResponseMessage Reply(string json) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
