using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BulletinBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Events created before categories existed are backfilled as Sonstiges.
            // An empty default would not map to any EventCategory member and would
            // fail when those rows are read back.
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Sonstiges");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Category",
                table: "Events",
                column: "Category");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_Category",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Events");
        }
    }
}
