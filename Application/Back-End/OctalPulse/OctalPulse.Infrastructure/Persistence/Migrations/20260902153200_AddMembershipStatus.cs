using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OctalPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMembershipStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "TrackMembers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Approved");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ProjectMembers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Approved");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "TrackMembers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ProjectMembers");
        }
    }
}
