using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddLeaveRequestEditTrail : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "edited_at_utc",
            table: "LeaveRequests",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "edited_by_name",
            table: "LeaveRequests",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "edited_by_user_id",
            table: "LeaveRequests",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "previous_end_date",
            table: "LeaveRequests",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "previous_start_date",
            table: "LeaveRequests",
            type: "date",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "edited_at_utc",
            table: "LeaveRequests");

        migrationBuilder.DropColumn(
            name: "edited_by_name",
            table: "LeaveRequests");

        migrationBuilder.DropColumn(
            name: "edited_by_user_id",
            table: "LeaveRequests");

        migrationBuilder.DropColumn(
            name: "previous_end_date",
            table: "LeaveRequests");

        migrationBuilder.DropColumn(
            name: "previous_start_date",
            table: "LeaveRequests");
    }
}
