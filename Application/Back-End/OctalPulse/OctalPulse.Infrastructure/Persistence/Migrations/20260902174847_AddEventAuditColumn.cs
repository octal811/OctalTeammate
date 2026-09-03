using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OctalPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventAuditColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "Events",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_DeletedByUserId",
                table: "Events",
                column: "DeletedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Users_DeletedByUserId",
                table: "Events",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_Users_DeletedByUserId",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_DeletedByUserId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "Events");
        }
    }
}
