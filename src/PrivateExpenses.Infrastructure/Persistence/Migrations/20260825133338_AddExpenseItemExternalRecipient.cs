using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrivateExpenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseItemExternalRecipient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalRecipientName",
                table: "ExpenseItems",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalRecipientName",
                table: "ExpenseItems");

            migrationBuilder.DropColumn(
                name: "ExternalSettledAt",
                table: "ExpenseItems");

            migrationBuilder.DropColumn(
                name: "IsExternalSettled",
                table: "ExpenseItems");
        }
    }
}
