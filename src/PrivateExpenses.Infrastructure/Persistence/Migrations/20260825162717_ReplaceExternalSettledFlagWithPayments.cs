using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrivateExpenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceExternalSettledFlagWithPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalSettledAt",
                table: "ExpenseItems");

            migrationBuilder.DropColumn(
                name: "IsExternalSettled",
                table: "ExpenseItems");

            migrationBuilder.CreateTable(
                name: "ExternalPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OwedToPersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AmountCents = table.Column<long>(type: "INTEGER", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalPayments_People_OwedToPersonId",
                        column: x => x.OwedToPersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalPayments_OwedToPersonId",
                table: "ExternalPayments",
                column: "OwedToPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalPayments_RecipientName",
                table: "ExternalPayments",
                column: "RecipientName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalPayments");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalSettledAt",
                table: "ExpenseItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExternalSettled",
                table: "ExpenseItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
