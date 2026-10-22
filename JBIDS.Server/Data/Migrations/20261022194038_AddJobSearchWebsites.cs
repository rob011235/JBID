using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace JBIDS.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddJobSearchWebsites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobSearchWebsites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSearchWebsites", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "JobSearchWebsites",
                columns: new[] { "Id", "Domain", "IsEnabled", "Name" },
                values: new object[,]
                {
                    { 1, "indeed.com", true, "Indeed" },
                    { 2, "linkedin.com", true, "LinkedIn Jobs" },
                    { 3, "glassdoor.com", true, "Glassdoor" },
                    { 4, "ziprecruiter.com", true, "ZipRecruiter" },
                    { 5, "dice.com", true, "Dice" },
                    { 6, "usajobs.gov", true, "USAJOBS" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobSearchWebsites_Domain",
                table: "JobSearchWebsites",
                column: "Domain",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobSearchWebsites");
        }
    }
}
