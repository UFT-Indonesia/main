using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class MakeLeaveReasonRequired : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Schema only, deliberately no backfill: EF scaffolded `defaultValue: ""`, which was
        // removed. An empty string would violate LeaveRequest.ReasonMinLength, and inventing
        // a reason for a historical row is worse than failing. Rows with a NULL reason must be
        // cleaned up first (see scripts/cleanup-null-leave-reasons.sql); if any remain, this
        // migration fails loudly, which is the correct outcome.
        migrationBuilder.AlterColumn<string>(
            name: "reason",
            table: "LeaveRequests",
            type: "character varying(500)",
            maxLength: 500,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(500)",
            oldMaxLength: 500,
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "reason",
            table: "LeaveRequests",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(500)",
            oldMaxLength: 500);
    }
}
