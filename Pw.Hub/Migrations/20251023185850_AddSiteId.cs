using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pw.Hub.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Email",
                table: "Accounts",
                newName: "SiteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SiteId",
                table: "Accounts",
                newName: "Email");
        }
    }
}
