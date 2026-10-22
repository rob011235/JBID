// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Server.Services;

/// <summary>A transient search candidate awaiting human review and import.</summary>
public sealed class JobDiscoveryCandidate
{
    /// <summary>Gets the opaque identifier for this search result.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Gets or sets the source URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the editable announcement title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the employer, supplied by the reviewer rather than guessed.</summary>
    public string Employer { get; set; } = string.Empty;

    /// <summary>Gets or sets the search snippet, which is not imported as source text.</summary>
    public string Snippet { get; set; } = string.Empty;

    /// <summary>Gets or sets the search query that found this candidate.</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>Gets or sets extracted or manually pasted source text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether this candidate is selected for extraction.</summary>
    public bool Selected { get; set; }

    /// <summary>Gets or sets a safe per-result retrieval or import message.</summary>
    public string? Message { get; set; }

    /// <summary>Gets or sets a value indicating whether the source already exists in this project.</summary>
    public bool Imported { get; set; }
}
