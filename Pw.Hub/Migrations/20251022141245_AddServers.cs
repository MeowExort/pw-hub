using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pw.Hub.Migrations
{
    /// <inheritdoc />
    public partial class AddServers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountServer",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    OptionId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AccountId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountServer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountServer_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccountCharacter",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    OptionId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ServerId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountCharacter", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountCharacter_AccountServer_ServerId",
                        column: x => x.ServerId,
                        principalTable: "AccountServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountCharacter_ServerId",
                table: "AccountCharacter",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountServer_AccountId",
                table: "AccountServer",
                column: "AccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountCharacter");

            migrationBuilder.DropTable(
                name: "AccountServer");
        }
    }
}
