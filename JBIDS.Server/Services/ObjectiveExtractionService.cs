// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Server.Services;

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using JBIDS.Extraction;
using JBIDS.Server.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

/// <summary>Exports account-owned requests and atomically imports evidenced candidate objectives.</summary>
/// <param name="factory">The database context factory.</param>
/// <param name="authentication">The authenticated browser account.</param>
public sealed class ObjectiveExtractionService(IDbContextFactory<ApplicationDbContext> factory, AuthenticationStateProvider authentication)
{
    /// <summary>Exports up to five unprocessed sources without marking them complete.</summary>
    /// <param name="projectId">The project.</param>
    /// <returns>The immutable extraction request.</returns>
    public async Task<ExtractionRequest> ExportAsync(int projectId)
    {
        var owner = await this.OwnerAsync();
        await using var db = await factory.CreateDbContextAsync();
        if (!await db.Projects.AnyAsync(p => p.Id == projectId))
        {
            throw new ValidationException("Project not found.");
        }

        var sources = await db.Announcements.AsNoTracking().Where(a => a.ProjectId == projectId && a.ObjectivesExtractedOn == null).OrderBy(a => a.Id).Take(5).ToListAsync();
        if (sources.Count == 0)
        {
            throw new ValidationException("There are no announcements awaiting extraction.");
        }

        // Bound the prompt size without silently truncating a source.
        var selected = new List<SourceInput>();
        var length = 0;
        foreach (var source in sources)
        {
            if (source.Text.Length > 250000)
            {
                throw new ValidationException("An announcement exceeds 250,000 characters. Shorten it before extraction.");
            }

            if (length + source.Text.Length > 250000)
            {
                break;
            }

            selected.Add(new SourceInput(source.Id, source.ExtractionRevision, source.Title, source.Text));
            length += source.Text.Length;
        }

        var objectives = await db.Objectives.AsNoTracking().Where(o => o.ProjectId == projectId).OrderBy(o => o.Id).ToListAsync();
        var request = new ExtractionRequest(1, Guid.NewGuid(), selected.ToArray(), objectives.Select(o => new ExistingObjective(o.Id, o.Text, o.Level.ToString() ?? string.Empty, o.ParentObjectiveId)).ToArray());
        var json = JsonSerializer.Serialize(request, ExtractionJson.Options);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 1500000)
        {
            throw new ValidationException("This project's extraction input is too large for one package.");
        }

        db.ExtractionBatches.Add(new ObjectiveExtractionBatch { Id = request.BatchId, OwnerId = owner, ProjectId = projectId, RequestJson = json });
        await db.SaveChangesAsync();
        return request;
    }

    /// <summary>Allows an explicit rerun and invalidates outstanding results for this source.</summary>
    /// <param name="projectId">The project.</param>
    /// <param name="sourceId">The source to reprocess.</param>
    /// <returns>The asynchronous operation.</returns>
    public async Task ReprocessAsync(int projectId, int sourceId)
    {
        await this.OwnerAsync();
        await using var db = await factory.CreateDbContextAsync();
        var source = await db.Announcements.SingleAsync(a => a.Id == sourceId && a.ProjectId == projectId);
        source.ObjectivesExtractedOn = null;
        source.LastExtractionBatchId = null;
        source.ExtractionRevision = Guid.NewGuid();
        await db.SaveChangesAsync();
    }

    /// <summary>Validates and saves successful results, evidence and completion together.</summary>
    /// <param name="projectId">The project receiving the results.</param>
    /// <param name="response">The helper's structured output.</param>
    /// <returns>The number of newly completed sources.</returns>
    public async Task<int> ImportAsync(int projectId, ExtractionResponse response)
    {
        var owner = await this.OwnerAsync();
        if (response.Version != 1 || response.Sources is null || response.Sources.Length is < 1 or > 5 || response.Sources.Any(s => s is null) || response.Sources.Select(s => s.Id).Distinct().Count() != response.Sources.Length)
        {
            throw new ValidationException("Invalid extraction result format.");
        }

        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var batch = await db.ExtractionBatches.SingleOrDefaultAsync(b => b.Id == response.BatchId && b.ProjectId == projectId && b.OwnerId == owner)
            ?? throw new ValidationException("This extraction request does not belong to this account and project.");
        var request = JsonSerializer.Deserialize<ExtractionRequest>(batch.RequestJson, ExtractionJson.Options) ?? throw new ValidationException("Invalid saved request.");
        var objectives = await db.Objectives.Where(o => o.ProjectId == projectId).OrderBy(o => o.Id).ToListAsync();
        var completed = 0;
        foreach (var result in response.Sources)
        {
            var snapshot = request.Sources.SingleOrDefault(a => a.Id == result.Id);
            var source = await db.Announcements.SingleOrDefaultAsync(a => a.Id == result.Id && a.ProjectId == projectId);
            if (snapshot is null || source is null || result.Revision != snapshot.Revision || source.ExtractionRevision != snapshot.Revision || source.Text != snapshot.Text)
            {
                throw new ValidationException("An announcement changed or was removed. Export a fresh request.");
            }

            if (source.LastExtractionBatchId == batch.Id && source.ObjectivesExtractedOn is not null)
            {
                continue;
            }

            if (source.ObjectivesExtractedOn is not null)
            {
                throw new ValidationException("An announcement was already processed by another request. Refresh the project.");
            }

            ValidateCandidates(result.Objectives, snapshot.Text);
            var mapped = new Dictionary<string, TrainingObjective>(StringComparer.Ordinal);
            var pending = result.Objectives.ToList();
            while (pending.Count > 0)
            {
                var ready = pending.FirstOrDefault(c => c.ParentKey is null || mapped.ContainsKey(c.ParentKey));
                if (ready is null)
                {
                    throw new ValidationException("Objective hierarchy contains a cycle or missing parent.");
                }

                var parentId = ready.ParentKey is null ? (int?)null : mapped[ready.ParentKey].Id;
                TrainingObjective? objective;
                if (ready.ExistingObjectiveId is int existingId)
                {
                    var original = request.ExistingObjectives.SingleOrDefault(o => o.Id == existingId);
                    objective = objectives.SingleOrDefault(o => o.Id == existingId);
                    if (original is null || objective is null || original.Text != objective.Text || original.Level != objective.Level.ToString() || original.ParentId != objective.ParentObjectiveId || Normalize(ready.Text) != Normalize(objective.Text) || ready.Level != objective.Level.ToString())
                    {
                        throw new ValidationException("A reused objective changed or is not in this project's request. Export again.");
                    }

                    if (ready.ParentKey is not null && parentId != objective.ParentObjectiveId)
                    {
                        throw new ValidationException("Extraction cannot reparent an existing objective. Edit its parent separately.");
                    }
                }
                else
                {
                    objective = objectives.FirstOrDefault(o => Normalize(o.Text) == Normalize(ready.Text));
                    if (objective is not null && (objective.ParentObjectiveId != parentId || objective.Level.ToString() != ready.Level))
                    {
                        throw new ValidationException("A duplicate objective has a different parent or Bloom level. Reuse its existing identifier.");
                    }

                    if (objective is null)
                    {
                        objective = new TrainingObjective { ProjectId = projectId, Text = ready.Text.Trim(), Level = Enum.Parse<BloomLevel>(ready.Level), ParentObjectiveId = parentId, Status = ReviewStatus.Draft };
                        db.Objectives.Add(objective);
                        await db.SaveChangesAsync();
                        objectives.Add(objective);
                    }
                }

                mapped.Add(ready.Key, objective);
                if (!await db.Evidence.AnyAsync(e => e.ObjectiveId == objective.Id && e.AnnouncementId == source.Id && e.Excerpt == ready.Excerpt))
                {
                    db.Evidence.Add(new ObjectiveEvidence { ObjectiveId = objective.Id, AnnouncementId = source.Id, Excerpt = ready.Excerpt });
                    if (objective.Status == ReviewStatus.Approved)
                    {
                        objective.Status = ReviewStatus.Draft;
                    }

                    await db.SaveChangesAsync();
                }

                pending.Remove(ready);
            }

            source.ObjectivesExtractedOn = DateTime.UtcNow;
            source.LastExtractionBatchId = batch.Id;
            completed++;
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return completed;
    }

    private static void ValidateCandidates(CandidateObjective[] candidates, string text)
    {
        if (candidates is null || candidates.Length > 200 || candidates.Any(c => c is null || string.IsNullOrWhiteSpace(c.Key) || c.Key.Length > 100 || string.IsNullOrWhiteSpace(c.Text) || c.Text.Length > 4000 || string.IsNullOrWhiteSpace(c.Excerpt) || c.Excerpt.Length > 10000 || !text.Contains(c.Excerpt, StringComparison.Ordinal) || !Enum.TryParse<BloomLevel>(c.Level, out var level) || !Enum.IsDefined(level) || level.ToString() != c.Level) || candidates.Select(c => c.Key).Distinct(StringComparer.Ordinal).Count() != candidates.Length)
        {
            throw new ValidationException("Invalid candidate objectives. Each needs a unique key, a Bloom level and an exact source excerpt.");
        }
    }

    private static string Normalize(string value) => System.Text.RegularExpressions.Regex.Replace(value.Trim(), @"\s+", " ").TrimEnd('.', '!', '?').ToUpperInvariant();

    private async Task<string> OwnerAsync()
    {
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        return user.Identity?.IsAuthenticated == true && user.FindFirstValue(ClaimTypes.NameIdentifier) is string id
            ? id : throw new ValidationException("Sign in to extract objectives.");
    }
}
