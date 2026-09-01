using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMaxIzinHours : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "max_izin_hours",
            table: "AttendancePolicyHistories",
            type: "integer",
            nullable: false,
            defaultValue: 4);

        migrationBuilder.AddColumn<int>(
            name: "max_izin_hours",
            table: "AttendancePolicies",
            type: "integer",
            nullable: false,
            defaultValue: 4);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "max_izin_hours",
            table: "AttendancePolicyHistories");

        migrationBuilder.DropColumn(
            name: "max_izin_hours",
            table: "AttendancePolicies");
    }
}
