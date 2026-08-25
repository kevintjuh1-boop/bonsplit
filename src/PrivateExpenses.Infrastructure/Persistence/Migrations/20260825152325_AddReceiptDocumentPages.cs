using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrivateExpenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptDocumentPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReceiptDocumentPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReceiptDocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    StoredFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    MimeType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptDocumentPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptDocumentPages_ReceiptDocuments_ReceiptDocumentId",
                        column: x => x.ReceiptDocumentId,
                        principalTable: "ReceiptDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptDocumentPages_ReceiptDocumentId",
                table: "ReceiptDocumentPages",
                column: "ReceiptDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptDocumentPages_StoredFileName",
                table: "ReceiptDocumentPages",
                column: "StoredFileName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceiptDocumentPages");
        }
    }
}
