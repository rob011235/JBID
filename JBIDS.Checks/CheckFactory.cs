// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Checks;

using JBIDS.Server.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>Creates independent database contexts for integration checks.</summary>
/// <param name="options">The isolated database options.</param>
internal sealed class CheckFactory(DbContextOptions<ApplicationDbContext> options)
    : IDbContextFactory<ApplicationDbContext>
{
    /// <inheritdoc />
    public ApplicationDbContext CreateDbContext() => new ApplicationDbContext(options);
}
