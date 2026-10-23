// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Checks;

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using JBIDS.Extraction;
using JBIDS.Server.Data;
using JBIDS.Server.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

/// <summary>Checks extraction transactions, completion, hierarchy and evidence.</summary>
internal static class ObjectiveExtractionChecks
{
    /// <summary>Runs against an isolated database without calling an LLM.</summary>
    /// <param name="factory">The context factory.</param>
    /// <returns>The asynchronous checks.</returns>
    public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory)
    {
        var auth = new TestAuthentication();
        var service = new ObjectiveExtractionService(factory, auth);
        await Invalid(() => service.ExportAsync(1));
        var owner = new ApplicationUser { UserName = "extraction-owner" };
        var otherOwner = new ApplicationUser { UserName = "extraction-other" };
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Users.AddRange(owner, otherOwner);
            await db.SaveChangesAsync();
        }

        auth.SignIn(owner.Id);
        var store = new CurriculumService(factory);
        var project = new CurriculumProject { Name = "Extraction checks", Description = "Synthetic data" };
        await store.SaveProjectAsync(project);
        var source = new JobAnnouncement { ProjectId = project.Id, Title = "Developer", Employer = "Test", Url = "https://example.com/extraction", Text = "Develop database applications. Implement CRUD operations. Write parameterized SQL queries." };
        var empty = new JobAnnouncement { ProjectId = project.Id, Title = "No teachable skills", Employer = "Test", Url = "https://example.com/empty", Text = "Apply today." };
        await store.SaveAnnouncementAsync(source);
        await store.SaveAnnouncementAsync(empty);
        var request = await service.ExportAsync(project.Id);
        Assert((await store.AnnouncementsAsync(project.Id)).All(a => a.ObjectivesExtractedOn is null), "Export does not complete sources.");
        var candidates = new[]
        {
            new CandidateObjective("sql", null, "Write parameterized SQL queries.", "Apply", "crud", "Write parameterized SQL queries."),
            new CandidateObjective("database", null, "Develop database applications.", "Create", null, "Develop database applications."),
            new CandidateObjective("crud", null, "Implement CRUD operations.", "Apply", "database", "Implement CRUD operations."),
        };
        var response = new ExtractionResponse(1, request.BatchId, new[] { new SourceResult(source.Id, source.ExtractionRevision, candidates), new SourceResult(empty.Id, empty.ExtractionRevision, Array.Empty<CandidateObjective>()) });
        auth.SignIn(otherOwner.Id);
        await Invalid(() => service.ImportAsync(project.Id, response));
        auth.SignIn(owner.Id);
        var malformed = response with { Sources = new[] { response.Sources[0] with { Objectives = new[] { candidates[1] with { Excerpt = "Invented evidence" } } } } };
        await Invalid(() => service.ImportAsync(project.Id, malformed));
        Assert((await store.ObjectivesAsync(project.Id)).Count == 0, "Invalid evidence saves nothing.");
        Assert(await service.ImportAsync(project.Id, response) == 2, "Empty and nonempty extractions complete.");
        var saved = await store.ObjectivesAsync(project.Id);
        Assert(saved.Count == 3 && saved.All(o => o.Status == ReviewStatus.Draft), "All candidates start as drafts.");
        var root = saved.Single(o => o.ParentObjectiveId is null);
        var child = saved.Single(o => o.ParentObjectiveId == root.Id);
        var leaf = saved.Single(o => o.ParentObjectiveId == child.Id);
        Assert(leaf.Text.StartsWith("Write", StringComparison.Ordinal), "Out-of-order nested candidates preserve hierarchy.");
        Assert(await service.ImportAsync(project.Id, response) == 0 && (await store.EvidenceAsync(project.Id)).Count == 3, "Repeated import is idempotent.");
        await Invalid(() => service.ExportAsync(project.Id));
        root.ParentObjectiveId = leaf.Id;
        await Invalid(() => store.SaveObjectiveAsync(root));
        root.ParentObjectiveId = null;
        var foreignProject = new CurriculumProject { Name = "Foreign extraction", Description = "Isolation" };
        await store.SaveProjectAsync(foreignProject);
        var foreignObjective = new TrainingObjective { ProjectId = foreignProject.Id, Text = "Foreign", Level = BloomLevel.Apply };
        await store.SaveObjectiveAsync(foreignObjective);
        root.ParentObjectiveId = foreignObjective.Id;
        await Invalid(() => store.SaveObjectiveAsync(root));
        root.ParentObjectiveId = null;
        await Invalid(() => service.ImportAsync(foreignProject.Id, response));
        var second = new JobAnnouncement { ProjectId = project.Id, Title = "Another developer", Employer = "Test", Url = "https://example.com/second", Text = source.Text };
        await store.SaveAnnouncementAsync(second);
        await store.ReviewAsync(project.Id, leaf.Id, ReviewStatus.Approved);
        var secondRequest = await service.ExportAsync(project.Id);
        var reuse = new CandidateObjective("same", leaf.Id, leaf.Text, "Apply", null, "Write parameterized SQL queries.");
        await service.ImportAsync(project.Id, new ExtractionResponse(1, secondRequest.BatchId, new[] { new SourceResult(second.Id, second.ExtractionRevision, new[] { reuse }) }));
        Assert((await store.ObjectivesAsync(project.Id)).Count == 3, "Multiple sources reuse an objective.");
        Assert((await store.EvidenceAsync(project.Id)).Count(e => e.ObjectiveId == leaf.Id) == 2, "Shared objective retains both sources.");
        Assert((await store.ObjectivesAsync(project.Id)).Single(o => o.Id == leaf.Id).Status == ReviewStatus.Draft, "New evidence resets approval.");
        await service.ReprocessAsync(project.Id, empty.Id);
        var staleRequest = await service.ExportAsync(project.Id);
        var emptySnapshot = staleRequest.Sources.Single(s => s.Id == empty.Id);
        empty.Text += " New text.";
        await store.SaveAnnouncementAsync(empty);
        await Invalid(() => service.ImportAsync(project.Id, new ExtractionResponse(1, staleRequest.BatchId, new[] { new SourceResult(empty.Id, emptySnapshot.Revision, Array.Empty<CandidateObjective>()) })));
        var fresh = await service.ExportAsync(project.Id);
        var before = fresh.Sources.Single(s => s.Id == empty.Id);
        await service.ReprocessAsync(project.Id, empty.Id);
        await Invalid(() => service.ImportAsync(project.Id, new ExtractionResponse(1, fresh.BatchId, new[] { new SourceResult(empty.Id, before.Revision, Array.Empty<CandidateObjective>()) })));
        fresh = await service.ExportAsync(project.Id);
        before = fresh.Sources.Single(s => s.Id == empty.Id);
        var cyclic = new[] { candidates[1] with { Key = "a", ParentKey = "b" }, candidates[2] with { Key = "b", ParentKey = "a" } };
        await service.ReprocessAsync(project.Id, source.Id);
        var cycleRequest = await service.ExportAsync(project.Id);
        var cycleSource = cycleRequest.Sources.Single(s => s.Id == source.Id);
        await Invalid(() => service.ImportAsync(project.Id, new ExtractionResponse(1, cycleRequest.BatchId, new[] { new SourceResult(source.Id, cycleSource.Revision, cyclic) })));

        // A valid first source must also roll back if a later source is invalid.
        await service.ReprocessAsync(project.Id, source.Id);
        var atomic = await service.ExportAsync(project.Id);
        var atomicSources = new[]
        {
            new SourceResult(source.Id, atomic.Sources.Single(s => s.Id == source.Id).Revision, new[] { candidates[1] with { Key = "new", Text = "Create a different database application." } }),
            new SourceResult(empty.Id, before.Revision, new[] { candidates[1] }),
        };
        var invalidSecond = new ExtractionResponse(1, atomic.BatchId, atomicSources);
        await Invalid(() => service.ImportAsync(project.Id, invalidSecond));
        Assert((await store.ObjectivesAsync(project.Id)).Count == 3 && (await store.AnnouncementsAsync(project.Id)).Single(a => a.Id == source.Id).ObjectivesExtractedOn is null, "A later failure rolls back objectives and completion.");
        Console.WriteLine("PASS: extraction ownership, nested hierarchy, cycles, exact evidence, shared objectives, empty completion, repeat imports, stale results and transaction rollback.");
    }

    private static async Task Invalid(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (ValidationException)
        {
            return;
        }

        throw new InvalidOperationException("Expected extraction validation failure.");
    }

    private static void Assert(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class TestAuthentication : AuthenticationStateProvider
    {
        private ClaimsPrincipal user = new ClaimsPrincipal(new ClaimsIdentity());

        public void SignIn(string id) => this.user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id) }, "Test"));

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(this.user));
    }
}
