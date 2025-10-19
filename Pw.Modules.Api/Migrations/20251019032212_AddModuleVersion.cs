using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pw.Modules.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddModuleVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Version",
                table: "Modules",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "1.0.0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "Modules");
        }
    }
}
