using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pw.Hub.Migrations
{
    /// <inheritdoc />
    public partial class Accounts_DeleteEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "Accounts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Accounts",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }
    }
}
