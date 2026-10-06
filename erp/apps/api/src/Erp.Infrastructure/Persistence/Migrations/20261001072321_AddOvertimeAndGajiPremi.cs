using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddOvertimeAndGajiPremi : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "GajiPremiPeriods",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                start_date = table.Column<DateOnly>(type: "date", nullable: false),
                closed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                closed_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                closed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GajiPremiPeriods", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OvertimeAssignments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                date = table.Column<DateOnly>(type: "date", nullable: false),
                start_time = table.Column<TimeOnly>(type: "time", nullable: false),
                end_time = table.Column<TimeOnly>(type: "time", nullable: false),
                is_day_off = table.Column<bool>(type: "boolean", nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                requested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                decided_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                decision_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                frozen_hours = table.Column<int>(type: "integer", nullable: true),
                frozen_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                frozen_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OvertimeAssignments", x => x.Id);
                table.ForeignKey(
                    name: "FK_OvertimeAssignments_Employees_employee_id",
                    column: x => x.employee_id,
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Rapels",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                work_date = table.Column<DateOnly>(type: "date", nullable: false),
                note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                attachment_storage_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                attachment_file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                attachment_content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                attachment_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                payout_period_start = table.Column<DateOnly>(type: "date", nullable: true),
                requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                requested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                decided_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                decision_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Rapels", x => x.Id);
                table.ForeignKey(
                    name: "FK_Rapels_Employees_employee_id",
                    column: x => x.employee_id,
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "OvertimeCorrectionRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                work_date = table.Column<DateOnly>(type: "date", nullable: false),
                kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                punched_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                attachment_storage_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                attachment_file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                attachment_content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                attachment_size_bytes = table.Column<long>(type: "bigint", nullable: false),
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
                table.PrimaryKey("PK_OvertimeCorrectionRequests", x => x.Id);
                table.ForeignKey(
                    name: "FK_OvertimeCorrectionRequests_Employees_employee_id",
                    column: x => x.employee_id,
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_OvertimeCorrectionRequests_OvertimeAssignments_assignment_id",
                    column: x => x.assignment_id,
                    principalTable: "OvertimeAssignments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_GajiPremiPeriods_start_date",
            table: "GajiPremiPeriods",
            column: "start_date",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OvertimeAssignments_date",
            table: "OvertimeAssignments",
            column: "date");

        migrationBuilder.CreateIndex(
            name: "IX_OvertimeAssignments_employee_id_date",
            table: "OvertimeAssignments",
            columns: new[] { "employee_id", "date" },
            unique: true,
            filter: "status IN ('Pending', 'Approved', 'Expired')");

        migrationBuilder.CreateIndex(
            name: "IX_OvertimeCorrectionRequests_assignment_id_status",
            table: "OvertimeCorrectionRequests",
            columns: new[] { "assignment_id", "status" });

        migrationBuilder.CreateIndex(
            name: "IX_OvertimeCorrectionRequests_employee_id",
            table: "OvertimeCorrectionRequests",
            column: "employee_id");

        migrationBuilder.CreateIndex(
            name: "IX_OvertimeCorrectionRequests_work_date",
            table: "OvertimeCorrectionRequests",
            column: "work_date");

        migrationBuilder.CreateIndex(
            name: "IX_Rapels_employee_id_status",
            table: "Rapels",
            columns: new[] { "employee_id", "status" });

        migrationBuilder.CreateIndex(
            name: "IX_Rapels_payout_period_start",
            table: "Rapels",
            column: "payout_period_start");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "GajiPremiPeriods");

        migrationBuilder.DropTable(
            name: "OvertimeCorrectionRequests");

        migrationBuilder.DropTable(
            name: "Rapels");

        migrationBuilder.DropTable(
            name: "OvertimeAssignments");
    }
}
