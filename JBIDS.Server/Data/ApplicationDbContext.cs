// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Server.Data
{
    using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// Provides access to the application identity database.
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class.
        /// </summary>
        /// <param name="options">The database provider and connection options.</param>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        /// <summary>Gets the projects stored in the database.</summary>
        public DbSet<CurriculumProject> Projects => this.Set<CurriculumProject>();

        /// <summary>Gets the announcements stored in the database.</summary>
        public DbSet<JobAnnouncement> Announcements => this.Set<JobAnnouncement>();

        /// <summary>Gets the objectives stored in the database.</summary>
        public DbSet<TrainingObjective> Objectives => this.Set<TrainingObjective>();

        /// <summary>Gets the evidence stored in the database.</summary>
        public DbSet<ObjectiveEvidence> Evidence => this.Set<ObjectiveEvidence>();

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<JobAnnouncement>().HasOne<CurriculumProject>().WithMany().HasForeignKey(a => a.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<TrainingObjective>().HasOne<CurriculumProject>().WithMany().HasForeignKey(o => o.ProjectId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<ObjectiveEvidence>().HasOne<TrainingObjective>().WithMany().HasForeignKey(e => e.ObjectiveId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<ObjectiveEvidence>().HasOne<JobAnnouncement>().WithMany().HasForeignKey(e => e.AnnouncementId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
