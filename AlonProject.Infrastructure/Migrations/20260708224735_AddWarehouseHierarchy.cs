using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlonProject.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnerId",
                table: "Warehouses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentWarehouseId",
                table: "Warehouses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_OwnerId",
                table: "Warehouses",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_ParentWarehouseId",
                table: "Warehouses",
                column: "ParentWarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_Users_OwnerId",
                table: "Warehouses",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_Warehouses_ParentWarehouseId",
                table: "Warehouses",
                column: "ParentWarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_Users_OwnerId",
                table: "Warehouses");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_Warehouses_ParentWarehouseId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_OwnerId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_ParentWarehouseId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "ParentWarehouseId",
                table: "Warehouses");
        }
    }
}
