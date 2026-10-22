using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JBIDS.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnouncementDiscoveryProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscoveryDescription",
                table: "Announcements",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscoveryQuery",
                table: "Announcements",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscoveryDescription",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "DiscoveryQuery",
                table: "Announcements");
        }
    }
}
