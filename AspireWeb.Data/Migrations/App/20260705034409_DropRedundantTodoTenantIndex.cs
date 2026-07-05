using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AspireWeb.Data.Migrations.App;

/// <inheritdoc />
public partial class _20260705034409_DropRedundantTodoTenantIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_TodoItems_TenantId",
            table: "TodoItems");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_TodoItems_TenantId",
            table: "TodoItems",
            column: "TenantId");
    }
}
