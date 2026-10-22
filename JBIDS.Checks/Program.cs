// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Checks;

using System.ComponentModel.DataAnnotations;
using JBIDS.Server.Data;
using JBIDS.Server.Services;
using Microsoft.EntityFrameworkCore;

/// <summary>Runs curriculum persistence and review integration checks.</summary>
internal static class Program
{
    /// <summary>Runs checks using SQLite or SQL Server LocalDB.</summary>
    /// <param name="args">Pass --sqlserver to test the SQL Server migrations.</param>
    /// <returns>The asynchronous check run.</returns>
    public static async Task Main(string[] args)
    {
        // Default checks use an isolated SQLite file. Pass --sqlserver to check SQL Server migrations too.
        var sqlServer = args.Contains("--sqlserver");
        var database = "JBIDS_Iteration1_Checks_" + Guid.NewGuid().ToString("N");
        var sqlitePath = Path.Combine(Path.GetTempPath(), database + ".db");
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
        if (sqlServer)
        {
            builder.UseSqlServer($"Server=(localdb)\\mssqllocaldb;Database={database};Trusted_Connection=True;TrustServerCertificate=True");
        }
        else
        {
            builder.UseSqlite($"Data Source={sqlitePath}");
        }

        var options = builder.Options;
        var factory = new CheckFactory(options);
        await using var db = factory.CreateDbContext();
        try
        {
            if (sqlServer)
            {
                await db.Database.MigrateAsync();
            }
            else
            {
                await db.Database.EnsureCreatedAsync();
            }

            var store = new CurriculumService(factory);
            var project = new CurriculumProject { Name = "Integration test", Description = "Synthetic employer evidence" };
            await store.SaveProjectAsync(project);
            var other = new CurriculumProject { Name = "Other project", Description = "Isolation check" };
            await store.SaveProjectAsync(other);
            var sources = new List<JobAnnouncement>();
            for (var i = 0; i < 5; i++)
            {
                var a = new JobAnnouncement { ProjectId = project.Id, Title = $"Developer {i}", Employer = "Test employer", Url = $"https://example.com/jobs/{i}", Text = $"Build and test embedded software {i}." };
                await store.SaveAnnouncementAsync(a);
                sources.Add(a);
            }

            var objectives = new List<TrainingObjective>();
            for (var i = 0; i < 10; i++)
            {
                var o = new TrainingObjective { ProjectId = project.Id, Text = $"Implement embedded software task {i}.", Level = BloomLevel.Apply };
                await store.SaveObjectiveAsync(o);
                await ExpectValidation(() => store.ReviewAsync(project.Id, o.Id, ReviewStatus.Approved));
                await store.SaveEvidenceAsync(project.Id, new ObjectiveEvidence { ObjectiveId = o.Id, AnnouncementId = sources[i % 5].Id, Excerpt = sources[i % 5].Text });
                await store.ReviewAsync(project.Id, o.Id, ReviewStatus.Approved);
                objectives.Add(o);
            }

            var extra = new ObjectiveEvidence { ObjectiveId = objectives[0].Id, AnnouncementId = sources[1].Id, Excerpt = sources[1].Text };
            await store.SaveEvidenceAsync(project.Id, extra);
            await AssertStatus(0, ReviewStatus.Draft);
            await store.ReviewAsync(project.Id, objectives[0].Id, ReviewStatus.Approved);
            extra.Excerpt = "Build and test embedded software";
            await store.SaveEvidenceAsync(project.Id, extra);
            await AssertStatus(0, ReviewStatus.Draft);
            await store.ReviewAsync(project.Id, objectives[0].Id, ReviewStatus.Approved);

            // A newly constructed service/context simulates a fresh application session.
            store = new CurriculumService(new CheckFactory(options));
            Check((await store.ObjectivesAsync(project.Id)).Count(o => o.Status == ReviewStatus.Approved) == 10, "Ten approvals persist");
            Check((await store.AnnouncementsAsync(project.Id)).Count == 5, "Five sources persist");
            Check((await store.EvidenceAsync(project.Id)).Count == 11, "Many-to-many evidence persists");
            Check((await store.ObjectivesAsync(other.Id)).Count == 0, "Project lists are isolated");
            var foreign = new JobAnnouncement { ProjectId = other.Id, Title = "Other", Employer = "Other", Url = "https://example.com/other", Text = "Other evidence" };
            await store.SaveAnnouncementAsync(foreign);
            await ExpectValidation(() => store.SaveEvidenceAsync(project.Id, new ObjectiveEvidence { ObjectiveId = objectives[0].Id, AnnouncementId = foreign.Id, Excerpt = "Other evidence" }));
            var changed = (await store.ObjectivesAsync(project.Id))[0];
            changed.Text += " Changed.";
            await store.SaveObjectiveAsync(changed);
            await AssertStatus(0, ReviewStatus.Draft);
            await store.ReviewAsync(project.Id, changed.Id, ReviewStatus.Approved);
            changed.Level = BloomLevel.Create;
            await store.SaveObjectiveAsync(changed);
            await AssertStatus(0, ReviewStatus.Draft);
            await store.ReviewAsync(project.Id, changed.Id, ReviewStatus.Rejected);
            await AssertStatus(0, ReviewStatus.Rejected);
            await store.ReviewAsync(project.Id, changed.Id, ReviewStatus.Draft);
            await AssertStatus(0, ReviewStatus.Draft);
            await store.ReviewAsync(project.Id, changed.Id, ReviewStatus.Approved);
            sources[0].Text += " Updated requirement.";
            await store.SaveAnnouncementAsync(sources[0]);
            await AssertStatus(0, ReviewStatus.Draft);
            await AssertStatus(5, ReviewStatus.Draft);
            await AssertStatus(2, ReviewStatus.Approved);
            await ExpectValidation(() => store.SaveObjectiveAsync(new TrainingObjective { ProjectId = project.Id, Text = " ", Level = BloomLevel.Apply }));
            await ExpectValidation(() => store.SaveObjectiveAsync(new TrainingObjective { ProjectId = project.Id, Text = "Objective", Level = (BloomLevel)99 }));
            await ExpectValidation(() => store.SaveEvidenceAsync(project.Id, new ObjectiveEvidence { ObjectiveId = changed.Id, AnnouncementId = sources[0].Id, Excerpt = " " }));
            sources[0].Url = "javascript:alert(1)";
            await ExpectValidation(() => store.SaveAnnouncementAsync(sources[0]));
            Console.WriteLine("PASS: persistence, ten approvals, evidence relationships, project isolation, validation, rejection, and review invalidation.");

            async Task AssertStatus(int index, ReviewStatus expected)
            {
                var actual = (await store.ObjectivesAsync(project.Id)).Single(o => o.Id == objectives[index].Id).Status;
                Check(actual == expected, $"Objective {index}: expected {expected}, got {actual}");
            }
        }
        finally
        {
            // Cleanup must not hide a failed connection or assertion.
            try
            {
                await db.Database.EnsureDeletedAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Temporary database cleanup failed: {ex.Message}");
            }
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    private static async Task ExpectValidation(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (ValidationException)
        {
            return;
        }

        throw new Exception("Expected validation failure.");
    }
}
