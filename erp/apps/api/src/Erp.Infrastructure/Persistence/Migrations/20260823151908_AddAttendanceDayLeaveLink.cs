using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAttendanceDayLeaveLink : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "cancellation_reason",
            table: "LeaveRequests",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "leave_request_id",
            table: "AttendanceDays",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceDays_leave_request_id",
            table: "AttendanceDays",
            column: "leave_request_id");

        migrationBuilder.AddForeignKey(
            name: "FK_AttendanceDays_LeaveRequests_leave_request_id",
            table: "AttendanceDays",
            column: "leave_request_id",
            principalTable: "LeaveRequests",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_AttendanceDays_LeaveRequests_leave_request_id",
            table: "AttendanceDays");

        migrationBuilder.DropIndex(
            name: "IX_AttendanceDays_leave_request_id",
            table: "AttendanceDays");

        migrationBuilder.DropColumn(
            name: "cancellation_reason",
            table: "LeaveRequests");

        migrationBuilder.DropColumn(
            name: "leave_request_id",
            table: "AttendanceDays");
    }
}
