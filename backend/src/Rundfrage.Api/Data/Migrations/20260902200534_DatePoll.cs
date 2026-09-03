using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rundfrage.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DatePoll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Polls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ParticipantToken = table.Column<string>(type: "character varying(22)", maxLength: 22, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetentionDeadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Polls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CandidateDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PollId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateDays_Polls_PollId",
                        column: x => x.PollId,
                        principalTable: "Polls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Responses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PollId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EditToken = table.Column<string>(type: "character varying(22)", maxLength: 22, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Responses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Responses_Polls_PollId",
                        column: x => x.PollId,
                        principalTable: "Polls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DayAnswers",
                columns: table => new
                {
                    ResponseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    Availability = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DayAnswers", x => new { x.ResponseId, x.CandidateDayId });
                    table.ForeignKey(
                        name: "FK_DayAnswers_CandidateDays_CandidateDayId",
                        column: x => x.CandidateDayId,
                        principalTable: "CandidateDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DayAnswers_Responses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "Responses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateDays_PollId_Date",
                table: "CandidateDays",
                columns: new[] { "PollId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DayAnswers_CandidateDayId",
                table: "DayAnswers",
                column: "CandidateDayId");

            migrationBuilder.CreateIndex(
                name: "IX_Polls_ParticipantToken",
                table: "Polls",
                column: "ParticipantToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Polls_RetentionDeadline",
                table: "Polls",
                column: "RetentionDeadline");

            migrationBuilder.CreateIndex(
                name: "IX_Responses_EditToken",
                table: "Responses",
                column: "EditToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Responses_PollId",
                table: "Responses",
                column: "PollId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DayAnswers");

            migrationBuilder.DropTable(
                name: "CandidateDays");

            migrationBuilder.DropTable(
                name: "Responses");

            migrationBuilder.DropTable(
                name: "Polls");
        }
    }
}
