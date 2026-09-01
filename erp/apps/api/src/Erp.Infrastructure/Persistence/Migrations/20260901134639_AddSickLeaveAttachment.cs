using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSickLeaveAttachment : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "attachment_content_type",
            table: "LeaveRequests",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "attachment_file_name",
            table: "LeaveRequests",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "attachment_size_bytes",
            table: "LeaveRequests",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "attachment_storage_key",
            table: "LeaveRequests",
            type: "character varying(300)",
            maxLength: 300,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "attachment_content_type",
            table: "LeaveRequests");

        migrationBuilder.DropColumn(
            name: "attachment_file_name",
            table: "LeaveRequests");

        migrationBuilder.DropColumn(
            name: "attachment_size_bytes",
            table: "LeaveRequests");

        migrationBuilder.DropColumn(
            name: "attachment_storage_key",
            table: "LeaveRequests");
    }
}
