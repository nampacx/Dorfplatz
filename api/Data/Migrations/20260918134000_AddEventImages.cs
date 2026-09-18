using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BulletinBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalImageUrl",
                table: "Events",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageBlobName",
                table: "Events",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalImageUrl",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ImageBlobName",
                table: "Events");
        }
    }
}
