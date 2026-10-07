using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddLeaveSalaryDeduction : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "deduction_divisor",
            table: "Employees",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "deduction_flat_amount",
            table: "Employees",
            type: "numeric(18,0)",
            precision: 18,
            scale: 0,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "EmployeeSalaryHistories",
            columns: table => new
            {
                employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                amount = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeSalaryHistories", x => new { x.employee_id, x.effective_from });
            });

        migrationBuilder.CreateTable(
            name: "LeaveDeductionLines",
            columns: table => new
            {
                leave_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                date = table.Column<DateOnly>(type: "date", nullable: false),
                month = table.Column<DateOnly>(type: "date", nullable: false),
                employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                leave_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                free_days = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                cut_days = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                salary = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                daily_rate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeaveDeductionLines", x => new { x.leave_request_id, x.date });
            });

        migrationBuilder.CreateTable(
            name: "LeaveDeductionMonths",
            columns: table => new
            {
                month = table.Column<DateOnly>(type: "date", nullable: false),
                closed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                closed_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                closed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeaveDeductionMonths", x => x.month);
            });

        migrationBuilder.CreateTable(
            name: "PayrollSettings",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false),
                divisor = table.Column<int>(type: "integer", nullable: false),
                first_month = table.Column<DateOnly>(type: "date", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PayrollSettings", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_LeaveDeductionLines_employee_id",
            table: "LeaveDeductionLines",
            column: "employee_id");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveDeductionLines_month",
            table: "LeaveDeductionLines",
            column: "month");

        // History starts at launch: each employee's current salary, from the date it took effect.
        migrationBuilder.Sql(
            """
            INSERT INTO "EmployeeSalaryHistories" (employee_id, effective_from, amount)
            SELECT "Id", effective_salary_from, monthly_wage_amount FROM "Employees";
            """);

        // The launch month is the first one that can be closed; before it, over-cap leave was refused.
        migrationBuilder.Sql(
            """
            INSERT INTO "PayrollSettings" ("Id", divisor, first_month)
            VALUES (1, 20, date_trunc('month', now() AT TIME ZONE 'Asia/Jakarta')::date);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EmployeeSalaryHistories");

        migrationBuilder.DropTable(
            name: "LeaveDeductionLines");

        migrationBuilder.DropTable(
            name: "LeaveDeductionMonths");

        migrationBuilder.DropTable(
            name: "PayrollSettings");

        migrationBuilder.DropColumn(
            name: "deduction_divisor",
            table: "Employees");

        migrationBuilder.DropColumn(
            name: "deduction_flat_amount",
            table: "Employees");
    }
}
