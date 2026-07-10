using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlonProject.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCatalogOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnerId",
                table: "ProductCatalogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductCatalogs_OwnerId",
                table: "ProductCatalogs",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductCatalogs_Users_OwnerId",
                table: "ProductCatalogs",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Backfill: assign each existing product to the owner of the
            // warehouse tree its first item lives in. Products never used by
            // any item stay unowned (visible to everyone until edited).
            migrationBuilder.Sql(@"
UPDATE p SET OwnerId = COALESCE(w.OwnerId, pw.OwnerId)
FROM ProductCatalogs p
CROSS APPLY (
    SELECT TOP 1 i.WarehouseId
    FROM Items i
    WHERE i.ProductCatalogId = p.Id
    ORDER BY i.Id
) fi
JOIN Warehouses w ON w.Id = fi.WarehouseId
LEFT JOIN Warehouses pw ON pw.Id = w.ParentWarehouseId;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductCatalogs_Users_OwnerId",
                table: "ProductCatalogs");

            migrationBuilder.DropIndex(
                name: "IX_ProductCatalogs_OwnerId",
                table: "ProductCatalogs");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "ProductCatalogs");
        }
    }
}
