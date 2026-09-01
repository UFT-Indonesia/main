using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddLeaveHalfDayAndHourlyIzin : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "end_hour",
            table: "LeaveRequests",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "half_day",
            table: "LeaveRequests",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "half_day_period",
            table: "LeaveRequests",
            type: "character varying(16)",
            maxLength: 16,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "start_hour",
            table: "LeaveRequests",
            type: "integer",
            nullable: true);

        migrationBuilder.AlterColumn<decimal>(
            name: "entitled_days",
            table: "EmployeeLeaveQuotas",
            type: "numeric",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "end_hour",
            table: "LeaveRequests");

        migrationBuilder.DropColumn(
            name: "half_day",
            table: "LeaveRequests");

        migrationBuilder.DropColumn(
            name: "half_day_period",
            table: "LeaveRequests");

        migrationBuilder.DropColumn(
            name: "start_hour",
            table: "LeaveRequests");

        migrationBuilder.AlterColumn<int>(
            name: "entitled_days",
            table: "EmployeeLeaveQuotas",
            type: "integer",
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "numeric");
    }
}
