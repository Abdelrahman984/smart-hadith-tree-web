using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHadithTree.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupRuleOrdinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Ordinal",
                table: "MukhtalitGroupRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ordinal",
                table: "MukhtalitGroupRules");
        }
    }
}
