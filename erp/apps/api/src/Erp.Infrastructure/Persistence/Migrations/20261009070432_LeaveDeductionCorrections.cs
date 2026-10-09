using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class LeaveDeductionCorrections : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey(
            name: "PK_LeaveDeductionLines",
            table: "LeaveDeductionLines");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "corrected_at_utc",
            table: "LeaveRequests",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "corrected_by_name",
            table: "LeaveRequests",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "corrected_month",
            table: "LeaveRequests",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "correction_reason",
            table: "LeaveRequests",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "divisor",
            table: "LeaveDeductionMonths",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<Guid>(
            name: "id",
            table: "LeaveDeductionLines",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<DateOnly>(
            name: "late_target_month",
            table: "LeaveDeductionLines",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "paid_at_close",
            table: "LeaveDeductionLines",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "superseded_at_utc",
            table: "LeaveDeductionLines",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "superseded_by_name",
            table: "LeaveDeductionLines",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "superseded_target_month",
            table: "LeaveDeductionLines",
            type: "date",
            nullable: true);

        // Lines written before this migration were all written by closing their month; months closed
        // before it were closed with today's divisor (it can't have changed without a history row).
        migrationBuilder.Sql(
            """
            UPDATE "LeaveDeductionLines" SET id = gen_random_uuid(), paid_at_close = true;
            UPDATE "LeaveDeductionMonths" SET divisor = (SELECT divisor FROM "PayrollSettings" WHERE "Id" = 1);
            """);

        migrationBuilder.AddPrimaryKey(
            name: "PK_LeaveDeductionLines",
            table: "LeaveDeductionLines",
            column: "id");

        migrationBuilder.CreateTable(
            name: "LeaveDeductionAdjustments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                month = table.Column<DateOnly>(type: "date", nullable: false),
                employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                amount = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeaveDeductionAdjustments", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "LeaveDeductionCorrections",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                target_month = table.Column<DateOnly>(type: "date", nullable: false),
                source_month = table.Column<DateOnly>(type: "date", nullable: false),
                employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                leave_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                date = table.Column<DateOnly>(type: "date", nullable: false),
                leave_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                cut_days = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                daily_rate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeaveDeductionCorrections", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "LeaveDeductionMonthExceptions",
            columns: table => new
            {
                month = table.Column<DateOnly>(type: "date", nullable: false),
                employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                flat_amount_per_day = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: true),
                divisor = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeaveDeductionMonthExceptions", x => new { x.month, x.employee_id });
            });

        migrationBuilder.CreateTable(
            name: "PayrollSettingsChanges",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                old_divisor = table.Column<int>(type: "integer", nullable: false),
                new_divisor = table.Column<int>(type: "integer", nullable: false),
                changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                changed_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                changed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PayrollSettingsChanges", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_LeaveDeductionLines_leave_request_id_date",
            table: "LeaveDeductionLines",
            columns: new[] { "leave_request_id", "date" },
            unique: true,
            filter: "superseded_at_utc IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveDeductionAdjustments_month",
            table: "LeaveDeductionAdjustments",
            column: "month");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveDeductionCorrections_source_month",
            table: "LeaveDeductionCorrections",
            column: "source_month");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveDeductionCorrections_target_month",
            table: "LeaveDeductionCorrections",
            column: "target_month");

        migrationBuilder.CreateIndex(
            name: "IX_PayrollSettingsChanges_changed_at_utc",
            table: "PayrollSettingsChanges",
            column: "changed_at_utc");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The old key is one row per leave day; superseded history rows can't survive going back to it.
        migrationBuilder.Sql("""DELETE FROM "LeaveDeductionLines" WHERE superseded_at_utc IS NOT NULL;""");

        migrationBuilder.DropTable(
            name: "LeaveDeductionAdjustments");

        migrationBuilder.DropTable(
            name: "LeaveDeductionCorrections");

        migrationBuilder.DropTable(
            name: "LeaveDeductionMonthExceptions");

        migrationBuilder.DropTable(
            name: "PayrollSettingsChanges");

        migrationBuilder.DropPrimaryKey(
            name: "PK_LeaveDeductionLines",
            table: "LeaveDeductionLines");

        migrationBuilder.DropIndex(
            name: "IX_LeaveDeductionLines_leave_request_id_date",
            table: "LeaveDeductionLines");

        migrationBuilder.DropColumn(
            name: "corrected_at_utc",
            table: "LeaveRequests");

        migrationBuilder.DropColumn(
            name: "corrected_by_name",
            table: "LeaveRequests");

        migrationBuilder.DropColumn(
            name: "corrected_month",
            table: "LeaveRequests");

        migrationBuilder.DropColumn(
            name: "correction_reason",
            table: "LeaveRequests");

        migrationBuilder.DropColumn(
            name: "divisor",
            table: "LeaveDeductionMonths");

        migrationBuilder.DropColumn(
            name: "id",
            table: "LeaveDeductionLines");

        migrationBuilder.DropColumn(
            name: "late_target_month",
            table: "LeaveDeductionLines");

        migrationBuilder.DropColumn(
            name: "paid_at_close",
            table: "LeaveDeductionLines");

        migrationBuilder.DropColumn(
            name: "superseded_at_utc",
            table: "LeaveDeductionLines");

        migrationBuilder.DropColumn(
            name: "superseded_by_name",
            table: "LeaveDeductionLines");

        migrationBuilder.DropColumn(
            name: "superseded_target_month",
            table: "LeaveDeductionLines");

        migrationBuilder.AddPrimaryKey(
            name: "PK_LeaveDeductionLines",
            table: "LeaveDeductionLines",
            columns: new[] { "leave_request_id", "date" });
    }
}
