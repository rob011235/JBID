// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

// Positional record parameters also declare PascalCase public properties.
#pragma warning disable SA1313

namespace JBIDS.Extraction;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>The versioned local-helper request.</summary>
/// <param name="Version">The protocol version.</param>
/// <param name="BatchId">The immutable request identifier.</param>
/// <param name="Sources">The preserved announcements.</param>
/// <param name="ExistingObjectives">The project objectives available for reuse.</param>
public sealed record ExtractionRequest(int Version, Guid BatchId, SourceInput[] Sources, ExistingObjective[] ExistingObjectives);

/// <summary>A source snapshot.</summary>
/// <param name="Id">The source identifier.</param>
/// <param name="Revision">The source content generation.</param>
/// <param name="Title">The job title.</param>
/// <param name="Text">The source text.</param>
public sealed record SourceInput(int Id, Guid Revision, string Title, string Text);

/// <summary>A reusable objective snapshot.</summary>
/// <param name="Id">The objective identifier.</param>
/// <param name="Text">The measurable objective.</param>
/// <param name="Level">The Bloom level.</param>
/// <param name="ParentId">The broader objective.</param>
public sealed record ExistingObjective(int Id, string Text, string Level, int? ParentId);

/// <summary>The helper's output, also usable by a future API provider.</summary>
/// <param name="Version">The protocol version.</param>
/// <param name="BatchId">The request identifier.</param>
/// <param name="Sources">Only successfully processed sources, including empty results.</param>
public sealed record ExtractionResponse(int Version, Guid BatchId, SourceResult[] Sources);

/// <summary>A successful extraction for one source.</summary>
/// <param name="Id">The announcement identifier.</param>
/// <param name="Revision">The original source generation.</param>
/// <param name="Objectives">The candidate objectives, possibly empty.</param>
public sealed record SourceResult(int Id, Guid Revision, CandidateObjective[] Objectives);

/// <summary>A new or reused candidate with source evidence.</summary>
/// <param name="Key">A source-local identifier.</param>
/// <param name="ExistingObjectiveId">An existing objective to reuse, or null.</param>
/// <param name="Text">The measurable candidate text.</param>
/// <param name="Level">A revised Bloom level name.</param>
/// <param name="ParentKey">The source-local broader objective key, or null.</param>
/// <param name="Excerpt">An exact excerpt from the announcement.</param>
public sealed record CandidateObjective(string Key, int? ExistingObjectiveId, string Text, string Level, string? ParentKey, string Excerpt);

/// <summary>Serialization settings shared by the helper and the website.</summary>
public static class ExtractionJson
{
    /// <summary>Gets strict camel-case JSON settings.</summary>
    public static JsonSerializerOptions Options { get; } = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        MaxDepth = 32,
    };
}
