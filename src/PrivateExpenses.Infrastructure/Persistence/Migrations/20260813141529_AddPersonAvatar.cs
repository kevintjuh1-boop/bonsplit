using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrivateExpenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonAvatar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarMimeType",
                table: "People",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarStoredFileName",
                table: "People",
                type: "TEXT",
                maxLength: 260,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarMimeType",
                table: "People");

            migrationBuilder.DropColumn(
                name: "AvatarStoredFileName",
                table: "People");
        }
    }
}
