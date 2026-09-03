using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BulletinBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberSuspension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The new AccessStatus.Suspended needs no schema change: Status is
            // stored as its name in a varchar, so a new member is just a new
            // value. Only the reason column is new, and existing rows have no
            // reason because nothing has been suspended yet.
            migrationBuilder.AddColumn<string>(
                name: "StatusReason",
                table: "Users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StatusReason",
                table: "Users");
        }
    }
}
