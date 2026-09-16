using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rundfrage.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatorId",
                table: "WishLists",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatorId",
                table: "Polls",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Creators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    LinkToken = table.Column<string>(type: "TEXT", maxLength: 22, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Creators", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WishLists_CreatorId",
                table: "WishLists",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Polls_CreatorId",
                table: "Polls",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Creators_LinkToken",
                table: "Creators",
                column: "LinkToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Creators_Name",
                table: "Creators",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Polls_Creators_CreatorId",
                table: "Polls",
                column: "CreatorId",
                principalTable: "Creators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WishLists_Creators_CreatorId",
                table: "WishLists",
                column: "CreatorId",
                principalTable: "Creators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Polls_Creators_CreatorId",
                table: "Polls");

            migrationBuilder.DropForeignKey(
                name: "FK_WishLists_Creators_CreatorId",
                table: "WishLists");

            migrationBuilder.DropTable(
                name: "Creators");

            migrationBuilder.DropIndex(
                name: "IX_WishLists_CreatorId",
                table: "WishLists");

            migrationBuilder.DropIndex(
                name: "IX_Polls_CreatorId",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "CreatorId",
                table: "WishLists");

            migrationBuilder.DropColumn(
                name: "CreatorId",
                table: "Polls");
        }
    }
}
