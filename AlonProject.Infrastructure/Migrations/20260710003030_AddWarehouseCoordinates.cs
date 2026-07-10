using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlonProject.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Warehouses",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Warehouses",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Warehouses");
        }
    }
}
