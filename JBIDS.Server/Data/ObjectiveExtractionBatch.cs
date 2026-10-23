// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Server.Data;

using System.ComponentModel.DataAnnotations;

/// <summary>An immutable account-owned snapshot of extraction input.</summary>
public sealed class ObjectiveExtractionBatch
{
    /// <summary>Gets or sets the request identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the project identifier.</summary>
    public int ProjectId { get; set; }

    /// <summary>Gets or sets the requesting account.</summary>
    [MaxLength(450)]
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the immutable input JSON.</summary>
    public string RequestJson { get; set; } = string.Empty;
}
