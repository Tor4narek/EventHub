using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddEventImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventImportRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventImportRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventImportItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Source = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    OriginalDescription = table.Column<string>(type: "text", nullable: true),
                    EventDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    MainImg = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    TagIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    SuggestedTagIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    SuggestedDescription = table.Column<string>(type: "text", nullable: true),
                    Warnings = table.Column<string[]>(type: "text[]", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeaseUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventImportItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventImportItems_EventImportRuns_ImportRunId",
                        column: x => x.ImportRunId,
                        principalTable: "EventImportRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventImportItems_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventImportItems_ConfirmedSourceKey",
                table: "EventImportItems",
                column: "SourceKey",
                unique: true,
                filter: "\"Status\" = 'Confirmed'");

            migrationBuilder.CreateIndex(
                name: "IX_EventImportItems_EventId",
                table: "EventImportItems",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_EventImportItems_ImportRunId",
                table: "EventImportItems",
                column: "ImportRunId");

            migrationBuilder.CreateIndex(
                name: "IX_EventImportItems_Status_LeaseUntil",
                table: "EventImportItems",
                columns: new[] { "Status", "LeaseUntil" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventImportItems");

            migrationBuilder.DropTable(
                name: "EventImportRuns");
        }
    }
}
