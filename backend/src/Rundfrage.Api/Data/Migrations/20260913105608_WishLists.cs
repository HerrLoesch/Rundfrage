using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rundfrage.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class WishLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WishLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    TargetDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ListToken = table.Column<string>(type: "TEXT", maxLength: 22, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WishItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WishListId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "NOCASE"),
                    WantedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WishItems_WishLists_WishListId",
                        column: x => x.WishListId,
                        principalTable: "WishLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WishClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WishItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ClaimToken = table.Column<string>(type: "TEXT", maxLength: 22, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WishClaims_WishItems_WishItemId",
                        column: x => x.WishItemId,
                        principalTable: "WishItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WishClaims_ClaimToken",
                table: "WishClaims",
                column: "ClaimToken");

            migrationBuilder.CreateIndex(
                name: "IX_WishClaims_WishItemId",
                table: "WishClaims",
                column: "WishItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WishItems_WishListId_Name",
                table: "WishItems",
                columns: new[] { "WishListId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WishLists_ListToken",
                table: "WishLists",
                column: "ListToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WishLists_TargetDate",
                table: "WishLists",
                column: "TargetDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WishClaims");

            migrationBuilder.DropTable(
                name: "WishItems");

            migrationBuilder.DropTable(
                name: "WishLists");
        }
    }
}
