using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAttendancePolicy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AttendancePolicies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                shift_start = table.Column<TimeOnly>(type: "time", nullable: false),
                shift_end = table.Column<TimeOnly>(type: "time", nullable: false),
                clock_in_grace_minutes = table.Column<int>(type: "integer", nullable: false),
                clock_out_grace_minutes = table.Column<int>(type: "integer", nullable: false),
                time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendancePolicies", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AttendancePolicyHistories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                shift_start = table.Column<TimeOnly>(type: "time", nullable: false),
                shift_end = table.Column<TimeOnly>(type: "time", nullable: false),
                clock_in_grace_minutes = table.Column<int>(type: "integer", nullable: false),
                clock_out_grace_minutes = table.Column<int>(type: "integer", nullable: false),
                time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                changed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendancePolicyHistories", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AttendancePolicyHistories_policy_id",
            table: "AttendancePolicyHistories",
            column: "policy_id");

        // Seed the single global policy row with today's appsettings.json defaults —
        // this table is now the source of truth, replacing the old "Attendance"
        // config section.
        migrationBuilder.InsertData(
            table: "AttendancePolicies",
            columns: new[]
            {
                "Id",
                "shift_start",
                "shift_end",
                "clock_in_grace_minutes",
                "clock_out_grace_minutes",
                "time_zone_id",
                "updated_by_user_id",
                "updated_at_utc",
            },
            values: new object[]
            {
                new Guid("00000000-0000-0000-0000-000000000001"),
                new TimeOnly(9, 0),
                new TimeOnly(18, 0),
                5,
                5,
                "Asia/Jakarta",
                Guid.Empty,
                DateTimeOffset.UtcNow,
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AttendancePolicies");

        migrationBuilder.DropTable(
            name: "AttendancePolicyHistories");
    }
}
