// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Server.Data;

using System.ComponentModel.DataAnnotations;

/// <summary>Bloom's revised cognitive levels.</summary>
public enum BloomLevel
{
    /// <summary>Recall facts.</summary>
    Remember = 1,

    /// <summary>Explain meaning.</summary>
    Understand,

    /// <summary>Use knowledge.</summary>
    Apply,

    /// <summary>Examine relationships.</summary>
    Analyze,

    /// <summary>Judge using criteria.</summary>
    Evaluate,

    /// <summary>Produce original work.</summary>
    Create,
}

/// <summary>The human review decision.</summary>
public enum ReviewStatus
{
    /// <summary>Awaiting review.</summary>
    Draft,

    /// <summary>Accepted by the educator.</summary>
    Approved,

    /// <summary>Declined by the educator.</summary>
    Rejected,
}

/// <summary>A curriculum development workspace.</summary>
public sealed class CurriculumProject
{
    /// <summary>Gets or sets the record identifier.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the project name.</summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the project description.</summary>
    [Required]
    public string Description { get; set; } = string.Empty;
}

/// <summary>A preserved employer source.</summary>
public sealed class JobAnnouncement
{
    /// <summary>Gets or sets the record identifier.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the owning project identifier.</summary>
    public int ProjectId { get; set; }

    /// <summary>Gets or sets the advertised job title.</summary>
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the employer name.</summary>
    [Required]
    [MaxLength(300)]
    public string Employer { get; set; } = string.Empty;

    /// <summary>Gets or sets the original announcement URL.</summary>
    [Required]
    [Url]
    [MaxLength(2048)]
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the date the source was collected.</summary>
    public DateTime CollectedOn { get; set; } = DateTime.Today;

    /// <summary>Gets or sets the saved content.</summary>
    [Required]
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the successful extraction timestamp, including empty results.</summary>
    public DateTime? ObjectivesExtractedOn { get; set; }

    /// <summary>Gets or sets the content generation used to reject stale extraction results.</summary>
    public Guid ExtractionRevision { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the last imported extraction batch for repeat-upload protection.</summary>
    public Guid? LastExtractionBatchId { get; set; }

    /// <summary>Gets or sets the query that discovered this source.</summary>
    [MaxLength(400)]
    public string? DiscoveryQuery { get; set; }

    /// <summary>Gets or sets the user-described occupation and search filters.</summary>
    [MaxLength(500)]
    public string? DiscoveryDescription { get; set; }
}

/// <summary>A reviewable training objective.</summary>
public sealed class TrainingObjective
{
    /// <summary>Gets or sets the record identifier.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the owning project identifier.</summary>
    public int ProjectId { get; set; }

    /// <summary>Gets or sets the saved content.</summary>
    [Required]
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional broader objective, with unrestricted nesting depth.</summary>
    public int? ParentObjectiveId { get; set; }

    /// <summary>Gets or sets the Bloom cognitive level.</summary>
    [Required]
    public BloomLevel? Level { get; set; }

    /// <summary>Gets or sets the human review decision.</summary>
    public ReviewStatus Status { get; set; }
}

/// <summary>Evidence connecting an objective and an announcement.</summary>
public sealed class ObjectiveEvidence
{
    /// <summary>Gets or sets the record identifier.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the supported objective identifier.</summary>
    public int ObjectiveId { get; set; }

    /// <summary>Gets or sets the supporting announcement identifier.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Choose an announcement.")]
    public int AnnouncementId { get; set; }

    /// <summary>Gets or sets the preserved supporting excerpt.</summary>
    [Required]
    public string Excerpt { get; set; } = string.Empty;
}
