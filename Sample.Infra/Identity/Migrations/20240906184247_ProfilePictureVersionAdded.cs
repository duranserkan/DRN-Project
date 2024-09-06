using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sample.Infra.Identity.Migrations
{
    /// <inheritdoc />
    public partial class ProfilePictureVersionAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "version",
                schema: "sample_identity_context",
                table: "profile_pictures",
                type: "smallint",
                rowVersion: true,
                nullable: false,
                defaultValue: (byte)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "version",
                schema: "sample_identity_context",
                table: "profile_pictures");
        }
    }
}
