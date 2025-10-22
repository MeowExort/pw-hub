using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pw.Hub.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultCharacterToServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultCharacterOptionId",
                table: "AccountServer",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultCharacterOptionId",
                table: "AccountServer");
        }
    }
}
