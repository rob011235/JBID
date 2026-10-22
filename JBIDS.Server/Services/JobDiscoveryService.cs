// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Server.Services;

using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using JBIDS.Server.Data;
using Microsoft.AspNetCore.Components.Authorization;

/// <summary>Finds, extracts, and imports announcements through Tavily.</summary>
/// <param name="http">The fixed-host Tavily HTTP client.</param>
/// <param name="keys">Retrieves only the current user's key.</param>
/// <param name="usage">Reserves persistent monthly credits.</param>
/// <param name="curriculum">Persists collected announcement content.</param>
/// <param name="websites">Supplies the enabled search domains.</param>
/// <param name="authentication">Identifies the owner of this transient search.</param>
public sealed class JobDiscoveryService(
    HttpClient http,
    TavilyConfigurationService keys,
    TavilyUsageService usage,
    CurriculumService curriculum,
    JobSearchWebsiteService websites,
    AuthenticationStateProvider authentication)
{
    private readonly List<JobDiscoveryCandidate> candidates = new List<JobDiscoveryCandidate>();
    private readonly List<string> messages = new List<string>();
    private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
    private string? owner;
    private int projectId;
    private string description = string.Empty;
    private int extractionAttempts;

    /// <summary>Gets candidates in the current transient search.</summary>
    public IReadOnlyList<JobDiscoveryCandidate> Candidates => this.candidates;

    /// <summary>Gets nonfatal warnings from individual search requests.</summary>
    public IReadOnlyList<string> Messages => this.messages;

    /// <summary>Gets the number of URLs submitted for extraction this run.</summary>
    public int ExtractionAttempts => this.extractionAttempts;

    /// <summary>Normalizes source URLs for duplicate matching.</summary>
    /// <param name="url">A public HTTP or HTTPS URL.</param>
    /// <returns>A normalized URL or null for unsupported destinations.</returns>
    public static string? NormalizeUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http")
            || uri.IsLoopback || !string.IsNullOrEmpty(uri.UserInfo) || uri.HostNameType != UriHostNameType.Dns
            || !uri.Host.Contains('.') || uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) || url!.Length > 2048)
        {
            return null;
        }

        var builder = new UriBuilder(uri) { Fragment = string.Empty };
        builder.Query = string.Join("&", uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) && !p.StartsWith("gclid=", StringComparison.OrdinalIgnoreCase) && !p.StartsWith("fbclid=", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.Ordinal));
        builder.Path = builder.Path.TrimEnd('/');
        return builder.Uri.AbsoluteUri;
    }

    /// <summary>Runs one to five explicit basic queries and keeps at most fifty unique candidates.</summary>
    /// <param name="id">The target curriculum project.</param>
    /// <param name="jobDescription">The occupation and optional search filters.</param>
    /// <param name="queries">User-reviewed query strings, each at most 400 characters.</param>
    /// <returns>The asynchronous search run.</returns>
    public async Task SearchAsync(int id, string jobDescription, IReadOnlyList<string> queries)
    {
        await this.EnterAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(jobDescription) || jobDescription.Length > 500 || queries.Count is < 1 or > 5 || queries.Any(q => string.IsNullOrWhiteSpace(q) || q.Length > 400))
            {
                throw new ValidationException("Describe the job and enter one to five queries, each up to 400 characters.");
            }

            if (await curriculum.ProjectAsync(id) is null)
            {
                throw new ValidationException("Project not found.");
            }

            var domains = (await websites.EnabledAsync()).Select(w => w.Domain).ToArray();
            if (domains.Length == 0)
            {
                throw new InvalidOperationException("No job search websites are enabled. Ask the site administrator to enable a website.");
            }

            var key = await this.KeyAsync();
            this.owner = await this.UserIdAsync();
            this.projectId = id;
            this.description = jobDescription;
            this.extractionAttempts = 0;
            this.candidates.Clear();
            this.messages.Clear();
            var existing = (await curriculum.AnnouncementsAsync(id)).Select(a => NormalizeUrl(a.Url)).ToHashSet();
            foreach (var query in queries.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    await usage.ReserveAsync(1);
                    using var json = await this.PostAsync("search", key, new
                    {
                        query,
                        search_depth = "basic",
                        auto_parameters = false,
                        max_results = 20,
                        include_answer = false,
                        include_raw_content = false,
                        include_images = false,
                        include_usage = true,
                        include_domains = domains,
                        include_domains_mode = "restrict",
                    });
                    if (!json.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                    {
                        throw new InvalidOperationException("Tavily returned an unexpected search response. Please try again later.");
                    }

                    foreach (var result in results.EnumerateArray().Take(20))
                    {
                        var url = NormalizeUrl(Value(result, "url"));
                        if (url is null || !domains.Any(d => new Uri(url).Host.Equals(d, StringComparison.OrdinalIgnoreCase) || new Uri(url).Host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase)) || this.candidates.Any(c => c.Url == url) || this.candidates.Count >= 50)
                        {
                            continue;
                        }

                        var title = Value(result, "title");
                        this.candidates.Add(new JobDiscoveryCandidate
                        {
                            Url = url,
                            Title = title.Length > 300 ? title[..300] : title,
                            Snippet = Value(result, "content"),
                            Query = query,
                            Imported = existing.Contains(url),
                            Message = existing.Contains(url) ? "Already in this project." : null,
                        });
                    }
                }
                catch (InvalidOperationException ex)
                {
                    this.messages.Add(ex.Message);
                    break;
                }
            }
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <summary>Extracts only selected candidates, in batches of at most five URLs.</summary>
    /// <param name="ids">Candidate identifiers from the current search.</param>
    /// <returns>The asynchronous extraction run.</returns>
    public async Task ExtractAsync(IReadOnlyList<Guid> ids)
    {
        await this.EnterAsync();
        try
        {
            await this.VerifyOwnerAsync();
            var selected = ids.Distinct().Select(id => this.candidates.SingleOrDefault(c => c.Id == id)
                ?? throw new ValidationException("Choose results from the current search.")).Where(c => !c.Imported && string.IsNullOrWhiteSpace(c.Text)).ToList();
            if (selected.Count == 0 || this.extractionAttempts + selected.Count > 50)
            {
                throw new ValidationException("Choose unextracted results. Each search run allows at most 50 URL extraction attempts.");
            }

            var key = await this.KeyAsync();
            foreach (var batch in selected.Chunk(5))
            {
                await usage.ReserveAsync(1);
                this.extractionAttempts += batch.Length;
                foreach (var candidate in batch)
                {
                    candidate.Message = "Text could not be extracted. Open the original page and paste its announcement text below.";
                }

                try
                {
                    using var json = await this.PostAsync("extract", key, new { urls = batch.Select(c => c.Url).ToArray(), extract_depth = "basic", format = "text", include_images = false, include_usage = true });
                    if (json.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var result in results.EnumerateArray())
                        {
                            var candidate = batch.FirstOrDefault(c => c.Url == NormalizeUrl(Value(result, "url")));
                            var text = Value(result, "raw_content");
                            if (candidate is not null && !string.IsNullOrWhiteSpace(text) && text.Length <= 250000)
                            {
                                candidate.Text = text;
                                candidate.Message = "Text retrieved. Confirm this is an announcement, enter the employer, and review the content before importing.";
                            }
                        }
                    }
                }
                catch (InvalidOperationException ex)
                {
                    foreach (var candidate in batch)
                    {
                        candidate.Message = ex.Message + " You can paste the original text manually.";
                    }

                    break;
                }
            }
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <summary>Imports one reviewed candidate without making an external request.</summary>
    /// <param name="candidateId">The current candidate identifier.</param>
    /// <returns>Whether a new source was imported rather than skipped as a duplicate.</returns>
    public async Task<bool> ImportAsync(Guid candidateId)
    {
        await this.EnterAsync();
        try
        {
            await this.VerifyOwnerAsync();
            var candidate = this.candidates.Single(c => c.Id == candidateId);
            var source = new JobAnnouncement
            {
                ProjectId = this.projectId,
                Title = candidate.Title.Trim(),
                Employer = candidate.Employer.Trim(),
                Url = candidate.Url,
                Text = candidate.Text.Trim(),
                CollectedOn = DateTime.Today,
                DiscoveryQuery = candidate.Query,
                DiscoveryDescription = this.description,
            };
            var added = await curriculum.ImportAnnouncementAsync(source);
            candidate.Imported = true;
            candidate.Selected = false;
            candidate.Message = added ? "Imported into this project." : "Duplicate skipped: this URL or identical source text is already in the project.";
            return added;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <summary>Runs a query and saves every new result, then enriches saved previews with full text when available.</summary>
    /// <param name="id">The target curriculum project.</param>
    /// <param name="query">The user's search query.</param>
    /// <returns>The number of new announcements saved.</returns>
    public async Task<int> FindAndSaveAsync(int id, string query)
    {
        await this.SearchAsync(id, query, new[] { query });
        var saved = new Dictionary<Guid, JobAnnouncement>();
        foreach (var candidate in this.candidates.Where(c => !c.Imported))
        {
            var source = new JobAnnouncement
            {
                ProjectId = id,
                Url = candidate.Url,
                Title = string.IsNullOrWhiteSpace(candidate.Title) ? new Uri(candidate.Url).Host : candidate.Title,
                Employer = "Not identified",
                Text = "[Search preview - full announcement text not retrieved]\nSource: " + candidate.Url + "\n\n" + candidate.Snippet,
                CollectedOn = DateTime.Today,
                DiscoveryQuery = query,
                DiscoveryDescription = query,
            };
            if (await curriculum.ImportAnnouncementAsync(source))
            {
                saved.Add(candidate.Id, source);
            }
            else
            {
                candidate.Imported = true;
            }
        }

        if (saved.Count > 0)
        {
            try
            {
                await this.ExtractAsync(saved.Keys.ToArray());
            }
            catch (InvalidOperationException ex)
            {
                this.messages.Add(ex.Message);
            }

            foreach (var candidate in this.candidates.Where(c => saved.ContainsKey(c.Id)))
            {
                var source = saved[candidate.Id];
                if (!string.IsNullOrWhiteSpace(candidate.Text))
                {
                    source.Text = candidate.Text;
                    await curriculum.SaveAnnouncementAsync(source);
                }

                candidate.Imported = true;
            }
        }

        return saved.Count;
    }

    private static string Value(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private async Task<JsonDocument> PostAsync(string endpoint, string key, object body)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tavily.com/" + endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Tavily rejected the API key. Check Configuration and your Tavily account.",
                    HttpStatusCode.TooManyRequests => "Tavily's rate limit was reached. Wait before trying again.",
                    (HttpStatusCode)432 or (HttpStatusCode)433 => "Tavily's credit limit was reached. Check your provider account.",
                    _ => "Tavily could not complete the request. No automatic retry was made.",
                });
            }

            await response.Content.LoadIntoBufferAsync(8 * 1024 * 1024, timeout.Token);
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            // Provider bodies and headers may contain secrets; never return or log them.
            throw new InvalidOperationException("Tavily could not be reached or returned an unreadable response. No automatic retry was made.");
        }
    }

    private async Task<string> KeyAsync() => await keys.GetApiKeyAsync()
        ?? throw new InvalidOperationException("Save your Tavily API key on the Configuration page first.");

    private async Task<string> UserIdAsync() => (await authentication.GetAuthenticationStateAsync()).User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("Sign in to find announcements.");

    private async Task VerifyOwnerAsync()
    {
        if (this.owner is null || this.owner != await this.UserIdAsync())
        {
            throw new UnauthorizedAccessException("Start a new search after signing in.");
        }
    }

    private async Task EnterAsync()
    {
        if (!await this.gate.WaitAsync(0))
        {
            throw new InvalidOperationException("Another discovery operation is still running.");
        }
    }
}
