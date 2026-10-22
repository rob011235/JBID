// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Server.Services;

using System.ComponentModel.DataAnnotations;
using JBIDS.Server.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>Persists curriculum work and enforces human review rules.</summary>
/// <param name="factory">Creates an independent context for each operation.</param>
public sealed class CurriculumService(IDbContextFactory<ApplicationDbContext> factory)
{
    /// <summary>Lists saved projects.</summary>
    /// <returns>The asynchronous operation and its saved records.</returns>
    public async Task<List<CurriculumProject>> ProjectsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Projects.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
    }

    /// <summary>Finds a project.</summary>
    /// <param name="id">The project identifier.</param>
    /// <returns>The asynchronous operation and its saved records.</returns>
    public async Task<CurriculumProject?> ProjectAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Projects.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id);
    }

    /// <summary>Lists preserved announcements in a project.</summary>
    /// <param name="projectId">The owning project identifier.</param>
    /// <returns>The asynchronous operation and its saved records.</returns>
    public async Task<List<JobAnnouncement>> AnnouncementsAsync(int projectId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Announcements.AsNoTracking().Where(a => a.ProjectId == projectId).OrderBy(a => a.Title).ToListAsync();
    }

    /// <summary>Lists objectives in a project.</summary>
    /// <param name="projectId">The owning project identifier.</param>
    /// <returns>The asynchronous operation and its saved records.</returns>
    public async Task<List<TrainingObjective>> ObjectivesAsync(int projectId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Objectives.AsNoTracking().Where(o => o.ProjectId == projectId).OrderBy(o => o.Id).ToListAsync();
    }

    /// <summary>Lists objective evidence in a project.</summary>
    /// <param name="projectId">The owning project identifier.</param>
    /// <returns>The asynchronous operation and its saved records.</returns>
    public async Task<List<ObjectiveEvidence>> EvidenceAsync(int projectId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Evidence.AsNoTracking().Where(e => db.Objectives.Any(o => o.Id == e.ObjectiveId && o.ProjectId == projectId)).OrderBy(e => e.Id).ToListAsync();
    }

    /// <summary>Creates a validated project.</summary>
    /// <param name="input">The values to validate and save.</param>
    /// <returns>The asynchronous operation.</returns>
    public async Task SaveProjectAsync(CurriculumProject input)
    {
        Validate(input);
        await using var db = await factory.CreateDbContextAsync();
        db.Projects.Add(input);
        await db.SaveChangesAsync();
    }

    /// <summary>Saves an announcement and invalidates affected approvals.</summary>
    /// <param name="input">The values to validate and save.</param>
    /// <returns>The asynchronous operation.</returns>
    public async Task SaveAnnouncementAsync(JobAnnouncement input)
    {
        Validate(input);
        if (!Uri.TryCreate(input.Url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
        {
            throw new ValidationException("Enter an HTTP or HTTPS source URL.");
        }

        if (input.CollectedOn == default)
        {
            throw new ValidationException("Enter the collection date.");
        }

        await using var db = await factory.CreateDbContextAsync();
        if (!await db.Projects.AnyAsync(p => p.Id == input.ProjectId))
        {
            throw new ValidationException("Project not found.");
        }

        if (input.Id == 0)
        {
            db.Announcements.Add(input);
        }
        else
        {
            var saved = await db.Announcements.SingleAsync(a => a.Id == input.Id && a.ProjectId == input.ProjectId);
            if (saved.Text != input.Text)
            {
                var affected = await db.Objectives.Where(o => o.Status == ReviewStatus.Approved && db.Evidence.Any(e => e.ObjectiveId == o.Id && e.AnnouncementId == saved.Id)).ToListAsync();
                affected.ForEach(o => o.Status = ReviewStatus.Draft);
            }

            db.Entry(saved).CurrentValues.SetValues(input);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Saves objective content without bypassing review.</summary>
    /// <param name="input">The values to validate and save.</param>
    /// <returns>The asynchronous operation.</returns>
    public async Task SaveObjectiveAsync(TrainingObjective input)
    {
        Validate(input);
        if (input.Level is null || !Enum.IsDefined(input.Level.Value))
        {
            throw new ValidationException("Choose a Bloom's level.");
        }

        await using var db = await factory.CreateDbContextAsync();
        if (!await db.Projects.AnyAsync(p => p.Id == input.ProjectId))
        {
            throw new ValidationException("Project not found.");
        }

        if (input.Id == 0)
        {
            input.Status = ReviewStatus.Draft;
            db.Objectives.Add(input);
        }
        else
        {
            var saved = await db.Objectives.SingleAsync(o => o.Id == input.Id && o.ProjectId == input.ProjectId);
            if (saved.Status == ReviewStatus.Approved && (saved.Text != input.Text || saved.Level != input.Level))
            {
                saved.Status = ReviewStatus.Draft;
            }

            saved.Text = input.Text;
            saved.Level = input.Level;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Saves evidence within its project and invalidates approval when changed.</summary>
    /// <param name="projectId">The owning project identifier.</param>
    /// <param name="input">The values to validate and save.</param>
    /// <returns>The asynchronous operation.</returns>
    public async Task SaveEvidenceAsync(int projectId, ObjectiveEvidence input)
    {
        Validate(input);
        await using var db = await factory.CreateDbContextAsync();
        var objective = await db.Objectives.SingleAsync(o => o.Id == input.ObjectiveId && o.ProjectId == projectId);
        if (!await db.Announcements.AnyAsync(a => a.Id == input.AnnouncementId && a.ProjectId == projectId))
        {
            throw new ValidationException("Choose an announcement from this project.");
        }

        if (input.Id == 0)
        {
            db.Evidence.Add(input);
            if (objective.Status == ReviewStatus.Approved)
            {
                objective.Status = ReviewStatus.Draft;
            }
        }
        else
        {
            var saved = await db.Evidence.SingleAsync(e => e.Id == input.Id && e.ObjectiveId == objective.Id);
            if (saved.AnnouncementId != input.AnnouncementId || saved.Excerpt != input.Excerpt)
            {
                if (objective.Status == ReviewStatus.Approved)
                {
                    objective.Status = ReviewStatus.Draft;
                }

                saved.AnnouncementId = input.AnnouncementId;
                saved.Excerpt = input.Excerpt;
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Records a human review decision after validating approval requirements.</summary>
    /// <param name="projectId">The owning project identifier.</param>
    /// <param name="objectiveId">The objective identifier.</param>
    /// <param name="status">The review decision.</param>
    /// <returns>The asynchronous operation.</returns>
    public async Task ReviewAsync(int projectId, int objectiveId, ReviewStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ValidationException("Invalid review status.");
        }

        await using var db = await factory.CreateDbContextAsync();
        var objective = await db.Objectives.SingleAsync(o => o.Id == objectiveId && o.ProjectId == projectId);
        if (status == ReviewStatus.Approved)
        {
            Validate(objective);
            if (objective.Level is null || !Enum.IsDefined(objective.Level.Value))
            {
                throw new ValidationException("Choose a Bloom's level before approval.");
            }

            var evidence = await db.Evidence.Where(e => e.ObjectiveId == objective.Id).ToListAsync();
            if (evidence.Count == 0 || evidence.Any(e => string.IsNullOrWhiteSpace(e.Excerpt)))
            {
                throw new ValidationException("Add supporting evidence before approval.");
            }
        }

        objective.Status = status;
        await db.SaveChangesAsync();
    }

    private static void Validate(object value) => Validator.ValidateObject(value, new ValidationContext(value), validateAllProperties: true);
}
