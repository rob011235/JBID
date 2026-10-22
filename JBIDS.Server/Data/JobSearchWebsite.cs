// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Server.Data;

using System.ComponentModel.DataAnnotations;

/// <summary>A job search domain that site administrators can enable for discovery.</summary>
public sealed class JobSearchWebsite
{
    /// <summary>Gets or sets the website identifier.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the display name.</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the normalized domain without a path.</summary>
    [Required]
    [MaxLength(253)]
    public string Domain { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether searches include this domain.</summary>
    public bool IsEnabled { get; set; } = true;
}
