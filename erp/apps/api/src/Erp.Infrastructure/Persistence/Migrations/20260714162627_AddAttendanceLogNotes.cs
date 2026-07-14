using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAttendanceLogNotes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "note",
            table: "AttendanceLogs");

        migrationBuilder.CreateTable(
            name: "AttendanceLogNotes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                attendance_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendanceLogNotes", x => x.Id);
                table.ForeignKey(
                    name: "FK_AttendanceLogNotes_AttendanceLogs_attendance_log_id",
                    column: x => x.attendance_log_id,
                    principalTable: "AttendanceLogs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceLogNotes_attendance_log_id",
            table: "AttendanceLogNotes",
            column: "attendance_log_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AttendanceLogNotes");

        migrationBuilder.AddColumn<string>(
            name: "note",
            table: "AttendanceLogs",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);
    }
}
