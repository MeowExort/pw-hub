using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pw.Hub.Migrations
{
    /// <inheritdoc />
    public partial class AddSquadOrderIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrderIndex",
                table: "Squads",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrderIndex",
                table: "Squads");
        }
    }
}
