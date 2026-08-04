using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAccountProvisioning : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AuthUsers_EmployeeId",
            table: "AuthUsers");

        migrationBuilder.AddColumn<bool>(
            name: "MustChangePassword",
            table: "AuthUsers",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_AuthUsers_EmployeeId",
            table: "AuthUsers",
            column: "EmployeeId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AuthUsers_EmployeeId",
            table: "AuthUsers");

        migrationBuilder.DropColumn(
            name: "MustChangePassword",
            table: "AuthUsers");

        migrationBuilder.CreateIndex(
            name: "IX_AuthUsers_EmployeeId",
            table: "AuthUsers",
            column: "EmployeeId");
    }
}
