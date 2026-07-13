using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAttendanceDay : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AttendanceDays",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                calendar_date = table.Column<DateOnly>(type: "date", nullable: false),
                tap_in_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                tap_out_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendanceDays", x => x.Id);
                table.ForeignKey(
                    name: "FK_AttendanceDays_Employees_employee_id",
                    column: x => x.employee_id,
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceDays_employee_id_calendar_date",
            table: "AttendanceDays",
            columns: new[] { "employee_id", "calendar_date" },
            unique: true);

        // One-time backfill: derive one AttendanceDay per (employee, Asia/Jakarta
        // calendar day) from all existing punches. Uses the default shift window
        // (09:00-18:00) and grace values (5/5 minutes) — historical config values
        // are not recoverable, so this is an accepted one-time approximation.
        migrationBuilder.Sql(
            """
            INSERT INTO "AttendanceDays" ("Id", employee_id, calendar_date, tap_in_utc, tap_out_utc, status)
            SELECT
                gen_random_uuid(),
                g.employee_id,
                g.calendar_date,
                g.first_punch,
                CASE WHEN g.punch_count > 1 THEN g.last_punch END,
                CASE
                    WHEN g.punch_count > 1
                         AND (g.first_punch AT TIME ZONE 'Asia/Jakarta') <= (g.calendar_date + time '09:00' + interval '5 minutes')
                         AND (g.last_punch AT TIME ZONE 'Asia/Jakarta') >= (g.calendar_date + time '18:00' - interval '5 minutes')
                    THEN 'Complete'
                    ELSE 'Incomplete'
                END
            FROM (
                SELECT
                    employee_id,
                    (punched_at_utc AT TIME ZONE 'Asia/Jakarta')::date AS calendar_date,
                    MIN(punched_at_utc) AS first_punch,
                    MAX(punched_at_utc) AS last_punch,
                    COUNT(*) AS punch_count
                FROM "AttendanceLogs"
                GROUP BY employee_id, (punched_at_utc AT TIME ZONE 'Asia/Jakarta')::date
            ) AS g;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AttendanceDays");
    }
}
