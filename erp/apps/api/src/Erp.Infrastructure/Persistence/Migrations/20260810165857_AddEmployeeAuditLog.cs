using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddEmployeeAuditLog : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EmployeeAuditLogs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                old_value_json = table.Column<string>(type: "text", nullable: true),
                new_value_json = table.Column<string>(type: "text", nullable: true),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                actor_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeAuditLogs", x => x.Id);
                table.ForeignKey(
                    name: "FK_EmployeeAuditLogs_Employees_employee_id",
                    column: x => x.employee_id,
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeAuditLogs_employee_id_occurred_at_utc",
            table: "EmployeeAuditLogs",
            columns: new[] { "employee_id", "occurred_at_utc" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeAuditLogs_event_type",
            table: "EmployeeAuditLogs",
            column: "event_type");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeAuditLogs_occurred_at_utc",
            table: "EmployeeAuditLogs",
            column: "occurred_at_utc");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EmployeeAuditLogs");
    }
}
