using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OctalPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "Tracks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "MinorTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "MinorTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "MajorTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "MajorTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_DeletedByUserId",
                table: "Tracks",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_DeletedByUserId",
                table: "Projects",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MinorTasks_CreatedByUserId",
                table: "MinorTasks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MinorTasks_DeletedByUserId",
                table: "MinorTasks",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MajorTasks_CreatedByUserId",
                table: "MajorTasks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MajorTasks_DeletedByUserId",
                table: "MajorTasks",
                column: "DeletedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MajorTasks_Users_CreatedByUserId",
                table: "MajorTasks",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MajorTasks_Users_DeletedByUserId",
                table: "MajorTasks",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MinorTasks_Users_CreatedByUserId",
                table: "MinorTasks",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MinorTasks_Users_DeletedByUserId",
                table: "MinorTasks",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Users_DeletedByUserId",
                table: "Projects",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tracks_Users_DeletedByUserId",
                table: "Tracks",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MajorTasks_Users_CreatedByUserId",
                table: "MajorTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_MajorTasks_Users_DeletedByUserId",
                table: "MajorTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_MinorTasks_Users_CreatedByUserId",
                table: "MinorTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_MinorTasks_Users_DeletedByUserId",
                table: "MinorTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Users_DeletedByUserId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Tracks_Users_DeletedByUserId",
                table: "Tracks");

            migrationBuilder.DropIndex(
                name: "IX_Tracks_DeletedByUserId",
                table: "Tracks");

            migrationBuilder.DropIndex(
                name: "IX_Projects_DeletedByUserId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_MinorTasks_CreatedByUserId",
                table: "MinorTasks");

            migrationBuilder.DropIndex(
                name: "IX_MinorTasks_DeletedByUserId",
                table: "MinorTasks");

            migrationBuilder.DropIndex(
                name: "IX_MajorTasks_CreatedByUserId",
                table: "MajorTasks");

            migrationBuilder.DropIndex(
                name: "IX_MajorTasks_DeletedByUserId",
                table: "MajorTasks");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "MinorTasks");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "MinorTasks");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "MajorTasks");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "MajorTasks");
        }
    }
}
