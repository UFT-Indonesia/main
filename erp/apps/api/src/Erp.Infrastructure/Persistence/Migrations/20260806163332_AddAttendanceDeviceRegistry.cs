using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAttendanceDeviceRegistry : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AttendanceDevices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                device_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                secret = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                enabled = table.Column<bool>(type: "boolean", nullable: false),
                registered_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendanceDevices", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceLogs_employee_id_device_id_punched_at_utc",
            table: "AttendanceLogs",
            columns: new[] { "employee_id", "device_id", "punched_at_utc" },
            unique: true,
            filter: "device_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceDevices_device_key",
            table: "AttendanceDevices",
            column: "device_key",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AttendanceDevices");

        migrationBuilder.DropIndex(
            name: "IX_AttendanceLogs_employee_id_device_id_punched_at_utc",
            table: "AttendanceLogs");
    }
}
