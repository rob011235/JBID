using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JBIDS.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateObjectiveExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentObjectiveId",
                table: "Objectives",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExtractionRevision",
                table: "Announcements",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "LastExtractionBatchId",
                table: "Announcements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ObjectivesExtractedOn",
                table: "Announcements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExtractionBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractionBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExtractionBatches_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExtractionBatches_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Objectives_ParentObjectiveId",
                table: "Objectives",
                column: "ParentObjectiveId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtractionBatches_OwnerId",
                table: "ExtractionBatches",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtractionBatches_ProjectId",
                table: "ExtractionBatches",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Objectives_Objectives_ParentObjectiveId",
                table: "Objectives",
                column: "ParentObjectiveId",
                principalTable: "Objectives",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Objectives_Objectives_ParentObjectiveId",
                table: "Objectives");

            migrationBuilder.DropTable(
                name: "ExtractionBatches");

            migrationBuilder.DropIndex(
                name: "IX_Objectives_ParentObjectiveId",
                table: "Objectives");

            migrationBuilder.DropColumn(
                name: "ParentObjectiveId",
                table: "Objectives");

            migrationBuilder.DropColumn(
                name: "ExtractionRevision",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "LastExtractionBatchId",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "ObjectivesExtractedOn",
                table: "Announcements");
        }
    }
}
