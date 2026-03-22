using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetSentry.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddTemperatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "CpuTemp",
                table: "Metrics",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "GpuTemp",
                table: "Metrics",
                type: "real",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CpuTemp",
                table: "Metrics");

            migrationBuilder.DropColumn(
                name: "GpuTemp",
                table: "Metrics");
        }
    }
}
