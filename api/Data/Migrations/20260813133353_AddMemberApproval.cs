using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BulletinBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecidedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DecidedByUserId",
                table: "Users",
                type: "uuid",
                nullable: true);

            // EF generated defaultValue: "" here, which maps to no AccessStatus
            // member and would throw on the first read. "Pending" is the real
            // default for a new account.
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            // Everyone who already has an account keeps their access. Without
            // this, shipping the approval queue locks out every existing member
            // — including whoever would have to approve them again.
            migrationBuilder.Sql(@"UPDATE ""Users"" SET ""Status"" = 'Approved';");

            // The value comes from the application from here on (AppUser.Status
            // initialises to Pending). Dropping the database default means an
            // insert that forgets the column fails loudly instead of silently
            // admitting someone.
            migrationBuilder.Sql(@"ALTER TABLE ""Users"" ALTER COLUMN ""Status"" DROP DEFAULT;");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Status",
                table: "Users",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Status",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DecidedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DecidedByUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Users");
        }
    }
}
