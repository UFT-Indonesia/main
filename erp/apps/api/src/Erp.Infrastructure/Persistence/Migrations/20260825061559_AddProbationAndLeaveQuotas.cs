using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddProbationAndLeaveQuotas : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(
            name: "hire_date",
            table: "Employees",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "probation_ends_on_override",
            table: "Employees",
            type: "date",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "EmployeeLeaveQuotas",
            columns: table => new
            {
                leave_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                entitled_days = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeLeaveQuotas", x => new { x.employee_id, x.leave_type });
                table.ForeignKey(
                    name: "FK_EmployeeLeaveQuotas_Employees_employee_id",
                    column: x => x.employee_id,
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ProbationExtensionRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                current_ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                proposed_ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                requested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                decided_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                decision_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProbationExtensionRequests", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProbationExtensionRequests_Employees_employee_id",
                    column: x => x.employee_id,
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProbationExtensionRequests_employee_id_status",
            table: "ProbationExtensionRequests",
            columns: new[] { "employee_id", "status" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EmployeeLeaveQuotas");

        migrationBuilder.DropTable(
            name: "ProbationExtensionRequests");

        migrationBuilder.DropColumn(
            name: "hire_date",
            table: "Employees");

        migrationBuilder.DropColumn(
            name: "probation_ends_on_override",
            table: "Employees");
    }
}
